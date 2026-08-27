namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// A single, independently testable correlation rule — see skill.md §3, §14. A rule is a pure
/// function of already-discovered entities: it must never rescan the filesystem, execute a
/// command, or otherwise touch anything outside <paramref name="context"/> (skill.md §24-25).
/// </summary>
public interface ICorrelationRule
{
    string Id { get; }

    IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context);
}
