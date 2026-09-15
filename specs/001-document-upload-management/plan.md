# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-09-15 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-document-upload-management/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Add secure, centralized document upload/storage/browsing/sharing to ContosoDashboard, integrated
with existing Projects and Tasks. Documents are stored on the local filesystem (outside `wwwroot`)
behind an `IFileStorageService` abstraction, with metadata in three new EF Core entities
(`Document`, `DocumentShare`, `DocumentActivityLog`). Access is enforced service-side on every
read/write (never UI-attribute-only), individual and team shares are evaluated live against
current project membership, and a locally-run ClamAV scan gates every upload — all fully offline,
per the constitution's training-only/offline-first mandate.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (ASP.NET Core 9.0), matching the rest of ContosoDashboard.
**Primary Dependencies**: Blazor Server (existing UI model), EF Core 9 (existing ORM), `nClam`
(new — ClamAV client, research.md #1), Bootstrap 5.3 (existing styling). No new cloud SDKs.
**Storage**: EF Core against the app's existing offline-capable relational database (SQLite
locally on this Linux dev environment / SQL Server LocalDB on Windows, per the prior .NET 9 /
SQLite migration) for metadata; local filesystem under `AppData/uploads/` for file bytes, accessed
only through `IFileStorageService` (research.md #4).
**Testing**: New `ContosoDashboard.Tests` xUnit project (research.md #2) — the app currently has no
automated tests; this feature is the first to add one, prioritizing service-layer authorization
tests given Constitution Principle III.
**Target Platform**: Same as the existing app — cross-platform ASP.NET Core server (Linux/Windows),
Blazor Server over SignalR, no mobile/native target (FR out-of-scope confirms web-only).
**Project Type**: Single project (existing ContosoDashboard Blazor Server monolith) — extended in
place, not split into separate frontend/backend projects.
**Performance Goals**: Upload a 25 MB file in <30s typical network (SC-006); document list/search
<2s for ≤500 accessible documents (SC-007); preview begins <3s (SC-008).
**Constraints**: Fully offline/no cloud dependency (FR-023, Constitution Principle I); no rewrite
of existing Models/Services/Data/Pages layering (Constitution Principle II); local disk storage
only; 25 MB max file size (FR-003); existing mock cookie authentication unchanged (Constitution
tech stack constraints).
**Scale/Scope**: Single ContosoDashboard instance, small seeded training user base; per-user
document scale target of ~500 documents (SC-007) rather than large multi-tenant volumes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Result |
|---|---|---|
| I. Training-Only, Offline-First Scope (NON-NEGOTIABLE) | Feature must run with no internet/cloud dependency. | **PASS** — local filesystem storage, locally-run ClamAV (research.md #1), no cloud SDKs anywhere in the design. |
| II. Layered Separation of Concerns | Business logic in Services, not Pages; existing Models/Data/Services/Pages layering preserved. | **PASS** — `Document`/`DocumentShare`/`DocumentActivityLog` in Models, all logic in new `IDocumentService`/`IFileStorageService`/`IVirusScanner`, Pages only call the service (contracts/document-service.md). |
| III. Service-Level Authorization & IDOR Prevention (NON-NEGOTIABLE) | Every service method independently checks caller authorization; not just `[Authorize]` on pages. | **PASS by design** — every `IDocumentService` method's authorization rule is enumerated in contracts/document-service.md; unauthorized reads return 404-equivalent, never confirm existence. Verified in Phase 1 by the dedicated xUnit test project (research.md #2). |
| IV. Infrastructure Abstraction for Cloud Migration | Storage/auth/DB behind interfaces, swappable via DI/config only. | **PASS** — `IFileStorageService` matches the stakeholder-specified cloud-migration shape exactly (contracts/file-storage-service.md); DB continues using the existing EF Core provider abstraction. |
| V. Documented Simplicity & Honesty About Limitations | Simplest implementation satisfying the spec; no speculative extensibility. | **PASS** — no version history, no soft-delete, no tag many-to-many table, no generic audit framework, no MVC controllers added beyond two small Minimal API endpoints — all justified in research.md against the spec's actual Out of Scope list. |

No violations — **Complexity Tracking is empty** (see below).

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── document-service.md
│   ├── file-storage-service.md
│   └── document-download-endpoint.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs                  # NEW — Document entity (data-model.md)
│   ├── DocumentShare.cs             # NEW
│   └── DocumentActivityLog.cs       # NEW (+ DocumentActivityType enum)
├── Data/
│   └── ApplicationDbContext.cs      # MODIFIED — add 3 new DbSets, relationships, indexes
├── Services/
│   ├── IDocumentService.cs          # NEW (contracts/document-service.md)
│   ├── DocumentService.cs           # NEW
│   ├── IFileStorageService.cs       # NEW (contracts/file-storage-service.md)
│   ├── LocalFileStorageService.cs   # NEW — training/local implementation
│   ├── IVirusScanner.cs             # NEW (research.md #1)
│   └── ClamAvVirusScanner.cs        # NEW
├── Pages/
│   ├── Documents.razor              # NEW — "My Documents" list/search/filter (US2, US1 entry point)
│   ├── DocumentUpload.razor         # NEW, or an upload modal/component reused from Documents.razor
│   ├── SharedWithMe.razor           # NEW (US4)
│   ├── ProjectDetails.razor         # MODIFIED — add project Documents tab (US2)
│   ├── Tasks.razor                  # MODIFIED — add attach/upload-from-task affordance (US5)
│   ├── Index.razor                  # MODIFIED — add "Recent Documents" dashboard widget (US5)
│   └── DocumentReports.razor        # NEW — Administrator activity report (US6)
├── Program.cs                       # MODIFIED — register new services + 2 Minimal API endpoints (contracts/document-download-endpoint.md)
└── AppData/uploads/                 # NEW — local file storage root, outside wwwroot

ContosoDashboard.Tests/              # NEW test project (research.md #2)
├── ContosoDashboard.Tests.csproj
└── Services/
    ├── DocumentServiceAuthorizationTests.cs   # Principle III regression coverage
    └── DocumentServiceTests.cs
```

**Structure Decision**: Extend the existing single Blazor Server project in place — no new
top-level project except the test project. This matches Constitution Principle II (preserve the
existing Models/Data/Services/Pages layering) and Principle V (no speculative restructuring).

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

*No violations — table intentionally omitted.*
