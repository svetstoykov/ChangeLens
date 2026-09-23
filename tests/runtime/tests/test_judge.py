from test_checks import evidence

from changelens_review.plans.checks.judge import evaluate_judge, validate_judge

LABEL, PARSER, STABLE, DECOY = "src/label.ts", "src/parse-name.ts", "src/stable.ts", "src/decoy.ts"


def poll(*areas: dict, thesis_nodes: tuple[str, ...] = ()) -> dict:
    """A final poll whose reading model has one evidence node per fixture path, n1..n4."""
    nodes = [
        {"nodeId": f"n{index}", "path": path, "side": "after", "startLine": 1, "endLine": 2}
        for index, path in enumerate((LABEL, PARSER, STABLE, DECOY), start=1)
    ]
    return {
        "state": "completed",
        "readingModel": {
            "thesis": {"claimId": "thesis", "text": "t", "evidenceNodeIds": list(thesis_nodes)},
            "areas": list(areas),
            "evidence": nodes,
        },
    }


def area(summary: tuple[str, ...] = (), steps: tuple[str, ...] = (), participants: tuple[str, ...] = ()) -> dict:
    return {
        "id": "a",
        "summary": {"claimId": "s", "text": "s", "evidenceNodeIds": list(summary)},
        "orderedSteps": [{"claimId": f"step:{n}", "text": "x", "evidenceNodeIds": [n]} for n in steps],
        "participants": [{"id": n, "name": n, "evidenceNodeIds": [n]} for n in participants],
    }


def judge(f01, name: str, expected, final_poll: dict | None):
    return evaluate_judge(name, expected, evidence(f01, final_poll=final_poll))


def test_should_link_passes_when_one_area_cites_both_paths(f01) -> None:
    result = judge(f01, "should_link", {"from": LABEL, "to": PARSER}, poll(area(summary=("n1",), participants=("n2",))))

    assert result.passed
    assert result.actual == {"linked": [f"{LABEL} -> {PARSER}"], "unlinked": []}


def test_should_link_fails_when_the_paths_are_cited_in_different_areas(f01) -> None:
    result = judge(f01, "should_link", [{"from": LABEL, "to": PARSER}], poll(area(steps=("n1",)), area(steps=("n2",))))

    assert not result.passed
    assert result.actual == {"linked": [], "unlinked": [f"{LABEL} -> {PARSER}"]}


def test_should_flag_related_counts_thesis_and_area_references(f01) -> None:
    passing = judge(f01, "should_flag_related", [STABLE, LABEL], poll(area(steps=("n1",)), thesis_nodes=("n3",)))
    failing = judge(f01, "should_flag_related", [STABLE, LABEL], poll(area(steps=("n1",))))

    assert passing.passed
    assert not failing.passed
    assert failing.actual == {"flagged": [LABEL], "missing": [STABLE]}


def test_should_not_flag_fails_when_a_listed_path_is_cited(f01) -> None:
    assert judge(f01, "should_not_flag", [DECOY], poll(area(summary=("n1", "n3")))).passed

    result = judge(f01, "should_not_flag", [DECOY], poll(area(participants=("n4",))))

    assert not result.passed
    assert result.actual == {"flagged": [DECOY]}


def test_soft_checks_fail_without_a_reading_model(f01) -> None:
    result = judge(f01, "should_flag_related", [STABLE], {"state": "failed", "readingModel": None})

    assert not result.passed
    assert result.detail == "the final poll result carries no reading model"


def test_judge_validation_rejects_unknown_names_and_malformed_values() -> None:
    assert validate_judge("should_link", {"from": LABEL, "to": PARSER}) is None
    assert validate_judge("should_not_flag", [DECOY]) is None
    assert "unknown judge check" in validate_judge("should_explain", [LABEL])
    assert validate_judge("should_link", {"from": LABEL}) is not None
    assert validate_judge("should_link", []) is not None
    assert validate_judge("should_flag_related", []) is not None
    assert validate_judge("should_not_flag", [""]) is not None
