using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace DoomLauncher.Helpers;

public class SyncCollection<T> where T : class
{
    private IList<T> Source { get; }
    private IList<T> Target { get; }
    public Func<T, bool>? Filter { get; set; }
    public int DebounceTime { get; set; } = 300;
    private List<Func<T, T, int>> SortComparers { get; } = [];

    private readonly HashSet<string> Dependencies = [];

    public SyncCollection(IList<T> source, IList<T> target)
    {
        Source = source;
        Target = target;
        foreach (var item in Source)
        {
            if (item is INotifyPropertyChanged i)
            {
                i.PropertyChanged += ItemPropertyChanged;
            }
        }
        if (Source is INotifyCollectionChanged col)
        {
            col.CollectionChanged += Col_CollectionChanged;
        }
    }

    public void ClearSort()
    {
        Dependencies.Clear();
        SortComparers.Clear();
    }

    public void AddSort(string propName, Func<T, object?> getter, bool isDescending = false)
    {
        Dependencies.Add(propName);
        SortComparers.Add((x, y) =>
        {
            var cx = getter(x) as IComparable;
            var cy = getter(y) as IComparable;
            var res = cx == cy ? 0 : cx == null ? -1 : cy == null ? +1 : cx.CompareTo(cy);
            return isDescending ? -res : +res;
        });
    }

    private void Col_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is INotifyPropertyChanged i)
                {
                    i.PropertyChanged -= ItemPropertyChanged;
                }
            }
        }
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is INotifyPropertyChanged i)
                {
                    i.PropertyChanged += ItemPropertyChanged;
                }
            }
        }
        SyncImmediate();
    }

    private void ItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && Dependencies.Contains(e.PropertyName))
        {
            SyncImmediate();
        }
    }

    private int Compare(T x, T y)
    {
        foreach (var sd in SortComparers)
        {
            var res = sd(x, y);
            if (res != 0)
            {
                return res;
            }
        }
        return 0;
    }

    public void SyncImmediate()
    {
        var list = Filter != null ? Source.Where(Filter).ToList() : Source.ToList();
        if (SortComparers.Count > 0)
        {
            list.Sort(Compare);
        }
        for (int i = 0; i < list.Count; i++)
        {
            while (i < Target.Count && Target[i] != list[i])
            {
                if (!list.Contains(Target[i]))
                {
                    Target.RemoveAt(i);
                }
                else
                {
                    Target.Insert(i, list[i]);
                }
            }
            if (i >= Target.Count)
            {
                Target.Add(list[i]);
            }
        }
        while (Target.Count > list.Count)
        {
            Target.RemoveAt(list.Count);
        }
    }

    private int debounce = 0;
    public async void SyncDebounce(Action? synced = null)
    {
        debounce++;
        await Task.Delay(DebounceTime);
        debounce--;
        if (debounce == 0)
        {
            SyncImmediate();
            synced?.Invoke();
        }
    }
}
