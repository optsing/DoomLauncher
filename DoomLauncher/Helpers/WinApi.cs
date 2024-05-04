using Microsoft.UI.Windowing;
using Microsoft.UI;
using System;
using System.Runtime.InteropServices;
using DoomLauncher.ViewModels;

namespace DoomLauncher;

public partial class WinApi
{
    public static IntPtr HWND;
    public static WindowId WindowId;

    public static void MinimizeAndSwitchToAnotherWindow(IntPtr anotherHWnd)
    {
        if (AppWindow.GetFromWindowId(WindowId).Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
        SetForegroundWindow(anotherHWnd);
    }

    public static void RestoreAndSwitchToThisWindow()
    {
        if (AppWindow.GetFromWindowId(WindowId).Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized)
        {
            if (SettingsViewModel.Current.WindowMaximized)
            {
                presenter.Maximize();
            }
            else
            {
                presenter.Restore();
            }
        }
        SetForegroundWindow(HWND);
    }

    [LibraryImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr handle);

    [LibraryImport("shell32.dll", SetLastError = true)]
    private static partial IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

    public static string[] CommandLineToArgs(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out int argc);
        if (argv == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception();
        try
        {
            var args = new string[argc];
            for (var i = 0; i < args.Length; i++)
            {
                var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                if (Marshal.PtrToStringUni(p) is not string arg)
                {
                    throw new System.ComponentModel.Win32Exception();
                }
                else
                {
                    args[i] = arg;
                }
            }
            return args;
        }
        finally
        {
            Marshal.FreeHGlobal(argv);
        }
    }

    [LibraryImport("Shcore.dll")]
    private static partial int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

    internal enum Monitor_DPI_Type : int
    {
        MDT_Effective_DPI = 0,
        MDT_Angular_DPI = 1,
        MDT_Raw_DPI = 2,
        MDT_Default = MDT_Effective_DPI,
    }

    public static double GetScaleAdjustment(IntPtr hWnd)
    {
        var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
        var hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        // Get DPI.
        var result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out var dpiX, out var _);
        if (result != 0)
        {
            throw new Exception("Could not get DPI for monitor.");
        }

        var scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }
}
