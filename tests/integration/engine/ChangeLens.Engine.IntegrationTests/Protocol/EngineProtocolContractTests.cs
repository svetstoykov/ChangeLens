using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Support;
using Json.Schema;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies the version 1 engine-protocol schemas and shared fixtures.
/// </summary>
public sealed class EngineProtocolContractTests
{
    private static readonly IReadOnlyDictionary<string, JsonSchema> Schemas = LoadSchemas();

    /// <summary>
    ///     Verifies that a shared fixture satisfies its declared protocol schema.
    /// </summary>
    /// <param name="schemaFileName">The schema file used to validate the fixture.</param>
    /// <param name="fixtureFileName">The fixture file relative to the v1 fixtures directory.</param>
    [Theory]
    [InlineData("engine-status.schema.json", "engine-check-status.request.json")]
    [InlineData("engine-status.schema.json", "engine-check-status.result.json")]
    [InlineData("engine-status.schema.json", "ordered-errors.response.json")]
    [InlineData("error-response.schema.json", "ordered-errors.response.json")]
    [InlineData("engine-status.schema.json", "uncorrelated-error.response.json")]
    [InlineData("error-response.schema.json", "uncorrelated-error.response.json")]
    [InlineData("payload-free-result.schema.json", "engine-check-status.result.json")]
    [InlineData("repository-open.schema.json", "repositories-open.request.json")]
    [InlineData("repository-open.schema.json", "repositories-open.branch.result.json")]
    [InlineData("repository-open.schema.json", "repositories-open.detached.result.json")]
    [InlineData("repository-restore-last.schema.json", "repositories-restore-last.request.json")]
    [InlineData("repository-restore-last.schema.json", "repositories-restore-last.none.result.json")]
    [InlineData(
        "repository-restore-last.schema.json",
        "repositories-restore-last.restored.result.json")]
    [InlineData("repository-list-recent.schema.json", "repositories-list-recent.request.json")]
    [InlineData("repository-list-recent.schema.json", "repositories-list-recent.result.json")]
    [InlineData("repository-remove-recent.schema.json", "repositories-remove-recent.request.json")]
    [InlineData("repository-remove-recent.schema.json", "repositories-remove-recent.result.json")]
    [InlineData("preference-color-theme.schema.json", "preferences-get-color-theme.request.json")]
    [InlineData("preference-color-theme.schema.json", "preferences-get-color-theme.result.json")]
    [InlineData("preference-color-theme.schema.json", "preferences-set-color-theme.request.json")]
    [InlineData("preference-color-theme.schema.json", "preferences-set-color-theme.result.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-list-targets.request.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-list-targets.first-page.result.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-list-targets.next-page.request.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-list-targets.empty.result.json")]
    [InlineData("comparison-prepare.schema.json", "comparisons-prepare.request.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-prepare.ready.result.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-prepare.empty.result.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-prepare.conflicts.result.json")]
    [InlineData(
        "comparison-check-freshness.schema.json",
        "comparisons-check-freshness.request.json")]
    [InlineData(
        "comparison-check-freshness.schema.json",
        "comparisons-check-freshness.current.result.json")]
    [InlineData(
        "comparison-check-freshness.schema.json",
        "comparisons-check-freshness.stale.result.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-check-remote-baseline.request.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-check-remote-baseline.current.result.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-check-remote-baseline.moved.result.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-check-remote-baseline.no-remote.result.json")]
    [InlineData(
        "comparison-refresh-remote-baseline.schema.json",
        "comparisons-refresh-remote-baseline.request.json")]
    [InlineData(
        "comparison-refresh-remote-baseline.schema.json",
        "comparisons-refresh-remote-baseline.result.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-error-target-invalid.response.json")]
    [InlineData(
        "comparison-refresh-remote-baseline.schema.json",
        "comparisons-error-target-invalid.response.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-error-remote-unreachable.response.json")]
    [InlineData(
        "comparison-refresh-remote-baseline.schema.json",
        "comparisons-error-remote-unreachable.response.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-error-remote-authentication-required.response.json")]
    [InlineData(
        "comparison-refresh-remote-baseline.schema.json",
        "comparisons-error-remote-authentication-required.response.json")]
    [InlineData(
        "comparison-check-remote-baseline.schema.json",
        "comparisons-error-remote-timed-out.response.json")]
    [InlineData(
        "comparison-refresh-remote-baseline.schema.json",
        "comparisons-error-remote-timed-out.response.json")]
    [InlineData(
        "comparison-refresh-remote-baseline.schema.json",
        "comparisons-error-no-remote-configured.response.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-error-invalid-target-query.response.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-error-invalid-target-page.response.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-error-targets-changed.response.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-error-target-unavailable.response.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-error-target-invalid.response.json")]
    [InlineData(
        "comparison-check-freshness.schema.json",
        "comparisons-error-target-invalid.response.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-error-unrelated-history.response.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-error-ambiguous-base.response.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-error-changed-during-preparation.response.json")]
    [InlineData(
        "comparison-prepare.schema.json",
        "comparisons-error-too-large.response.json")]
    [InlineData(
        "comparison-list-targets.schema.json",
        "comparisons-error-timed-out.response.json")]
    [InlineData(
        "comparison-check-freshness.schema.json",
        "comparisons-error-inspection-failed.response.json")]
    [InlineData(
        "comparison-check-freshness.schema.json",
        "comparisons-error-invalid-freshness-token.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-invalid-target-query.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-invalid-target-page.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-targets-changed.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-target-unavailable.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-target-invalid.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-unrelated-history.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-ambiguous-base.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-changed-during-preparation.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-too-large.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-timed-out.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-inspection-failed.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-invalid-freshness-token.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-remote-unreachable.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-remote-authentication-required.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-remote-timed-out.response.json")]
    [InlineData(
        "error-response.schema.json",
        "comparisons-error-no-remote-configured.response.json")]
    [InlineData("analysis-start.schema.json", "analysis-start.accepted.result.json")]
    [InlineData("analysis-poll-run.schema.json", "analysis-poll-run.completed.result.json")]
    [InlineData("analysis-poll-run.schema.json", "analysis-poll-run.captured.result.json")]
    [InlineData("analysis-get-active.schema.json", "analysis-get-active.active.result.json")]
    [InlineData("analysis-cancel.schema.json", "analysis-cancel.result.json")]
    public void SharedFixtureMatchesSchema(string schemaFileName, string fixtureFileName)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(FixturePath(fixtureFileName)));

        var result = Schemas[schemaFileName].Evaluate(fixture.RootElement);

        Assert.True(result.IsValid);
    }

    /// <summary>
    ///     Verifies that a non-GUID snapshot identifier fails poll-result schema validation.
    /// </summary>
    [Fact]
    public void PollResultSchemaRejectsNonGuidSnapshotIdentifier()
    {
        var malformed = File.ReadAllText(FixturePath("analysis-poll-run.captured.result.json"))
            .Replace("\"snapshotId\":\"7198a1b2-3c4d-4e5f-8a9b-0123456789ab\"", "\"snapshotId\":\"snapshot-1\"",
                StringComparison.Ordinal);
        using var instance = JsonDocument.Parse(malformed);

        var result = Schemas["analysis-poll-run.schema.json"].Evaluate(instance.RootElement);

        Assert.False(result.IsValid);
    }

    /// <summary>
    ///     Verifies that inputless actions accept ignored parameters.
    /// </summary>
    /// <param name="schemaFileName">The schema used to validate the request.</param>
    /// <param name="json">The inputless action request with an ignored parameters value.</param>
    [Theory]
    [InlineData(
        "engine-status.schema.json",
        """{"protocolVersion":1,"requestId":"id","action":"engine.checkStatus","parameters":{"ignored":true}}""")]
    [InlineData(
        "repository-restore-last.schema.json",
        """{"protocolVersion":1,"requestId":"id","action":"repositories.restoreLast","parameters":null}""")]
    [InlineData(
        "repository-list-recent.schema.json",
        """{"protocolVersion":1,"requestId":"id","action":"repositories.listRecent","parameters":[]}""")]
    [InlineData(
        "preference-color-theme.schema.json",
        """{"protocolVersion":1,"requestId":"id","action":"preferences.getColorTheme","parameters":"ignored"}""")]
    public void InputlessActionSchemasAcceptIgnoredParameters(string schemaFileName, string json)
    {
        using var instance = JsonDocument.Parse(json);

        var result = Schemas[schemaFileName].Evaluate(instance.RootElement);

        Assert.True(result.IsValid);
    }

    /// <summary>
    ///     Verifies that the shared error schema rejects empty and malformed error collections.
    /// </summary>
    /// <param name="json">The invalid error response.</param>
    [Theory]
    [InlineData("""{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[]}""")]
    [InlineData("""{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Validation","code":"","message":"Bad"}]}""")]
    [InlineData("""{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Unknown","code":"fixture.first","message":"Bad"}]}""")]
    public void ErrorSchemaRejectsInvalidCollections(string json)
    {
        using var instance = JsonDocument.Parse(json);

        var result = Schemas["error-response.schema.json"].Evaluate(instance.RootElement);

        Assert.False(result.IsValid);
    }

    /// <summary>
    ///     Verifies that protocol schemas reject identifiers and error text containing only whitespace.
    /// </summary>
    /// <param name="schemaFileName">The schema used to validate the document.</param>
    /// <param name="json">The document containing a whitespace-only string.</param>
    [Theory]
    [InlineData(
        "engine-status.schema.json",
        """{"protocolVersion":1,"requestId":" \t ","action":"engine.checkStatus"}""")]
    [InlineData(
        "payload-free-result.schema.json",
        """{"protocolVersion":1,"type":"result","requestId":" \t ","result":null}""")]
    [InlineData(
        "error-response.schema.json",
        """{"protocolVersion":1,"type":"error","requestId":" \t ","errors":[{"type":"Validation","code":"fixture.first","message":"Bad"}]}""")]
    [InlineData(
        "error-response.schema.json",
        """{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Validation","code":" \t ","message":"Bad"}]}""")]
    [InlineData(
        "error-response.schema.json",
        """{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Validation","code":"fixture.first","message":" \t "}]}""")]
    public void SchemasRejectWhitespaceOnlyStrings(string schemaFileName, string json)
    {
        using var instance = JsonDocument.Parse(json);

        var result = Schemas[schemaFileName].Evaluate(instance.RootElement);

        Assert.False(result.IsValid);
    }

    /// <summary>
    ///     Verifies that the repository-open schema rejects malformed requests and results.
    /// </summary>
    /// <param name="json">The malformed repository-open message.</param>
    [Theory]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open"}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":1}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":" "}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":"/repo","extra":true}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"branch","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"detached","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"detached","revision":"0123456789ABCDEF0123456789ABCDEF01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"detached","revision":"0123456789abcdef"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":" ","canonicalPath":"/repo","head":{"kind":"branch","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":" ","head":{"kind":"branch","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"branch","name":" ","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    public void RepositoryOpenSchemaRejectsMalformedMessages(string json)
    {
        using var instance = JsonDocument.Parse(json);

        var result = Schemas["repository-open.schema.json"].Evaluate(instance.RootElement);

        Assert.False(result.IsValid);
    }

    /// <summary>
    ///     Verifies that duplicate repository-open path properties are rejected before schema evaluation.
    /// </summary>
    [Fact]
    public void RepositoryOpenContractRejectsDuplicatePathProperties()
    {
        const string request =
            """{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":"/first","path":"/second"}}""";

        Assert.False(IsContractValid("repository-open.schema.json", request));
    }

    /// <summary>
    ///     Verifies the target-list contract rejects malformed requests and result fields.
    /// </summary>
    /// <param name="json">The malformed target-list exchange.</param>
    [Theory]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets"}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":null}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":{}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listtargets","parameters":{"path":"/repo"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":{"Path":"/repo"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":{"path":"/repo","query":null}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":{"path":"/repo","after":"refs/heads/main"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":{"path":"/repo","targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":{"path":"/repo","after":"refs/heads/main","targetSetToken":"ABCDEF6789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"targets":[{"kind":"REMOTE_TRACKING","name":"origin/main","fullName":"refs/remotes/origin/main","revision":"0123456789abcdef0123456789abcdef01234567"}],"suggestedTarget":null,"nextCursor":null,"targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","unsupportedTargetCount":0}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"targets":[],"suggestedTarget":null,"nextCursor":null,"targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","unsupportedTargetCount":-1}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"targets":[],"suggestedTarget":null,"nextCursor":null,"targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","unsupportedTargetCount":0.5}}""")]
    public void ComparisonListTargetsSchemaRejectsMalformedMessages(string json)
    {
        Assert.False(IsContractValid("comparison-list-targets.schema.json", json));
    }

    /// <summary>
    ///     Verifies target-list scalar bounds and duplicate-property rejection.
    /// </summary>
    [Fact]
    public void ComparisonListTargetsSchemaRejectsOverlongAndDuplicateFields()
    {
        var token = new string('0', 64);

        Assert.False(
            IsContractValid(
                "comparison-list-targets.schema.json",
                JsonSerializer.Serialize(
                    new
                    {
                        protocolVersion = 1,
                        requestId = "id",
                        action = "comparisons.listTargets",
                        parameters = new { path = "/repo", query = new string('q', 257) },
                    })));
        Assert.False(
            IsContractValid(
                "comparison-list-targets.schema.json",
                JsonSerializer.Serialize(
                    new
                    {
                        protocolVersion = 1,
                        requestId = "id",
                        action = "comparisons.listTargets",
                        parameters = new
                        {
                            path = "/repo",
                            after = "refs/heads/" + new string('c', 4_086),
                            targetSetToken = token,
                        },
                    })));
        Assert.False(
            IsContractValid(
                "comparison-list-targets.schema.json",
                """{"protocolVersion":1,"requestId":"id","action":"comparisons.listTargets","parameters":{"path":"/first","path":"/second"}}"""));
    }

    /// <summary>
    ///     Verifies the comparison-preparation contract rejects malformed requests, counts, revisions, and unions.
    /// </summary>
    /// <param name="json">The malformed comparison-preparation exchange.</param>
    [Theory]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.prepare","parameters":{"path":"/repo"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.prepare","parameters":{"path":"/repo","target":null}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.Prepare","parameters":{"path":"/repo","target":"refs/heads/main"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.prepare","parameters":{"path":"/repo","target":"refs/heads/main","extra":true}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"branch","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}},"target":{"kind":"local","name":"topic","fullName":"refs/heads/topic","revision":"bad"},"mergeBaseRevision":"0123456789abcdef0123456789abcdef01234567","currentWorkCommitCount":1,"targetOnlyCommitCount":0,"changedFileTotal":1,"uncommittedFileTotal":0,"stagedFileCount":0,"unstagedFileCount":0,"untrackedFileCount":0,"readiness":{"state":"ready"},"freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"branch","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}},"target":{"kind":"local","name":"topic","fullName":"refs/heads/topic","revision":"0123456789abcdef0123456789abcdef01234567"},"mergeBaseRevision":"0123456789abcdef0123456789abcdef01234567","currentWorkCommitCount":-1,"targetOnlyCommitCount":0,"changedFileTotal":1,"uncommittedFileTotal":0,"stagedFileCount":0,"unstagedFileCount":0,"untrackedFileCount":0,"readiness":{"state":"ready","conflictedFileCount":0},"freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"branch","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}},"target":{"kind":"local","name":"topic","fullName":"refs/heads/topic","revision":"0123456789abcdef0123456789abcdef01234567"},"mergeBaseRevision":"0123456789abcdef0123456789abcdef01234567","currentWorkCommitCount":1.5,"targetOnlyCommitCount":0,"changedFileTotal":1,"uncommittedFileTotal":0,"stagedFileCount":0,"unstagedFileCount":0,"untrackedFileCount":0,"readiness":{"state":"unknown"},"freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    public void ComparisonPrepareSchemaRejectsMalformedMessages(string json)
    {
        Assert.False(IsContractValid("comparison-prepare.schema.json", json));
    }

    /// <summary>
    ///     Verifies preparation rejects an overlong target and duplicate target property.
    /// </summary>
    [Fact]
    public void ComparisonPrepareSchemaRejectsOverlongAndDuplicateTargets()
    {
        Assert.False(
            IsContractValid(
                "comparison-prepare.schema.json",
                JsonSerializer.Serialize(
                    new
                    {
                        protocolVersion = 1,
                        requestId = "id",
                        action = "comparisons.prepare",
                        parameters = new
                        {
                            path = "/repo",
                            target = "refs/heads/" + new string('t', 4_086),
                        },
                    })));
        Assert.False(
            IsContractValid(
                "comparison-prepare.schema.json",
                """{"protocolVersion":1,"requestId":"id","action":"comparisons.prepare","parameters":{"path":"/repo","target":"refs/heads/one","target":"refs/heads/two"}}"""));
    }

    /// <summary>
    ///     Verifies the freshness contract rejects malformed requests, tokens, tags, and union fields.
    /// </summary>
    /// <param name="json">The malformed freshness exchange.</param>
    [Theory]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.checkFreshness","parameters":{"path":"/repo","target":"refs/heads/main"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.checkFreshness","parameters":{"path":"/repo","target":"refs/heads/main","freshnessToken":null}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.checkfreshness","parameters":{"path":"/repo","target":"refs/heads/main","freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.checkFreshness","parameters":{"path":"/repo","target":"refs/heads/main","freshnessToken":"ABCDEF6789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"state":"CURRENT"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"state":"current","extra":true}}""")]
    public void ComparisonCheckFreshnessSchemaRejectsMalformedMessages(string json)
    {
        Assert.False(IsContractValid("comparison-check-freshness.schema.json", json));
    }

    /// <summary>
    ///     Verifies the remote-baseline-check contract rejects malformed requests and result fields.
    /// </summary>
    /// <param name="json">The malformed remote-baseline-check exchange.</param>
    [Theory]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.checkRemoteBaseline","parameters":{"path":"/repo"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.checkRemoteBaseline","parameters":{"path":"/repo","target":"refs/heads/main"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.checkremotebaseline","parameters":{"path":"/repo","target":"refs/remotes/origin/main"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"state":"CURRENT","remoteRevision":"0123456789abcdef0123456789abcdef01234567"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"state":"current"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"state":"noRemote","remoteRevision":"0123456789abcdef0123456789abcdef01234567"}}""")]
    public void ComparisonCheckRemoteBaselineSchemaRejectsMalformedMessages(string json)
    {
        Assert.False(IsContractValid("comparison-check-remote-baseline.schema.json", json));
    }

    /// <summary>
    ///     Verifies the remote-baseline-refresh contract rejects malformed requests and result fields.
    /// </summary>
    /// <param name="json">The malformed remote-baseline-refresh exchange.</param>
    [Theory]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.refreshRemoteBaseline","parameters":{"path":"/repo"}}""")]
    [InlineData(
        """{"protocolVersion":1,"requestId":"id","action":"comparisons.refreshRemoteBaseline","parameters":{"path":"/repo","target":"refs/heads/main"}}""")]
    [InlineData(
        """{"protocolVersion":1,"type":"result","requestId":"id","result":{"remoteRevision":"bad"}}""")]
    public void ComparisonRefreshRemoteBaselineSchemaRejectsMalformedMessages(string json)
    {
        Assert.False(IsContractValid("comparison-refresh-remote-baseline.schema.json", json));
    }

    /// <summary>Verifies analysis-start transport structure and identifier bounds.</summary>
    [Theory]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"analysis.start","parameters":{"path":"/repo","target":"refs/heads/main"}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"analysis.start","parameters":{"path":"/repo","target":"refs/heads/main","freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","checks":{"build":true,"tests":false}}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"analysis.start","parameters":{"path":"/repo","path":"/repo","target":"refs/heads/main","freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}}""")]
    public void AnalysisStartSchemaRejectsMalformedMessages(string json)
    {
        Assert.False(IsContractValid("analysis-start.schema.json", json));
    }

    /// <summary>Verifies analysis-poll-run rejects invalid terminal counts and run identifiers.</summary>
    [Theory]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"analysis.pollRun","parameters":{"runId":"not-a-guid"}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"runId":"0198a1b2-3c4d-4e5f-8a9b-0123456789ab","state":"completedWithLimitations","repository":{"repositoryId":"5298a1b2-3c4d-4e5f-8a9b-0123456789ab","displayName":"repo","canonicalPath":"/repo","head":"0123456789abcdef0123456789abcdef01234567"},"comparison":{"target":"refs/heads/main","targetRevision":"89abcdef0123456789abcdef0123456789abcdef","freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"},"requestedAt":1,"captureStartedAt":null,"capturedAt":null,"snapshotId":null,"cancellationRequested":false,"facts":[],"terminal":{"kind":"completedWithLimitations","terminalAt":2,"limitationCount":-1},"interruptedAt":null,"interruptionReason":null}}""")]
    public void AnalysisPollRunSchemaRejectsMalformedMessages(string json)
    {
        Assert.False(IsContractValid("analysis-poll-run.schema.json", json));
    }

    private static IReadOnlyDictionary<string, JsonSchema> LoadSchemas()
    {
        var names = new[]
        {
            "engine-status.schema.json",
            "error-response.schema.json",
            "payload-free-result.schema.json",
            "repository-open.schema.json",
            "repository-restore-last.schema.json",
            "repository-list-recent.schema.json",
            "repository-remove-recent.schema.json",
            "preference-color-theme.schema.json",
            "comparison-list-targets.schema.json",
            "comparison-prepare.schema.json",
            "comparison-check-freshness.schema.json",
            "comparison-check-remote-baseline.schema.json",
            "comparison-refresh-remote-baseline.schema.json",
            "analysis-start.schema.json",
            "analysis-poll-run.schema.json",
            "analysis-get-active.schema.json",
            "analysis-cancel.schema.json",
        };

        return names.ToDictionary(
            name => name,
            name => JsonSchema.FromFile(Path.Combine(RepositoryPaths.EngineProtocolV1, name)),
            StringComparer.Ordinal);
    }

    /// <summary>
    ///     Determines whether raw JSON satisfies a protocol schema after duplicate-property validation.
    /// </summary>
    /// <param name="schemaFileName">The schema used to validate the JSON.</param>
    /// <param name="json">The raw JSON to validate.</param>
    /// <returns>
    ///     <see langword="true" /> if the JSON has no duplicate properties and satisfies the schema; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool IsContractValid(string schemaFileName, string json)
    {
        using var instance = JsonDocument.Parse(json);

        return !ContainsDuplicateProperties(instance.RootElement) &&
               Schemas[schemaFileName].Evaluate(instance.RootElement).IsValid;
    }

    /// <summary>
    ///     Determines whether the JSON value contains an object with duplicate property names.
    /// </summary>
    /// <param name="element">The JSON value to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> if an object contains duplicate property names; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool ContainsDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || ContainsDuplicateProperties(property.Value))
                {
                    return true;
                }
            }

            return false;
        }

        return element.ValueKind == JsonValueKind.Array && element.EnumerateArray().Any(ContainsDuplicateProperties);
    }

    private static string FixturePath(string fileName) => Path.Combine(
        RepositoryPaths.EngineProtocolV1,
        "fixtures",
        fileName);
}
