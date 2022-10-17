using System;
using System.Runtime.InteropServices;

namespace DoomLauncher;

public class WindowsSystemDispatcherQueueHelper
{
    private IntPtr _dispatcherQueueController = IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential)]
    internal struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, ref IntPtr instance);

    public void EnsureWindowsSystemDispatcherQueueController()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
        {
            // one already exists, so we'll just use it.
            return;
        }

        if (_dispatcherQueueController == IntPtr.Zero)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
            options.threadType = 2;
            options.apartmentType = 2;

            CreateDispatcherQueueController(options, ref _dispatcherQueueController);
        }
    }
}
