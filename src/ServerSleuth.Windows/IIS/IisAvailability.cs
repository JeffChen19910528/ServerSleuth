namespace ServerSleuth.Windows.IIS;

/// <summary>Whether IIS's configuration API could be reached on this machine.</summary>
public enum IisAvailability
{
    Available,
    NotInstalled,
    AccessDenied,
    Failed
}
