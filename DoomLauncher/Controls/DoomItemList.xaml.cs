using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Collections;
using System.Collections.Specialized;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher.Controls;

[INotifyPropertyChanged]
public sealed partial class DoomItemList : UserControl
{
    public string HeaderTitle { get => (string)GetValue(HeaderTitleProperty); set => SetValue(HeaderTitleProperty, value); }

    public static readonly DependencyProperty HeaderTitleProperty =
        DependencyProperty.Register(nameof(HeaderTitle), typeof(string), typeof(DoomItemList), new PropertyMetadata(""));

    public FlyoutBase HeaderFlyout { get => (FlyoutBase)GetValue(HeaderFlyoutProperty); set => SetValue(HeaderFlyoutProperty, value); }

    public static readonly DependencyProperty HeaderFlyoutProperty =
        DependencyProperty.Register(nameof(HeaderFlyout), typeof(FlyoutBase), typeof(DoomItemList), new PropertyMetadata(null));

    public DataTemplate ItemTemplate { get => (DataTemplate)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(DoomItemList), new PropertyMetadata(null));

    public object ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(DoomItemList), new PropertyMetadata(null, OnItemsSourceChanged));

    public Visibility IsListItemsVisible =>
        (ItemsSource is IList list && list.Count > 0) ? Visibility.Visible : Visibility.Collapsed;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (DoomItemList)d;
        if (e.OldValue is INotifyCollectionChanged oldCol)
        {
            oldCol.CollectionChanged -= self.NewCol_CollectionChanged;
        }
        if (e.NewValue is INotifyCollectionChanged newCol)
        {
            newCol.CollectionChanged += self.NewCol_CollectionChanged;
            self.OnPropertyChanged(nameof(IsListItemsVisible));
        }
    }

    private void NewCol_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsListItemsVisible));
    }

    public DoomItemList()
    {
        this.InitializeComponent();
    }
}
