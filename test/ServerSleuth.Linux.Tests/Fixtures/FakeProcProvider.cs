using ServerSleuth.Linux.Process;

namespace ServerSleuth.Linux.Tests.Fixtures;

public sealed class FakeProcProvider(IReadOnlyList<ProcProcessSnapshot> snapshots) : IProcProvider
{
    public IReadOnlyList<ProcProcessSnapshot> GetProcessSnapshots() => snapshots;
}
