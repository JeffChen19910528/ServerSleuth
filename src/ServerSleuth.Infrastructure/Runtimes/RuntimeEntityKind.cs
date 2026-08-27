namespace ServerSleuth.Infrastructure.Runtimes;

/// <summary>Which Core entity type a RuntimeDetectionRow becomes — see skill.md §18: an SDK
/// is never reported as a Runtime unless runtime evidence independently supports it.</summary>
public enum RuntimeEntityKind
{
    Runtime,
    Sdk
}
