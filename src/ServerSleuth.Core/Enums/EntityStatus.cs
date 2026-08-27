namespace ServerSleuth.Core.Enums;

/// <summary>
/// Distinguishes what kind of evidence-backed claim is being made about an entity.
/// "Installed" must never be treated as implying "Used" — see skill.md §2.
/// </summary>
public enum EntityStatus
{
    Unknown,
    Installed,
    Configured,
    Running,
    Listening,
    Referenced,
    Used
}
