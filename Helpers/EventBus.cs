using System;

namespace DoomLauncher;

static class EventBus
{
    public static event EventHandler<DoomEntry>? OnStart;
    public static void Start(object sender, DoomEntry entry) => OnStart?.Invoke(sender, entry);
    public static event EventHandler<DoomEntry>? OnEdit;
    public static void Edit(object sender, DoomEntry entry) => OnEdit?.Invoke(sender, entry);
    public static event EventHandler<DoomEntry>? OnCopy;
    public static void Copy(object sender, DoomEntry entry) => OnCopy?.Invoke(sender, entry);
    public static event EventHandler<DoomEntry>? OnExport;
    public static void Export(object sender, DoomEntry entry) => OnExport?.Invoke(sender, entry);
    public static event EventHandler<DoomEntry>? OnCreateShortcut;
    public static void CreateShortcut(object sender, DoomEntry entry) => OnCreateShortcut?.Invoke(sender, entry);
    public static event EventHandler<DoomEntry>? OnRemove;
    public static void Remove(object sender, DoomEntry entry) => OnRemove?.Invoke(sender, entry);
    public static event EventHandler<string?>? OnProgress;
    public static void Progress(object sender, string? title) => OnProgress?.Invoke(sender, title);
    public static event EventHandler<(string? imagePath, AnimationDirection direction)>? OnChangeBackground;
    public static void ChangeBackground(object sender, string? imagePath, AnimationDirection direction) => OnChangeBackground?.Invoke(sender, (imagePath, direction));
}
