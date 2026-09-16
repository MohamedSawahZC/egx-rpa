// =============================================================================
//  DailyExport.cs - period XML export runs once per calendar day at a clock time
//  (12-hour + AM/PM). Not chained after each client save.
// =============================================================================
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RbaCore
{
    public static class DailyExport
    {
        private const string StampFile = "daily-export-last.txt";

        public static DateTime ScheduledToday(BotSettings s, DateTime now)
        {
            int hour12 = s != null ? s.DailyExportHour : 5;
            int minute = s != null ? s.DailyExportMinute : 0;
            bool pm = s == null || s.DailyExportPm;
            if (hour12 < 1 || hour12 > 12) { hour12 = 5; }
            if (minute < 0 || minute > 59) { minute = 0; }

            int hour24 = hour12 % 12;
            if (pm) { hour24 += 12; }
            return new DateTime(now.Year, now.Month, now.Day, hour24, minute, 0);
        }

        public static string Describe(BotSettings s)
        {
            int hour12 = s != null ? s.DailyExportHour : 5;
            int minute = s != null ? s.DailyExportMinute : 0;
            string ampm = (s == null || s.DailyExportPm) ? "PM" : "AM";
            return hour12.ToString("00", CultureInfo.InvariantCulture) + ":" +
                minute.ToString("00", CultureInfo.InvariantCulture) + " " + ampm;
        }

        public static bool IsDue(BotSettings s, string stateDir, DateTime now)
        {
            if (s == null || !s.DailyExportEnabled) { return false; }
            DateTime when = ScheduledToday(s, now);
            if (now < when) { return false; }

            string last = ReadLastDay(stateDir);
            string today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return !string.Equals(last, today, StringComparison.Ordinal);
        }

        public static void MarkDone(string stateDir, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(stateDir)) { return; }
            try
            {
                Directory.CreateDirectory(stateDir);
                string path = Path.Combine(stateDir, StampFile);
                File.WriteAllText(path, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    new UTF8Encoding(true));
            }
            catch { }
        }

        private static string ReadLastDay(string stateDir)
        {
            if (string.IsNullOrWhiteSpace(stateDir)) { return ""; }
            string path = Path.Combine(stateDir, StampFile);
            if (!File.Exists(path)) { return ""; }
            try { return File.ReadAllText(path, Encoding.UTF8).Trim(); }
            catch { return ""; }
        }
    }
}
