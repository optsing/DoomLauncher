using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            m_window = new MainWindow();
            m_window.rootPage.Loaded += (object sender, RoutedEventArgs e) =>
            {
                ParseAppArgs(m_window.rootPage, appArgs);
            };
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
            Exit();
        }
    }

    private void MainInstance_Activated(object? sender, AppActivationArguments e)
    {
        if (m_window != null)
        {
            WinApi.SetForegroundWindow(m_window.hWnd);
            m_window.dispatcher.TryEnqueue(
                () => ParseAppArgs(m_window.rootPage, e)
            );
        }
    }

    private static async void ParseAppArgs(RootPage rootPage, AppActivationArguments appArgs)
    {
        if (appArgs.Kind == ExtendedActivationKind.Protocol)
        {
            if (appArgs.Data is Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs protocolArgs)
            {
                await rootPage.ImportEntryFromDoomWorldId(protocolArgs.Uri.Host, withConfirm: true);
            }
        }
        else if (appArgs.Kind == ExtendedActivationKind.File)
        {
            if (appArgs.Data is Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs)
            {
                var files = new List<StorageFile>();
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
                if (files.Count > 0)
                {
                    await rootPage.ImportEntriesFromFiles(files, withConfirm: true);
                }
            }
        }
        else if (appArgs.Kind == ExtendedActivationKind.Launch)
        {
            if (appArgs.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launchArgs)
            {
                var launchOptions = CommandLine.ParseCommandLine(launchArgs.Arguments);
                if (launchOptions != null)
                {
                    if (launchOptions.EntryId is string entryId)
                    {
                        rootPage.LaunchEntryFromId(entryId, launchOptions.ForceClose);
                    }
                    else if (launchOptions.EntryName is string entryName)
                    {
                        rootPage.LaunchEntryFromName(entryName, launchOptions.ForceClose);
                    }
                }
            }
        }
    }

    private MainWindow? m_window;
}
