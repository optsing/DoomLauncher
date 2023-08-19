using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;

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
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Get or register the main instance
        var mainInstance = AppInstance.FindOrRegisterForKey("GZDoomLauncher");

        // Get the activation args
        var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        if (mainInstance.IsCurrent)
        {
            mainInstance.Activated += MainInstance_Activated;
            var initialFiles = ParseAppArgs(appArgs);
            m_window = new MainWindow(initialFiles);
            m_window.Activate();
        }
        // If the main instance isn't this current instance
        else
        {
            try
            {
                // Redirect activation to that instance
                await mainInstance.RedirectActivationToAsync(appArgs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }

            // And exit our instance and stop
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }
    }

    private void MainInstance_Activated(object? sender, AppActivationArguments e)
    {
        if (m_window != null)
        {
            WindowHelper.SetForegroundWindow(m_window.hWnd);
            var files = ParseAppArgs(e);
            if (files.Count > 0)
            {
                m_window.dispatcher.TryEnqueue(
                    () => m_window.rootPage.ImportEntriesFromFiles(files, withConfirm: true)
                );
            }
        }
    }

    private static List<StorageFile> ParseAppArgs(AppActivationArguments appArgs)
    {
        var files = new List<StorageFile>();
        if (appArgs.Data is Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs)
        {
            
            foreach (var item in fileArgs.Files)
            {
                if (item is StorageFile file)
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                    if (ext == ".gzdl")
                    {
                        files.Add(file);
                    }
                }
            }
        }
        return files;
    }

    private MainWindow? m_window;
}
