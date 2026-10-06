using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Formats;
using Xunit;

namespace Firezip.Tests;

public class ArchiveEngineTests
{
    [Fact]
    public void EmptyProviderCollection_UsesBuiltInProviders()
    {
        // Microsoft.Extensions.DependencyInjection supplies an empty IEnumerable<T>
        // when no custom IArchiveProvider registrations exist.
        var engine = new ArchiveEngine(Array.Empty<IArchiveProvider>());

        Assert.Contains(engine.Providers, provider => provider.Format == ArchiveFormat.Zip);
        Assert.Contains(engine.Providers, provider => provider.Format == ArchiveFormat.SevenZip);
    }
}
