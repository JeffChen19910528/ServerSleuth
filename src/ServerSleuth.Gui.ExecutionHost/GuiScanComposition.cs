using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Gui.ExecutionHost;

/// <summary>The result of composing everything a scan execution needs — a connected-or-
/// connectable <see cref="ITargetTransport"/> and the <see cref="IServiceProvider"/> whose
/// <c>IProcessRunner</c>/<c>IFileSystemReader</c>/scanner registrations are already wired to
/// that same transport (exactly the composition-root pattern <c>ServerSleuth.Cli</c>'s own
/// <c>CompositionRoot.Build</c> already established — see <see cref="DefaultGuiScanComposition"/>'s
/// doc comment).</summary>
internal sealed record GuiScanComposition
{
    public required ITargetTransport Transport { get; init; }
    public required IServiceProvider Services { get; init; }
}
