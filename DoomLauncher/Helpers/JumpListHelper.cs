using DoomLauncher.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.StartScreen;

namespace DoomLauncher.Helpers;

internal class JumpListHelper
{
    public static async Task Update()
    {
        JumpList jumpList = await JumpList.LoadCurrentAsync();
        jumpList.Items.Clear();

        jumpList.SystemGroupKind = JumpListSystemGroupKind.None;

        var entries = SettingsViewModel.Current.Entries
            .Where(entry => entry.LastLaunch != null)
            .OrderByDescending(entry => entry.LastLaunch);

        foreach (var entry in entries)
        {
            JumpListItem item = JumpListItem.CreateWithArguments($"launch --id {entry.Id}", entry.Name);
            item.GroupName = "Последние запущенные";
            item.Logo = new Uri("ms-appx:///Assets/app.ico");
            jumpList.Items.Add(item);
        }

        await jumpList.SaveAsync();
    }
}
