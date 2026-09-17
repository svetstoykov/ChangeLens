using System.Text.RegularExpressions;
using ChangeLens.Core.ContextPolicy.Models;

namespace ChangeLens.Core.ContextPolicy.Services;

/// <summary>
///     Scans quoted lines for recognizable secret values and private key material.
/// </summary>
/// <remarks>
///     The scanner is stateless and thread-safe. It recognizes well-known credential formats and assignment
///     shapes, so it can miss an unrecognized secret; it never reports more than one match per line.
/// </remarks>
public static partial class ContextPolicySecretScanner
{
    private const string PrivateKeyBlockName = "private key block";

    private static readonly ContextPolicySecretPattern[] Patterns =
    [
        new("aws access key id", AwsAccessKeyId(), false),
        new("github token", GitHubToken(), false),
        new("anthropic api key", AnthropicApiKey(), false),
        new("openai api key", OpenAiApiKey(), false),
        new("slack token", SlackToken(), false),
        new("google api key", GoogleApiKey(), false),
        new("gitlab token", GitLabToken(), false),
        new("npm token", NpmToken(), false),
        new("sendgrid api key", SendGridApiKey(), false),
        new("jwt", Jwt(), false),
        new("connection string password", ConnectionStringPassword(), true),
        new("xml key value attribute", XmlKeyValueAttribute(), true),
        new("bearer token", BearerToken(), true),
        new("url credentials", UrlCredentials(), true),
        new("secret assignment", SecretAssignment(), true),
    ];

    private static readonly HashSet<string> BareKeywordValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "default", "null", "true", "false", "new", "nil", "undefined", "empty",
    };

    private static readonly HashSet<string> PlaceholderWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "changeme", "placeholder", "example", "sample", "dummy", "fake", "todo", "none", "null",
    };

    /// <summary>
    ///     Scans lines in order and returns at most one secret match per line.
    /// </summary>
    /// <param name="lines">The quoted lines to scan. Cannot be <see langword="null" />.</param>
    /// <param name="minimumValueLength">The shortest captured value a value-capturing pattern treats as a secret.</param>
    /// <returns>The matches ordered by ascending line index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<ContextPolicySecretMatch> Scan(IReadOnlyList<string> lines, int minimumValueLength)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var matches = new List<ContextPolicySecretMatch>();
        var insidePrivateKeyBlock = false;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (insidePrivateKeyBlock)
            {
                matches.Add(new ContextPolicySecretMatch(lineIndex, PrivateKeyBlockName));
                insidePrivateKeyBlock = !PrivateKeyEnd().IsMatch(line);
                continue;
            }

            if (PrivateKeyBegin().IsMatch(line))
            {
                matches.Add(new ContextPolicySecretMatch(lineIndex, PrivateKeyBlockName));
                insidePrivateKeyBlock = !PrivateKeyEnd().IsMatch(line);
                continue;
            }

            var patternName = MatchLine(line, minimumValueLength);
            if (patternName is not null)
            {
                matches.Add(new ContextPolicySecretMatch(lineIndex, patternName));
            }
        }

        return matches;
    }

    private static string? MatchLine(string line, int minimumValueLength)
    {
        foreach (var pattern in Patterns)
        {
            if (pattern.CapturesValue)
            {
                if (HasAcceptedValue(pattern.Regex, line, minimumValueLength))
                {
                    return pattern.Name;
                }
            }
            else if (pattern.Regex.IsMatch(line))
            {
                return pattern.Name;
            }
        }

        return null;
    }

    private static bool HasAcceptedValue(Regex regex, string line, int minimumValueLength)
    {
        foreach (Match match in regex.Matches(line))
        {
            var bare = match.Groups["bare"];
            if (bare.Success)
            {
                if (ContainsBareForbiddenCharacter(bare.Value))
                {
                    continue;
                }

                var bareValue = TrimValue(bare.Value);
                if (BareDottedIdentifier().IsMatch(bareValue) || BareKeywordValues.Contains(bareValue))
                {
                    continue;
                }

                if (bareValue.Length >= minimumValueLength && !IsPlaceholder(bareValue))
                {
                    return true;
                }

                continue;
            }

            var captured = match.Groups["value"];
            if (!captured.Success)
            {
                continue;
            }

            var value = TrimValue(captured.Value);
            if (value.Length >= minimumValueLength && !IsPlaceholder(value))
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimValue(string value) => value.Trim().Trim('"', '\'', ',', ';');

    private static bool ContainsBareForbiddenCharacter(string value) =>
        value.Any(character => character is '(' or ')' or '{' or '}' or '[' or ']' or '<' or '>');

    private static bool IsPlaceholder(string value)
    {
        if (value.StartsWith("${", StringComparison.Ordinal) || value.StartsWith("$(", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Length >= 2 && value.StartsWith('%') && value.EndsWith('%'))
        {
            return true;
        }

        if (value.StartsWith('<') && value.EndsWith('>'))
        {
            return true;
        }

        if (value.Contains("{{", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Length > 0 && value.All(character => character is '*' or 'x' or 'X'))
        {
            return true;
        }

        if (value.StartsWith("your", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return PlaceholderWords.Contains(value);
    }

    [GeneratedRegex("""-----BEGIN[A-Z ]*PRIVATE KEY-----""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex PrivateKeyBegin();

    [GeneratedRegex("""-----END[A-Z ]*PRIVATE KEY-----""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex PrivateKeyEnd();

    [GeneratedRegex("""AKIA[0-9A-Z]{16}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex AwsAccessKeyId();

    [GeneratedRegex("""gh[pousr]_[A-Za-z0-9]{36,}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex GitHubToken();

    [GeneratedRegex("""sk-ant-[A-Za-z0-9\-_]{20,}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex AnthropicApiKey();

    [GeneratedRegex("""sk-[A-Za-z0-9]{20,}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex OpenAiApiKey();

    [GeneratedRegex("""xox[abprs]-[A-Za-z0-9-]{10,}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex SlackToken();

    [GeneratedRegex("""AIza[0-9A-Za-z\-_]{35}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex GoogleApiKey();

    [GeneratedRegex("""glpat-[A-Za-z0-9\-_]{20,}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex GitLabToken();

    [GeneratedRegex("""npm_[A-Za-z0-9]{36}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex NpmToken();

    [GeneratedRegex("""SG\.[A-Za-z0-9\-_]{20,}""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex SendGridApiKey();

    [GeneratedRegex(
        """eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex Jwt();

    [GeneratedRegex(
        """(?:Password|Pwd)\s*=\s*(?<value>[^;"'\s]{4,})""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPassword();

    [GeneratedRegex(
        """key\s*=\s*["'][^"']*(?:password|passwd|pwd|secret|token|apikey|api[_-]?key|access[_-]?key""" +
        """|client[_-]?secret|secret[_-]?key|private[_-]?key|signing[_-]?key|encryption[_-]?key)["']\s+value\s*=\s*""" +
        """["'](?<value>[^"']*)["']""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase)]
    private static partial Regex XmlKeyValueAttribute();

    [GeneratedRegex(
        """authorization["']?\s*[=:,]\s*["']?(?:bearer|basic)\s+(?<value>[^"'\s]+)""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase)]
    private static partial Regex BearerToken();

    [GeneratedRegex(
        """://[^\s/:@]+:(?<value>[^\s/:@]+)@""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex UrlCredentials();

    [GeneratedRegex(
        """(?:password|passwd|pwd|secret|token|apikey|api[_-]?key|access[_-]?key|client[_-]?secret|secret[_-]?key""" +
        """|private[_-]?key|signing[_-]?key|encryption[_-]?key)(?:[_-][A-Za-z0-9]+)*["']?\s*[=:]\s*""" +
        """(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<bare>\S+))""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase)]
    private static partial Regex SecretAssignment();

    [GeneratedRegex(
        """^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+$""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex BareDottedIdentifier();
}
