namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>Format is decided by extension — deliberately simple, per skill.md §7 ("do not
/// attempt to build full standards-compliant parsers for every possible format"). Extended
/// Phase 6E with Linux-common extensions (`.cnf`, `.env`, `.conf`); a Linux configuration file
/// with no extension at all (e.g. `sshd_config`) is `Unknown` by this detector alone — its
/// per-technology parser (driven by the file's <see cref="ScanRoot"/> source, not this
/// detector) still analyzes it as text regardless.</summary>
public static class ConfigurationFormatDetector
{
    public static ConfigurationFormat FromFileName(string fileName)
    {
        // Path.GetExtension only inspects the trailing "." — unlike GetDirectoryName/Combine,
        // it never rewrites directory separators, so it's safe to use unmodified regardless of
        // whether this assembly happens to run on Windows or Linux.
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".json" => ConfigurationFormat.Json,
            ".config" or ".xml" => ConfigurationFormat.Xml,
            ".ini" or ".cnf" => ConfigurationFormat.Ini,
            ".yaml" or ".yml" => ConfigurationFormat.Yaml,
            ".properties" => ConfigurationFormat.Properties,
            ".env" => ConfigurationFormat.EnvFile,
            ".conf" => ConfigurationFormat.Unknown, // nginx/httpd/postgresql .conf shapes are technology-specific, not one common format
            _ => ConfigurationFormat.Unknown
        };
    }
}
