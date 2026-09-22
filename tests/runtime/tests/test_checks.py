from pathlib import Path

from changelens_review.plans.checks.model import CaseEvidence
from changelens_review.plans.checks.registry import evaluate_expectation, validate_expectation
from changelens_review.plans.state_snapshot import DatabaseSnapshot
from changelens_review.provider.proxy import ExchangeRecord


def exchange(request_text: str = "{}") -> ExchangeRecord:
    return ExchangeRecord(
        sequence=1,
        role="curator",
        started_at="t",
        latency_ms=1.0,
        request_text=request_text,
        response_status=200,
        response_text="{}",
        outcome="scripted",
        detail=None,
        model=None,
        prompt_tokens=None,
        completion_tokens=None,
        cost=None,
    )


def database(entries: tuple = (), **run_values: object) -> DatabaseSnapshot:
    row = {
        "run_id": "run-1",
        "validation_removal_count": 0,
        "excluded_staged_count": 0,
        "excluded_unstaged_count": 0,
        "excluded_untracked_count": 0,
        "excluded_conflicted_count": 0,
        "excluded_uncommitted_total": 0,
        "manifest_hash": "abc",
    }
    row.update(run_values)
    return DatabaseSnapshot(runs=(row,), steps=(), manifest_entries=tuple(entries))


def oracle_entries(f01) -> list[dict]:
    _, oracle = f01
    return [
        {"run_id": "run-1", "path": c.path, "original_path": c.original_path, "category": c.category}
        for c in oracle.changes
    ]


def evidence(f01, **overrides: object) -> CaseEvidence:
    built, oracle = f01
    values: dict = {
        "final_poll": None,
        "run_id": "run-1",
        "error_responses": (),
        "exchanges": (),
        "database": None,
        "oracle": oracle,
        "repository": built.path,
        "markers": (),
        "snapshot_before": {"head": "a"},
        "snapshot_after": {"head": "a"},
        "state_directory": None,
        "log_paths": (),
    }
    values.update(overrides)
    return CaseEvidence(**values)


def reading_model(f01, citation: dict | None = None, node: dict | None = None) -> dict:
    built, oracle = f01
    lines = (built.path / "src/parse-name.ts").read_text().split("\n")[:-1]
    cited = {
        "claimId": "thesis",
        "nodeId": "n004",
        "side": "after",
        "path": "src/parse-name.ts",
        "objectId": oracle.head_blobs["src/parse-name.ts"].object_id,
        "startLine": 1,
        "endLine": 7,
        "focus": [],
        "provenance": "unchecked",
    }
    published = {
        "nodeId": "n004",
        "path": "src/parse-name.ts",
        "side": "after",
        "startLine": 1,
        "endLine": 7,
        "isChangedFile": True,
        "isRedacted": False,
        "isTruncated": False,
        "text": "\n".join(lines),
    }
    cited.update(citation or {})
    published.update(node or {})
    return {"state": "completed", "readingModel": {"citations": [cited], "evidence": [published]}}


def check(name: str, expected: object, case_evidence: CaseEvidence):
    return evaluate_expectation(name, expected, case_evidence)


def test_outcome(f01) -> None:
    poll = {"state": "failed", "terminal": {"kind": "failed", "failureCode": "analysis.unmappedReadingModel"}}

    assert check("outcome", "failed", evidence(f01, final_poll=poll)).passed
    assert check(
        "outcome", {"state": "failed", "failure_code": "analysis.unmappedReadingModel"}, evidence(f01, final_poll=poll)
    ).passed
    mismatch = check("outcome", "completed", evidence(f01, final_poll=poll))
    assert not mismatch.passed
    assert mismatch.actual == {"state": "failed", "failure_code": "analysis.unmappedReadingModel"}
    assert not check("outcome", "completed", evidence(f01)).passed


def test_removals_count(f01) -> None:
    assert check("removals.count", 4, evidence(f01, database=database(validation_removal_count=4))).passed
    assert not check("removals.count", 0, evidence(f01, database=database(validation_removal_count=4))).passed
    assert not check("removals.count", 0, evidence(f01)).passed


def test_provider_calls_and_error_code(f01) -> None:
    assert check("provider.calls", 2, evidence(f01, exchanges=(exchange(), exchange()))).passed
    assert not check("provider.calls", 1, evidence(f01)).passed

    errors = ({"type": "error", "errors": [{"code": "comparisons.targetInvalid"}]},)
    assert check("error_code", "comparisons.targetInvalid", evidence(f01, error_responses=errors)).passed
    assert not check("error_code", "comparisons.targetInvalid", evidence(f01)).passed


def test_captured_paths(f01) -> None:
    entries = oracle_entries(f01)

    assert check("captured_paths", "oracle", evidence(f01, database=database(tuple(entries)))).passed

    missing = check("captured_paths", "oracle", evidence(f01, database=database(tuple(entries[1:]))))
    assert not missing.passed
    assert missing.actual["missing"] == ["modified assets/blob.bin"]

    extra = (*entries, {"run_id": "run-1", "path": "src/decoy.ts", "original_path": None, "category": "modified"})
    unexpected = check("captured_paths", "oracle", evidence(f01, database=database(extra)))
    assert unexpected.actual["unexpected"] == ["modified src/decoy.ts"]

    other_run = tuple({**entry, "run_id": "run-0"} for entry in entries)
    assert not check("captured_paths", "oracle", evidence(f01, database=database(other_run))).passed


def test_excluded_counts(f01) -> None:
    assert check("excluded_counts", "oracle", evidence(f01, database=database())).passed

    dirty = database(excluded_staged_count=1, excluded_uncommitted_total=1)
    assert not check("excluded_counts", "oracle", evidence(f01, database=dirty)).passed
    explicit = {"staged": 1, "unstaged": 0, "untracked": 0, "conflicted": 0, "distinct": 1}
    assert check("excluded_counts", explicit, evidence(f01, database=dirty)).passed


def test_repo_unchanged(f01) -> None:
    assert check("repo_unchanged", True, evidence(f01)).passed

    changed = check("repo_unchanged", True, evidence(f01, snapshot_after={"head": "b"}))
    assert not changed.passed
    assert changed.actual == {"changed": ["head"]}


def test_no_marker_in(f01, tmp_path: Path) -> None:
    state = tmp_path / "state"
    state.mkdir()
    (state / "changelens.db").write_bytes(b"clean")
    log = tmp_path / "engine.log"
    log.write_text("clean")
    base = {"markers": ("secret-marker",), "state_directory": state, "log_paths": (log,)}
    locations = ["payload", "db", "logs"]

    clean = check("no_marker_in", locations, evidence(f01, exchanges=(exchange('{"a":"clean"}'),), **base))
    assert clean.passed

    leaked = check("no_marker_in", locations, evidence(f01, exchanges=(exchange('{"a":"secret-marker"}'),), **base))
    assert leaked.actual == {"payload": ["secret-marker"], "db": [], "logs": []}

    (state / "changelens.db-wal").write_bytes(b"..secret-marker..")
    assert check("no_marker_in", ["db"], evidence(f01, **base)).actual == {"db": ["secret-marker"]}

    assert not check("no_marker_in", ["logs"], evidence(f01)).passed


def test_citations_resolve(f01) -> None:
    assert check("citations", "resolve", evidence(f01, final_poll=reading_model(f01))).passed


def test_citation_problems_are_reported(f01) -> None:
    cases = {
        "does not match the fixture blob": reading_model(f01, citation={"objectId": "0" * 40}),
        "evidence text differs": reading_model(f01, node={"text": "tampered"}),
        "fall outside": reading_model(f01, citation={"endLine": 40}, node={"endLine": 40}),
        "not in the published evidence": reading_model(f01, citation={"nodeId": "n999"}),
    }
    for message, poll in cases.items():
        result = check("citations", "resolve", evidence(f01, final_poll=poll))
        assert not result.passed
        assert message in result.actual["problems"][0], (message, result.actual)


def test_citations_without_a_reading_model_fail(f01) -> None:
    result = check("citations", "resolve", evidence(f01, final_poll={"state": "failed", "readingModel": None}))

    assert not result.passed


def test_generic_paths(f01) -> None:
    poll = {"state": "completed", "readingModel": {"areas": [{"shape": "participantMap"}]}}

    assert check("response.readingModel.areas.0.shape", "participantMap", evidence(f01, final_poll=poll)).passed
    assert not check("response.readingModel.areas.1.shape", "participantMap", evidence(f01, final_poll=poll)).passed
    assert check("state.manifest_hash", "abc", evidence(f01, database=database())).passed
    assert not check("state.validation_removal_count", False, evidence(f01, database=database())).passed


def test_validate_expectation() -> None:
    assert validate_expectation("outcome", "completed") is None
    assert "outcome must be" in validate_expectation("outcome", "finished")
    assert validate_expectation("captured_paths", "all") is not None
    assert validate_expectation("no_marker_in", ["payload", "screen"]) is not None
    assert validate_expectation("provider.calls", -1) is not None
    assert validate_expectation("provider.calls", True) is not None
    assert validate_expectation("repo_unchanged", False) is not None
    assert "unknown check 'verdict'" in validate_expectation("verdict", 1)
    assert validate_expectation("response.readingModel.thesis.text", "x") is None
    assert validate_expectation("response.", "x") is not None
