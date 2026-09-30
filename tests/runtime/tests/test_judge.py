import json

from test_checks import evidence, exchange

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


DEFECT = {"path": PARSER, "side": "after", "lines": [3, 4], "severity": "warning"}


def citation(finding_id: str, side: str, focus: tuple[int, int] | None, path: str = PARSER) -> dict:
    """A finding citation; the anchor citation carries a focus range and supporting citations carry none."""
    return {
        "claimId": f"finding:{finding_id}",
        "nodeId": f"{side}-{path}",
        "side": side,
        "path": path,
        "startLine": 1,
        "endLine": 10,
        "focus": [] if focus is None else [{"nodeId": f"{side}-{path}", "startLine": focus[0], "endLine": focus[1]}],
    }


def reviewed(findings: list[tuple[str, str]], citations: list[dict], status: str = "ran") -> dict:
    """A final poll whose reading model carries the given (id, severity) findings, their citations, and review."""
    final_poll = poll()
    final_poll["readingModel"].update(
        {
            "findings": [
                {"id": finding_id, "severity": severity, "title": finding_id} for finding_id, severity in findings
            ],
            "citations": citations,
            "review": {"status": status},
        }
    )
    return final_poll


def test_should_find_matches_an_anchor_overlapping_the_defect_on_its_side(f01) -> None:
    result = judge(
        f01,
        "should_find",
        DEFECT,
        reviewed(
            [("f1", "info"), ("f2", "critical")], [citation("f1", "after", (3, 3)), citation("f2", "after", (4, 6))]
        ),
    )

    assert result.passed
    assert result.actual == {"found": [{"defect": f"{PARSER} after 3-4", "findings": ["f2"]}], "missed": []}


NOT_ANCHORED = {"defect": f"{PARSER} after 3-4", "reason": "no finding anchored on these lines"}


def test_should_find_ignores_the_other_side_at_the_same_path_and_lines(f01) -> None:
    result = judge(f01, "should_find", DEFECT, reviewed([("f1", "warning")], [citation("f1", "before", (3, 4))]))

    assert not result.passed
    assert result.actual == {"found": [], "missed": [NOT_ANCHORED]}


def test_should_find_ignores_supporting_citations_and_anchors_outside_the_lines(f01) -> None:
    citations = [
        citation("f1", "after", None),
        citation("f1", "after", (7, 8), path=LABEL),
        citation("f2", "after", (5, 9)),
    ]

    result = judge(f01, "should_find", [DEFECT], reviewed([("f1", "warning"), ("f2", "warning")], citations))

    assert not result.passed
    assert result.actual == {"found": [], "missed": [NOT_ANCHORED]}


OTHER_DEFECT = {"path": LABEL, "side": "after", "lines": [7, 8], "severity": "warning"}


def test_should_find_list_needs_every_defect_and_any_of_needs_one(f01) -> None:
    final_poll = reviewed([("f1", "warning")], [citation("f1", "after", (7, 8), path=LABEL)])

    every = judge(f01, "should_find", [DEFECT, OTHER_DEFECT], final_poll)
    one = judge(f01, "should_find", {"any": [DEFECT, OTHER_DEFECT]}, final_poll)
    none = judge(f01, "should_find", {"any": [DEFECT]}, final_poll)

    assert not every.passed
    assert one.passed
    assert one.actual == {"found": [{"defect": f"{LABEL} after 7-8", "findings": ["f1"]}], "missed": [NOT_ANCHORED]}
    assert not none.passed


def reviewer_request(*nodes: tuple[str, str, int, int]) -> str:
    """A recorded reviewer request whose binder carries (path, side, start, end) evidence nodes."""
    evidence_nodes = [
        {"nodeId": f"n{index}", "path": path, "side": side, "startLine": start, "endLine": end}
        for index, (path, side, start, end) in enumerate(nodes)
    ]
    payload = {"comparison": {}, "evidence": evidence_nodes}
    return json.dumps({"messages": [{"role": "user", "content": json.dumps(payload)}]})


def test_should_find_names_why_a_defect_was_missed(f01) -> None:
    hidden = exchange(reviewer_request((PARSER, "After", 5, 9), (PARSER, "Before", 3, 4)), role="reviewer")
    shown = exchange(reviewer_request((PARSER, "After", 1, 3)), role="reviewer")
    mild = reviewed([("f1", "info")], [citation("f1", "after", (4, 4))])

    not_disclosed = evaluate_judge(
        "should_find", DEFECT, evidence(f01, final_poll=reviewed([], []), exchanges=(hidden,))
    )
    not_anchored = evaluate_judge("should_find", DEFECT, evidence(f01, final_poll=reviewed([], []), exchanges=(shown,)))
    below = evaluate_judge("should_find", DEFECT, evidence(f01, final_poll=mild, exchanges=(hidden,)))

    assert not_disclosed.actual["missed"] == [
        {"defect": f"{PARSER} after 3-4", "reason": "not in the reviewer's evidence"}
    ]
    assert not_anchored.actual["missed"] == [NOT_ANCHORED]
    assert below.actual["missed"] == [
        {"defect": f"{PARSER} after 3-4", "reason": "anchored below warning", "findings": ["f1"]}
    ]


def test_findings_soft_checks_fail_when_the_review_did_not_run(f01) -> None:
    for name, expected in (("should_find", DEFECT), ("should_not_warn", True)):
        result = judge(f01, name, expected, reviewed([], [], status="failed"))

        assert not result.passed
        assert result.detail == "the review did not run; its status is 'failed'"


def test_should_not_warn_lists_published_warnings_and_criticals(f01) -> None:
    assert judge(f01, "should_not_warn", True, reviewed([("f1", "info")], [])).passed

    result = judge(f01, "should_not_warn", True, reviewed([("f1", "info"), ("f2", "warning"), ("f3", "critical")], []))

    assert not result.passed
    assert result.actual == {
        "warnings": [
            {"id": "f2", "severity": "warning", "title": "f2"},
            {"id": "f3", "severity": "critical", "title": "f3"},
        ]
    }


def test_findings_soft_check_validation_rejects_malformed_defects() -> None:
    assert validate_judge("should_find", DEFECT) is None
    assert validate_judge("should_find", [DEFECT, {**DEFECT, "side": "before"}]) is None
    assert validate_judge("should_not_warn", True) is None
    assert validate_judge("should_find", {key: DEFECT[key] for key in ("path", "lines", "severity")}) is not None
    assert validate_judge("should_find", {**DEFECT, "side": "After"}) is not None
    assert validate_judge("should_find", {**DEFECT, "lines": [4, 3]}) is not None
    assert validate_judge("should_find", {**DEFECT, "lines": [0, 3]}) is not None
    assert validate_judge("should_find", {**DEFECT, "severity": "question"}) is not None
    assert validate_judge("should_find", []) is not None
    assert validate_judge("should_find", {"any": [DEFECT, {**DEFECT, "path": LABEL}]}) is None
    assert validate_judge("should_find", {"any": []}) is not None
    assert validate_judge("should_find", {"any": [DEFECT], "all": [DEFECT]}) is not None
    assert validate_judge("should_not_warn", False) is not None


def test_judge_validation_rejects_unknown_names_and_malformed_values() -> None:
    assert validate_judge("should_link", {"from": LABEL, "to": PARSER}) is None
    assert validate_judge("should_not_flag", [DECOY]) is None
    assert "unknown judge check" in validate_judge("should_explain", [LABEL])
    assert validate_judge("should_link", {"from": LABEL}) is not None
    assert validate_judge("should_link", []) is not None
    assert validate_judge("should_flag_related", []) is not None
    assert validate_judge("should_not_flag", [""]) is not None
