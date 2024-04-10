using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.StartScreen;

namespace DoomLauncher;

internal class JumpListHelper
{
    public static async Task Update()
    {
        JumpList jumpList = await JumpList.LoadCurrentAsync();
        jumpList.Items.Clear();

        jumpList.SystemGroupKind = JumpListSystemGroupKind.None;

        foreach (var entry in Settings.Current.Entries.Where(entry => entry.LastLaunch != null).OrderByDescending(entry => entry.LastLaunch))
        {
            JumpListItem item = JumpListItem.CreateWithArguments($"launch --id {entry.Id}", entry.Name);
            item.GroupName = "Последние запущенные";
            item.Logo = new Uri("ms-appx:///Assets/app.ico");
            jumpList.Items.Add(item);
        }

        await jumpList.SaveAsync();
    }
}
