// =============================================================================
//  PayloadBootstrap.cs - single-EXE workspace: embed Sample/Docs/config, extract once.
// =============================================================================
using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using RbaCore;

namespace RbaUi
{
    public static class PayloadBootstrap
    {
        public const string ResourceName = "RpaUi.EmbeddedPayload";
        public const string ProductFolder = "RPA-V1";
        public const string PayloadVersion = "82";

        // Prefer LocalAppData so the deliverable stays one EXE on Desktop / USB.
        public static string GetWorkspaceRoot()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductFolder);
            return root;
        }

        public static string EnsureWorkspace()
        {
            string root = GetWorkspaceRoot();
            string marker = Path.Combine(root, ".payload-version");
            string cfgPath = Path.Combine(root, "config", "bot-config.json");
            bool needExtract = !File.Exists(marker) ||
                               !File.Exists(cfgPath) ||
                               !Directory.Exists(Path.Combine(root, "Sample"));
            if (File.Exists(marker))
            {
                try
                {
                    string v = File.ReadAllText(marker, Encoding.UTF8).Trim();
                    if (!string.Equals(v, PayloadVersion, StringComparison.Ordinal))
                    {
                        needExtract = true;
                    }
                }
                catch { needExtract = true; }
            }

            if (needExtract)
            {
                ExtractPayload(root);
                File.WriteAllText(marker, PayloadVersion, new UTF8Encoding(true));
            }

            PatchBotConfigPaths(root);
            EnsureDirs(root);
            return root;
        }

        private static void EnsureDirs(string root)
        {
            try { Directory.CreateDirectory(Path.Combine(root, "state")); } catch { }
            try { Directory.CreateDirectory(Path.Combine(root, "config")); } catch { }
            string tracking = Path.Combine(root, "state", "tracking.csv");
            if (!File.Exists(tracking))
            {
                File.WriteAllText(tracking, "NationalId,Status,UpdatedAt,Note" + Environment.NewLine, new UTF8Encoding(true));
            }
        }

        private static void ExtractPayload(string root)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            Stream raw = asm.GetManifestResourceStream(ResourceName);
            if (raw == null)
            {
                string beside = Path.GetDirectoryName(asm.Location);
                if (Directory.Exists(Path.Combine(beside, "Sample")))
                {
                    return;
                }
                throw new InvalidOperationException(
                    "Embedded payload missing. Rebuild with build-rpa-v1.cmd (single-EXE package).");
            }

            Directory.CreateDirectory(root);
            string tmpZip = Path.Combine(Path.GetTempPath(), "rpa-v1-payload-" + Guid.NewGuid().ToString("N") + ".zip");
            string tmpDir = Path.Combine(Path.GetTempPath(), "rpa-v1-ext-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (FileStream fs = File.Create(tmpZip))
                {
                    byte[] buf = new byte[81920];
                    int n;
                    while ((n = raw.Read(buf, 0, buf.Length)) > 0)
                    {
                        fs.Write(buf, 0, n);
                    }
                }

                Directory.CreateDirectory(tmpDir);
                ZipFile.ExtractToDirectory(tmpZip, tmpDir);

                MirrorDir(Path.Combine(tmpDir, "Sample"), Path.Combine(root, "Sample"));
                string bundledInbox = Path.Combine(tmpDir, "OfflineInbox");
                string destInbox = Path.Combine(root, "OfflineInbox");
                if (Directory.Exists(bundledInbox))
                {
                    MirrorDir(bundledInbox, destInbox);
                }
                CopyConfigPreserveUser(Path.Combine(tmpDir, "config"), Path.Combine(root, "config"));
                string stIn = Path.Combine(tmpDir, "state", "tracking.csv");
                string stOut = Path.Combine(root, "state", "tracking.csv");
                Directory.CreateDirectory(Path.Combine(root, "state"));
                if (!File.Exists(stOut) && File.Exists(stIn))
                {
                    File.Copy(stIn, stOut, false);
                }
            }
            finally
            {
                try { raw.Dispose(); } catch { }
                try { if (File.Exists(tmpZip)) { File.Delete(tmpZip); } } catch { }
                try { if (Directory.Exists(tmpDir)) { Directory.Delete(tmpDir, true); } } catch { }
            }
        }

        // Copy packaged config files only when missing. Never overwrite selectors.csv
        // or bot-config.json — those hold the operator click-map and paths.
        private static void CopyConfigPreserveUser(string src, string dst)
        {
            if (!Directory.Exists(src)) { return; }
            Directory.CreateDirectory(dst);
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(src.Length).TrimStart('\\', '/');
                string name = Path.GetFileName(rel);
                if (string.Equals(name, "selectors.csv", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "bot-config.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "settings.json", StringComparison.OrdinalIgnoreCase))
                {
                    string targetKeep = Path.Combine(dst, rel);
                    if (File.Exists(targetKeep)) { continue; }
                }
                string target = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void MirrorDir(string src, string dst)
        {
            if (!Directory.Exists(src)) { return; }
            if (Directory.Exists(dst))
            {
                try { Directory.Delete(dst, true); } catch { }
            }
            Directory.CreateDirectory(dst);
            foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(src.Length).TrimStart('\\', '/');
                Directory.CreateDirectory(Path.Combine(dst, rel));
            }
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(src.Length).TrimStart('\\', '/');
                string target = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void PatchBotConfigPaths(string root)
        {
            string cfgPath = Path.Combine(root, "config", "bot-config.json");
            if (!File.Exists(cfgPath)) { return; }

            string docs = Path.Combine(root, "SftpCache", "docs");
            bool locked = SettingsVault.LooksLocked(cfgPath);
            string text = SettingsVault.ReadUtf8Auto(cfgPath);
            if (string.IsNullOrEmpty(text)) { return; }
            text = text.Replace("__SHEET__", "");
            text = text.Replace("__DOCS__", JsonEscape(docs));

            string replica = ResolveCodingReplicaPath();
            if (!string.IsNullOrEmpty(replica) && text.IndexOf("__CODING_REPLICA__", StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("__CODING_REPLICA__", JsonEscape(replica));
            }

            // Fill empty / placeholder paths only — never overwrite operator SFTP sheet.
            string sheetVal = ReadJsonString(text, "sheetPath");
            string docsVal = ReadJsonString(text, "scansRoot");
            string exeVal = ReadJsonString(text, "exePath");
            if (string.IsNullOrWhiteSpace(docsVal) || docsVal.IndexOf("__", StringComparison.Ordinal) >= 0)
            {
                string inboxDocs = Path.Combine(root, "OfflineInbox", "inbox", "docs");
                text = ReplaceJsonString(text, "scansRoot", Directory.Exists(inboxDocs) ? inboxDocs : docs);
            }
            else if (docsVal.IndexOf("SftpCache", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string inboxDocs = Path.Combine(root, "OfflineInbox", "inbox", "docs");
                if (Directory.Exists(inboxDocs))
                {
                    text = ReplaceJsonString(text, "scansRoot", inboxDocs);
                }
            }
            if (string.IsNullOrWhiteSpace(sheetVal) || sheetVal.IndexOf("__", StringComparison.Ordinal) >= 0)
            {
                // Operator flow uses SFTP cache; leave blank until sync.
                text = ReplaceJsonString(text, "sheetPath", "");
            }
            if (!string.IsNullOrEmpty(replica) &&
                (string.IsNullOrWhiteSpace(exeVal) ||
                 exeVal.IndexOf("__CODING_REPLICA__", StringComparison.Ordinal) >= 0 ||
                 !File.Exists(exeVal)))
            {
                text = ReplaceJsonString(text, "exePath", replica);
            }

            if (locked) { SettingsVault.WriteUtf8Locked(cfgPath, text); }
            else { File.WriteAllText(cfgPath, text, new UTF8Encoding(true)); }
        }

        private static string ResolveCodingReplicaPath()
        {
            try
            {
                string beside = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string[] candidates = new string[]
                {
                    Path.Combine(beside ?? "", "CodingReplica.exe"),
                    Path.Combine(beside ?? "", "..", "CodingReplica-Setup", "CodingReplica.exe"),
                    Path.Combine(beside ?? "", "..", "RBA-CSharp", "CodingReplica-Setup", "CodingReplica.exe"),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                        "EGX_Coding_Automation", "RBA-CSharp", "CodingReplica-Setup", "CodingReplica.exe"),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "OneDrive", "Desktop", "EGX_Coding_Automation",
                        "RBA-CSharp", "CodingReplica-Setup", "CodingReplica.exe")
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    try
                    {
                        string full = Path.GetFullPath(candidates[i]);
                        if (File.Exists(full)) { return full; }
                    }
                    catch { }
                }
            }
            catch { }
            return "";
        }

        private static string ReadJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) { return ""; }
            string pattern = "\"" + key + "\"";
            int keyAt = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyAt < 0) { return ""; }
            int colon = json.IndexOf(':', keyAt);
            if (colon < 0) { return ""; }
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) { return ""; }
            int q2 = q1 + 1;
            while (q2 < json.Length)
            {
                if (json[q2] == '"' && json[q2 - 1] != '\\') { break; }
                q2++;
            }
            if (q2 >= json.Length) { return ""; }
            string raw = json.Substring(q1 + 1, q2 - q1 - 1);
            return raw.Replace("\\\\", "\\");
        }

        private static string JsonEscape(string path)
        {
            if (path == null) { return ""; }
            return path.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ReplaceJsonString(string json, string key, string value)
        {
            // Simple "key": "..." rewriter (config is flat enough for this).
            string pattern = "\"" + key + "\"";
            int keyAt = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyAt < 0) { return json; }
            int colon = json.IndexOf(':', keyAt);
            if (colon < 0) { return json; }
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) { return json; }
            int q2 = q1 + 1;
            while (q2 < json.Length)
            {
                if (json[q2] == '"' && json[q2 - 1] != '\\') { break; }
                q2++;
            }
            if (q2 >= json.Length) { return json; }
            string escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return json.Substring(0, q1 + 1) + escaped + json.Substring(q2);
        }
    }
}
