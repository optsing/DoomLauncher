using System;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.  Other entry points
    /// will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Get or register the main instance
        var mainInstance = AppInstance.FindOrRegisterForKey("doom");

        // If the main instance isn't this current instance
        if (!mainInstance.IsCurrent)
        {
            // Get the activation args
            var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

            // Redirect activation to that instance
            await mainInstance.RedirectActivationToAsync(appArgs);

            // And exit our instance and stop
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }

        m_window = new MainWindow();
        m_window.Activate();
        var mica = new MicaBackground(m_window);
        mica.TrySetAcrylicBackdrop();
    }

    private Window m_window;
}
