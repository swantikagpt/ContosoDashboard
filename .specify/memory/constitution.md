<!--
Sync Impact Report
- Version change: (unset/template) → 1.0.0
- Modified principles: none (initial ratification; all placeholders replaced)
- Added sections:
  - Core Principles: I. Training-Only, Offline-First Scope; II. Layered Separation of Concerns;
    III. Service-Level Authorization & IDOR Prevention; IV. Infrastructure Abstraction for Cloud
    Migration; V. Documented Simplicity & Honesty About Limitations
  - Technology Stack Constraints
  - Spec-Driven Development Workflow
  - Governance
- Removed sections: none (template placeholders only)
- Templates requiring follow-up: none — plan/spec/tasks templates consume this file at runtime
  and were not modified by this command.
- Deferred TODOs: none. RATIFICATION_DATE inferred from the repository's first commit
  (2025-11-26); confirm with the project owner if a different date should be of record.
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training-Only, Offline-First Scope (NON-NEGOTIABLE)
ContosoDashboard exists to teach Spec-Driven Development with the GitHub Spec Kit, not to serve
real users. Every feature MUST run fully offline with no external service or cloud dependency
required to build, run, or demonstrate it. Features MUST NOT be described, documented, or coded
as production-ready; any security, auth, or data-handling shortcut taken for training purposes
MUST be labeled as such (in code comments and README) rather than presented as a real-world
pattern. This keeps the training environment available without network access or paid services
and prevents learners from mistaking teaching shortcuts for production guidance.

### II. Layered Separation of Concerns
Code MUST be organized into the existing `Models`, `Data`, `Services`, and `Pages`/`Shared`
layers, and business logic MUST live in `Services`, not in Razor components or code-behind files.
Pages and components MAY call services and render results but MUST NOT contain data-access code,
authorization decisions, or cross-entity business rules directly. This separation is what lets the
codebase double as a clear teaching example of clean architecture, and it is what a generated
plan/tasks artifact MUST preserve when adding new features.

### III. Service-Level Authorization & IDOR Prevention (NON-NEGOTIABLE)
Every service method that reads or writes user-, task-, project-, or notification-scoped data MUST
independently verify the caller's role and ownership/membership before returning or mutating data,
even when a page-level `[Authorize]` attribute or policy already restricts access to that page.
UI-level authorization is defense-in-depth, not a substitute for service-level checks. This
prevents Insecure Direct Object Reference (IDOR) vulnerabilities where a component reused on a
different page — or a future API surface — would otherwise leak another user's data.

### IV. Infrastructure Abstraction for Cloud Migration
Infrastructure concerns that plausibly move to Azure in a real deployment (file storage,
authentication/identity, and the database provider) MUST be accessed through an interface
(e.g. `IFileStorageService`) or a provider-agnostic EF Core/ASP.NET abstraction, never through a
concrete SDK or connection type referenced directly from `Pages` or `Services` business logic. Do
not hardcode assumptions about a single database engine (e.g. SQL Server-only syntax) into model
configuration when a provider-neutral alternative exists. Swapping an implementation (e.g. local
SQLite for offline/non-Windows dev to a hosted SQL database, or mock cookie auth to Microsoft
Entra ID) MUST be possible via configuration and DI registration changes alone, without touching
calling code. This is the concrete lesson the project teaches about designing for portability.

### V. Documented Simplicity & Honesty About Limitations
Prefer the simplest implementation that satisfies the current spec; do not add abstractions,
configuration options, or extensibility points beyond what an approved spec/plan calls for. Known
limitations (mock auth, no MFA, no password hashing, offline-only storage, etc.) MUST be recorded
in the README or spec rather than fixed silently through unreviewed scope creep. When a
requirement is genuinely ambiguous, the spec MUST be clarified (`/speckit.clarify`) rather than
resolved with a silent implementation guess.

## Technology Stack Constraints

- **Framework**: ASP.NET Core (current target: .NET 9, tracking the latest .NET LTS/STS SDK
  actually installed in the training environment) with Blazor Server for interactive UI.
- **Data access**: Entity Framework Core against a local, offline-capable relational database
  (SQL Server LocalDB on Windows, SQLite where LocalDB is unavailable, e.g. Linux/macOS dev
  environments). Model configuration MUST avoid provider-specific SQL that would break this
  portability.
- **Authentication**: Cookie-based mock authentication with claims-based identity and
  role-based policies (Employee, TeamLead, ProjectManager, Administrator). Real identity
  providers (Microsoft Entra ID, Identity Server, Auth0, etc.) are documented migration targets,
  not implemented dependencies.
- **UI**: Bootstrap for styling; no additional front-end framework may be introduced without a
  spec/plan documenting why Blazor Server + Bootstrap is insufficient.
- **No external network calls**: the application MUST NOT require internet access, API keys, or
  third-party SaaS accounts to run in the training environment.

## Spec-Driven Development Workflow

This repository is itself the GitHub Spec Kit training vehicle, so its own process MUST follow
the Spec Kit lifecycle: `/speckit.constitution` (this file) → `/speckit.specify` →
`/speckit.clarify` → `/speckit.plan` → `/speckit.tasks` → `/speckit.implement`, with
`/speckit.analyze` and `/speckit.checklist` used as quality gates before implementation begins.
Feature work MUST originate from a spec under `specs/`, not from an ad hoc prompt directly against
application code, so that the constitution, spec, plan, and tasks stay traceable to what was
actually built.

## Governance

This constitution supersedes ad hoc practice for anything it explicitly governs. Amendments are
made by editing this file, prepending an updated Sync Impact Report, and bumping the version
according to semantic versioning:

- **MAJOR**: A principle is removed or redefined in a backward-incompatible way (e.g. relaxing
  the offline-only or service-level-authorization requirements).
- **MINOR**: A new principle or governance section is added, or existing guidance is materially
  expanded.
- **PATCH**: Wording, clarification, or typo fixes with no change in obligations.

Every `/speckit.plan` and `/speckit.tasks` run, and any manual PR review, MUST verify the proposed
work against these principles; unresolved conflicts MUST be raised back to `/speckit.clarify` or
resolved by amending this constitution first. Complexity that is not justified by an approved
spec MUST be simplified before merge rather than accepted as-is.

**Version**: 1.0.0 | **Ratified**: 2025-11-26 | **Last Amended**: 2026-09-15
