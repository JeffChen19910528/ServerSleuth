using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// A safe, inert <see cref="IProcessRunner"/> for a remote Windows/WinRM scan — the
/// <see cref="IProcessRunner"/> counterpart to <see cref="UnavailableRemoteFileSystemReader"/>.
/// Nothing <see cref="ServiceCollectionExtensions.AddServerSleuthWindowsRemote"/> registers
/// actually depends on <see cref="IProcessRunner"/> (the three scanners that would —
/// <c>RuntimeDiscoveryScanner</c>, <c>WindowsConfigurationScanner</c>,
/// <c>WindowsBinaryDiscoveryScanner</c> — are deliberately excluded, see that method's own doc
/// comment) — this exists purely so <c>ITargetTransport.ProcessRunner</c> has SOMETHING to
/// return that structurally CANNOT execute anything, rather than leaving the property
/// unsatisfiable or, worse, silently falling back to the LOCAL <c>ProcessRunner</c>.
/// </summary>
public sealed class UnavailableRemoteProcessRunner : IProcessRunner
{
    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new ProcessResult { Status = OperationStatus.Unsupported });
}
