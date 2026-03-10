<!--
SYNC IMPACT REPORT
==================
Version change: [CONSTITUTION_VERSION] → 1.0.0

Modified principles: All — first real constitution; all blank template placeholders replaced.

Added sections:
  - Core Principles (I–VI)
  - Technology Stack
  - Development Workflow
  - Governance

Removed sections: None (template placeholders replaced, no pre-existing content lost)

Templates requiring updates:
  ✅ .specify/templates/plan-template.md  — "Constitution Check" gate is generic; aligns correctly with all six principles; no changes needed.
  ✅ .specify/templates/spec-template.md  — Functional requirements + test tiers align with Principle III; no changes needed.
  ✅ .specify/templates/tasks-template.md — Three-tier test task structure aligns with Principle III; no changes needed.

Follow-up TODOs:
  - None. All placeholders resolved.
-->

# PoFace Constitution

## Core Principles

### I. Zero-Waste Codebase

The codebase MUST remain free of dead weight at all times.

- Unused files, dead code, obsolete assets, and commented-out blocks MUST be deleted — not commented out or left "for later."
- Every file and symbol MUST have a clear, current purpose. If it has none, it MUST be removed.
- Cleanup tasks MUST be included alongside implementation work in every feature's task list.

**Rationale**: Accumulated waste compounds over time; a clean codebase is faster to navigate, safer to change, and cheaper to maintain.

### II. SOLID & GoF Design Patterns

Every non-trivial piece of logic MUST be designed according to SOLID principles and relevant Gang of Four (GoF) patterns.

- **Single Responsibility**: Each class/method MUST have one reason to change.
- **Open/Closed**: Extend via abstraction; stable interfaces MUST NOT be modified without a breaking-change decision.
- **Liskov / Interface Segregation / Dependency Inversion**: Applied wherever polymorphism or injection is used.
- GoF patterns (Factory, Strategy, Repository, Mediator, Decorator, etc.) MUST be applied where they reduce coupling or clarify intent.
- Non-obvious design decisions MUST include an explanatory comment citing the pattern and the reasoning behind it.

**Rationale**: Consistent patterns make the codebase predictable, extensible, and easier to review and onboard.

### III. Test Coverage (NON-NEGOTIABLE)

Every major feature MUST ship with three tiers of automated tests before merge.

- **Unit tests** (C#, xUnit): Test individual classes/methods in isolation; all public API surface MUST be covered.
- **Integration tests** (C#, xUnit + `WebApplicationFactory`): Test full vertical slices end-to-end within the API boundary, including database and service interactions.
- **E2E tests** (TypeScript, Playwright): Test user-facing flows through the Blazor WASM UI against a running environment.
- Tests MUST be written before or alongside implementation; no feature is considered "done" until all three tiers pass.
- Test projects MUST mirror the source structure to keep navigation predictable.

**Rationale**: Untested code is a liability; three-tier coverage ensures correctness at every layer of the stack.

### IV. Vertical Slice Architecture (VSA)

Code MUST be organized by feature, not by technical layer.

- Each feature folder (e.g., `Features/Orders/`) MUST contain its own DTOs, command/query handlers, validators, and endpoint registration.
- Cross-cutting concerns (auth, logging, error handling) belong in shared infrastructure, not inside feature folders.
- Root-level horizontal layer folders (`Controllers/`, `Services/`, `Repositories/`) are forbidden unless they hold only cross-cutting infrastructure.
- MediatR (or equivalent mediator) MUST be used to decouple feature handlers from the HTTP layer.

**Rationale**: VSA keeps related logic co-located, minimizes merge conflicts, and allows features to be built, reviewed, and deleted independently.

### V. Observability & Debug-First

Errors MUST be observable at every layer, and that information MUST reach both the developer and the UI.

- Every exception and unexpected state MUST be logged with structured, contextual detail (feature, operation, relevant IDs, inner exception message).
- When fixing a bug, detailed diagnostic logs MUST be added around the problem location **before** the fix is applied, and retained afterward.
- Error details MUST be surfaced to the Blazor UI in development/staging environments via a structured `ProblemDetails` response; errors MUST NOT be swallowed silently.
- Production error responses MUST be safe (no stack traces exposed), but a correlation ID MUST be returned so logs can be correlated.
- Serilog (or equivalent structured logger) MUST be used; `Console.WriteLine` / `Debug.WriteLine` for diagnostic purposes is forbidden in committed code.

**Rationale**: Invisible errors slow debugging to a crawl; flowing error detail from server to UI eliminates context-switching and guesswork.

### VI. Clarification-First

No implementation work MUST begin on an ambiguous task.

- If a requirement, acceptance criterion, or design decision is unclear, the developer MUST ask targeted clarifying questions until full clarity is reached.
- Assumptions MUST NOT be coded silently; if a reasonable assumption is made it MUST be documented in a code comment or task note and flagged for confirmation.
- Speckit agents MUST invoke the `/speckit.clarify` workflow before producing a plan for any feature where key decisions remain unresolved.

**Rationale**: Fixing a misunderstood requirement after implementation costs far more than a short clarification conversation upfront.

## Technology Stack

The following technology choices are binding for all features. Deviations require a formal constitution amendment before implementation begins.

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly (.NET 10) |
| Backend API | ASP.NET Core (.NET 10) — Minimal API or Controller-based |
| Hosting | Azure App Service — single App Service hosting both WASM static files and API |
| UI Controls (advanced) | Radzen Blazor Components — MUST be used for any complex/advanced UI elements |
| UI Controls (basic) | Native Blazor components acceptable for simple, low-complexity elements |
| Testing — Unit / Integration | C# + xUnit + `WebApplicationFactory` |
| Testing — E2E | TypeScript + Playwright |
| Logging | Serilog with structured sinks (Console + Azure Application Insights) |
| Dependency Injection | Built-in .NET DI container |
| CQRS / Mediator | MediatR (or functional equivalent) for feature handler dispatch |
| ORM / Data Access | Entity Framework Core; Dapper permitted for performance-critical queries |

## Development Workflow

The following rules govern day-to-day engineering practice.

- **Spec-first**: A `spec.md` MUST exist and be reviewed before a `plan.md` is started; a `plan.md` MUST exist before tasks are generated.
- **Feature branches**: All work MUST be done on a branch named `###-feature-name`, where the prefix matches the spec folder number.
- **Constitution Check gate**: Every `plan.md` MUST include a completed Constitution Check section validating compliance with all six principles before Phase 0 research begins.
- **Vertical slice delivery**: Features MUST be delivered as complete vertical slices (front-end + back-end + all three test tiers) aligned to user story priority — never as naked layers.
- **Zero broken builds**: The `main` branch MUST NOT contain code that fails to build or has failing tests at any time.
- **Cleanup as you go**: Each PR MUST include removal of any code it makes obsolete; cleanup MUST NOT be deferred unless scope is large and explicitly tracked as a follow-up task.
- **PR reviews**: All PRs MUST be reviewed against this constitution; principle violations are grounds for rejection without merge.

## Governance

This constitution supersedes all other coding standards, style guides, or informal conventions in this repository. Where conflicts exist, the constitution wins.

- **Amendments**: Any change requires a version bump, a clear rationale, and an update to this file via PR. Principle removals or redefinitions increment the MAJOR version.
- **Versioning policy**: `MAJOR.MINOR.PATCH` — MAJOR for breaking governance changes, MINOR for new principles or sections added, PATCH for clarifications and wording fixes.
- **Compliance review**: Constitution Check MUST be completed in every `plan.md`. PR reviewers MUST verify compliance before approving.
- **Conflict resolution**: If a task conflicts with a principle, the principle wins unless a formal exception is documented in the task and a constitution amendment is opened.
- **Runtime guidance**: Refer to `.specify/templates/` and `.github/agents/` files for workflow-specific guidance during speckit command execution.

**Version**: 1.0.0 | **Ratified**: 2026-03-09 | **Last Amended**: 2026-03-09
