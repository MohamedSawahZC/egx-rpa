param(
    [Parameter(Mandatory = $true)]
    [string]$DocsRoot,

    [string]$IdPhotosRoot = "",

    [string]$NationalId = "29011291601031",

    # Optional: rewrite DocsCount in Test-Save CSV to match folder file count.
    [string]$SampleCsv = ""
)

# Stage real ID photos from project "ID Photos" into Sample\Docs\{NationalId}.
# Normalize to JPG, size <= 500 KB, DPI 100..300. No fake generated images.
$ErrorActionPreference = "Stop"

$maxBytes = 500 * 1024
$targetDpi = 200
$minDpi = 100
$maxDpi = 300

# Prefer NationalId from the sample CSV first data row when provided.
if (-not [string]::IsNullOrWhiteSpace($SampleCsv) -and (Test-Path -LiteralPath $SampleCsv)) {
    try {
        $utf8Peek = New-Object System.Text.UTF8Encoding $true
        $peekLines = [System.IO.File]::ReadAllLines($SampleCsv, $utf8Peek)
        if ($peekLines.Length -ge 2) {
            $rePeek = [regex]::new('(?:^|,)(?:"([^"]*)"|([^",]*))')
            $hdrCols = @()
            foreach ($m in $rePeek.Matches($peekLines[0])) {
                if ($m.Groups[1].Success) { $hdrCols += $m.Groups[1].Value }
                else { $hdrCols += $m.Groups[2].Value }
            }
            $nidIdx = [Array]::IndexOf($hdrCols, "NationalId")
            if ($nidIdx -ge 0) {
                $cells0 = New-Object System.Collections.Generic.List[string]
                foreach ($m in $rePeek.Matches($peekLines[1])) {
                    if ($m.Groups[1].Success) { $cells0.Add($m.Groups[1].Value) }
                    else { $cells0.Add($m.Groups[2].Value) }
                }
                if ($cells0.Count -gt $nidIdx -and -not [string]::IsNullOrWhiteSpace($cells0[$nidIdx])) {
                    $NationalId = $cells0[$nidIdx].Trim()
                    Write-Host ("NationalId from sample CSV: " + $NationalId)
                }
            }
        }
    }
    catch {
        Write-Host ("Could not peek NationalId from CSV: " + $_.Exception.Message)
    }
}

if ([string]::IsNullOrWhiteSpace($IdPhotosRoot)) {
    $IdPhotosRoot = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "ID Photos"
    if (-not (Test-Path $IdPhotosRoot)) {
        $IdPhotosRoot = Join-Path (Split-Path $PSScriptRoot -Parent) "..\ID Photos"
    }
    $IdPhotosRoot = [System.IO.Path]::GetFullPath($IdPhotosRoot)
}

if (-not (Test-Path -LiteralPath $IdPhotosRoot)) {
    throw ("ID Photos folder not found: " + $IdPhotosRoot)
}

$exts = @(".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp")
$sources = @(Get-ChildItem -LiteralPath $IdPhotosRoot -File -ErrorAction SilentlyContinue |
    Where-Object { $exts -contains $_.Extension.ToLowerInvariant() } |
    Sort-Object Name)

if ($sources.Count -lt 1) {
    throw ("No image files in ID Photos: " + $IdPhotosRoot)
}

if (Test-Path -LiteralPath $DocsRoot) {
    Remove-Item -LiteralPath $DocsRoot -Recurse -Force
}
$keep = Join-Path $DocsRoot $NationalId
New-Item -ItemType Directory -Path $keep -Force | Out-Null

Add-Type -AssemblyName System.Drawing

$jpegCodec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
    Where-Object { $_.MimeType -eq "image/jpeg" } |
    Select-Object -First 1
if (-not $jpegCodec) { throw "JPEG codec not available" }

function Save-CompliantJpeg {
    param(
        [string]$SourcePath,
        [string]$DestPath
    )

    $img = $null
    $bmp = $null
    try {
        $img = [System.Drawing.Image]::FromFile($SourcePath)
        $w = $img.Width
        $h = $img.Height
        if ($w -lt 1 -or $h -lt 1) { throw ("Invalid image size: " + $SourcePath) }

        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $bmp.SetResolution([float]$targetDpi, [float]$targetDpi)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($img, 0, 0, $w, $h)
        $g.Dispose()
    }
    finally {
        if ($img) { $img.Dispose() }
    }

    $quality = 85L
    $ep = New-Object System.Drawing.Imaging.EncoderParameters 1
    $ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter (
        [System.Drawing.Imaging.Encoder]::Quality, $quality)

    if (Test-Path -LiteralPath $DestPath) { Remove-Item -LiteralPath $DestPath -Force }
    $bmp.Save($DestPath, $jpegCodec, $ep)
    $ep.Dispose()
    $bmp.Dispose()

    $len = (Get-Item -LiteralPath $DestPath).Length
    while ($len -gt $maxBytes -and $quality -gt 40) {
        $quality -= 10
        $reload = [System.Drawing.Image]::FromFile($DestPath)
        $clone = New-Object System.Drawing.Bitmap $reload.Width, $reload.Height
        $clone.SetResolution([float]$targetDpi, [float]$targetDpi)
        $gc = [System.Drawing.Graphics]::FromImage($clone)
        $gc.DrawImage($reload, 0, 0, $reload.Width, $reload.Height)
        $gc.Dispose()
        $reload.Dispose()
        Remove-Item -LiteralPath $DestPath -Force

        $ep2 = New-Object System.Drawing.Imaging.EncoderParameters 1
        $ep2.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter (
            [System.Drawing.Imaging.Encoder]::Quality, [long]$quality)
        $clone.Save($DestPath, $jpegCodec, $ep2)
        $ep2.Dispose()
        $clone.Dispose()
        $len = (Get-Item -LiteralPath $DestPath).Length
    }

    if ($len -gt $maxBytes) {
        throw ("Still over 500 KB after compress: " + $DestPath + " (" + $len + " bytes)")
    }
}

$i = 0
foreach ($src in $sources) {
    $i++
    $base = ("{0:00}-{1}" -f $i, [System.IO.Path]::GetFileNameWithoutExtension($src.Name))
    $base = ($base -replace '[^\w\-]+', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($base)) { $base = ("{0:00}-doc" -f $i) }
    $dest = Join-Path $keep ($base + ".jpg")
    Save-CompliantJpeg -SourcePath $src.FullName -DestPath $dest

    $out = Get-Item -LiteralPath $dest
    $chk = [System.Drawing.Image]::FromFile($dest)
    try {
        $hx = [math]::Round($chk.HorizontalResolution, 0)
        $vy = [math]::Round($chk.VerticalResolution, 0)
        if ($hx -lt $minDpi -or $hx -gt $maxDpi -or $vy -lt $minDpi -or $vy -gt $maxDpi) {
            throw ("DPI out of range after convert: " + $dest)
        }
        Write-Host ("  {0}  {1} KB  {2}x{3}px  {4} DPI  (from {5})" -f `
            $out.Name, [math]::Round($out.Length / 1KB, 1), $chk.Width, $chk.Height, $hx, $src.Name)
    }
    finally {
        $chk.Dispose()
    }
}

$count = $i
Write-Host ("Test docs ready: " + $keep + " (" + $count + " JPG from ID Photos, max 500KB, DPI " + $targetDpi + ")")

# Extra Test-Save rows each need Docs\{NationalId} — copy the same compliant JPGs.
if (-not [string]::IsNullOrWhiteSpace($SampleCsv) -and (Test-Path -LiteralPath $SampleCsv)) {
    $utf8N = New-Object System.Text.UTF8Encoding $true
    $nidLines = [System.IO.File]::ReadAllLines($SampleCsv, $utf8N)
    if ($nidLines.Length -ge 2) {
        $reN = [regex]::new('(?:^|,)(?:"([^"]*)"|([^",]*))')
        $hdrN = @()
        foreach ($m in $reN.Matches($nidLines[0])) {
            if ($m.Groups[1].Success) { $hdrN += $m.Groups[1].Value }
            else { $hdrN += $m.Groups[2].Value }
        }
        $nidCol = [Array]::IndexOf($hdrN, "NationalId")
        if ($nidCol -ge 0) {
            $jpgs = @(Get-ChildItem -LiteralPath $keep -Filter "*.jpg" -File -ErrorAction SilentlyContinue)
            for ($r = 1; $r -lt $nidLines.Length; $r++) {
                if ([string]::IsNullOrWhiteSpace($nidLines[$r])) { continue }
                $cellsN = New-Object System.Collections.Generic.List[string]
                foreach ($m in $reN.Matches($nidLines[$r])) {
                    if ($m.Groups[1].Success) { $cellsN.Add($m.Groups[1].Value) }
                    else { $cellsN.Add($m.Groups[2].Value) }
                }
                if ($cellsN.Count -le $nidCol) { continue }
                $other = $cellsN[$nidCol].Trim()
                if ([string]::IsNullOrWhiteSpace($other)) { continue }
                if ($other -eq $NationalId) { continue }
                $otherDir = Join-Path $DocsRoot $other
                New-Item -ItemType Directory -Path $otherDir -Force | Out-Null
                foreach ($j in $jpgs) {
                    Copy-Item -LiteralPath $j.FullName -Destination (Join-Path $otherDir $j.Name) -Force
                }
                Write-Host ("Copied " + $jpgs.Count + " JPG into " + $otherDir)
            }
        }
    }
}

# Keep Test-Save DocsCount in sync without rewriting Arabic via Export-Csv.
if (-not [string]::IsNullOrWhiteSpace($SampleCsv) -and (Test-Path -LiteralPath $SampleCsv)) {
    $utf8 = New-Object System.Text.UTF8Encoding $true
    $lines = [System.IO.File]::ReadAllLines($SampleCsv, $utf8)
    if ($lines.Length -ge 2) {
        $header = $lines[0]
        $cols = @()
        # Simple CSV split respecting quotes.
        $re = [regex]::new('(?:^|,)(?:"([^"]*)"|([^",]*))')
        $ms = $re.Matches($header)
        foreach ($m in $ms) {
            if ($m.Groups[1].Success) { $cols += $m.Groups[1].Value }
            else { $cols += $m.Groups[2].Value }
        }
        $idx = [Array]::IndexOf($cols, "DocsCount")
        if ($idx -lt 0) {
            Write-Host "DocsCount column missing in sample CSV - skipped sync"
        }
        else {
            for ($r = 1; $r -lt $lines.Length; $r++) {
                if ([string]::IsNullOrWhiteSpace($lines[$r])) { continue }
                $cells = New-Object System.Collections.Generic.List[string]
                foreach ($m in $re.Matches($lines[$r])) {
                    if ($m.Groups[1].Success) { $cells.Add($m.Groups[1].Value) }
                    else { $cells.Add($m.Groups[2].Value) }
                }
                while ($cells.Count -le $idx) { $cells.Add("") }
                $cells[$idx] = [string]$count
                $parts = New-Object System.Collections.Generic.List[string]
                foreach ($c in $cells) {
                    $needsQuote = $c.Contains(",") -or $c.Contains('"') -or $c.Contains("`n")
                    if ($needsQuote) { $parts.Add('"' + ($c.Replace('"', '""')) + '"') }
                    else { $parts.Add('"' + $c + '"') }
                }
                $lines[$r] = [string]::Join(",", $parts)
            }
            [System.IO.File]::WriteAllLines($SampleCsv, $lines, $utf8)
            Write-Host ("Updated DocsCount=" + $count + " in " + $SampleCsv)
        }
    }
}
