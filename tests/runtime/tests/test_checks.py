import json
from pathlib import Path

from changelens_review.plans.checks.generic import is_generic_name
from changelens_review.plans.checks.model import CaseEvidence
from changelens_review.plans.checks.registry import evaluate_expectation, validate_expectation
from changelens_review.plans.state_snapshot import DatabaseSnapshot
from changelens_review.provider.proxy import ExchangeRecord


def exchange(request_text: str = "{}", role: str = "curator") -> ExchangeRecord:
    return ExchangeRecord(
        sequence=1,
        role=role,
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
        estimated_prompt_tokens=None,
        estimated_completion_tokens=None,
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
        "step_responses": (),
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


def test_provider_calls_by_role(f01) -> None:
    exchanges = (exchange(role="curator"), exchange(role="checker"), exchange(role="curator"))

    assert check("provider.calls.curator", 2, evidence(f01, exchanges=exchanges)).passed
    assert not check("provider.calls.curator", 1, evidence(f01, exchanges=exchanges)).passed
    assert check("provider.calls.checker", 1, evidence(f01, exchanges=exchanges)).passed
    assert not check("provider.calls.checker", 0, evidence(f01, exchanges=exchanges)).passed
    assert check("provider.calls.curator", 0, evidence(f01)).passed
    assert check("provider.calls.checker", 0, evidence(f01)).passed


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


def test_no_marker_in_explanation(f01) -> None:
    model = {
        "thesis": {"text": "A clean thesis."},
        "areas": [],
        "evidence": [{"nodeId": "n1", "path": "src/x.ts", "text": "secret-marker only in the excerpt"}],
    }
    published = database(reading_model_json=json.dumps(model))

    assert check("no_marker_in", ["explanation"], evidence(f01, markers=("secret-marker",), database=published)).passed

    leaked = check(
        "no_marker_in",
        ["explanation"],
        evidence(
            f01,
            markers=("secret-marker",),
            database=database(
                reading_model_json=json.dumps({**model, "thesis": {"text": "secret-marker in the thesis"}})
            ),
        ),
    )
    assert not leaked.passed
    assert leaked.actual == {"explanation": ["secret-marker"]}

    absent_row = check("no_marker_in", ["explanation"], evidence(f01, markers=("secret-marker",)))
    assert not absent_row.passed
    assert absent_row.detail == "no reading model was published"

    absent_model = check(
        "no_marker_in", ["db", "explanation"], evidence(f01, markers=("secret-marker",), database=database())
    )
    assert not absent_model.passed
    assert absent_model.detail == "no reading model was published"
    assert absent_model.actual == {"db": [], "explanation": []}


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


def test_contains(f01) -> None:
    poll = {
        "readingModel": {
            "limitations": [
                {"kind": "fileNotRead", "path": "src/flask/app.py"},
                {"kind": "fileNotRead", "path": "src/flask/views.py"},
            ],
            "assurances": [{"kind": "checkerNotRun"}, {"kind": "checkerNotRun"}],
            "claims": ["thesis", "guard"],
        }
    }
    ev = evidence(f01, final_poll=poll)

    one = check(
        "contains",
        {"in": "readingModel.limitations", "item": {"kind": "fileNotRead", "path": "src/flask/app.py"}},
        ev,
    )
    assert one.passed
    assert one.actual == 1

    exact = check("contains", {"in": "readingModel.assurances", "item": {"kind": "checkerNotRun"}, "count": 2}, ev)
    assert exact.passed
    assert exact.actual == 2

    scalar = check("contains", {"in": "readingModel.claims", "item": "thesis"}, ev)
    assert scalar.passed
    assert scalar.actual == 1

    assert not check("contains", {"in": "readingModel.assurances", "item": {"kind": "checkerRun"}}, ev).passed
    assert not check("contains", {"in": "readingModel.limitations", "item": {"kind": "fileRead"}}, ev).passed
    assert not check(
        "contains", {"in": "readingModel.assurances", "item": {"kind": "checkerNotRun"}, "count": 1}, ev
    ).passed
    assert check("contains", {"in": "readingModel.limitations", "item": {"kind": "fileRead"}, "count": 0}, ev).passed


def test_lacks(f01) -> None:
    poll = {"readingModel": {"assurances": [{"kind": "checkerNotRun"}, {"kind": "checkerNotRun"}]}}
    ev = evidence(f01, final_poll=poll)

    assert check("lacks", {"in": "readingModel.assurances", "item": {"kind": "uncommittedWorkExcluded"}}, ev).passed

    present = check("lacks", {"in": "readingModel.assurances", "item": {"kind": "checkerNotRun"}}, ev)
    assert not present.passed
    assert present.actual == 2


def test_contains_and_lacks_over_a_star(f01) -> None:
    poll = {
        "readingModel": {
            "areas": [
                {"relationships": [{"trust": "checked"}, {"trust": "unchecked"}]},
                {"relationships": [{"trust": "checked"}]},
                {"shape": "participantMap"},
            ]
        }
    }
    ev = evidence(f01, final_poll=poll)

    assert check(
        "contains", {"in": "readingModel.areas.*.relationships", "item": {"trust": "checked"}, "count": 2}, ev
    ).passed
    assert check("lacks", {"in": "readingModel.areas.*.relationships", "item": {"trust": "skipped"}}, ev).passed


def test_contains_missing_and_invalid_paths(f01) -> None:
    ev = evidence(f01, final_poll={"readingModel": {"assurances": []}})

    missing = check("contains", {"in": "readingModel.omissionSummaries", "item": {"kind": "x"}}, ev)
    assert not missing.passed
    assert missing.detail == "readingModel.omissionSummaries does not exist"

    scalar = check("lacks", {"in": "readingModel.state", "item": "completed"}, ev)
    assert not scalar.passed
    assert scalar.detail == "readingModel.state does not exist"

    flat = evidence(f01, final_poll={"readingModel": {"state": "completed"}})
    not_list = check("contains", {"in": "readingModel.state", "item": "completed"}, flat)
    assert not not_list.passed
    assert not_list.detail == "readingModel.state is not a list"

    assert check("lacks", {"in": "readingModel.assurances", "item": {"kind": "x"}}, ev).passed

    no_poll = check("contains", {"in": "readingModel.assurances", "item": {"kind": "x"}}, evidence(f01))
    assert not no_poll.passed
    assert no_poll.detail == "no analysis.pollRun result was observed"


def test_generic_paths(f01) -> None:
    poll = {"state": "completed", "readingModel": {"areas": [{"shape": "participantMap"}]}}

    assert check("response.readingModel.areas.0.shape", "participantMap", evidence(f01, final_poll=poll)).passed
    assert not check("response.readingModel.areas.1.shape", "participantMap", evidence(f01, final_poll=poll)).passed
    assert check("state.manifest_hash", "abc", evidence(f01, database=database())).passed
    assert not check("state.validation_removal_count", False, evidence(f01, database=database())).passed


def test_generic_step_paths(f01) -> None:
    steps = (
        {"response": {"state": "rejectedStale", "freshnessToken": "t"}},
        None,
        {"errors": [{"code": "analysis.runActive"}]},
    )

    assert check("steps[0].response.state", "rejectedStale", evidence(f01, step_responses=steps)).passed
    assert check("steps[0].response.freshnessToken", "t", evidence(f01, step_responses=steps)).passed
    assert check("steps[2].errors.0.code", "analysis.runActive", evidence(f01, step_responses=steps)).passed
    assert not check("steps[0].response.state", "completed", evidence(f01, step_responses=steps)).passed
    assert not check("steps[1].response.state", "rejectedStale", evidence(f01, step_responses=steps)).passed
    assert not check("steps[3].response.state", "rejectedStale", evidence(f01, step_responses=steps)).passed

    want_response = check("steps[2].response.state", "rejectedStale", evidence(f01, step_responses=steps))
    assert not want_response.passed
    assert want_response.detail == "steps[2].response.state does not exist"

    want_errors = check("steps[0].errors.0.code", "analysis.runActive", evidence(f01, step_responses=steps))
    assert not want_errors.passed
    assert want_errors.detail == "steps[0].errors.0.code does not exist"


def test_is_generic_name() -> None:
    assert is_generic_name("steps[0].response.state")
    assert is_generic_name("steps[1].errors.0.code")
    assert is_generic_name("response.readingModel.areas.0.shape")
    assert is_generic_name("state.manifest_hash")
    assert not is_generic_name("steps[].response.x")
    assert not is_generic_name("steps[a].response.x")
    assert not is_generic_name("steps[0].result.x")
    assert not is_generic_name("steps[0].response")
    assert not is_generic_name("steps[0].response.")


def test_validate_expectation() -> None:
    assert validate_expectation("outcome", "completed") is None
    assert "outcome must be" in validate_expectation("outcome", "finished")
    assert validate_expectation("captured_paths", "all") is not None
    assert validate_expectation("no_marker_in", ["payload", "screen"]) is not None
    assert validate_expectation("provider.calls", -1) is not None
    assert validate_expectation("provider.calls", True) is not None
    assert validate_expectation("provider.calls.curator", 1) is None
    assert validate_expectation("provider.calls.checker", -1) is not None
    assert validate_expectation("repo_unchanged", False) is not None
    assert "unknown check 'verdict'" in validate_expectation("verdict", 1)
    assert validate_expectation("response.readingModel.thesis.text", "x") is None
    assert validate_expectation("steps[3].response.state", "rejectedStale") is None
    assert validate_expectation("steps[2].errors.0.code", "analysis.runActive") is None
    assert validate_expectation("steps[0].response", "x") is not None
    assert validate_expectation("response.", "x") is not None
    assert validate_expectation("contains", {"in": "readingModel.limitations", "item": {"kind": "x"}}) is None
    assert validate_expectation("contains", {"in": "readingModel.limitations", "item": None, "count": 0}) is None
    assert validate_expectation("lacks", {"in": "readingModel.assurances", "item": {"kind": "x"}}) is None
    assert "contains must be" in validate_expectation("contains", {"in": "readingModel.assurances"})
    assert "contains must be" in validate_expectation("contains", "readingModel.assurances")
    assert "contains must be" in validate_expectation("contains", {"in": "", "item": 1})
    assert "contains must be" in validate_expectation("contains", {"in": "readingModel..assurances", "item": 1})
    assert "contains must be" in validate_expectation(
        "contains", {"in": "readingModel.assurances", "item": 1, "count": -1}
    )
    assert "contains must be" in validate_expectation(
        "contains", {"in": "readingModel.assurances", "item": 1, "count": True}
    )
    assert "contains must be" in validate_expectation(
        "contains", {"in": "readingModel.assurances", "item": 1, "extra": 2}
    )
    assert "lacks must be" in validate_expectation("lacks", {"in": "readingModel.assurances", "item": 1, "count": 1})
