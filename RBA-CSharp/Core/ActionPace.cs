// =============================================================================
//  ActionPace.cs - slower timings for Coding.exe (VB6, not a fast UI).
// =============================================================================
using System.Threading;

namespace RbaCore
{
    public static class ActionPace
    {
        public const int OperatorSettleMs = 550;
        public const int OperatorStepMs = 700;
        public const int BetweenClientsMs = 1100;
        public const int AfterDialogMs = 850;
        public const int AfterSaveMs = 1300;
        public const int RetryPauseMs = 900;

        public static void Wait(int ms)
        {
            if (ms > 0) { Thread.Sleep(ms); }
        }
    }
}
