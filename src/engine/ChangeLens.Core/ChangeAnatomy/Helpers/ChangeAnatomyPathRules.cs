namespace ChangeLens.Core.ChangeAnatomy.Helpers;

/// <summary>
///     Provides the shared path exclusions used by deterministic analysis stages.
/// </summary>
/// <remarks>
///     The same decision must be used by change anatomy and later correspondence indexing so excluded paths do not
///     produce keys that can never match. Reasons are deliberately short and safe to include in run diagnostics.
/// </remarks>
public static class ChangeAnatomyPathRules
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".idea", ".vs", ".vscode", "node_modules", "bower_components", "vendor", "third_party",
        "thirdparty", "Pods", "bin", "obj", "dist", "target", "coverage", "__pycache__", ".venv", "venv",
        ".tox", ".mypy_cache", ".pytest_cache", ".next", ".nuxt", ".gradle", ".terraform",
    };

    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "cargo.lock", "poetry.lock", "composer.lock",
        "gemfile.lock", "go.sum", "packages.lock.json", ".npmrc", ".pypirc", "id_rsa", "id_dsa", "id_ecdsa",
        "id_ed25519", "credentials", "credentials.json", "secrets", "secrets.json",
    };

    private static readonly string[] SecretExtensions =
        [".pem", ".key", ".pfx", ".p12", ".jks", ".keystore", ".ppk", ".crt", ".cer", ".der"];

    private static readonly string[] GeneratedSuffixes =
        [".min.js", ".min.css", ".map", ".designer.cs", ".g.cs", ".g.i.cs", ".generated.cs", ".pb.go", "_pb2.py", ".feature.cs"];

    private static readonly string[] BinaryExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".svgz", ".pdf", ".zip", ".gz", ".tar", ".7z", ".rar",
        ".jar", ".war", ".nupkg", ".dll", ".exe", ".so", ".dylib", ".pdb", ".class", ".pyc", ".o", ".a", ".wasm",
        ".woff", ".woff2", ".ttf", ".eot", ".otf", ".mp3", ".mp4", ".mov", ".avi", ".webm",
    ];

    /// <summary>
    ///     Returns a skip reason when the repository-relative path is excluded, or <see langword="null" /> otherwise.
    /// </summary>
    /// <param name="path">The repository-relative path to inspect. Cannot be <see langword="null" />.</param>
    /// <returns>A short skip reason, or <see langword="null" /> when the path may be analyzed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> is <see langword="null" />.</exception>
    public static string? ExclusionReason(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "empty path";
        }

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (ExcludedDirectories.Contains(segments[index]))
            {
                return $"inside '{segments[index]}'";
            }
        }

        var name = segments[^1];
        if (ExcludedFileNames.Contains(name) || name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return "lock, credential, or secret file";
        }

        if (name.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase))
        {
            return "lock, credential, or secret file";
        }

        if (name.Equals(".env", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return "environment file";
        }

        if (EndsWithAny(name, SecretExtensions))
        {
            return "key or certificate file";
        }

        if (EndsWithAny(name, GeneratedSuffixes))
        {
            return "generated file";
        }

        return EndsWithAny(name, BinaryExtensions) ? "binary extension" : null;
    }

    private static bool EndsWithAny(string value, IReadOnlyList<string> suffixes) =>
        suffixes.Any(suffix => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}
