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

    public bool TrySetAcrylicBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            _dispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();

            // Hooking up the policy object
            _backdropConfiguration = new();
            _window.Activated += Window_Activated;
            _window.Closed += Window_Closed;
            ((FrameworkElement)_window.Content).ActualThemeChanged += Window_ThemeChanged;

            // Initial configuration state.
            _backdropConfiguration.IsInputActive = true;
            SetConfigurationSourceTheme();

            // Enable the system backdrop.
            // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
            _micaController.AddSystemBackdropTarget(_window.As<ICompositionSupportsSystemBackdrop>());
            _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
            return true; // succeeded
        }

        return false; // Acrylic is not supported on this system
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        _backdropConfiguration.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
        // use this closed window.
        if (_micaController != null)
        {
            _micaController.Dispose();
            _micaController = null;
        }
        _window.Activated -= Window_Activated;
        _backdropConfiguration = null;
    }

    private void Window_ThemeChanged(FrameworkElement sender, object args)
    {
        if (_backdropConfiguration != null)
        {
            SetConfigurationSourceTheme();
        }
    }

    private void SetConfigurationSourceTheme()
    {
        switch (((FrameworkElement)_window.Content).ActualTheme)
        {
            case ElementTheme.Dark: _backdropConfiguration.Theme = SystemBackdropTheme.Dark; break;
            case ElementTheme.Light: _backdropConfiguration.Theme = SystemBackdropTheme.Light; break;
            case ElementTheme.Default: _backdropConfiguration.Theme = SystemBackdropTheme.Default; break;
        }
    }
}
