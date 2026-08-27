using System.Security.Cryptography.X509Certificates;

namespace ServerSleuth.Windows.Certificates;

/// <summary>The certificate stores relevant to server/application certificates — see
/// skill.md §15. Not every store on the machine is scanned, only these targeted ones.</summary>
public sealed record CertificateStoreSource
{
    public required StoreLocation Location { get; init; }
    public required string StoreName { get; init; }
    public required string Label { get; init; }

    public static readonly CertificateStoreSource LocalMachineMy = new()
    {
        Location = StoreLocation.LocalMachine,
        StoreName = "My",
        Label = @"LocalMachine\My"
    };

    public static readonly CertificateStoreSource LocalMachineWebHosting = new()
    {
        Location = StoreLocation.LocalMachine,
        StoreName = "WebHosting",
        Label = @"LocalMachine\WebHosting"
    };

    public static readonly CertificateStoreSource LocalMachineRoot = new()
    {
        Location = StoreLocation.LocalMachine,
        StoreName = "Root",
        Label = @"LocalMachine\Root"
    };

    public static readonly CertificateStoreSource CurrentUserMy = new()
    {
        Location = StoreLocation.CurrentUser,
        StoreName = "My",
        Label = @"CurrentUser\My"
    };

    public static readonly IReadOnlyList<CertificateStoreSource> All =
        [LocalMachineMy, LocalMachineWebHosting, LocalMachineRoot, CurrentUserMy];
}
