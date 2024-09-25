using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace DoomLauncher.Helpers;

public class FileSavePicker()
{
    public IDictionary<string, IList<string>> FileTypeChoices { get; } = new Dictionary<string, IList<string>>();
    public string SuggestedFileName { get; set; } = string.Empty;
    public string SuggestedStartLocation { get; set; } = string.Empty;
    public string DefaultFileExtension { get; set; } = string.Empty;
    public string CommitButtonText { get; set; } = string.Empty;
    public string TitleText { get; set; } = string.Empty;
    public IntPtr HWND { get; set; }

    public Task<string?> PickSaveFileAsync()
    {
        return Task.Run(() => PickSaveFile());
    }

    private unsafe string? PickSaveFile()
    {
        if (FileTypeChoices.Count == 0)
        {
            throw new Exception("No file types provided");
        }
        try
        {
            PInvoke.CoCreateInstance<IFileSaveDialog>(
                typeof(FileSaveDialog).GUID,
                null,
                CLSCTX.CLSCTX_INPROC_SERVER,
                out var fsd).ThrowOnFailure();

            List<COMDLG_FILTERSPEC> extensions = [];
            foreach (var (name, types) in FileTypeChoices)
            {
                COMDLG_FILTERSPEC extension;
                fixed (char* nameLocal = name)
                {
                    extension.pszName = new PWSTR(nameLocal);
                }
                fixed (char* joinedTypes = string.Join(';', types))
                {
                    extension.pszSpec = new PWSTR(joinedTypes);
                }
                extensions.Add(extension);
            }
            fsd->SetFileTypes(extensions.ToArray());

            if (!string.IsNullOrEmpty(SuggestedStartLocation))
            {
                PInvoke.SHCreateItemFromParsingName(
                    SuggestedStartLocation,
                    null,
                    typeof(IShellItem).GUID,
                    out var directoryShellItem).ThrowOnFailure();
                fsd->SetDefaultFolder((IShellItem*)directoryShellItem);
            }

            if (!string.IsNullOrEmpty(SuggestedFileName))
            {
                fsd->SetFileName(SuggestedFileName);
            }

            if (!string.IsNullOrEmpty(DefaultFileExtension))
            {
                fsd->SetDefaultExtension(DefaultFileExtension);
            }

            if (!string.IsNullOrEmpty(CommitButtonText))
            {
                fsd->SetOkButtonLabel(CommitButtonText);
            }

            if (!string.IsNullOrEmpty(TitleText))
            {
                fsd->SetTitle(TitleText); 
            }

            fsd->Show(new HWND(HWND));
            IShellItem* ppsi = default;
            fsd->GetResult(&ppsi);

            PWSTR filename;
            ppsi->GetDisplayName(SIGDN.SIGDN_FILESYSPATH, &filename);

            return filename.ToString();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        return null;
    }
}
