using DoomLauncher.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace DoomLauncher.Helpers;

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
        PInvoke.SetForegroundWindow(new HWND(anotherHWnd));
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
        PInvoke.SetForegroundWindow(new HWND(HWND));
    }

    public static double GetScaleAdjustment(IntPtr hWnd)
    {
        var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
        var hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        PInvoke
            .GetDpiForMonitor(new HMONITOR(hMonitor), MONITOR_DPI_TYPE.MDT_DEFAULT, out var dpiX, out var _)
            .ThrowOnFailure();

        var scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }
}
