// =============================================================================
//  ConfirmDialog.cs - single place that answers Coding.exe modal prompts.
//
//  Coding raises «تأكيد» (Yes/No) after حفظ, then usually an info box. Standard
//  MessageBox captions carry an accelerator ampersand ("&Yes"), so every caption
//  is compared with '&' stripped. A negative button is never clicked.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Threading;

namespace RbaCore
{
    public class ConfirmDialogResult
    {
        public int Handled;
        public string LastTitle = "";
        public string LastButton = "";
        public bool TimedOut;
        // Body text of every prompt answered — lets callers react to what Coding said.
        public List<string> Messages = new List<string>();

        public bool SaidAlreadyExists
        {
            get
            {
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (ConfirmDialog.IsAlreadyExistsMessage(Messages[i])) { return true; }
                }
                return false;
            }
        }

        public bool SaidServerError
        {
            get
            {
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (ConfirmDialog.IsServerErrorMessage(Messages[i])) { return true; }
                }
                return false;
            }
        }

        public bool SaidMissingDocuments
        {
            get
            {
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (ConfirmDialog.IsMissingDocumentsMessage(Messages[i])) { return true; }
                }
                return false;
            }
        }
    }

    public static class ConfirmDialog
    {
        // Ordered by preference: an explicit «نعم/Yes» wins over a generic «موافق/OK».
        // «حفظ» is deliberately absent — it is a form action, never a prompt answer.
        private static readonly string[] Affirmative = new string[]
        {
            "نعم", "Yes", "موافق", "تأكيد", "تاكيد", "OK", "Ok", "متابعة", "Continue"
        };

        // A prompt is a small popup; the entry form also holds buttons and must not match.
        private const int MaxPromptControls = 12;

        // How long to keep watching for a follow-up box after answering one.
        private const int GraceMs = 2800;

        // Coding refuses a duplicate with «العميل موجود من قبل» + a single OK.
        private static readonly string[] AlreadyExistsPhrases = new string[]
        {
            "العميل موجود من قبل", "العميل موجود مسبقا", "موجود من قبل",
            "مسجل من قبل", "تم تكويده من قبل", "already exists"
        };

        public static bool IsAlreadyExistsMessage(string text)
        {
            return ArabicText.ContainsAny(text, AlreadyExistsPhrases);
        }

        public static bool IsMissingDocumentsMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) { return false; }
            return ArabicText.ContainsAny(text, new string[]
            {
                "ادخل المستندات", "أدخل المستندات", "بيانات غير كاملة"
            });
        }

        public static bool IsServerErrorMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) { return false; }
            string[] phrases = new string[]
            {
                "عفوا حدث خطأ", "عفواً حدث خطأ", "خطأ خارجى", "خطأ خارجي",
                "إعادة المحاولة", "حدث خطأ ما"
            };
            return ArabicText.ContainsAny(text, phrases);
        }

        // Static/label text shown inside a prompt, joined into one line.
        public static string ReadMessage(IntPtr dialog)
        {
            if (dialog == IntPtr.Zero || !Win32Interop.IsWindow(dialog)) { return ""; }
            List<string> parts = new List<string>();
            List<W32WindowInfo> kids = Win32Driver.GetChildren(dialog);
            for (int i = 0; i < kids.Count; i++)
            {
                if (!kids[i].Visible) { continue; }
                string kind = FieldDiscovery.GetControlKind(kids[i].Class);
                if (kind == "Button") { continue; }
                string t = kids[i].Text != null ? kids[i].Text.Trim() : "";
                if (t.Length > 0) { parts.Add(t); }
            }
            if (parts.Count == 0)
            {
                // VB6 labels can be windowless — MSAA is the only way to read them.
                List<string> viaAcc = AccessibleText.Harvest(dialog);
                for (int i = 0; i < viaAcc.Count; i++)
                {
                    if (IsAffirmative(viaAcc[i]) || IsNegative(viaAcc[i])) { continue; }
                    parts.Add(viaAcc[i]);
                }
            }
            return string.Join(" | ", parts.ToArray());
        }

        private static readonly string[] Negative = new string[]
        {
            "لا", "No", "إلغاء", "الغاء", "إلغاء الأمر", "Cancel", "Close", "إغلاق",
            "حذف", "تراجع", "خروج", "Exit", "Abort", "Retry", "Ignore"
        };

        public static bool IsAffirmative(string caption)
        {
            return MatchesAny(caption, Affirmative) && !IsNegative(caption);
        }

        public static bool IsNegative(string caption)
        {
            return MatchesAny(caption, Negative);
        }

        // Answer every modal prompt raised by the process until none is left.
        // Returns how many were handled; stops early on the dialog budget.
        public static ConfirmDialogResult ConfirmAll(
            int processId,
            IntPtr owner,
            int timeoutMs,
            int maxDialogs,
            Action<string, string> log)
        {
            ConfirmDialogResult result = new ConfirmDialogResult();
            if (log == null) { log = delegate(string m, string l) { }; }
            if (maxDialogs <= 0) { maxDialogs = 4; }

            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs > 0 ? timeoutMs : 5000);
            while (DateTime.Now < deadline && result.Handled < maxDialogs)
            {
                IntPtr dialog = FindModal(processId, owner);
                if (dialog == IntPtr.Zero)
                {
                    // Nothing on screen yet — the prompt may still be opening.
                    Thread.Sleep(120);
                    continue;
                }

                string title = Win32Driver.GetWindowTitle(dialog) ?? "";
                // Read the body BEFORE clicking — the window is gone right after.
                string message = ReadMessage(dialog);

                string clicked;
                if (!ClickAffirmative(dialog, out clicked))
                {
                    log("confirm: «" + title + "» has no safe Yes/OK button — left open", "WARN");
                    result.LastTitle = title;
                    if (message.Length > 0) { result.Messages.Add(message); }
                    result.TimedOut = true;
                    return result;
                }

                result.Handled++;
                result.LastTitle = title;
                result.LastButton = clicked;
                if (message.Length > 0) { result.Messages.Add(message); }
                log("confirm: «" + title + "»" +
                    (message.Length > 0 ? " [" + message + "]" : "") +
                    " → clicked «" + clicked + "»",
                    IsAlreadyExistsMessage(message) ? "WARN" : "OK");

                if (!WaitClosed(dialog, 3000))
                {
                    log("confirm: «" + title + "» did not close after «" + clicked + "»", "WARN");
                    result.TimedOut = true;
                    return result;
                }
                Thread.Sleep(150);
                // A follow-up box appears fast; do not hold the full budget open for it.
                DateTime grace = DateTime.Now.AddMilliseconds(GraceMs);
                if (grace < deadline) { deadline = grace; }
            }

            if (result.Handled == 0) { result.TimedOut = true; }
            return result;
        }

        // Same as ConfirmAll but silence is success: a follow-up «Coding» / OK box
        // after ترحيل is optional and must not fail the run when it never appears.
        public static ConfirmDialogResult ConfirmIfPresent(
            int processId,
            IntPtr owner,
            int timeoutMs,
            int maxDialogs,
            Action<string, string> log)
        {
            ConfirmDialogResult result = ConfirmAll(processId, owner, timeoutMs, maxDialogs, log);
            if (result.Handled == 0) { result.TimedOut = false; }
            return result;
        }

        // Visible top-level popup belonging to the app, excluding the owner form.
        public static IntPtr FindModal(int processId, IntPtr owner)
        {
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                IntPtr h = tops[i];
                if (h == owner) { continue; }
                if (!Win32Interop.IsWindow(h) || !Win32Interop.IsWindowVisible(h)) { continue; }
                if (processId > 0 && Win32Driver.GetProcessId(h) != processId) { continue; }

                // «المستند» has موافق/OK but it is a file picker, never a Yes/No prompt.
                // Clicking it here used to dismiss the picker empty and kill the attach.
                if (IsFilePickerWindow(h)) { continue; }

                string cls = Win32Driver.GetClassName(h) ?? "";
                if (cls == "#32770") { return h; }
                // VB6 MsgBox after ترحيل is often titled «Coding» with a single OK.
                if (LooksLikeCodingOkBox(h, owner)) { return h; }
                // VB6 forms can also act as a prompt — only small ones exposing Yes/OK.
                if (cls.IndexOf("ThunderRT6", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    cls.IndexOf("MDIForm", StringComparison.OrdinalIgnoreCase) < 0 &&
                    LooksLikePrompt(h))
                {
                    return h;
                }
            }
            return IntPtr.Zero;
        }

        // Drive/Dir/File browser («المستند») or any titled document picker.
        public static bool IsFilePickerWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Win32Interop.IsWindow(hwnd)) { return false; }

            string title = Win32Driver.GetWindowTitle(hwnd) ?? "";
            if (title.IndexOf("المستند", StringComparison.Ordinal) >= 0 ||
                title.IndexOf("مستند", StringComparison.Ordinal) >= 0 ||
                title.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.Equals("فتح", StringComparison.Ordinal))
            {
                return true;
            }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(hwnd);
            bool drive = false, dir = false, files = false;
            for (int i = 0; i < kids.Count; i++)
            {
                string cls = kids[i].Class != null ? kids[i].Class : "";
                if (cls.IndexOf("DriveListBox", StringComparison.OrdinalIgnoreCase) >= 0) { drive = true; }
                else if (cls.IndexOf("DirListBox", StringComparison.OrdinalIgnoreCase) >= 0) { dir = true; }
                else if (cls.IndexOf("FileListBox", StringComparison.OrdinalIgnoreCase) >= 0) { files = true; }
            }
            return (drive && dir) || (dir && files) || (drive && files);
        }

        public static bool ClickAffirmative(IntPtr dialog, out string clicked)
        {
            clicked = "";
            if (dialog == IntPtr.Zero || !Win32Interop.IsWindow(dialog)) { return false; }

            // Control ids are exact on a real MessageBox — no caption guessing needed.
            int[] ids = new int[] { Win32Interop.IDYES, Win32Interop.IDOK };
            for (int i = 0; i < ids.Length; i++)
            {
                IntPtr btn = Win32Interop.GetDlgItem(dialog, ids[i]);
                if (btn == IntPtr.Zero) { continue; }
                if (!Win32Interop.IsWindowVisible(btn) || !Win32Interop.IsWindowEnabled(btn)) { continue; }
                clicked = Clean(Win32Driver.GetWindowText(btn));
                if (string.IsNullOrEmpty(clicked))
                {
                    clicked = ids[i] == Win32Interop.IDYES ? "Yes" : "OK";
                }
                if (IsNegative(clicked)) { continue; }
                Win32Driver.Click(btn, 200, false);
                return true;
            }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(dialog);
            for (int p = 0; p < Affirmative.Length; p++)
            {
                for (int i = 0; i < kids.Count; i++)
                {
                    if (!kids[i].Visible) { continue; }
                    if (FieldDiscovery.GetControlKind(kids[i].Class) != "Button") { continue; }
                    string text = Clean(kids[i].Text);
                    if (IsNegative(text)) { continue; }
                    if (!string.Equals(text, Affirmative[p], StringComparison.OrdinalIgnoreCase)) { continue; }
                    clicked = text;
                    Win32Driver.Click(kids[i].Handle, 200, false);
                    return true;
                }
            }
            return false;
        }

        public static bool IsSaveOnCloseMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) { return false; }
            string[] phrases = new string[]
            {
                "هل تريد الحفظ", "هل تريد حفظ", "حفظ التغييرات",
                "Save changes", "Do you want to save", "want to save"
            };
            return ArabicText.ContainsAny(text, phrases);
        }

        public static bool ClickNegative(IntPtr dialog, out string clicked)
        {
            clicked = "";
            if (dialog == IntPtr.Zero || !Win32Interop.IsWindow(dialog)) { return false; }

            int[] ids = new int[] { Win32Interop.IDNO, Win32Interop.IDCANCEL };
            for (int i = 0; i < ids.Length; i++)
            {
                IntPtr btn = Win32Interop.GetDlgItem(dialog, ids[i]);
                if (btn == IntPtr.Zero) { continue; }
                if (!Win32Interop.IsWindowVisible(btn) || !Win32Interop.IsWindowEnabled(btn)) { continue; }
                clicked = Clean(Win32Driver.GetWindowText(btn));
                if (string.IsNullOrEmpty(clicked))
                {
                    clicked = ids[i] == Win32Interop.IDNO ? "No" : "Cancel";
                }
                Win32Driver.Click(btn, 200, false);
                return true;
            }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(dialog);
            string[] prefer = new string[] { "لا", "No" };
            for (int p = 0; p < prefer.Length; p++)
            {
                for (int i = 0; i < kids.Count; i++)
                {
                    if (!kids[i].Visible) { continue; }
                    if (FieldDiscovery.GetControlKind(kids[i].Class) != "Button") { continue; }
                    string text = Clean(kids[i].Text);
                    if (!string.Equals(text, prefer[p], StringComparison.OrdinalIgnoreCase)) { continue; }
                    clicked = text;
                    Win32Driver.Click(kids[i].Handle, 200, false);
                    return true;
                }
            }
            return false;
        }

        // After WM_CLOSE / «جديد»: discard dirty data (No), keep OK for info boxes.
        public static ConfirmDialogResult AnswerClosePrompts(
            int processId,
            IntPtr owner,
            int timeoutMs,
            int maxDialogs,
            Action<string, string> log)
        {
            ConfirmDialogResult result = new ConfirmDialogResult();
            if (log == null) { log = delegate(string m, string l) { }; }
            if (maxDialogs <= 0) { maxDialogs = 4; }

            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs > 0 ? timeoutMs : 4000);
            while (DateTime.Now < deadline && result.Handled < maxDialogs)
            {
                IntPtr dialog = FindModal(processId, owner);
                if (dialog == IntPtr.Zero)
                {
                    Thread.Sleep(120);
                    continue;
                }

                string title = Win32Driver.GetWindowTitle(dialog) ?? "";
                string message = ReadMessage(dialog);
                string blob = title + " " + message;
                string clicked;
                bool savePrompt = IsSaveOnCloseMessage(blob);
                bool ok = savePrompt
                    ? ClickNegative(dialog, out clicked)
                    : ClickAffirmative(dialog, out clicked);
                if (!ok)
                {
                    log("close-prompt: «" + title + "» has no No/OK button — left open", "WARN");
                    result.TimedOut = true;
                    return result;
                }

                result.Handled++;
                result.LastTitle = title;
                result.LastButton = clicked;
                if (message.Length > 0) { result.Messages.Add(message); }
                log("close-prompt: «" + title + "»" +
                    (message.Length > 0 ? " [" + message + "]" : "") +
                    " → clicked «" + clicked + "»" +
                    (savePrompt ? " (discard — do not re-save)" : ""),
                    "OK");

                if (!WaitClosed(dialog, 3000))
                {
                    log("close-prompt: «" + title + "» did not close", "WARN");
                    result.TimedOut = true;
                    return result;
                }
                Thread.Sleep(150);
                DateTime grace = DateTime.Now.AddMilliseconds(GraceMs);
                if (grace < deadline) { deadline = grace; }
            }
            return result;
        }

        private static bool LooksLikePrompt(IntPtr root)
        {
            if (IsFilePickerWindow(root)) { return false; }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(root);
            if (kids.Count > MaxPromptControls) { return false; }
            bool hasAffirmative = false;
            for (int i = 0; i < kids.Count; i++)
            {
                if (!kids[i].Visible) { continue; }
                string kind = FieldDiscovery.GetControlKind(kids[i].Class);
                // Path/Excel export form has a TextBox — never treat it as Yes/OK.
                if (kind == "TextBox" || kind == "ComboBox" || kind == "ListBox" ||
                    kind == "DatePicker")
                {
                    return false;
                }
                if (kind != "Button") { continue; }
                if (IsAffirmative(Clean(kids[i].Text))) { hasAffirmative = true; }
            }
            return hasAffirmative;
        }

        // After F9 / ترحيل Coding may raise a tiny info box titled like the EXE.
        private static bool LooksLikeCodingOkBox(IntPtr hwnd, IntPtr owner)
        {
            if (hwnd == owner) { return false; }
            string cls = Win32Driver.GetClassName(hwnd) ?? "";
            if (cls.IndexOf("MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            if (cls.IndexOf("MDIClient", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }

            string title = Win32Driver.GetWindowTitle(hwnd) ?? "";
            string t = title.Trim();
            bool codingTitle =
                string.Equals(t, "Coding", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "تكويد", StringComparison.Ordinal) ||
                (ArabicText.Contains(t, "تكويد") && t.Length <= 24);
            if (!codingTitle && cls != "#32770") { return false; }

            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(hwnd);
                if (r.W > 720 || r.H > 420) { return false; }
            }
            catch { return false; }

            return LooksLikePrompt(hwnd);
        }

        private static bool WaitClosed(IntPtr dialog, int timeoutMs)
        {
            DateTime until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < until)
            {
                if (!Win32Interop.IsWindow(dialog) || !Win32Interop.IsWindowVisible(dialog))
                {
                    return true;
                }
                Thread.Sleep(80);
            }
            return false;
        }

        private static bool MatchesAny(string caption, string[] options)
        {
            string t = Clean(caption);
            if (t.Length == 0) { return false; }
            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(t, options[i], StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        private static string Clean(string caption)
        {
            if (string.IsNullOrEmpty(caption)) { return ""; }
            return caption.Replace("&", "").Trim();
        }
    }
}
