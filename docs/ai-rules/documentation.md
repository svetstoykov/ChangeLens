# Documentation Rules

These rules apply to architecture, decision, evaluation, product, and technical-debt documentation under `docs/`. They do not replace the repository instructions in `AGENTS.md`; they define how the documentation is named, organized, and maintained.

## File names

- Use lowercase kebab-case for every documentation filename.
- Use stable identifiers at the start of filenames when a document belongs to a phase or sub-phase.
- Write phase identifiers with two digits: `phase-02`.
- Write gate or sub-phase identifiers with the phase and gate number: `phase-02-gate-2.2`.
- Name phase-level architecture documents `phase-02-<topic>.md`.
- Name gate-level or sub-phase architecture documents `phase-02-gate-2.2-<topic>.md`.
- Number decision records sequentially within their phase directory: `0001-<topic>.md`, `0002-<topic>.md`.
- Number decision proposals as `proposal-0001-<topic>.md`. When a proposal becomes the accepted decision record, rename it to its final sequential decision number if it is retained as the decision record.
- Prefix phase-specific evaluation and technical-debt documents with the phase identifier when applicable, for example `phase-02-generation-fidelity.md`.
- Do not put status, dates, author names, or temporary words such as `draft` in filenames. Record those in the document header.
- Do not create a second numbering scheme for a new phase or gate. Sequence numbers restart only within a new phase directory.

When an existing document does not follow these rules, leave it in place unless the task explicitly includes a documentation cleanup. New documents and documents being substantially revised must follow the convention.

## Document locations

Put each document in the directory that owns its purpose:

| Directory | Use for |
| --- | --- |
| `docs/architecture/` | Phase architecture, scope, contracts, responsibilities, review gates, acceptance boundaries, and handoff contracts. |
| `docs/decisions/<phase>/` | Durable decisions and decision proposals that constrain a phase or sub-phase. Use one directory per phase, such as `phase-02/`. |
| `docs/evaluation/` | Spikes, experiments, prototypes, comparison results, and evidence gathered to inform a decision. |
| `docs/product/` | Product requirements, user outcomes, workflows, and feature specifications that are not primarily architectural decisions. |
| `docs/tech-debt/` | Deliberately deferred work, known gaps, and preserved designs for work that may return later. |
| `docs/ai-rules/` | Durable instructions for agents and contributors. Keep these concise and broadly applicable. |
| `docs/superpowers/` | Local workflow artifacts created by an approved Superpowers skill. This material remains untracked. |

If a document could fit more than one directory, choose the directory matching its primary purpose and link to it from the other relevant document. Do not duplicate the same source of truth across directories.

Do not put implementation code, generated output, operating-system metadata, or general meeting notes in `docs/`.

## Required document headers

Every phase, decision, evaluation, product, and technical-debt document must identify:

- `Status` — for example `Proposed`, `Accepted`, `In progress`, `Deferred`, or `Superseded`.
- `Last updated` — ISO date format: `YYYY-MM-DD`.
- `Phase` or `Applies to` when the document is phase-specific.
- `Related documents` or equivalent links when the document changes or informs another contract.

Decision records must also state the decision, the context, and the consequences. Proposals must state that they are proposals and must not be treated as accepted constraints until explicitly accepted.

## Phase and sub-phase references

- Use the same phase and gate identifier in filenames, headings, and cross-references.
- Refer to a gate by its complete identifier and name, such as “Phase 2 gate 2.2 — Frozen change snapshot.”
- Do not use an unexplained number such as “2.2” or “D8” as the only reference to a requirement or decision.
- When a phase contract is amended, update the contract and link the amendment or decision that caused the change. Do not leave conflicting active wording in separate documents.
- Keep the phase contract as the source of truth for current scope and acceptance behavior; keep decision records as the source of truth for why the design was chosen.

## Maintenance

- Prefer updating an existing source-of-truth document over creating a near-duplicate.
- Mark obsolete documents `Superseded` and link to the replacement; do not silently leave competing active documents.
- Use relative Markdown links for repository documents.
- Keep detailed, fast-changing requirements in `docs/product/` or a feature-specific specification rather than in `docs/ai-rules/`.
