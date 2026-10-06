using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Firezip.Core.Collections;

/// <summary>
/// An ObservableCollection that supports batch updates (ReplaceRange, AddRange)
/// to notify UI controls with a single Reset event instead of multiple per-item layout invalidations.
/// </summary>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private const string CountPropertyName = "Count";
    private const string IndexerPropertyName = "Item[]";

    public ObservableRangeCollection() : base() { }

    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }

    /// <summary>
    /// Replaces all items in the collection with the specified collection in a single batch notification.
    /// </summary>
    public void ReplaceRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        CheckReentrancy();

        var list = collection as IList<T> ?? collection.ToList();

        Items.Clear();
        foreach (var item in list)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(CountPropertyName));
        OnPropertyChanged(new PropertyChangedEventArgs(IndexerPropertyName));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Adds multiple items to the collection in a single batch notification.
    /// </summary>
    public void AddRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        CheckReentrancy();

        var list = collection as IList<T> ?? collection.ToList();
        if (list.Count == 0) return;

        foreach (var item in list)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(CountPropertyName));
        OnPropertyChanged(new PropertyChangedEventArgs(IndexerPropertyName));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
