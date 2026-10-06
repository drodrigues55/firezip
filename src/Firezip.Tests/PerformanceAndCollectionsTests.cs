using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Firezip.Core.Collections;
using Firezip.Core.Models;

namespace Firezip.Tests;

public class PerformanceAndCollectionsTests
{
    [Fact]
    public void ObservableRangeCollection_ReplaceRange_RaisesSingleResetEvent()
    {
        var collection = new ObservableRangeCollection<string>(["a", "b"]);
        int resetCount = 0;
        int countChanged = 0;

        collection.CollectionChanged += (s, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                resetCount++;
        };

        ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Count")
                countChanged++;
        };

        collection.ReplaceRange(["item1", "item2", "item3", "item4"]);

        Assert.Equal(1, resetCount);
        Assert.Equal(1, countChanged);
        Assert.Equal(4, collection.Count);
        Assert.Equal("item1", collection[0]);
        Assert.Equal("item4", collection[3]);
    }

    [Fact]
    public void ObservableRangeCollection_AddRange_AppendsItemsAndRaisesReset()
    {
        var collection = new ObservableRangeCollection<int>([1, 2]);
        int resetCount = 0;

        collection.CollectionChanged += (s, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                resetCount++;
        };

        collection.AddRange([3, 4, 5]);

        Assert.Equal(1, resetCount);
        Assert.Equal(5, collection.Count);
        Assert.Equal(5, collection[4]);
    }

    [Fact]
    public void ArchiveInfo_GetEntryByPath_ResolvesCaseInsensitiveAndNormalized()
    {
        var entries = new List<ArchiveEntry>
        {
            new() { FullPath = "docs/readme.txt", Name = "readme.txt", Size = 100 },
            new() { FullPath = "src/main.cs", Name = "main.cs", Size = 200 },
            new() { FullPath = "images/logo.png", Name = "logo.png", Size = 500 }
        };

        var info = new ArchiveInfo
        {
            FilePath = "test.zip",
            Format = ArchiveFormat.Zip,
            Entries = entries
        };

        var match1 = info.GetEntryByPath("docs/readme.txt");
        var match2 = info.GetEntryByPath("DOCS\\README.TXT");
        var match3 = info.GetEntryByPath("/images/logo.png/");
        var nonExistent = info.GetEntryByPath("does/not/exist.txt");

        Assert.NotNull(match1);
        Assert.Equal("readme.txt", match1.Name);
        Assert.NotNull(match2);
        Assert.Equal("readme.txt", match2.Name);
        Assert.NotNull(match3);
        Assert.Equal("logo.png", match3.Name);
        Assert.Null(nonExistent);
    }

    [Fact]
    public void ArchiveInfo_GetEntriesInDirectory_ReturnsDirectChildrenOnly()
    {
        var entries = new List<ArchiveEntry>
        {
            new() { FullPath = "root.txt", Name = "root.txt", Size = 10 },
            new() { FullPath = "sub/child1.txt", Name = "child1.txt", Size = 20 },
            new() { FullPath = "sub/child2.txt", Name = "child2.txt", Size = 30 },
            new() { FullPath = "sub/nested/deep.txt", Name = "deep.txt", Size = 40 }
        };

        var info = new ArchiveInfo
        {
            FilePath = "test.zip",
            Format = ArchiveFormat.Zip,
            Entries = entries
        };

        var rootChildren = info.GetEntriesInDirectory(string.Empty).ToList();
        var subChildren = info.GetEntriesInDirectory("sub").ToList();
        var deepChildren = info.GetEntriesInDirectory("sub/nested").ToList();

        Assert.Single(rootChildren);
        Assert.Equal("root.txt", rootChildren[0].Name);

        Assert.Equal(2, subChildren.Count);
        Assert.Contains(subChildren, c => c.Name == "child1.txt");
        Assert.Contains(subChildren, c => c.Name == "child2.txt");

        Assert.Single(deepChildren);
        Assert.Equal("deep.txt", deepChildren[0].Name);
    }

    [Fact]
    public void LargeArchive_TraversalAndLookup_Under10Milliseconds()
    {
        // Generate 10,000 synthetic entries across 10 directories
        var entries = new List<ArchiveEntry>(10_000);
        for (int i = 0; i < 10_000; i++)
        {
            int folderIdx = i % 10;
            var fullPath = $"folder_{folderIdx}/file_{i:D6}.dat";
            entries.Add(new ArchiveEntry
            {
                FullPath = fullPath,
                Name = $"file_{i:D6}.dat",
                ParentDirectory = $"folder_{folderIdx}",
                Size = 1024,
                CompressedSize = 512
            });
        }

        var sw = Stopwatch.StartNew();
        var info = new ArchiveInfo
        {
            FilePath = "large_bench.zip",
            Format = ArchiveFormat.Zip,
            Entries = entries
        };
        sw.Stop();
        var initMs = sw.ElapsedMilliseconds;

        Assert.Equal(10_000, info.TotalFiles);
        Assert.Equal(10_000 * 1024, info.TotalUncompressedSize);
        Assert.Equal(10_000 * 512, info.TotalCompressedSize);

        // Directory lookup benchmark
        sw.Restart();
        var folder0 = info.GetEntriesInDirectory("folder_0").ToList();
        sw.Stop();
        var dirQueryMs = sw.Elapsed.TotalMilliseconds;

        Assert.Equal(1000, folder0.Count);
        Assert.True(dirQueryMs < 50.0, $"Directory query took {dirQueryMs:F2}ms, expected < 50ms");

        // Specific path lookup benchmark
        sw.Restart();
        var entry = info.GetEntryByPath("folder_5/file_005555.dat");
        sw.Stop();
        var pathQueryMs = sw.Elapsed.TotalMilliseconds;

        Assert.NotNull(entry);
        Assert.Equal("file_005555.dat", entry.Name);
        Assert.True(pathQueryMs < 20.0, $"Path query took {pathQueryMs:F2}ms, expected < 20ms");
    }
}
