namespace ServerSleuth.Windows.Services;

/// <summary>
/// What HKLM\SYSTEM\CurrentControlSet\Services\&lt;name&gt; (and its Parameters subkey) expose.
/// Get-Service alone cannot provide any of this — see skill.md §7.
/// </summary>
public sealed record ServiceRegistryDetail
{
    public string? ImagePath { get; init; }
    public string? ObjectName { get; init; }
    public string? Description { get; init; }
    public int? StartMode { get; init; } // 0=Boot,1=System,2=Automatic,3=Manual,4=Disabled
    public bool? DelayedAutoStart { get; init; }
    public IReadOnlyList<string> DependOnService { get; init; } = [];
    public string? ServiceDll { get; init; }
    public bool HasRecoveryConfiguration { get; init; }
}
