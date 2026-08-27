namespace ServerSleuth.Infrastructure.Configuration;

public enum ConfigurationParseStatus
{
    Parsed,
    PartiallyParsed,
    Unsupported,
    ParseError,
    SkippedTooLarge,
    Unreadable,
    NotFound,
    AccessDenied
}
