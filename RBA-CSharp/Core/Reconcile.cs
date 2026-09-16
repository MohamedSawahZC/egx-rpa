// =============================================================================
//  Reconcile.cs - apply EGX export XML + rejection HTM into tracking.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

namespace RbaCore
{
    public class ReconcileResult
    {
        public int Coded;
        public int Rejected;
        public List<string> LogLines;
    }

    public static class Reconcile
    {
        public static ReconcileResult Run(string exportFolder, string trackingPath, int rejectionCodePage)
        {
            ReconcileResult result = new ReconcileResult();
            result.LogLines = new List<string>();
            result.Coded = 0;
            result.Rejected = 0;

            List<OrderedDictionary> tracking = RunTracker.LoadTracking(trackingPath);
            Dictionary<string, OrderedDictionary> byId = IndexByNationalId(tracking);
            Dictionary<string, OrderedDictionary> byName = IndexByName(tracking);

            string xml = ReportParsers.GetLatestCodingExport(exportFolder);
            if (!string.IsNullOrEmpty(xml))
            {
                List<CodingExportRow> coded = ReportParsers.ParseCodingXmlExport(xml);
                result.LogLines.Add("coding export: " + Path.GetFileName(xml) + " (" + coded.Count + " rows)");
                for (int i = 0; i < coded.Count; i++)
                {
                    CodingExportRow c = coded[i];
                    OrderedDictionary row = null;
                    string idNum = c.ID_NUMBER != null ? c.ID_NUMBER.Trim() : "";
                    if (!string.IsNullOrEmpty(idNum) && byId.ContainsKey(idNum)) { row = byId[idNum]; }
                    else if (!string.IsNullOrEmpty(c.CLIENT_NAME) && byName.ContainsKey(c.CLIENT_NAME.Trim()))
                    {
                        row = byName[c.CLIENT_NAME.Trim()];
                    }
                    if (row == null) { continue; }
                    row["Status"] = "CODED";
                    row["NIN"] = c.NIN != null ? c.NIN : "";
                    row["CODE12"] = c.CODE12 != null ? c.CODE12 : "";
                    row["RejectReason"] = "";
                    row["LastUpdated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    result.Coded++;
                }
            }
            else
            {
                result.LogLines.Add("no Clients*.xml found in " + exportFolder);
            }

            string htm = ReportParsers.GetLatestRejectionReport(exportFolder);
            if (!string.IsNullOrEmpty(htm))
            {
                List<RejectionRow> rejs = ReportParsers.ParseRejectionHtmReport(htm, rejectionCodePage);
                result.LogLines.Add("rejection report: " + Path.GetFileName(htm) + " (" + rejs.Count + " rows)");
                for (int i = 0; i < rejs.Count; i++)
                {
                    RejectionRow rj = rejs[i];
                    if (string.IsNullOrEmpty(rj.Name)) { continue; }
                    string key = rj.Name.Trim();
                    if (!byName.ContainsKey(key)) { continue; }
                    OrderedDictionary row = byName[key];
                    string status = CsvUtil.Get(row, "Status", "");
                    if (string.Equals(status, "CODED", StringComparison.OrdinalIgnoreCase)) { continue; }
                    row["Status"] = "REJECTED";
                    row["RejectReason"] = rj.RejectReason != null ? rj.RejectReason : "";
                    row["LastUpdated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    result.Rejected++;
                }
            }

            RunTracker.SaveTracking(trackingPath, tracking);
            result.LogLines.Add("reconcile done: coded=" + result.Coded + " rejected=" + result.Rejected);
            return result;
        }

        private static Dictionary<string, OrderedDictionary> IndexByNationalId(List<OrderedDictionary> rows)
        {
            Dictionary<string, OrderedDictionary> map = new Dictionary<string, OrderedDictionary>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                string nid = CsvUtil.Get(rows[i], "NationalId", "").Trim();
                if (!string.IsNullOrEmpty(nid) && !map.ContainsKey(nid)) { map[nid] = rows[i]; }
            }
            return map;
        }

        private static Dictionary<string, OrderedDictionary> IndexByName(List<OrderedDictionary> rows)
        {
            Dictionary<string, OrderedDictionary> map = new Dictionary<string, OrderedDictionary>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                string name = CsvUtil.Get(rows[i], "Name", "").Trim();
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name)) { map[name] = rows[i]; }
            }
            return map;
        }
    }
}
