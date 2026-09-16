namespace ChangeLens.Core.Correspondence.Services;

/// <summary>
///     Provides the file-name and extension table that recognizes a captured file's language.
/// </summary>
/// <remarks>
///     Markdown and plain text are recognized but excluded from the cross-language boost. Documentation quotes the
///     identifiers a change touches, so boosting prose would rank it above the code that defines them.
/// </remarks>
internal static class CorrespondenceLanguageRules
{
    private static readonly Dictionary<string, string> FileNameLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = "dockerfile",
        ["Containerfile"] = "dockerfile",
        ["Makefile"] = "make",
        ["GNUmakefile"] = "make",
        ["CMakeLists.txt"] = "cmake",
        ["Gemfile"] = "ruby",
        ["Rakefile"] = "ruby",
        ["Podfile"] = "ruby",
        ["Brewfile"] = "ruby",
        ["Jenkinsfile"] = "groovy",
        ["Procfile"] = "text",
        ["Vagrantfile"] = "ruby",
    };

    private static readonly Dictionary<string, string> ExtensionLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        [".ts"] = "javascript", [".tsx"] = "javascript", [".js"] = "javascript", [".jsx"] = "javascript",
        [".mjs"] = "javascript", [".cjs"] = "javascript", [".mts"] = "javascript", [".cts"] = "javascript",
        [".vue"] = "javascript", [".svelte"] = "javascript",
        [".py"] = "python", [".pyi"] = "python",
        [".cs"] = "csharp", [".csx"] = "csharp", [".razor"] = "csharp", [".cshtml"] = "csharp",
        [".fs"] = "fsharp", [".fsx"] = "fsharp",
        [".vb"] = "visualbasic",
        [".java"] = "java",
        [".kt"] = "kotlin", [".kts"] = "kotlin",
        [".scala"] = "scala",
        [".groovy"] = "groovy",
        [".go"] = "go",
        [".rs"] = "rust",
        [".swift"] = "swift",
        [".m"] = "objectivec", [".mm"] = "objectivec",
        [".c"] = "c", [".h"] = "c", [".cc"] = "c", [".cpp"] = "c", [".cxx"] = "c", [".hpp"] = "c", [".hxx"] = "c",
        [".rb"] = "ruby", [".erb"] = "ruby",
        [".php"] = "php",
        [".pl"] = "perl", [".pm"] = "perl",
        [".ex"] = "elixir", [".exs"] = "elixir",
        [".erl"] = "erlang",
        [".hs"] = "haskell",
        [".dart"] = "dart",
        [".lua"] = "lua",
        [".r"] = "r",
        [".jl"] = "julia",
        [".sh"] = "shell", [".bash"] = "shell", [".zsh"] = "shell", [".fish"] = "shell",
        [".ps1"] = "powershell", [".psm1"] = "powershell",
        [".sql"] = "sql",
        [".graphql"] = "graphql", [".gql"] = "graphql",
        [".proto"] = "protobuf",
        [".html"] = "html", [".htm"] = "html",
        [".css"] = "css", [".scss"] = "css", [".sass"] = "css", [".less"] = "css",
        [".json"] = "json",
        [".yml"] = "yaml", [".yaml"] = "yaml",
        [".toml"] = "toml",
        [".xml"] = "xml",
        [".ini"] = "ini", [".conf"] = "ini", [".env"] = "ini", [".properties"] = "ini",
        [".tf"] = "terraform", [".tfvars"] = "terraform", [".hcl"] = "terraform",
        [".md"] = "markdown", [".mdx"] = "markdown", [".rst"] = "markdown",
        [".txt"] = "text",
        [".csproj"] = "msbuild", [".fsproj"] = "msbuild", [".vbproj"] = "msbuild", [".props"] = "msbuild",
        [".targets"] = "msbuild", [".sln"] = "msbuild",
    };

    private static readonly HashSet<string> ProseLanguages = new(StringComparer.Ordinal)
    {
        "markdown",
        "text",
    };

    /// <summary>
    ///     Returns the recognized language for a repository-relative path, or <see langword="null" /> when unknown.
    /// </summary>
    /// <param name="path">The repository-relative path to inspect. Cannot be <see langword="null" />.</param>
    /// <returns>The language name, or <see langword="null" /> when the path is not recognized.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> is <see langword="null" />.</exception>
    internal static string? LanguageFor(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var slash = path.LastIndexOf('/');
        var fileName = slash < 0 ? path : path[(slash + 1)..];
        if (fileName.Length == 0)
        {
            return null;
        }

        if (FileNameLanguages.TryGetValue(fileName, out var language))
        {
            return language;
        }

        var extension = Path.GetExtension(fileName);
        return extension.Length > 0 && ExtensionLanguages.TryGetValue(extension, out var byExtension)
            ? byExtension
            : null;
    }

    /// <summary>
    ///     Returns whether two recognized languages are different and eligible for the cross-language boost.
    /// </summary>
    /// <param name="left">The first recognized language, or <see langword="null" /> when unknown.</param>
    /// <param name="right">The second recognized language, or <see langword="null" /> when unknown.</param>
    /// <returns><see langword="true" /> only when both languages are known, non-prose, and different.</returns>
    internal static bool CrossesLanguage(string? left, string? right)
    {
        if (left is null || right is null || ProseLanguages.Contains(left) || ProseLanguages.Contains(right))
        {
            return false;
        }

        return !string.Equals(left, right, StringComparison.Ordinal);
    }
}
