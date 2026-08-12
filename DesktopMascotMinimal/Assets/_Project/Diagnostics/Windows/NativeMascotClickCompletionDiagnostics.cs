using System;

namespace DesktopMascot.Diagnostics
{
    internal static class NativeMascotClickCompletionDiagnostics
    {
        private const ulong MaximumDrainPerFrame = 64;

        internal static bool Passed { get; private set; }

        internal static void RunFocusedTests()
        {
            Passed = TestGenerationDeltas()
                && TestMultipleCompletionsBetweenSnapshots()
                && TestModuloWrap()
                && TestBoundedDebtDrain()
                && TestPendingOverflowFails();
        }

        private static bool TestGenerationDeltas()
        {
            return Delta(1, 0) == 1
                && Delta(4, 1) == 3
                && Delta(4, 4) == 0;
        }

        private static bool TestMultipleCompletionsBetweenSnapshots()
        {
            const ulong observed = 17;
            const ulong current = 22;
            return Delta(current, observed) == 5;
        }

        private static bool TestModuloWrap()
        {
            return Delta(1, ulong.MaxValue - 1) == 3;
        }

        private static bool TestBoundedDebtDrain()
        {
            var debt = new DiagnosticDebt(10);
            if (!debt.Observe(140) || debt.Pending != 130)
                return false;
            return debt.DrainOneFrame() == 64
                && debt.Pending == 66
                && debt.DrainOneFrame() == 64
                && debt.Pending == 2
                && debt.DrainOneFrame() == 2
                && debt.Pending == 0;
        }

        private static bool TestPendingOverflowFails()
        {
            var debt = new DiagnosticDebt(0, ulong.MaxValue);
            return !debt.Observe(1) && debt.Overflowed;
        }

        private static ulong Delta(ulong current, ulong observed) =>
            unchecked(current - observed);

        private sealed class DiagnosticDebt
        {
            private ulong observed;

            internal DiagnosticDebt(ulong observed, ulong pending = 0)
            {
                this.observed = observed;
                Pending = pending;
            }

            internal ulong Pending { get; private set; }
            internal bool Overflowed { get; private set; }

            internal bool Observe(ulong current)
            {
                var delta = Delta(current, observed);
                observed = current;
                if (ulong.MaxValue - Pending < delta)
                {
                    Overflowed = true;
                    return false;
                }
                Pending += delta;
                return true;
            }

            internal ulong DrainOneFrame()
            {
                var drained = Math.Min(Pending, MaximumDrainPerFrame);
                Pending -= drained;
                return drained;
            }
        }
    }
}
