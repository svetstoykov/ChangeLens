using ChangeLens.Infrastructure.ModelCompletion.Helpers;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion;

/// <summary>
///     Verifies that the provider API key is read from the key file entry and nowhere else.
/// </summary>
public sealed class ModelCompletionApiKeyFileReaderTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("changelens-key-file-");

    /// <inheritdoc />
    public void Dispose() => this.directory.Delete(recursive: true);

    /// <summary>
    ///     Reads the last key entry, with or without an export prefix and surrounding quotes, and ignores other entries.
    /// </summary>
    [Fact]
    public void Read_ReturnsTheLastKeyEntryValue()
    {
        var exported = this.WriteKeyFile("# local secrets", "OTHER_KEY=other", "export CHANGELENS_API_KEY=sk-exported");
        var quoted = this.WriteKeyFile("CHANGELENS_API_KEY=sk-first", "CHANGELENS_API_KEY=\"sk-quoted\"");

        Assert.Equal("sk-exported", ModelCompletionApiKeyFileReader.Read(exported));
        Assert.Equal("sk-quoted", ModelCompletionApiKeyFileReader.Read(quoted));
    }

    /// <summary>
    ///     Returns no key when no path is configured, the file is missing, or the file has no non-empty key entry.
    /// </summary>
    [Fact]
    public void Read_ReturnsNullWithoutAKeyEntry()
    {
        var withoutEntry = this.WriteKeyFile("OPENROUTER_API_KEY=sk-other", "CHANGELENS_API_KEY_OLD=sk-old");
        var empty = this.WriteKeyFile("export CHANGELENS_API_KEY=");

        Assert.Null(ModelCompletionApiKeyFileReader.Read(null));
        Assert.Null(ModelCompletionApiKeyFileReader.Read(Path.Combine(this.directory.FullName, "missing.env")));
        Assert.Null(ModelCompletionApiKeyFileReader.Read(withoutEntry));
        Assert.Null(ModelCompletionApiKeyFileReader.Read(empty));
    }

    private string WriteKeyFile(params string[] lines)
    {
        var path = Path.Combine(this.directory.FullName, $"{Guid.NewGuid():N}.env");
        File.WriteAllLines(path, lines);
        return path;
    }
}
