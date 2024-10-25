using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml;
using System;

namespace DoomLauncher.Helpers;

static class EventBus
{
    public static event Action<string?>? OnProgress;
    public static void Progress(string? title) => OnProgress?.Invoke(title);
    public static event Action<string?, AnimationDirection>? OnChangeBackground;
    public static void ChangeBackground(string? imagePath, AnimationDirection direction) => OnChangeBackground?.Invoke(imagePath, direction);
    public static event Action<string?>? OnChangeCaption;
    public static void ChangeCaption(string? caption) => OnChangeCaption?.Invoke(caption);
    public static event Action<DoomEntryViewModel?>? OnSetCurrentEntry;
    public static void SetCurrentEntry(DoomEntryViewModel? currentEntry) => OnSetCurrentEntry?.Invoke(currentEntry);
    public static event Action<bool>? OnDropHelper;
    public static void DropHelper(bool isDropHelperVisible) => OnDropHelper?.Invoke(isDropHelperVisible);

    public static event Action<DragEventArgs>? OnRightDragEnter;
    public static void RightDragEnter(DragEventArgs e) => OnRightDragEnter?.Invoke(e);
    public static event Action<DragEventArgs>? OnRightDragOver;
    public static void RightDragOver(DragEventArgs e) => OnRightDragOver?.Invoke(e);
    public static event Action<DragEventArgs>? OnRightDrop;
    public static void RightDrop(DragEventArgs e) => OnRightDrop?.Invoke(e);
}
