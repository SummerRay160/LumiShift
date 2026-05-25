using System;
using System.Runtime;

namespace LumiShift.Infrastructure
{
    public static class GcHelper
    {
        public static void CollectFull()
        {
            GC.Collect(1, GCCollectionMode.Forced, false);
            GC.WaitForPendingFinalizers();
            GC.Collect(1, GCCollectionMode.Forced, false);
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }
            catch
            {
            }
        }

        public static void TrimWorkingSet()
        {
            try
            {
                NativeMethods.SetProcessWorkingSetSize(
                    NativeMethods.GetCurrentProcess(),
                    new IntPtr(-1),
                    new IntPtr(-1));
            }
            catch
            {
            }
        }
    }
}
