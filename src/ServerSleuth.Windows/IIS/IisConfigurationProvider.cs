using System.Reflection;
using Microsoft.Extensions.Logging;

namespace ServerSleuth.Windows.IIS;

/// <summary>
/// Loads Microsoft.Web.Administration.dll from its well-known OS location
/// (%windir%\System32\inetsrv) via late binding (Assembly.LoadFrom + dynamic) rather than a
/// compile-time project reference. This is deliberate: a compile-time reference would require
/// either shipping/redistributing a Windows system DLL (a licensing problem) or hard-coding a
/// HintPath that only resolves on a machine with IIS Management installed, which would break
/// `dotnet build` for ServerSleuth.Windows on any machine without IIS present — including a
/// perfectly valid Windows Server that simply hasn't had the IIS role added yet. Late binding
/// means the project always builds anywhere, and at runtime a missing DLL becomes
/// IisAvailability.NotInstalled rather than a build break. See skill.md §10-11 (IIS-not-
/// installed and permission-denied must both be normal, non-fatal outcomes).
/// </summary>
public sealed class IisConfigurationProvider(ILogger<IisConfigurationProvider> logger) : IIisConfigurationProvider
{
    private static readonly string AssemblyPath = Path.Combine(Environment.SystemDirectory, "inetsrv", "Microsoft.Web.Administration.dll");

    public IisProbeResult GetSnapshot()
    {
        if (!File.Exists(AssemblyPath))
        {
            return IisProbeResult.NotInstalled();
        }

        object serverManager;
        try
        {
            var assembly = Assembly.LoadFrom(AssemblyPath);
            var serverManagerType = assembly.GetType("Microsoft.Web.Administration.ServerManager")
                ?? throw new InvalidOperationException("ServerManager type not found in Microsoft.Web.Administration.dll.");
            serverManager = Activator.CreateInstance(serverManagerType)!;
        }
        catch (UnauthorizedAccessException ex)
        {
            return IisProbeResult.Failure(IisAvailability.AccessDenied, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize IIS ServerManager.");
            return IisProbeResult.Failure(IisAvailability.Failed, ex.Message);
        }

        try
        {
            using var disposable = serverManager as IDisposable;
            dynamic manager = serverManager;

            var partialFailures = new List<string>();
            var sites = ReadSites(manager, partialFailures);
            var pools = ReadApplicationPools(manager, partialFailures);

            return IisProbeResult.Available(new IisSnapshot { Sites = sites, ApplicationPools = pools }, partialFailures);
        }
        catch (UnauthorizedAccessException ex)
        {
            return IisProbeResult.Failure(IisAvailability.AccessDenied, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate IIS configuration.");
            return IisProbeResult.Failure(IisAvailability.Failed, ex.Message);
        }
    }

    private List<IisSiteRow> ReadSites(dynamic manager, List<string> partialFailures)
    {
        var sites = new List<IisSiteRow>();

        foreach (dynamic site in manager.Sites)
        {
            try
            {
                sites.Add(ReadSite(site));
            }
            catch (Exception ex)
            {
                var name = TryGet(() => (string)site.Name, "<unknown>");
                partialFailures.Add($"Site '{name}': {ex.Message}");
            }
        }

        return sites;
    }

    private IisSiteRow ReadSite(dynamic site)
    {
        var applications = new List<IisApplicationRow>();
        foreach (dynamic application in site.Applications)
        {
            applications.Add(ReadApplication(application));
        }

        var bindings = new List<IisBindingRow>();
        foreach (dynamic binding in site.Bindings)
        {
            bindings.Add(ReadBinding(binding));
        }

        var rootApp = applications.FirstOrDefault(a => a.VirtualPath == "/");

        return new IisSiteRow
        {
            Name = (string)site.Name,
            SiteId = (long)site.Id,
            State = TryGet(() => site.State.ToString(), "Unknown"),
            PhysicalPath = rootApp?.PhysicalPath,
            Bindings = bindings,
            Applications = applications
        };
    }

    private static IisApplicationRow ReadApplication(dynamic application)
    {
        string? physicalPath = null;
        foreach (dynamic virtualDirectory in application.VirtualDirectories)
        {
            if ((string)virtualDirectory.Path == "/")
            {
                physicalPath = (string)virtualDirectory.PhysicalPath;
                break;
            }
        }

        return new IisApplicationRow
        {
            VirtualPath = (string)application.Path,
            PhysicalPath = physicalPath,
            ApplicationPoolName = TryGet(() => (string)application.ApplicationPoolName, null)
        };
    }

    private static IisBindingRow ReadBinding(dynamic binding)
    {
        var bindingInformation = TryGet(() => (string)binding.BindingInformation, string.Empty);
        var (ipAddress, port, hostName) = ParseBindingInformation(bindingInformation);

        byte[]? certificateHash = TryGet(() => (byte[])binding.CertificateHash, null);

        return new IisBindingRow
        {
            Protocol = (string)binding.Protocol,
            IpAddress = ipAddress,
            Port = port,
            HostName = string.IsNullOrEmpty(hostName) ? null : hostName,
            BindingInformation = bindingInformation,
            CertificateThumbprint = certificateHash is { Length: > 0 } ? Convert.ToHexString(certificateHash) : null,
            CertificateStoreName = TryGet(() => (string)binding.CertificateStoreName, null)
        };
    }

    private static (string IpAddress, int Port, string? HostName) ParseBindingInformation(string bindingInformation)
    {
        // Format is "IPAddress:Port:HostName", e.g. "*:80:" or "192.168.1.1:443:erp.company.com".
        var parts = bindingInformation.Split(':', 3);
        var ip = parts.Length > 0 && parts[0].Length > 0 ? parts[0] : "*";
        var port = parts.Length > 1 && int.TryParse(parts[1], out var parsedPort) ? parsedPort : 0;
        var host = parts.Length > 2 ? parts[2] : null;
        return (ip, port, host);
    }

    private List<IisAppPoolRow> ReadApplicationPools(dynamic manager, List<string> partialFailures)
    {
        var pools = new List<IisAppPoolRow>();

        foreach (dynamic pool in manager.ApplicationPools)
        {
            try
            {
                pools.Add(ReadApplicationPool(pool));
            }
            catch (Exception ex)
            {
                var name = TryGet(() => (string)pool.Name, "<unknown>");
                partialFailures.Add($"ApplicationPool '{name}': {ex.Message}");
            }
        }

        return pools;
    }

    private static IisAppPoolRow ReadApplicationPool(dynamic pool)
    {
        var identityType = TryGet(() => pool.ProcessModel.IdentityType.ToString(), "Unknown");
        var userName = identityType == "SpecificUser" ? TryGet(() => (string)pool.ProcessModel.UserName, null) : null;

        return new IisAppPoolRow
        {
            Name = (string)pool.Name,
            State = TryGet(() => pool.State.ToString(), "Unknown"),
            ManagedRuntimeVersion = TryGet(() => (string)pool.ManagedRuntimeVersion, null),
            ManagedPipelineMode = TryGet(() => pool.ManagedPipelineMode.ToString(), null),
            IdentityType = identityType,
            UserName = userName,
            Enable32BitAppOnWin64 = TryGet(() => (bool)pool.Enable32BitAppOnWin64, false),
            StartMode = TryGet(() => pool.StartMode.ToString(), null)
        };
    }

    private static T TryGet<T>(Func<T> accessor, T fallback)
    {
        try
        {
            return accessor();
        }
        catch
        {
            return fallback;
        }
    }
}
