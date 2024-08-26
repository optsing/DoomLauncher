using Microsoft.UI.Xaml;
using System;

namespace DoomLauncher.Helpers;

static class EventBus
{
    public static event EventHandler<string?>? OnProgress;
    public static void Progress(object sender, string? title) => OnProgress?.Invoke(sender, title);
    public static event EventHandler<(string? imagePath, AnimationDirection direction)>? OnChangeBackground;
    public static void ChangeBackground(object sender, string? imagePath, AnimationDirection direction) => OnChangeBackground?.Invoke(sender, (imagePath, direction));
    public static event EventHandler<string?>? OnChangeCaption;
    public static void ChangeCaption(object sender, string? caption) => OnChangeCaption?.Invoke(sender, caption);
    public static event EventHandler<bool>? OnDropHelper;
    public static void DropHelper(object sender, bool isDropHelperVisible) => OnDropHelper?.Invoke(sender, isDropHelperVisible);

    public static event DragEventHandler? OnRightDragEnter;
    public static void RightDragEnter(object sender, DragEventArgs e) => OnRightDragEnter?.Invoke(sender, e);
    public static event DragEventHandler? OnRightDragOver;
    public static void RightDragOver(object sender, DragEventArgs e) => OnRightDragOver?.Invoke(sender, e);
    public static event DragEventHandler? OnRightDrop;
    public static void RightDrop(object sender, DragEventArgs e) => OnRightDrop?.Invoke(sender, e);
}
