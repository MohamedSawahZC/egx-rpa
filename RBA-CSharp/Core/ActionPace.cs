// =============================================================================
//  ActionPace.cs - timings for Coding.exe (VB6). Defaults stay conservative;
//  Settings can lower them. Apply() is called from BotConfig.Load / save.
// =============================================================================
using System.Threading;

namespace RbaCore
{
    public static class ActionPace
    {
        public const int DefaultOperatorSettleMs = 550;
        public const int DefaultOperatorStepMs = 700;
        public const int DefaultBetweenClientsMs = 1100;
        public const int DefaultAfterDialogMs = 850;
        public const int DefaultAfterSaveMs = 1300;
        public const int DefaultRetryPauseMs = 900;

        public static int OperatorSettleMs = DefaultOperatorSettleMs;
        public static int OperatorStepMs = DefaultOperatorStepMs;
        public static int BetweenClientsMs = DefaultBetweenClientsMs;
        public static int AfterDialogMs = DefaultAfterDialogMs;
        public static int AfterSaveMs = DefaultAfterSaveMs;
        public static int RetryPauseMs = DefaultRetryPauseMs;

        public static void Apply(BotSettings settings)
        {
            Normalize(settings);
            if (settings == null)
            {
                OperatorSettleMs = DefaultOperatorSettleMs;
                OperatorStepMs = DefaultOperatorStepMs;
                BetweenClientsMs = DefaultBetweenClientsMs;
                AfterDialogMs = DefaultAfterDialogMs;
                AfterSaveMs = DefaultAfterSaveMs;
                RetryPauseMs = DefaultRetryPauseMs;
                return;
            }
            OperatorStepMs = settings.FillStepMs;
            OperatorSettleMs = settings.FillSettleMs;
            BetweenClientsMs = settings.BetweenClientsMs;
            AfterSaveMs = settings.AfterSaveMs;
        }

        public static void Normalize(BotSettings settings)
        {
            if (settings == null) { return; }
            settings.FillStepMs = ClampMs(settings.FillStepMs, DefaultOperatorStepMs);
            settings.FillSettleMs = ClampMs(settings.FillSettleMs, DefaultOperatorSettleMs);
            settings.BetweenClientsMs = ClampMs(settings.BetweenClientsMs, DefaultBetweenClientsMs);
            settings.AfterSaveMs = ClampMs(settings.AfterSaveMs, DefaultAfterSaveMs);
        }

        private static int ClampMs(int value, int fallback)
        {
            if (value < 50 || value > 8000) { return fallback; }
            return value;
        }

        public static void Wait(int ms)
        {
            if (ms > 0) { Thread.Sleep(ms); }
        }
    }
}
