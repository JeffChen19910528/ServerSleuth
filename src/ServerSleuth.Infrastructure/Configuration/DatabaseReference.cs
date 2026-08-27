namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>Never carries a credential — Password/Pwd/User Id are deliberately excluded even
/// though they appear in the source connection string. See skill.md §14.</summary>
public sealed record DatabaseReference
{
    public required string Type { get; init; } // "SqlServer","PostgreSql","MySql","Sqlite","Redis","Unknown"
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? Database { get; init; }
}
