using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class RootPage : Page
{
    public RootPage(AppWindow appWindow, Settings settings, IntPtr hWnd, string dataFolderPath)
    {
        InitializeComponent();

        this.appWindow = appWindow;
        this.settings = settings;
        this.hWnd = hWnd;
        this.dataFolderPath = dataFolderPath;

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            titleBar.Loaded += TitleBar_Loaded;
            titleBar.SizeChanged += TitleBar_SizeChanged;
        }


        frameMain.Content = notSelectedPage;
        DoomList.SelectedIndex = settings.SelectedModIndex;
    }

    private void TitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SetDragRegion();
    }

    private void TitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        SetDragRegion();
    }

    private void SetDragRegion()
    {
        var scaleAdjustment = DPIHelper.GetScaleAdjustment(hWnd);

        var x = (int)(48 * scaleAdjustment);

        var dragRect = new Windows.Graphics.RectInt32()
        {
            X = x,
            Y = 0,
            Width = (int)(titleBar.ActualWidth * scaleAdjustment) - x,
            Height = 48,
        };

        appWindow.TitleBar.SetDragRectangles(new[] { dragRect });
    }

    private readonly IntPtr hWnd;
    private readonly string dataFolderPath;


    private readonly Settings settings;

    private readonly NotSelectedPage notSelectedPage = new();

    private readonly AppWindow appWindow;

    private void DoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        settings.SelectedModIndex = DoomList.SelectedIndex;
        if (DoomList.SelectedItem is DoomEntry item)
        {
            DoomPage page = new(item, hWnd, dataFolderPath);
            page.OnStart += Page_OnStart;
            page.OnEdit += Page_OnEdit;
            page.OnRemove += Page_OnRemove;
            frameMain.Content = page;
        }
        else
        {
            frameMain.Content = notSelectedPage;
        }
        if (swMain.DisplayMode == SplitViewDisplayMode.Overlay)
        {
            swMain.IsPaneOpen = false;
        }
    }

    private async void Page_OnRemove(object sender, DoomEntry entry)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            PrimaryButtonText = "Удалить",
            Content = $"Вы уверены, что хотите удалить '{entry.Name}'?",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            settings.Entries.Remove(entry);
        }
    }

    private async void Page_OnEdit(object sender, DoomEntry entry)
    {
        if (await AddOrEditModDialogShow(entry.Name, entry.IWadFile, true) is EditModDialogResult result)
        {
            entry.Name = result.name;
            entry.IWadFile = result.iWadFile;
        }
    }

    private void Page_OnStart(object sender, DoomEntry entry)
    {
        Start(entry);
    }

    private async void Start(DoomEntry entry)
    {
        if (!IsGZDoomPathChoosen())
        {
            var success = await ChooseGZDoomPath();
            if (!success)
            {
                return;
            }
        }
        ProcessStartInfo processInfo = new()
        {
            FileName = settings.GZDoomPath,
            WorkingDirectory = Path.GetDirectoryName(settings.GZDoomPath),
        };
        if (!string.IsNullOrEmpty(entry.IWadFile.Key))
        {
            processInfo.ArgumentList.Add("-iwad");
            processInfo.ArgumentList.Add(entry.IWadFile.Key);
        }
        if (entry.ModFiles.Count > 0)
        {
            processInfo.ArgumentList.Add("-file");
            foreach (var modFile in entry.ModFiles)
            {
                processInfo.ArgumentList.Add(modFile.Path);
            }
        }
        Process.Start(processInfo);
        //if (appWindow.Presenter is OverlappedPresenter presenter)
        //{
        //    presenter.Minimize();
        //}
        Application.Current.Exit();
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (await AddOrEditModDialogShow("", Settings.IWads.First(), false) is EditModDialogResult result)
        {
            DoomEntry entry = new()
            {
                Id = Guid.NewGuid().ToString(),
                Name = result.name,
                IWadFile = result.iWadFile,
                ModFiles = new(),
            };
            settings.Entries.Add(entry);
            DoomList.SelectedItem = entry;
        }
    }


    private async Task<bool> ChooseGZDoomPath()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = "Выбрать GZDoom";
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            settings.GZDoomPath = file.Path;
            return true;
        }
        return false;
    }

    public bool IsGZDoomPathChoosen()
    {
        if (string.IsNullOrEmpty(settings.GZDoomPath))
        {
            return false;
        }
        if (!File.Exists(settings.GZDoomPath))
        {
            return false;
        }
        return true;
    }

    public readonly struct EditModDialogResult
    {
        public readonly string name;
        public readonly KeyValue iWadFile;

        public EditModDialogResult(string name, KeyValue iWadFile)
        {
            this.name = name;
            this.iWadFile = iWadFile;
        }
    }

    public async Task<EditModDialogResult?> AddOrEditModDialogShow(string modName, KeyValue iWadFile, bool isEditMode)
    {
        var content = new EditModContentDialog(modName, iWadFile);
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Content = content,
            PrimaryButtonText = isEditMode ? "Изменить" : "Создать",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return new(content.ModName, content.IWadFile);
        }
        return null;
    }

    private void ButtonMenu_Click(object sender, RoutedEventArgs e)
    {
        swMain.IsPaneOpen = !swMain.IsPaneOpen;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await ChooseGZDoomPath();
    }
}
