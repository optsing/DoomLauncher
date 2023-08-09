using System;
using System.Runtime.InteropServices;

namespace DoomLauncher;

public partial class WindowHelper
{
    [LibraryImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr handle);
}
