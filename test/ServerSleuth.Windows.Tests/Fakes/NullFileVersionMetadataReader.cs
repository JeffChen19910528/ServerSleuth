using ServerSleuth.Windows.Common;

namespace ServerSleuth.Windows.Tests.Fakes;

/// <summary>Always reports "no version info" — used where tests care about registry
/// mapping/aggregation behavior, not file-metadata enrichment.</summary>
internal sealed class NullFileVersionMetadataReader : IFileVersionMetadataReader
{
    public FileVersionMetadata? TryRead(string path) => null;
}
