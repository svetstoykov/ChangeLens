using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.ContextPolicy.Services;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.ContextPolicy;

/// <summary>
///     Verifies secret scanner pattern matching and captured-value filtering.
/// </summary>
public sealed class ContextPolicySecretScannerIntegrationTests
{
    /// <summary>
    ///     Rejects placeholders, bare references, keywords, and values below the minimum length.
    /// </summary>
    [Fact]
    public void Scan_ValueCapturingPatternsRejectPlaceholdersAndBareReferences()
    {
        var lines = new[]
        {
            "password = ${DB_PASSWORD} ;",
            "token: null",
            "secret = config.Secret",
            "pwd = \"short\"",
        };

        var matches = ContextPolicySecretScanner.Scan(lines, 8);

        Assert.Empty(matches);
    }

    /// <summary>
    ///     Matches a concrete quoted assignment with the secret assignment pattern.
    /// </summary>
    [Fact]
    public void Scan_MatchesConcreteSecretAssignment()
    {
        var lines = new[] { "password = \"hunter2hunter2\"" };

        var match = Assert.Single(ContextPolicySecretScanner.Scan(lines, 8));

        Assert.Equal(0, match.LineIndex);
        Assert.Equal("secret assignment", match.PatternName);
    }

    /// <summary>
    ///     Matches a known credential format with its own pattern.
    /// </summary>
    [Fact]
    public void Scan_MatchesKnownCredentialFormat()
    {
        var lines = new[] { "var apiKey = \"AKIAABCDEFGHIJKLMNOP\";" };

        var match = Assert.Single(ContextPolicySecretScanner.Scan(lines, 8));

        Assert.Equal("aws access key id", match.PatternName);
    }

    /// <summary>
    ///     Matches every line from the private key begin marker through the end marker and resumes after it.
    /// </summary>
    [Fact]
    public void Scan_MatchesEveryPrivateKeyBlockLine()
    {
        var lines = new[]
        {
            "-----BEGIN RSA PRIVATE KEY-----",
            "MIIEowIBAAKCAQEA",
            "c29tZW1vcmViYXNlNjQ=",
            "-----END RSA PRIVATE KEY-----",
            "trailing = 1",
        };

        var matches = ContextPolicySecretScanner.Scan(lines, 8);

        Assert.Equal(4, matches.Count);
        Assert.All(matches, match => Assert.Equal("private key block", match.PatternName));
        Assert.Equal(new[] { 0, 1, 2, 3 }, matches.Select(match => match.LineIndex));
    }

    /// <summary>
    ///     Extends the private key block to the last line when the end marker is absent.
    /// </summary>
    [Fact]
    public void Scan_ExtendsPrivateKeyBlockWhenEndMarkerIsAbsent()
    {
        var lines = new[]
        {
            "-----BEGIN OPENSSH PRIVATE KEY-----",
            "b3BlbnNzaC1rZXktdjEAAAAABG5vbmU",
            "c2Vjb25kIGxpbmU",
        };

        var matches = ContextPolicySecretScanner.Scan(lines, 8);

        Assert.Equal(3, matches.Count);
        Assert.All(matches, match => Assert.Equal("private key block", match.PatternName));
    }
}
