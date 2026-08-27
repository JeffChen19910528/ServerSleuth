namespace ServerSleuth.Infrastructure.Configuration;

public enum ConfigurationFormat
{
    Json,
    Xml,
    Ini,
    Yaml,
    Properties,

    /// <summary>`.env`/environment-style `KEY=value` files — added Phase 6E for Linux
    /// configuration discovery; distinct from <see cref="Properties"/> since env files have no
    /// section concept and are the shape systemd `EnvironmentFile=` references point at.</summary>
    EnvFile,

    Unknown
}
