namespace ServerSleuth.Core.Enums;

/// <summary>
/// Profiles are additive: Standard includes Quick's scope, Deep includes Standard's,
/// Migration includes Deep's — see skill.md §27.
/// </summary>
public enum ScanProfile
{
    Quick,
    Standard,
    Deep,
    Migration
}
