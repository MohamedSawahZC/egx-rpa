// =============================================================================
//  SftpInbox.cs - pull KYC sheet + docs from local SFTP root (or sftp.exe).
//  Default lab mode mirrors C:\RPA-SFTP\inbox → workspace (real-folder SFTP sim).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RbaCore
{
    public class SftpSyncResult
    {
        public bool Ok;
        public string Mode;
        public string SheetPath;
        public string DocsRoot;
        public int DocsFolders;
        public string Note;
    }

    // Result of a lightweight connection probe (list remote/local inbox files).
    public class SftpProbeResult
    {
        public bool Ok;
        public string Mode;
        public string Note;
        public int FileCount;
        public List<string> Entries = new List<string>();
    }

    public static class SftpInbox
    {
        public const string DefaultLocalRoot = @"C:\RPA-SFTP";
        public const string InboxRelative = "inbox";
        public const string SheetFileName = "kyc_export.csv";
        public const string DocsRelative = "docs";
        // Inbox sheet actually used on the last offline/SFTP sync (push writes here).
        public static string LastInboxSheet = "";
        public static string LastDocsRoot = "";
        public static string LastWorkingSheet = "";

        public static string ResolveLocalRoot(BotConfig cfg)
        {
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.SftpLocalRoot))
            {
                return cfg.SftpLocalRoot.Trim();
            }
            if (cfg != null && cfg.SftpOfflineMode && !string.IsNullOrWhiteSpace(cfg.BaseDir))
            {
                string bundled = Path.Combine(cfg.BaseDir, "OfflineInbox");
                if (Directory.Exists(bundled)) { return bundled; }
            }
            return DefaultLocalRoot;
        }

        // After a live SFTP pull, images live in SftpCache (kyc-imports/{NID}), not OfflineInbox\docs.
        private static string PreferRemoteCacheDocs(string cacheDocs, BotConfig cfg, string inboxDocs)
        {
            string[] roots = EffectiveDocsRoots(cacheDocs);
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsUsableDocsRoot(roots[i])) { return roots[i]; }
            }
            return PreferConfiguredDocs(cfg, cacheDocs, inboxDocs);
        }

        private static string PreferConfiguredDocs(BotConfig cfg, string cacheDocs, string inboxDocs)
        {
            string configured = cfg != null ? (cfg.ScansRoot ?? "").Trim() : "";
            if (IsUsableDocsRoot(configured) &&
                !PathsEqual(configured, cacheDocs) &&
                configured.IndexOf("SftpCache", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return configured;
            }
            if (IsUsableDocsRoot(inboxDocs)) { return inboxDocs; }
            return cacheDocs;
        }

        private static bool IsUsableDocsRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) { return false; }
            try
            {
                string[] dirs = Directory.GetDirectories(path);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string[] files = Directory.GetFiles(dirs[i]);
                    if (files != null && files.Length > 0) { return true; }
                }
                string[] top = Directory.GetFiles(path);
                return top != null && top.Length > 0;
            }
            catch { return false; }
        }

        private static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) { return false; }
            try
            {
                return string.Equals(
                    Path.GetFullPath(a).TrimEnd('\\', '/'),
                    Path.GetFullPath(b).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase); }
        }

        private static bool HasOfflineSheet(string localRoot)
        {
            try { return File.Exists(RemoteSheetPath(localRoot)); }
            catch { return false; }
        }

        public static string InboxPath(string localRoot)
        {
            string root = localRoot ?? DefaultLocalRoot;
            if (LooksLikeInboxFolder(root)) { return root; }
            return Path.Combine(root, InboxRelative);
        }

        // Accept either C:\data\inbox or C:\data (parent that contains inbox\).
        private static bool LooksLikeInboxFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) { return false; }
            if (File.Exists(Path.Combine(path, SheetFileName))) { return true; }
            if (Directory.Exists(Path.Combine(path, DocsRelative))) { return true; }
            string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(name, InboxRelative, StringComparison.OrdinalIgnoreCase);
        }

        public static string RemoteSheetPath(string localRoot)
        {
            return Path.Combine(InboxPath(localRoot), SheetFileName);
        }

        public static string RemoteDocsPath(string localRoot)
        {
            return Path.Combine(InboxPath(localRoot), DocsRelative);
        }

        // Remote inbox folder name. Empty / "." = server home (no extra cd).
        public static string ResolveRemoteInbox(BotConfig cfg)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SftpRemoteInbox))
            {
                return InboxRelative;
            }
            string v = cfg.SftpRemoteInbox.Trim().Replace("\\", "/").Trim('/');
            if (string.IsNullOrEmpty(v) || v == ".") { return ""; }
            if (string.Equals(v, InboxRelative, StringComparison.OrdinalIgnoreCase))
            {
                return "kyc-imports";
            }
            return v;
        }

        public static bool UsesOfflineFiles(BotConfig cfg)
        {
            if (cfg == null) { return true; }
            if (cfg.SftpOfflineMode) { return true; }
            return !UsesRemoteCli(cfg);
        }

        // Probe SFTP (or local inbox) and log a short file listing — no full download.
        public static SftpProbeResult TestConnection(BotConfig cfg, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            SftpProbeResult result = new SftpProbeResult();
            if (cfg == null)
            {
                result.Note = "missing config";
                log(result.Note, "ERROR");
                return result;
            }

            if (!HasRemoteHost(cfg))
            {
                result.Mode = cfg != null && cfg.SftpOfflineMode ? "offline" : "local-mirror";
                string localRoot = ResolveLocalRoot(cfg);
                string inbox = InboxPath(localRoot);
                string sheet = RemoteSheetPath(localRoot);
                string docs = RemoteDocsPath(localRoot);
                log("source probe mode=" + result.Mode + " root=" + inbox, "INFO");
                if (!Directory.Exists(inbox))
                {
                    result.Note = "local inbox missing: " + inbox;
                    log(result.Note, "ERROR");
                    return result;
                }
                try
                {
                    if (File.Exists(sheet))
                    {
                        FileInfo fi = new FileInfo(sheet);
                        string line = SheetFileName + "  (" + fi.Length + " bytes)";
                        result.Entries.Add(line);
                        log("  file: " + line, "OK");
                    }
                    else
                    {
                        log("  missing: " + SheetFileName, "WARN");
                    }
                    if (Directory.Exists(docs))
                    {
                        string[] dirs = Directory.GetDirectories(docs);
                        result.Entries.Add("docs/ (" + dirs.Length + " folders)");
                        log("  docs folders: " + dirs.Length, "OK");
                        int shown = 0;
                        for (int i = 0; i < dirs.Length && shown < 40; i++)
                        {
                            string name = Path.GetFileName(dirs[i]);
                            int files = 0;
                            try { files = Directory.GetFiles(dirs[i], "*", SearchOption.AllDirectories).Length; }
                            catch { }
                            string line = "docs/" + name + "  (" + files + " files)";
                            result.Entries.Add(line);
                            log("  " + line, "INFO");
                            shown++;
                        }
                        if (dirs.Length > shown)
                        {
                            log("  … +" + (dirs.Length - shown) + " more doc folders", "INFO");
                        }
                    }
                    else
                    {
                        log("  docs folder missing under inbox", "WARN");
                    }
                }
                catch (Exception ex)
                {
                    result.Note = ex.Message;
                    log("SFTP local probe failed: " + ex.Message, "ERROR");
                    return result;
                }
                result.FileCount = result.Entries.Count;
                result.Ok = true;
                result.Note = result.Mode + " inbox reachable — " + result.FileCount +
                    " entries at " + inbox;
                log(result.Note, "OK");
                return result;
            }

            result.Mode = "sftp-cli";
            string listing;
            string note;
            if (!TrySftpCliList(cfg, log, out listing, out note))
            {
                result.Note = note;
                log("SFTP connection FAILED — " + note, "ERROR");
                return result;
            }

            string[] rawLines = (listing ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int listed = 0;
            for (int i = 0; i < rawLines.Length; i++)
            {
                string line = (rawLines[i] ?? "").Trim();
                if (string.IsNullOrEmpty(line)) { continue; }
                // Skip OpenSSH noise / prompts.
                if (line.StartsWith("sftp>", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (line.IndexOf("Connected to", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                if (line.IndexOf("Changing to:", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                if (line.IndexOf("Connection closed", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                result.Entries.Add(line);
                if (listed < 80)
                {
                    log("  remote: " + line, "INFO");
                }
                listed++;
            }
            if (listed > 80)
            {
                log("  … +" + (listed - 80) + " more remote lines", "INFO");
            }
            result.FileCount = listed;
            result.Ok = true;
            result.Note = "connected " + (cfg.SftpUser ?? "") + "@" + cfg.SftpHost +
                          ":" + (cfg.SftpPort > 0 ? cfg.SftpPort : 22) +
                          " — listed " + listed + " lines";
            log("SFTP connection OK — " + result.Note, "OK");
            return result;
        }

        // Sync remote inbox into workspace cache. LocalMirror always works without sshd.
        public static SftpSyncResult Sync(BotConfig cfg, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            SftpSyncResult result = new SftpSyncResult();
            string localRoot = ResolveLocalRoot(cfg);
            string cacheRoot = Path.Combine(cfg.BaseDir, "SftpCache");
            string cacheSheet = Path.Combine(cacheRoot, SheetFileName);
            string destDocs = ResolveOperatorDocsRoot(cfg);
            string cacheDocs = destDocs;

            try
            {
                Directory.CreateDirectory(cacheRoot);
                Directory.CreateDirectory(destDocs);
            }
            catch (Exception ex)
            {
                result.Note = "cannot create SFTP cache: " + ex.Message;
                log(result.Note, "ERROR");
                return result;
            }

            bool offline = UsesOfflineFiles(cfg);
            if (offline)
            {
                return SyncOffline(cfg, cacheRoot, cacheSheet, cacheDocs, log, result);
            }

            bool useCli = UsesRemoteCli(cfg);

            if (useCli)
            {
                result.Mode = "sftp-cli";
                string note;
                log("SFTP docs destination (settings) = " + destDocs, "INFO");
                if (!TrySftpCliDownload(cfg, cacheSheet, destDocs, log, out note))
                {
                    result.Note = note;
                    log("SFTP CLI failed — " + note, "WARN");
                    if (File.Exists(cacheSheet))
                    {
                        log("Using last SftpCache after remote failure", "WARN");
                        result.Mode = "cache-fallback";
                    }
                    else
                    {
                        log("No cache — falling back to local inbox files", "WARN");
                        useCli = false;
                    }
                }
                else
                {
                    result.Note = note;
                }
            }

            if (!useCli || result.Mode == "cache-fallback")
            {
                if (result.Mode != "cache-fallback")
                {
                    result.Mode = offline && cfg.SftpOfflineMode ? "offline" : "local-mirror";
                    string remoteSheet = RemoteSheetPath(localRoot);
                    string remoteDocs = RemoteDocsPath(localRoot);
                    if (!File.Exists(remoteSheet) && File.Exists(cacheSheet))
                    {
                        log("Local inbox sheet missing — using existing SftpCache", "WARN");
                        result.Mode = "cache-fallback";
                    }
                    else if (!File.Exists(remoteSheet))
                    {
                        result.Note = "offline sheet missing: " + remoteSheet +
                                      " — ضع kyc_export.csv داخل المجلد المحلي/inbox أو فعّل SFTP";
                        log(result.Note, "ERROR");
                        return result;
                    }
                    else
                    {
                        File.Copy(remoteSheet, cacheSheet, true);
                        log(result.Mode + " sheet ← " + remoteSheet, "OK");
                        if (Directory.Exists(remoteDocs))
                        {
                            MirrorDirectory(remoteDocs, cacheDocs);
                            log(result.Mode + " docs ← " + remoteDocs, "OK");
                        }
                        result.Note = result.Mode + " from " + InboxPath(localRoot);
                    }
                }
            }

            result.SheetPath = cacheSheet;
            string pulledDocs = string.Equals(result.Mode, "sftp-cli", StringComparison.OrdinalIgnoreCase)
                ? PreferRemoteCacheDocs(cacheDocs, cfg, RemoteDocsPath(localRoot))
                : PreferConfiguredDocs(cfg, cacheDocs, RemoteDocsPath(localRoot));
            result.DocsRoot = PublishDocsToConfigured(cfg, pulledDocs, cacheDocs, log);
            DocImageCompose.PrepareTree(result.DocsRoot, cfg, log);
            result.DocsFolders = CountDocFolders(result.DocsRoot);
            result.Ok = File.Exists(cacheSheet);
            if (result.Ok)
            {
                cfg.SheetPath = cacheSheet;
                LastDocsRoot = result.DocsRoot;
                log("SFTP sync ok — sheet=" + cacheSheet + " docs=" + result.DocsRoot +
                    " folders=" + result.DocsFolders + " mode=" + result.Mode, "OK");
            }
            return result;
        }

        // Offline: stay on the operator folder from Settings (no AppData swap, no SFTP password).
        private static SftpSyncResult SyncOffline(
            BotConfig cfg,
            string cacheRoot,
            string cacheSheet,
            string cacheDocs,
            Action<string, string> log,
            SftpSyncResult result)
        {
            result.Mode = "offline";
            string localRoot = ResolveLocalRoot(cfg);
            string inboxSheet = FirstExistingFile(
                RemoteSheetPath(localRoot),
                Path.Combine(localRoot ?? "", SheetFileName),
                cfg != null ? cfg.SheetPath : "");
            if (string.IsNullOrEmpty(inboxSheet))
            {
                result.Note = "offline sheet missing under " + localRoot +
                    " — ضع kyc_export.csv في المجلد المحلي أو inbox";
                log(result.Note, "ERROR");
                return result;
            }

            string inboxDocs = FirstExistingDir(
                RemoteDocsPath(localRoot),
                Path.Combine(localRoot ?? "", "inbox", DocsRelative),
                Path.Combine(localRoot ?? "", DocsRelative),
                cfg != null ? cfg.ScansRoot : "");

            try
            {
                File.Copy(inboxSheet, cacheSheet, true);
            }
            catch (Exception ex)
            {
                result.Note = "cannot copy offline sheet: " + ex.Message;
                log(result.Note, "ERROR");
                return result;
            }

            if (!string.IsNullOrEmpty(inboxDocs))
            {
                try { MirrorDirectory(inboxDocs, cacheDocs); }
                catch (Exception ex) { log("offline docs copy: " + ex.Message, "WARN"); }
            }

            LastInboxSheet = inboxSheet;
            result.SheetPath = cacheSheet;
            string pulled = !string.IsNullOrEmpty(inboxDocs) ? inboxDocs : cacheDocs;
            result.DocsRoot = PublishDocsToConfigured(cfg, pulled, cacheDocs, log);
            DocImageCompose.PrepareTree(result.DocsRoot, cfg, log);
            result.DocsFolders = CountDocFolders(result.DocsRoot);
            result.Ok = File.Exists(cacheSheet);
            result.Note = "offline from " + inboxSheet + " docs=" + result.DocsRoot;
            if (result.Ok)
            {
                LastDocsRoot = result.DocsRoot;
                cfg.SheetPath = cacheSheet;
                log(result.Note + " folders=" + result.DocsFolders, "OK");
            }
            return result;
        }

        private static void CopySheetTo(string workingSheet, string dest, Action<string, string> log, int rows)
        {
            if (string.IsNullOrWhiteSpace(dest)) { return; }
            string dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }
            File.Copy(workingSheet, dest, true);
            log("sheet push → " + dest + " (" + rows + " clients)", "OK");
        }

        private static int CountDataRows(string path)
        {
            try
            {
                string[] lines = File.ReadAllLines(path);
                int n = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i])) { n++; }
                }
                return n;
            }
            catch { return 0; }
        }

        private static string FirstExistingFile(params string[] paths)
        {
            if (paths == null) { return ""; }
            for (int i = 0; i < paths.Length; i++)
            {
                string p = paths[i];
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) { return p; }
            }
            return "";
        }

        private static string FirstExistingDir(params string[] paths)
        {
            if (paths == null) { return ""; }
            for (int i = 0; i < paths.Length; i++)
            {
                string p = paths[i];
                if (IsUsableDocsRoot(p) &&
                    (p ?? "").IndexOf("SftpCache", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return p;
                }
            }
            for (int i = 0; i < paths.Length; i++)
            {
                if (IsUsableDocsRoot(paths[i])) { return paths[i]; }
            }
            return "";
        }

        // Write working sheet back to inbox (local mirror and/or remote SFTP when host is set).
        public static void PushSheetToInbox(BotConfig cfg, string workingSheet, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            if (string.IsNullOrWhiteSpace(workingSheet) || !File.Exists(workingSheet)) { return; }
            try
            {
                int rows = CountDataRows(workingSheet);
                CopySheetTo(workingSheet, LastInboxSheet, log, rows);
                string localRoot = ResolveLocalRoot(cfg);
                string remoteSheet = RemoteSheetPath(localRoot);
                if (!PathsEqual(remoteSheet, LastInboxSheet))
                {
                    CopySheetTo(workingSheet, remoteSheet, log, rows);
                }
            }
            catch (Exception ex)
            {
                log("SFTP push failed: " + ex.Message, "WARN");
            }

            if (UsesRemoteCli(cfg))
            {
                string note;
                if (!TrySftpCliUpload(cfg, workingSheet, log, out note))
                {
                    log("SFTP remote push failed: " + note, "WARN");
                }
                else
                {
                    log("SFTP remote push ok — " + note, "OK");
                }
            }
        }

        private static bool HasRemoteHost(BotConfig cfg)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SftpHost)) { return false; }
            string h = cfg.SftpHost.Trim();
            return !string.Equals(h, "local", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(h, "folder", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(h, "offline", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesRemoteCli(BotConfig cfg)
        {
            if (cfg == null || cfg.SftpOfflineMode) { return false; }
            return HasRemoteHost(cfg);
        }

        // Try modern OpenSSH defaults first. Forcing obsolete KEX/host-key lists
        // on current Windows OpenSSH often yields "Connection closed" even when
        // FileZilla / ping succeed.
        private static string[] SshClientOptionSets(int port)
        {
            string auth = "-o BatchMode=no" +
                          " -o PreferredAuthentications=password,keyboard-interactive" +
                          " -o PubkeyAuthentication=no" +
                          " -o IdentitiesOnly=yes" +
                          " -o NumberOfPasswordPrompts=3";
            string p = " -P " + port;
            return new string[]
            {
                auth + " -o StrictHostKeyChecking=accept-new" + p,
                auth + " -o StrictHostKeyChecking=no" + p,
                auth + " -o StrictHostKeyChecking=no" +
                    " -o KexAlgorithms=+diffie-hellman-group14-sha1,diffie-hellman-group-exchange-sha256" +
                    " -o HostKeyAlgorithms=+ssh-rsa" + p
            };
        }

        private static bool AuthLooksFailed(string text)
        {
            if (string.IsNullOrEmpty(text)) { return false; }
            return text.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Authentication failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Too many authentication", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasUsefulListing(string listing)
        {
            if (string.IsNullOrWhiteSpace(listing) || AuthLooksFailed(listing)) { return false; }
            string[] lines = listing.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int useful = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (string.IsNullOrEmpty(line)) { continue; }
                if (line.StartsWith("sftp>", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (line.IndexOf("Connection closed", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                if (line.IndexOf("Connected to", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                if (line.IndexOf("Couldn't", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                if (line.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                useful++;
            }
            return useful > 0;
        }

        private static bool SoftDisconnectOk(int exitCode, string listing, string cacheSheet)
        {
            if (AuthLooksFailed(listing)) { return false; }
            if (!string.IsNullOrEmpty(cacheSheet) && File.Exists(cacheSheet)) { return true; }
            return exitCode != 0 && HasUsefulListing(listing);
        }

        private static void AppendCd(StringBuilder sb, string remoteInbox)
        {
            if (sb == null || string.IsNullOrEmpty(remoteInbox)) { return; }
            sb.AppendLine("cd " + remoteInbox);
        }

        private static int CountDocFolders(string docsRoot)
        {
            if (!Directory.Exists(docsRoot)) { return 0; }
            try { return Directory.GetDirectories(docsRoot).Length; }
            catch { return 0; }
        }

        private static void MirrorDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string dir in Directory.GetDirectories(src))
            {
                string name = Path.GetFileName(dir);
                string target = Path.Combine(dst, name);
                Directory.CreateDirectory(target);
                foreach (string file in Directory.GetFiles(dir))
                {
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
                }
            }
            foreach (string file in Directory.GetFiles(src))
            {
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            }
        }

        // Optional real SFTP via OpenSSH sftp.exe batch (host/user/password).
        private static bool TrySftpCliList(
            BotConfig cfg, Action<string, string> log, out string listing, out string note)
        {
            listing = "";
            note = "";
            string sftpExe = FindSftpExe();
            if (string.IsNullOrEmpty(sftpExe))
            {
                note = "sftp.exe not found (install OpenSSH Client)";
                return false;
            }

            string remoteInbox = ResolveRemoteInbox(cfg);
            string batch = Path.Combine(Path.GetTempPath(), "rpa-sftp-ls-" + Guid.NewGuid().ToString("N") + ".bat");
            string host = cfg.SftpHost.Trim();
            int port = cfg.SftpPort > 0 ? cfg.SftpPort : 22;
            string user = string.IsNullOrWhiteSpace(cfg.SftpUser)
                ? Environment.UserName
                : cfg.SftpUser.Trim();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("pwd");
            AppendCd(sb, remoteInbox);
            sb.AppendLine("ls");
            sb.AppendLine("bye");
            File.WriteAllText(batch, sb.ToString(), Encoding.ASCII);

            try
            {
                string inboxLabel = string.IsNullOrEmpty(remoteInbox) ? "(home)" : remoteInbox;
                log("SFTP probe → " + user + "@" + host + ":" + port + " inbox=" + inboxLabel +
                    (cfg != null && cfg.SftpOfflineMode ? " (Offline checkbox ignored for probe)" : ""), "INFO");
                log("inbox = remote folder that holds kyc_export.csv + docs\\{NID}\\ — default 'inbox', or '.' for home", "INFO");
                int exitCode;
                if (!RunSftpBatch(sftpExe, port, batch, user, host,
                    cfg != null ? cfg.SftpPassword : "", 60000, out listing, out exitCode, out note))
                {
                    return false;
                }
                if (exitCode != 0 && !SoftDisconnectOk(exitCode, listing, null))
                {
                    note = "sftp exit=" + exitCode + " " + SanitizeSftpLog(listing);
                    return false;
                }
                if (exitCode != 0)
                {
                    log("SFTP session closed after listing (treated as OK) — " + SanitizeSftpLog(listing), "WARN");
                }
                note = "sftp CLI list ok";
                return true;
            }
            catch (Exception ex)
            {
                note = ex.Message;
                return false;
            }
            finally
            {
                try { File.Delete(batch); } catch { }
            }
        }

        private static bool TrySftpCliDownload(
            BotConfig cfg,
            string cacheSheet,
            string cacheDocs,
            Action<string, string> log,
            out string note)
        {
            note = "";
            string sftpExe = FindSftpExe();
            if (string.IsNullOrEmpty(sftpExe))
            {
                note = "sftp.exe not found";
                return false;
            }

            string remoteInbox = ResolveRemoteInbox(cfg);
            string batch = Path.Combine(Path.GetTempPath(), "rpa-sftp-" + Guid.NewGuid().ToString("N") + ".bat");
            string host = cfg.SftpHost.Trim();
            int port = cfg.SftpPort > 0 ? cfg.SftpPort : 22;
            string user = string.IsNullOrWhiteSpace(cfg.SftpUser)
                ? Environment.UserName
                : cfg.SftpUser.Trim();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("lcd " + QuoteSftp(Path.GetDirectoryName(cacheSheet)));
            AppendCd(sb, remoteInbox);
            sb.AppendLine("get " + SheetFileName + " " + Path.GetFileName(cacheSheet));
            sb.AppendLine("bye");
            File.WriteAllText(batch, sb.ToString(), Encoding.ASCII);

            try
            {
                string listing;
                int exitCode;
                if (!RunSftpBatch(sftpExe, port, batch, user, host,
                    cfg != null ? cfg.SftpPassword : "", 120000, out listing, out exitCode, out note))
                {
                    return false;
                }
                if (!File.Exists(cacheSheet))
                {
                    note = "sftp exit=" + exitCode + " sheet missing " + SanitizeSftpLog(listing);
                    log(note, "WARN");
                    return false;
                }

                // Production inbox is kyc-imports/{NID}/image — not inbox/docs/{NID}.
                // A failed `get -r docs` used to abort the same batch and skip images.
                DownloadRemoteDocTrees(sftpExe, port, user, host,
                    cfg != null ? cfg.SftpPassword : "", remoteInbox, cacheDocs, cacheSheet, log);

                if (exitCode != 0)
                {
                    log("SFTP download finished with closed session — sheet saved", "WARN");
                }
                note = "sftp CLI " + user + "@" + host + ":" + port +
                    (string.IsNullOrEmpty(cfg.SftpPassword) ? " (key/agent)" : " (password)");
                return true;
            }
            catch (Exception ex)
            {
                note = ex.Message;
                return false;
            }
            finally
            {
                try { File.Delete(batch); } catch { }
            }
        }

        // Second session so a missing remote «docs» folder cannot abort the sheet get.
        private static void DownloadRemoteDocTrees(
            string sftpExe, int port, string user, string host, string password,
            string remoteInbox, string cacheDocs, string cacheSheet, Action<string, string> log)
        {
            if (string.IsNullOrEmpty(cacheDocs)) { return; }
            try { Directory.CreateDirectory(cacheDocs); } catch { }

            FlattenImportedDocs(cacheDocs, log);
            List<string> nids = ReadSheetNationalIds(cacheSheet, 500);
            List<string> pending = new List<string>();
            for (int i = 0; i < nids.Count; i++)
            {
                if (!ClientHasImages(cacheDocs, nids[i])) { pending.Add(nids[i]); }
            }
            log("SFTP docs: destination=" + cacheDocs + " sheetNids=" + nids.Count +
                " pending=" + pending.Count + " withFiles=" + CountNidsWithImages(cacheDocs, nids), "INFO");
            if (pending.Count == 0) { return; }

            // From kyc-imports, `ls {NID}` prints NID/file when images exist (empty dirs print nothing).
            // Then get NID/file NID/file — never get -r (Windows creates an empty folder).
            string listBat = Path.Combine(Path.GetTempPath(), "rpa-sftp-ls-" + Guid.NewGuid().ToString("N") + ".bat");
            string getBat = Path.Combine(Path.GetTempPath(), "rpa-sftp-get-" + Guid.NewGuid().ToString("N") + ".bat");
            try
            {
                File.WriteAllText(listBat, BuildRemoteLsBatch(remoteInbox, pending), Encoding.ASCII);
                string listing;
                int exitCode;
                string note;
                RunSftpBatch(sftpExe, port, listBat, user, host, password, 180000,
                    out listing, out exitCode, out note);
                Dictionary<string, List<string>> remote = ParseRemoteDocsByNid(listing);
                int remoteFiles = CountMappedFiles(remote);
                log("SFTP remote inventory files=" + remoteFiles + " nidsWithFiles=" + remote.Count +
                    " " + SanitizeSftpLog(listing, 200), "INFO");

                if (remoteFiles == 0)
                {
                    log("SFTP remote inventory — no images under kyc-imports/{NID} (folders may be empty)", "WARN");
                    return;
                }

                foreach (KeyValuePair<string, List<string>> kv in remote)
                {
                    try { Directory.CreateDirectory(Path.Combine(cacheDocs, kv.Key)); } catch { }
                    log("SFTP will pull «" + kv.Key + "» remoteFiles=" + kv.Value.Count, "INFO");
                }

                File.WriteAllText(getBat, BuildNamedGetBatch(cacheDocs, remoteInbox, remote), Encoding.ASCII);
                string getListing;
                RunSftpBatch(sftpExe, port, getBat, user, host, password, 300000,
                    out getListing, out exitCode, out note);
                FlattenImportedDocs(cacheDocs, log);

                int localOk = 0;
                foreach (KeyValuePair<string, List<string>> kv in remote)
                {
                    int n = CountFilesRecursive(Path.Combine(cacheDocs, kv.Key));
                    if (n > 0) { localOk++; }
                    log("SFTP get «" + kv.Key + "» files=" + n + " dest=" +
                        Path.Combine(cacheDocs, kv.Key), n > 0 ? "OK" : "WARN");
                }
                log("SFTP named get done pulledNids=" + localOk + "/" + remote.Count +
                    " " + SanitizeSftpLog(getListing, 280),
                    localOk > 0 ? "OK" : "WARN");
            }
            catch (Exception ex)
            {
                log("SFTP docs pull failed: " + ex.Message, "WARN");
            }
            finally
            {
                try { if (File.Exists(listBat)) { File.Delete(listBat); } } catch { }
                try { if (File.Exists(getBat)) { File.Delete(getBat); } } catch { }
            }
        }

        private static int CountFiles(string dir)
        {
            return CountFilesRecursive(dir);
        }

        private static int CountFilesRecursive(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { return 0; }
                int n = Directory.GetFiles(dir).Length;
                string[] subs = Directory.GetDirectories(dir);
                for (int i = 0; i < subs.Length; i++)
                {
                    n += CountFilesRecursive(subs[i]);
                }
                return n;
            }
            catch { return 0; }
        }

        private static int CountMappedFiles(Dictionary<string, List<string>> map)
        {
            int n = 0;
            if (map == null) { return 0; }
            foreach (KeyValuePair<string, List<string>> kv in map)
            {
                if (kv.Value != null) { n += kv.Value.Count; }
            }
            return n;
        }

        private static Dictionary<string, List<string>> ParseRemoteDocsByNid(string listing)
        {
            Dictionary<string, List<string>> map =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(listing)) { return map; }
            string currentNid = "";
            string[] lines = listing.Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (line.Length == 0) { continue; }
                if (line.StartsWith("sftp>", StringComparison.OrdinalIgnoreCase))
                {
                    string cmd = line.Substring(5).Trim();
                    if (cmd.StartsWith("ls ", StringComparison.OrdinalIgnoreCase))
                    {
                        string arg = cmd.Substring(3).Trim().Replace("\\", "/");
                        int slash = arg.LastIndexOf('/');
                        if (slash >= 0) { arg = arg.Substring(slash + 1); }
                        string digits = KycSheetAdapter.DigitsOnly(arg);
                        currentNid = digits.Length >= 10 ? digits : "";
                    }
                    continue;
                }
                if (line.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int p = 0; p < parts.Length; p++)
                {
                    string token = parts[p].Replace("\\", "/");
                    int slash = token.LastIndexOf('/');
                    if (slash > 0)
                    {
                        string folder = token.Substring(0, slash);
                        string file = token.Substring(slash + 1);
                        int fs = folder.LastIndexOf('/');
                        if (fs >= 0) { folder = folder.Substring(fs + 1); }
                        string nid = KycSheetAdapter.DigitsOnly(folder);
                        if (nid.Length >= 10 && LooksLikeDocFile(file))
                        {
                            AddRemoteFile(map, nid, file);
                        }
                    }
                    else if (LooksLikeDocFile(token) && currentNid.Length >= 10)
                    {
                        AddRemoteFile(map, currentNid, token);
                    }
                }
            }
            return map;
        }

        private static void AddRemoteFile(Dictionary<string, List<string>> map, string nid, string file)
        {
            if (map == null || string.IsNullOrEmpty(nid) || string.IsNullOrEmpty(file)) { return; }
            List<string> files;
            if (!map.TryGetValue(nid, out files) || files == null)
            {
                files = new List<string>();
                map[nid] = files;
            }
            if (files.IndexOf(file) < 0) { files.Add(file); }
        }

        private static bool LooksLikeDocFile(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('.') < 0) { return false; }
            string ext = Path.GetExtension(name);
            if (string.IsNullOrEmpty(ext)) { return false; }
            ext = ext.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".jfif" ||
                   ext == ".tif" || ext == ".tiff" || ext == ".bmp" || ext == ".gif" ||
                   ext == ".pdf" || ext == ".webp" || ext == ".heic" || ext == ".heif";
        }

        // get -r can land as dest\kyc-imports\{NID}\file — attach reads dest\{NID}\file.
        private static void FlattenImportedDocs(string dest, Action<string, string> log)
        {
            if (string.IsNullOrEmpty(dest) || !Directory.Exists(dest)) { return; }
            PromoteNestedIdFolders(Path.Combine(dest, "kyc-imports"), dest, log);
            PromoteNestedIdFolders(Path.Combine(dest, DocsRelative), dest, log);
        }

        private static void PromoteNestedIdFolders(string nested, string dest, Action<string, string> log)
        {
            if (string.IsNullOrEmpty(nested) || PathsEqual(nested, dest) || !Directory.Exists(nested))
            {
                return;
            }
            string[] dirs;
            try { dirs = Directory.GetDirectories(nested); }
            catch { return; }
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                string digits = KycSheetAdapter.DigitsOnly(name);
                if (digits.Length < 10) { continue; }
                string[] files;
                try { files = Directory.GetFiles(dirs[i]); }
                catch { continue; }
                if (files == null || files.Length == 0) { continue; }
                string target = Path.Combine(dest, digits);
                try
                {
                    Directory.CreateDirectory(target);
                    for (int f = 0; f < files.Length; f++)
                    {
                        string to = Path.Combine(target, Path.GetFileName(files[f]));
                        File.Copy(files[f], to, true);
                    }
                    if (log != null)
                    {
                        log("docs flatten " + name + " → " + target + " (" + files.Length + ")", "OK");
                    }
                }
                catch (Exception ex)
                {
                    if (log != null) { log("docs flatten failed " + name + ": " + ex.Message, "WARN"); }
                }
            }
        }

        private static int CountNidsWithImages(string cacheDocs, List<string> nids)
        {
            int n = 0;
            if (nids == null) { return 0; }
            for (int i = 0; i < nids.Count; i++)
            {
                if (ClientHasImages(cacheDocs, nids[i])) { n++; }
            }
            return n;
        }

        private static bool ClientHasImages(string cacheDocs, string nid)
        {
            if (string.IsNullOrEmpty(nid)) { return false; }
            string[] roots = EffectiveDocsRoots(cacheDocs);
            for (int i = 0; i < roots.Length; i++)
            {
                try
                {
                    string under = Path.Combine(roots[i], nid);
                    if (Directory.Exists(under) && CountFilesRecursive(under) > 0) { return true; }
                }
                catch { }
            }
            return false;
        }

        public static bool IsInternalCachePath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.IndexOf("SftpCache", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Settings «مجلد المستندات» — never the hidden AppData SftpCache folder.
        public static string ResolveOperatorDocsRoot(BotConfig cfg)
        {
            string configured = cfg != null ? (cfg.ScansRoot ?? "").Trim() : "";
            if (!string.IsNullOrEmpty(configured) && !IsInternalCachePath(configured))
            {
                try { return Path.GetFullPath(configured); } catch { return configured; }
            }

            string inboxDocs = RemoteDocsPath(ResolveLocalRoot(cfg));
            if (!string.IsNullOrEmpty(inboxDocs))
            {
                try
                {
                    Directory.CreateDirectory(inboxDocs);
                    return Path.GetFullPath(inboxDocs);
                }
                catch { }
            }

            if (cfg != null && !string.IsNullOrEmpty(cfg.BaseDir))
            {
                string offline = Path.Combine(cfg.BaseDir, "OfflineInbox", InboxRelative, DocsRelative);
                try
                {
                    Directory.CreateDirectory(offline);
                    return Path.GetFullPath(offline);
                }
                catch { }
            }

            string fallback = cfg != null
                ? Path.Combine(cfg.BaseDir ?? "", "SftpCache", DocsRelative)
                : "";
            return fallback;
        }

        // Copy pulled images into Settings «مجلد المستندات» so attach reads the same path.
        private static string PublishDocsToConfigured(
            BotConfig cfg, string pulledDocs, string cacheDocs, Action<string, string> log)
        {
            string configured = ResolveOperatorDocsRoot(cfg);
            try { Directory.CreateDirectory(configured); } catch { }

            string source = !string.IsNullOrEmpty(pulledDocs) ? pulledDocs : cacheDocs;
            if (!string.IsNullOrEmpty(source) && Directory.Exists(source) && !PathsEqual(source, configured))
            {
                try
                {
                    MirrorDirectory(source, configured);
                    if (log != null)
                    {
                        log("docs synced → configured path " + configured, "OK");
                    }
                }
                catch (Exception ex)
                {
                    if (log != null) { log("docs copy to configured path failed: " + ex.Message, "WARN"); }
                }
            }

            if (cfg != null)
            {
                cfg.ScansRoot = configured;
            }
            LastDocsRoot = configured;
            return configured;
        }

        public static string[] EffectiveDocsRoots(string cacheDocs)
        {
            List<string> roots = new List<string>();
            AddIfDir(roots, LastDocsRoot);
            if (!string.IsNullOrEmpty(LastDocsRoot))
            {
                AddIfDir(roots, Path.Combine(LastDocsRoot, "kyc-imports"));
            }
            AddIfDir(roots, cacheDocs);
            if (!string.IsNullOrEmpty(cacheDocs))
            {
                AddIfDir(roots, Path.Combine(cacheDocs, "kyc-imports"));
                AddIfDir(roots, Path.Combine(cacheDocs, DocsRelative));
                AddIfDir(roots, Path.Combine(cacheDocs, InboxRelative, DocsRelative));
            }
            if (!string.IsNullOrEmpty(LastDocsRoot)) { AddIfDir(roots, LastDocsRoot); }
            return roots.ToArray();
        }

        private static void AddIfDir(List<string> roots, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || roots == null) { return; }
            try
            {
                if (!Directory.Exists(path)) { return; }
                string full = Path.GetFullPath(path);
                for (int i = 0; i < roots.Count; i++)
                {
                    if (string.Equals(roots[i], full, StringComparison.OrdinalIgnoreCase)) { return; }
                }
                roots.Add(full);
            }
            catch { }
        }

        private static List<string> ReadSheetNationalIds(string sheetPath, int max)
        {
            List<string> ids = new List<string>();
            if (string.IsNullOrEmpty(sheetPath) || !File.Exists(sheetPath) || max <= 0)
            {
                return ids;
            }
            try
            {
                string[] lines = File.ReadAllLines(sheetPath, Encoding.UTF8);
                if (lines.Length < 2) { return ids; }
                int col = 0;
                string header = lines[0] ?? "";
                string[] heads = header.Split(',');
                for (int h = 0; h < heads.Length; h++)
                {
                    string n = (heads[h] ?? "").Trim().Trim('"');
                    if (n.IndexOf("ID Number", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("NationalId", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("الرقم", StringComparison.Ordinal) >= 0)
                    {
                        col = h;
                        break;
                    }
                }
                for (int i = 1; i < lines.Length && ids.Count < max; i++)
                {
                    string[] cells = (lines[i] ?? "").Split(',');
                    if (col >= cells.Length) { continue; }
                    string id = KycSheetAdapter.DigitsOnly(cells[col].Trim().Trim('"'));
                    if (id.Length >= 10) { ids.Add(id); }
                }
            }
            catch { }
            return ids;
        }

        private static string BuildRemoteLsBatch(string remoteInbox, List<string> nids)
        {
            StringBuilder sb = new StringBuilder();
            AppendCd(sb, remoteInbox);
            if (nids != null)
            {
                for (int i = 0; i < nids.Count; i++)
                {
                    if (!string.IsNullOrEmpty(nids[i])) { sb.AppendLine("ls " + nids[i]); }
                }
            }
            sb.AppendLine("bye");
            return sb.ToString();
        }

        private static string BuildNamedGetBatch(
            string cacheDocs, string remoteInbox, Dictionary<string, List<string>> remote)
        {
            StringBuilder sb = new StringBuilder();
            AppendCd(sb, remoteInbox);
            if (remote != null)
            {
                foreach (KeyValuePair<string, List<string>> kv in remote)
                {
                    if (kv.Value == null) { continue; }
                    sb.AppendLine("lcd " + QuoteSftp(Path.Combine(cacheDocs, kv.Key)));
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        string fn = kv.Value[i];
                        if (string.IsNullOrEmpty(fn)) { continue; }
                        sb.AppendLine("get " + kv.Key + "/" + fn + " " + fn);
                    }
                }
            }
            sb.AppendLine("bye");
            return sb.ToString();
        }

        private static bool TrySftpCliUpload(
            BotConfig cfg, string workingSheet, Action<string, string> log, out string note)
        {
            note = "";
            string sftpExe = FindSftpExe();
            if (string.IsNullOrEmpty(sftpExe))
            {
                note = "sftp.exe not found";
                return false;
            }
            string remoteInbox = ResolveRemoteInbox(cfg);
            string batch = Path.Combine(Path.GetTempPath(), "rpa-sftp-up-" + Guid.NewGuid().ToString("N") + ".bat");
            string host = cfg.SftpHost.Trim();
            int port = cfg.SftpPort > 0 ? cfg.SftpPort : 22;
            string user = string.IsNullOrWhiteSpace(cfg.SftpUser)
                ? Environment.UserName
                : cfg.SftpUser.Trim();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("lcd " + QuoteSftp(Path.GetDirectoryName(workingSheet)));
            AppendCd(sb, remoteInbox);
            sb.AppendLine("put " + Path.GetFileName(workingSheet) + " " + SheetFileName);
            sb.AppendLine("bye");
            File.WriteAllText(batch, sb.ToString(), Encoding.ASCII);

            try
            {
                string listing;
                int exitCode;
                if (!RunSftpBatch(sftpExe, port, batch, user, host,
                    cfg != null ? cfg.SftpPassword : "", 120000, out listing, out exitCode, out note))
                {
                    return false;
                }
                if (exitCode != 0 && AuthLooksFailed(listing))
                {
                    note = "sftp exit=" + exitCode + " " + SanitizeSftpLog(listing);
                    return false;
                }
                note = user + "@" + host + ":" + port + "/" +
                    (string.IsNullOrEmpty(remoteInbox) ? "" : remoteInbox + "/") + SheetFileName;
                return true;
            }
            catch (Exception ex)
            {
                note = ex.Message;
                return false;
            }
            finally
            {
                try { File.Delete(batch); } catch { }
            }
        }

        private static bool RunSftpBatch(
            string sftpExe, int port, string batch, string user, string host,
            string password, int timeoutMs, out string listing, out int exitCode, out string note)
        {
            listing = "";
            exitCode = -1;
            note = "";
            password = NormalizeSftpSecret(password);
            if (BotConfig.IsPlaceholderSftpPassword(password))
            {
                note = "SFTP password empty/placeholder — set the real password in Settings (same as sftp.exe)";
                return false;
            }
            string[] optionSets = SshClientOptionSets(port);
            string lastNote = "";
            for (int i = 0; i < optionSets.Length; i++)
            {
                string askPassCleanup = null;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = sftpExe;
                    psi.Arguments = optionSets[i] + " -b \"" + batch + "\" " + user + "@" + host;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;
                    ApplyPasswordAskPass(psi, password, out askPassCleanup);
                    using (Process p = Process.Start(psi))
                    {
                        if (p == null)
                        {
                            lastNote = "failed to start sftp.exe";
                            continue;
                        }
                        string stdout = p.StandardOutput.ReadToEnd();
                        string stderr = p.StandardError.ReadToEnd();
                        if (!p.WaitForExit(timeoutMs))
                        {
                            try { p.Kill(); } catch { }
                            lastNote = "sftp timeout";
                            continue;
                        }
                        listing = (stdout ?? "") + "\n" + (stderr ?? "");
                        exitCode = p.ExitCode;
                    }
                    if (AuthLooksFailed(listing))
                    {
                        note = "sftp auth failed " + SanitizeSftpLog(listing);
                        return false;
                    }
                    if (exitCode == 0 || HasUsefulListing(listing) || SoftDisconnectOk(exitCode, listing, null))
                    {
                        note = "sftp ok";
                        return true;
                    }
                    lastNote = "sftp exit=" + exitCode + " " + SanitizeSftpLog(listing);
                }
                catch (Exception ex)
                {
                    lastNote = ex.Message;
                }
                finally
                {
                    CleanupAskPass(askPassCleanup);
                }
            }
            note = string.IsNullOrEmpty(lastNote) ? "sftp failed" : lastNote;
            return false;
        }

        private static string FindSftpExe()
        {
            string sftpExe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"OpenSSH\sftp.exe");
            if (File.Exists(sftpExe)) { return sftpExe; }
            string beside = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"OpenSSH\sftp.exe");
            if (File.Exists(beside)) { return beside; }
            try
            {
                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                string[] parts = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string cand = Path.Combine(parts[i].Trim(), "sftp.exe");
                    if (File.Exists(cand)) { return cand; }
                }
            }
            catch { }
            return "";
        }

        private static string NormalizeSftpSecret(string password)
        {
            if (string.IsNullOrEmpty(password)) { return ""; }
            StringBuilder sb = new StringBuilder(password.Length);
            for (int i = 0; i < password.Length; i++)
            {
                char c = password[i];
                if (c == '\r' || c == '\n') { continue; }
                if (c >= '\u0660' && c <= '\u0669') { sb.Append((char)('0' + (c - '\u0660'))); continue; }
                if (c >= '\u06F0' && c <= '\u06F9') { sb.Append((char)('0' + (c - '\u06F0'))); continue; }
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        private static string ExtractAskPassExe(string root)
        {
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                Stream s = asm.GetManifestResourceStream("RpaUi.SftpAskPass");
                if (s == null) { return ""; }
                string dest = Path.Combine(root, "RpaSftpAskPass.exe");
                using (s)
                using (FileStream fs = File.Create(dest))
                {
                    s.CopyTo(fs);
                }
                return dest;
            }
            catch
            {
                return "";
            }
        }

        // OpenSSH ignores .cmd ASKPASS on Windows. Helper is embedded inside RPA-V1.exe.
        private static void ApplyPasswordAskPass(
            ProcessStartInfo psi, string password, out string cleanupRoot)
        {
            cleanupRoot = null;
            if (psi == null || string.IsNullOrEmpty(password)) { return; }
            string root = Path.Combine(Path.GetTempPath(), "rpa-askpass-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string pwFile = Path.Combine(root, "pw.txt");
            File.WriteAllText(pwFile, password, new UTF8Encoding(false));

            string askPass = ExtractAskPassExe(root);
            if (string.IsNullOrEmpty(askPass)) { return; }

            psi.EnvironmentVariables["RPA_SFTP_PWFILE"] = pwFile;
            psi.EnvironmentVariables["SSH_ASKPASS"] = askPass;
            psi.EnvironmentVariables["SSH_ASKPASS_REQUIRE"] = "force";
            psi.EnvironmentVariables["DISPLAY"] = "localhost:0";
            cleanupRoot = root;
        }

        private static void CleanupAskPass(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) { return; }
            try { Directory.Delete(root, true); } catch { }
        }

        private static string SanitizeSftpLog(string text)
        {
            return SanitizeSftpLog(text, 240);
        }

        private static string SanitizeSftpLog(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) { return ""; }
            if (maxLen < 80) { maxLen = 80; }
            string t = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (t.Length > maxLen) { t = t.Substring(0, maxLen) + "…"; }
            return t;
        }

        private static string QuoteSftp(string path)
        {
            if (string.IsNullOrEmpty(path)) { return path; }
            return path.Replace("\\", "/");
        }
    }
}
