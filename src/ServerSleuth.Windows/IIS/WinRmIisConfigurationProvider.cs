using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.IIS;

/// <summary>
/// Satisfies the SAME <see cref="IIisConfigurationProvider"/> interface <see cref="IisScanner"/>
/// already depends on — thin adapter over the disclosed-gap <see cref="WinRmIisOperations"/>
/// (see that type's own doc comment for why remote IIS discovery is not implemented in this
/// phase). Always returns <see cref="IisProbeResult.Failure"/> with the same diagnostic — never
/// fabricated site/pool data, never a local fallback.
/// </summary>
public sealed class WinRmIisConfigurationProvider(WinRmIisOperations remoteIis) : IIisConfigurationProvider
{
    public IisProbeResult GetSnapshot()
    {
        var result = remoteIis.GetSnapshot();
        return IisProbeResult.Failure(IisAvailability.NotInstalled, result.ErrorMessage ?? "Remote IIS discovery is not implemented.");
    }
}
