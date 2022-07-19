using System;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace DoomLauncher;

public class MicaBackground
{
    private readonly Window _window;
    private DesktopAcrylicController _micaController = new();
    private SystemBackdropConfiguration _backdropConfiguration = new();
    private readonly WindowsSystemDispatcherQueueHelper _dispatcherQueueHelper = new();

    public MicaBackground(Window window)
    {
        _window = window;
    }

    public bool TrySetMicaBackdrop()
    {
        if (MicaController.IsSupported())
        {
            _dispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();
            _window.Activated += WindowOnActivated;
            _window.Closed += WindowOnClosed;
            _backdropConfiguration.IsInputActive = true;
            _backdropConfiguration.Theme = _window.Content switch
            {
                FrameworkElement { ActualTheme: ElementTheme.Dark } => SystemBackdropTheme.Dark,
                FrameworkElement { ActualTheme: ElementTheme.Light } => SystemBackdropTheme.Light,
                FrameworkElement { ActualTheme: ElementTheme.Default } => SystemBackdropTheme.Default,
                _ => throw new InvalidOperationException("Unknown theme")
            };
            _micaController.AddSystemBackdropTarget(_window.As<ICompositionSupportsSystemBackdrop>());
            _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
            return true;
        }

        return false;
    }

    private void WindowOnClosed(object sender, WindowEventArgs args)
    {
        _micaController.Dispose();
        _micaController = null!;
        _window.Activated -= WindowOnActivated;
        _backdropConfiguration = null!;
    }

    private void WindowOnActivated(object sender, WindowActivatedEventArgs args)
    {
        _backdropConfiguration.IsInputActive = args.WindowActivationState is not WindowActivationState.Deactivated;
    }
}
