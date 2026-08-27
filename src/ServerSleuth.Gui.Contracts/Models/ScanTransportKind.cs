namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-2's presentation-layer mirror of <c>ServerSleuth.Infrastructure.Targets.RemoteTransportKind</c>
/// — NOT a reuse of that type directly, because <c>ServerSleuth.Infrastructure</c> is outside
/// GUI-1's own established dependency boundary (`ServerSleuth.Gui` → `ServerSleuth.Core` +
/// `ServerSleuth.Analysis` only, mechanically verified by `NoDirectPlatformAccessTests`, which
/// this phase does not relax). Same two members, same names, same meaning — a GUI-3 phase that
/// IS allowed to reference `ServerSleuth.Infrastructure` maps this 1:1 onto the real enum at the
/// composition boundary; this type carries no logic of its own, only an equivalent shape.
/// </summary>
public enum ScanTransportKind
{
    Ssh,
    WinRm
}
