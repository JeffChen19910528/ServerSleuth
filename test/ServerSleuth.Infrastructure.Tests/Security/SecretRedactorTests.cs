using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Infrastructure.Tests.Security;

public class SecretRedactorTests
{
    private readonly SecretRedactor _redactor = new();

    [Theory]
    [InlineData("Password=hunter2", "hunter2")]
    [InlineData("Pwd=hunter2", "hunter2")]
    [InlineData("UserPassword=hunter2", "hunter2")]
    [InlineData("ConnectionString=Server=db;Password=hunter2;", "hunter2")]
    [InlineData("API_KEY=sk-abc123def456", "sk-abc123def456")]
    [InlineData("TOKEN=eyJraWQiOiJhYmMi", "eyJraWQiOiJhYmMi")]
    [InlineData("SECRET=supersecretvalue", "supersecretvalue")]
    [InlineData("PRIVATE_KEY=abc123xyz789", "abc123xyz789")]
    public void Redact_RemovesKnownSecretPatterns(string input, string secretValue)
    {
        var redacted = _redactor.Redact(input);

        Assert.DoesNotContain(secretValue, redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("PASSWORD=hunter2")]
    [InlineData("PaSsWoRd=hunter2")]
    [InlineData("password : hunter2")]
    public void Redact_IsCaseInsensitiveAndToleratesSpacingAroundSeparator(string input)
    {
        Assert.True(_redactor.ContainsSecret(input));
        Assert.DoesNotContain("hunter2", _redactor.Redact(input));
    }

    [Fact]
    public void Redact_MultipleSecretsInOneString_AreAllRedacted()
    {
        const string input = "Password=hunter2;TOKEN=abc123;API_KEY=xyz789";

        var redacted = _redactor.Redact(input);

        Assert.DoesNotContain("hunter2", redacted);
        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("xyz789", redacted);
        Assert.Equal(3, redacted.Split("[REDACTED]").Length - 1);
    }

    [Fact]
    public void Redact_PreservesKeyNameAndSeparator()
    {
        var redacted = _redactor.Redact("Password=hunter2");

        Assert.StartsWith("Password=", redacted);
    }

    [Fact]
    public void Redact_PrivateKeyPemBlock_IsFullyRedacted()
    {
        const string pem = """
            -----BEGIN RSA PRIVATE KEY-----
            MIIEowIBAAKCAQEA1c7+9z5Pad7OejecsQ0bu3aumPHRHmA1BQvB
            -----END RSA PRIVATE KEY-----
            """;

        var redacted = _redactor.Redact(pem);

        Assert.DoesNotContain("MIIEowIBAAKCAQEA1c7", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void Redact_BearerToken_RedactsTokenButKeepsScheme()
    {
        var redacted = _redactor.Redact("Authorization: Bearer abc.def.ghi123");

        Assert.StartsWith("Authorization: Bearer [REDACTED]", redacted);
        Assert.DoesNotContain("abc.def.ghi123", redacted);
    }

    [Theory]
    [InlineData("PasswordPolicy=Strict")]
    [InlineData("TokenType=Bearer")]
    [InlineData("SecretQuestion=FavoriteColor")]
    [InlineData("The token is stored securely in the vault")]
    [InlineData("https://example.com/api/token/123")]
    [InlineData("[REDACTED]")]
    public void Redact_DoesNotFalsePositiveOnLookalikeText(string input)
    {
        Assert.False(_redactor.ContainsSecret(input));
        Assert.Equal(input, _redactor.Redact(input));
    }

    [Fact]
    public void ContainsSecret_ReturnsFalseForPlainConfigurationText()
    {
        const string input = "MaxConnections=100;Timeout=30;LogLevel=Information";

        Assert.False(_redactor.ContainsSecret(input));
    }

    [Fact]
    public void ContainsSecret_ReturnsTrueWhenAnyRuleMatches()
    {
        Assert.True(_redactor.ContainsSecret("SECRET=value"));
    }

    [Fact]
    public void Redact_QuotedJsonStyleKey_IsDetectedDespiteClosingQuoteBeforeSeparator()
    {
        const string input = "\"password\": \"hunter2\"";

        Assert.True(_redactor.ContainsSecret(input));
        Assert.DoesNotContain("hunter2", _redactor.Redact(input));
    }

    [Theory]
    [InlineData("\"clientSecret\": \"abc123\"", "abc123")]
    [InlineData("client_secret=abc123", "abc123")]
    [InlineData("\"accessToken\": \"xyz789\"", "xyz789")]
    [InlineData("\"refreshToken\": \"rft001\"", "rft001")]
    public void Redact_CompoundCamelCaseOrSnakeCaseKeys_AreDetected(string input, string secretValue)
    {
        Assert.True(_redactor.ContainsSecret(input));
        Assert.DoesNotContain(secretValue, _redactor.Redact(input));
    }
}
