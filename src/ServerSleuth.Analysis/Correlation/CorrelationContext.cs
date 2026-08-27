using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// Identity resolution over the flattened set of discovered entities — see skill.md §4. Built
/// once per correlation run and shared read-only across every rule; rules never rescan or
/// mutate discovery entities, only read from these indexes. See skill.md §19.
/// </summary>
public sealed class CorrelationContext
{
    public IReadOnlyList<DiscoveryEntity> AllEntities { get; }
    public IReadOnlyDictionary<string, DiscoveryEntity> ById { get; }

    public IReadOnlyList<Application> Applications { get; }
    public IReadOnlyList<WebSite> WebSites { get; }
    public IReadOnlyList<ApplicationPool> ApplicationPools { get; }
    public IReadOnlyList<Service> Services { get; }
    public IReadOnlyList<ScheduledTask> ScheduledTasks { get; }
    public IReadOnlyList<ComComponent> ComComponents { get; }
    public IReadOnlyList<Certificate> Certificates { get; }
    public IReadOnlyList<Configuration> Configurations { get; }
    public IReadOnlyList<Dll> Dlls { get; }
    public IReadOnlyList<Runtime> Runtimes { get; }

    /// <summary>Dll entities keyed by normalized-path comparison key. A well-formed discovery
    /// input has at most one Dll per normalized path (Phase 4E-2 already dedupes by normalized
    /// path within a single scan) — but this stores a list defensively rather than assuming it,
    /// so a genuine collision is detected instead of one entity silently overwriting another.</summary>
    public IReadOnlyDictionary<string, List<Dll>> DllsByNormalizedPath { get; }

    /// <summary>Certificates keyed by normalized (uppercase, whitespace-stripped) thumbprint —
    /// the same normalization WindowsCertificateScanner already applies (see skill.md §19), so
    /// a binding's thumbprint joins directly without re-deriving the rule.</summary>
    public IReadOnlyDictionary<string, List<Certificate>> CertificatesByNormalizedThumbprint { get; }

    public CorrelationContext(IReadOnlyList<DiscoveryEntity> entities)
    {
        AllEntities = entities;

        var byId = new Dictionary<string, DiscoveryEntity>();
        foreach (var entity in entities)
        {
            byId.TryAdd(entity.Id, entity);
        }
        ById = byId;

        Applications = entities.OfType<Application>().ToList();
        WebSites = entities.OfType<WebSite>().ToList();
        ApplicationPools = entities.OfType<ApplicationPool>().ToList();
        Services = entities.OfType<Service>().ToList();
        ScheduledTasks = entities.OfType<ScheduledTask>().ToList();
        ComComponents = entities.OfType<ComComponent>().ToList();
        Certificates = entities.OfType<Certificate>().ToList();
        Configurations = entities.OfType<Configuration>().ToList();
        Dlls = entities.OfType<Dll>().ToList();
        Runtimes = entities.OfType<Runtime>().ToList();

        var dllIndex = new Dictionary<string, List<Dll>>();
        foreach (var dll in Dlls)
        {
            if (dll.Path is null)
            {
                continue;
            }

            var key = WindowsPathNormalizer.Normalize(dll.Path).ComparisonKey;
            if (!dllIndex.TryGetValue(key, out var list))
            {
                dllIndex[key] = list = [];
            }

            list.Add(dll);
        }
        DllsByNormalizedPath = dllIndex;

        var certIndex = new Dictionary<string, List<Certificate>>();
        foreach (var certificate in Certificates)
        {
            if (certificate.Thumbprint is null)
            {
                continue;
            }

            var key = NormalizeThumbprint(certificate.Thumbprint);
            if (!certIndex.TryGetValue(key, out var list))
            {
                certIndex[key] = list = [];
            }

            list.Add(certificate);
        }
        CertificatesByNormalizedThumbprint = certIndex;
    }

    /// <summary>Resolves a Dll by normalized path, disambiguating a (rare, defensive-only)
    /// multi-match by exact original-string match before giving up. Never guesses further.</summary>
    public Dll? TryResolveDllByPath(string rawPath)
    {
        var normalized = WindowsPathNormalizer.Normalize(rawPath);
        if (!DllsByNormalizedPath.TryGetValue(normalized.ComparisonKey, out var matches) || matches.Count == 0)
        {
            return null;
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        var exact = matches.Where(d => string.Equals(d.Path, normalized.Value, StringComparison.Ordinal)).ToList();
        return exact.Count == 1 ? exact[0] : null;
    }

    public static string NormalizeThumbprint(string thumbprint) =>
        thumbprint.Replace(" ", string.Empty).Trim().ToUpperInvariant();
}
