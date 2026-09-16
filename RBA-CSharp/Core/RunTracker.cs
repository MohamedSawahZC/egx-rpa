// =============================================================================
//  RunTracker.cs - runs.csv + tracking.csv with merge rules (never overwrite CODED).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;

namespace RbaCore
{
    public class BotRun
    {
        public string RunId;
        public string StartedAt;
        public string EndedAt;
        public int DurationSec;
        public string Trigger;
        public int Iteration;
        public string FileName;
        public string FilePath;
        public string Stage;
        public int TotalRows;
        public int Processed;
        public int Validated;
        public int Failed;
        public int Coded;
        public int Rejected;
        public int Retries;
        public int Errors;
        public string Status;
        public string Message;
        public string LogFile;
    }

    public class MergeResult
    {
        public List<OrderedDictionary> Rows;
        public int Protected;
    }

    public static class RunTracker
    {
        public static readonly string[] RunColumns = new string[] {
            "RunId","StartedAt","EndedAt","DurationSec","Trigger","Iteration",
            "FileName","FilePath","Stage",
            "TotalRows","Processed","Validated","Failed","Coded","Rejected","Retries","Errors",
            "Status","Message","LogFile"
        };

        public static readonly string[] TrackingColumns = new string[] {
            "NationalId","Name","Status","NIN","CODE12","RejectReason","Notes","SourceFile","LastUpdated"
        };

        private static readonly HashSet<string> ProtectedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "CODED", "REJECTED", "SENT_UNDER_REVIEW", "SAVED_PENDING", "ALREADY_CODED"
        };

        public static BotRun NewRun(string stateDir, string trigger, string filePath, string stage)
        {
            BotRun run = new BotRun();
            run.RunId = "R" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + (new Random().Next(100, 999));
            run.StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            run.EndedAt = "";
            run.DurationSec = 0;
            run.Trigger = string.IsNullOrEmpty(trigger) ? "Manual" : trigger;
            run.Iteration = 1;
            run.FilePath = filePath != null ? filePath : "";
            run.FileName = string.IsNullOrEmpty(filePath) ? "" : Path.GetFileName(filePath);
            run.Stage = stage != null ? stage : "";
            run.Status = "RUNNING";
            run.Message = "";
            run.LogFile = "";
            return run;
        }

        public static void CompleteRun(BotRun run, string status, string message)
        {
            run.EndedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            try
            {
                DateTime start = DateTime.ParseExact(run.StartedAt, "yyyy-MM-dd HH:mm:ss", null);
                run.DurationSec = (int)(DateTime.Now - start).TotalSeconds;
            }
            catch { }
            run.Status = status;
            if (!string.IsNullOrEmpty(message)) { run.Message = message; }
        }

        public static void SaveRun(string stateDir, BotRun run, int keepLast)
        {
            if (!Directory.Exists(stateDir)) { Directory.CreateDirectory(stateDir); }
            string path = Path.Combine(stateDir, "runs.csv");
            List<OrderedDictionary> existing = File.Exists(path) ? CsvUtil.ReadCsv(path) : new List<OrderedDictionary>();
            List<OrderedDictionary> outRows = new List<OrderedDictionary>();
            bool replaced = false;
            for (int i = 0; i < existing.Count; i++)
            {
                string id = CsvUtil.Get(existing[i], "RunId", "");
                if (id == run.RunId)
                {
                    outRows.Add(ToDict(run));
                    replaced = true;
                }
                else { outRows.Add(existing[i]); }
            }
            if (!replaced) { outRows.Add(ToDict(run)); }
            if (keepLast > 0 && outRows.Count > keepLast)
            {
                outRows = outRows.GetRange(outRows.Count - keepLast, keepLast);
            }
            CsvUtil.WriteCsv(path, RunColumns, outRows);
        }

        public static List<OrderedDictionary> GetRuns(string stateDir, int last)
        {
            string path = Path.Combine(stateDir, "runs.csv");
            List<OrderedDictionary> rows = File.Exists(path) ? CsvUtil.ReadCsv(path) : new List<OrderedDictionary>();
            if (last > 0 && rows.Count > last)
            {
                rows = rows.GetRange(rows.Count - last, last);
            }
            rows.Reverse();
            return rows;
        }

        private static OrderedDictionary ToDict(BotRun run)
        {
            OrderedDictionary d = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
            d["RunId"] = run.RunId;
            d["StartedAt"] = run.StartedAt;
            d["EndedAt"] = run.EndedAt;
            d["DurationSec"] = run.DurationSec.ToString();
            d["Trigger"] = run.Trigger;
            d["Iteration"] = run.Iteration.ToString();
            d["FileName"] = run.FileName;
            d["FilePath"] = run.FilePath;
            d["Stage"] = run.Stage;
            d["TotalRows"] = run.TotalRows.ToString();
            d["Processed"] = run.Processed.ToString();
            d["Validated"] = run.Validated.ToString();
            d["Failed"] = run.Failed.ToString();
            d["Coded"] = run.Coded.ToString();
            d["Rejected"] = run.Rejected.ToString();
            d["Retries"] = run.Retries.ToString();
            d["Errors"] = run.Errors.ToString();
            d["Status"] = run.Status;
            d["Message"] = run.Message;
            d["LogFile"] = run.LogFile;
            return d;
        }

        public static List<OrderedDictionary> LoadTracking(string path)
        {
            if (!File.Exists(path)) { return new List<OrderedDictionary>(); }
            return CsvUtil.ReadCsv(path);
        }

        public static void SaveTracking(string path, List<OrderedDictionary> rows)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
            // Normalize columns.
            List<OrderedDictionary> norm = new List<OrderedDictionary>();
            for (int i = 0; i < rows.Count; i++)
            {
                OrderedDictionary n = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < TrackingColumns.Length; c++)
                {
                    string col = TrackingColumns[c];
                    n[col] = CsvUtil.Get(rows[i], col, "");
                }
                norm.Add(n);
            }
            WithTrackingLock(delegate
            {
                CsvUtil.WriteCsv(path, TrackingColumns, norm);
            });
        }

        public static MergeResult MergeTracking(List<OrderedDictionary> existing, List<OrderedDictionary> fresh)
        {
            Dictionary<string, OrderedDictionary> map = new Dictionary<string, OrderedDictionary>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < existing.Count; i++)
            {
                string nid = CsvUtil.Get(existing[i], "NationalId", "").Trim();
                if (!string.IsNullOrEmpty(nid)) { map[nid] = CloneRow(existing[i]); }
            }

            int protectedCount = 0;
            for (int i = 0; i < fresh.Count; i++)
            {
                string nid = CsvUtil.Get(fresh[i], "NationalId", "").Trim();
                if (string.IsNullOrEmpty(nid)) { continue; }
                if (map.ContainsKey(nid))
                {
                    string oldStatus = CsvUtil.Get(map[nid], "Status", "");
                    if (ProtectedStatuses.Contains(oldStatus))
                    {
                        protectedCount++;
                        // Keep exchange outcome; refresh notes/source lightly.
                        string notes = CsvUtil.Get(fresh[i], "Notes", "");
                        if (!string.IsNullOrEmpty(notes) && string.IsNullOrEmpty(CsvUtil.Get(map[nid], "Notes", "")))
                        {
                            map[nid]["Notes"] = notes;
                        }
                        continue;
                    }
                }
                map[nid] = CloneRow(fresh[i]);
            }

            MergeResult mr = new MergeResult();
            mr.Rows = new List<OrderedDictionary>();
            foreach (KeyValuePair<string, OrderedDictionary> kv in map) { mr.Rows.Add(kv.Value); }
            mr.Protected = protectedCount;
            return mr;
        }

        public static void UpdateTrackingStore(string path, Func<List<OrderedDictionary>, List<OrderedDictionary>> mutator)
        {
            WithTrackingLock(delegate
            {
                List<OrderedDictionary> prev = LoadTrackingUnlocked(path);
                List<OrderedDictionary> next = mutator(prev);
                SaveTrackingUnlocked(path, next);
            });
        }

        private static List<OrderedDictionary> LoadTrackingUnlocked(string path)
        {
            if (!File.Exists(path)) { return new List<OrderedDictionary>(); }
            return CsvUtil.ReadCsv(path);
        }

        private static void SaveTrackingUnlocked(string path, List<OrderedDictionary> rows)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
            List<OrderedDictionary> norm = new List<OrderedDictionary>();
            for (int i = 0; i < rows.Count; i++)
            {
                OrderedDictionary n = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < TrackingColumns.Length; c++)
                {
                    string col = TrackingColumns[c];
                    n[col] = CsvUtil.Get(rows[i], col, "");
                }
                norm.Add(n);
            }
            CsvUtil.WriteCsv(path, TrackingColumns, norm);
        }

        private static OrderedDictionary CloneRow(OrderedDictionary src)
        {
            OrderedDictionary n = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < TrackingColumns.Length; c++)
            {
                string col = TrackingColumns[c];
                n[col] = CsvUtil.Get(src, col, "");
            }
            return n;
        }

        private static void WithTrackingLock(Action action)
        {
            Mutex mutex = null;
            bool held = false;
            try
            {
                try { mutex = new Mutex(false, @"Global\RBA.Tracking"); }
                catch { mutex = new Mutex(false, @"Local\RBA.Tracking"); }
                try { held = mutex.WaitOne(20000, false); }
                catch (AbandonedMutexException) { held = true; }
                if (!held) { throw new Exception("another process is writing tracking state"); }
                action();
            }
            finally
            {
                if (held) { try { mutex.ReleaseMutex(); } catch { } }
                if (mutex != null) { try { mutex.Dispose(); } catch { } }
            }
        }
    }
}
