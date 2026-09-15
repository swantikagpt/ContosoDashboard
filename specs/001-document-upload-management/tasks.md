---
description: "Task list for Document Upload and Management"
---

# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-upload-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included, scoped to Constitution Principle III (service-level authorization is
NON-NEGOTIABLE) as called for in plan.md's Technical Context — not full TDD for every task.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P6) to enable independent
implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US6)
- File paths are exact and relative to the repository root

## Path Conventions

Single project (existing `ContosoDashboard/` Blazor Server monolith), extended in place, plus a
new `ContosoDashboard.Tests/` project — per plan.md's Project Structure section.

---

## Phase 1: Setup

**Purpose**: Add the new package/project scaffolding this feature needs before any code is written.

- [ ] T001 [P] Add the `nClam` NuGet package reference to `ContosoDashboard/ContosoDashboard.csproj` (ClamAV client, research.md #1)
- [ ] T002 [P] Create the `ContosoDashboard.Tests` xUnit project (`dotnet new xunit -o ContosoDashboard.Tests`), add a project reference to `ContosoDashboard/ContosoDashboard.csproj`, and add the EF Core SQLite package needed for an in-memory `ApplicationDbContext` test fixture (research.md #2)
- [ ] T003 [P] Add `ContosoDashboard/AppData/uploads/` to `.gitignore` (new local file storage root must never be committed, mirroring the existing `*.db` entry)

**Checkpoint**: Packages and test project exist; no feature code yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema, storage/scanning abstractions, the core service contract, and the download
endpoints that every user story phase below builds on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T004 [P] Create the `Document` entity in `ContosoDashboard/Models/Document.cs` with exactly the fields and constraints from data-model.md's Document table: `DocumentId` (int PK), `Title` (required, max 255), `Description` (optional, max 2000), `Category` (required, max 50, text — not an enum), `Tags` (optional, max 500, comma-delimited), `FileName` (required, max 255), `FilePath` (required, max 500), `FileSizeBytes` (long, required), `FileType` (required, max 255), `UploadedByUserId` (int FK), `ProjectId` (int? FK), `TaskId` (int? FK), `UploadedDate`/`UpdatedDate` (DateTime, default `DateTime.UtcNow`), plus `UploadedByUser`/`Project?`/`Task?`/`Shares`/`ActivityRecords` navigation properties
- [ ] T005 [P] Create the `DocumentShare` entity in `ContosoDashboard/Models/DocumentShare.cs` per data-model.md: `DocumentShareId` (PK), `DocumentId` (required FK), `RecipientUserId` (int? FK, set for individual shares), `TeamProjectId` (int? FK, set for team shares), `SharedByUserId` (required FK), `CreatedDate` (default `DateTime.UtcNow`)
- [ ] T006 [P] Create the `DocumentActivityLog` entity and `DocumentActivityType` enum (`Upload`, `Download`, `Delete`, `Share`) in `ContosoDashboard/Models/DocumentActivityLog.cs` per data-model.md: `DocumentActivityLogId` (PK), `DocumentId` (int? FK), `DocumentTitleSnapshot` (required, max 255), `ActionType`, `PerformedByUserId` (required FK), `OccurredDate` (default `DateTime.UtcNow`)
- [ ] T007 Add `DbSet<Document>`, `DbSet<DocumentShare>`, `DbSet<DocumentActivityLog>` and their `OnModelCreating` configuration to `ContosoDashboard/Data/ApplicationDbContext.cs`: FK delete behaviors exactly as data-model.md specifies (`Document→User/Project/Task` = Restrict; `DocumentShare.DocumentId` = Cascade, `RecipientUserId`/`TeamProjectId`/`SharedByUserId` = Restrict; `DocumentActivityLog.DocumentId` = SetNull, `PerformedByUserId` = Restrict), plus the Indexes listed in data-model.md (unique index on `Document.FilePath`; indexes on `Document.UploadedByUserId`/`ProjectId`/`Category`; `DocumentShare.DocumentId`/`RecipientUserId`/`TeamProjectId`; `DocumentActivityLog.DocumentId` and `(ActionType, OccurredDate)`)
- [ ] T008 [P] Add an EF Core migration (or confirm `EnsureCreated()` picks up the new tables, matching how this app already initializes its database) reflecting T004–T007
- [ ] T009 [P] Add `UploadedDocuments : ICollection<Document>` navigation to `ContosoDashboard/Models/User.cs`, `Documents : ICollection<Document>` to `ContosoDashboard/Models/Project.cs`, and `Documents : ICollection<Document>` to `ContosoDashboard/Models/TaskItem.cs` (data-model.md Relationships section) — no changes to any existing field on these three models
- [ ] T010 [P] Create `IFileStorageService` in `ContosoDashboard/Services/IFileStorageService.cs` with exactly the four methods from contracts/file-storage-service.md: `UploadAsync(Stream, string relativePath, string contentType)`, `DeleteAsync(string relativePath)`, `DownloadAsync(string relativePath)`, `GetUrlAsync(string relativePath, TimeSpan expiration)`
- [ ] T011 Create `LocalFileStorageService` in `ContosoDashboard/Services/LocalFileStorageService.cs` implementing `IFileStorageService` using `System.IO.File`/`FileStream` under `ContosoDashboard/AppData/uploads/`, using the `{userId}/{projectId-or-"personal"}/{guid}.{ext}` path pattern from research.md #4 (depends on T010)
- [ ] T012 [P] Create `IVirusScanner` in `ContosoDashboard/Services/IVirusScanner.cs` with a single `Task<bool> ScanAsync(Stream fileStream)` method returning `true` when the file is clean (research.md #1)
- [ ] T013 [P] Create `ClamAvVirusScanner` in `ContosoDashboard/Services/ClamAvVirusScanner.cs` implementing `IVirusScanner` via `nClam` against a locally-running `clamd` (depends on T001, T012)
- [ ] T014 [P] Create `NoOpVirusScanner` in `ContosoDashboard/Services/NoOpVirusScanner.cs` implementing `IVirusScanner` by always returning `true`, for local dev environments without ClamAV installed (research.md #1 follow-up; depends on T012)
- [ ] T015 Create `IDocumentService` in `ContosoDashboard/Services/IDocumentService.cs` with exactly the methods and signatures listed in contracts/document-service.md's table (`UploadAsync`, `GetMyDocumentsAsync`, `GetProjectDocumentsAsync`, `SearchAsync`, `GetByIdAsync`, `UpdateMetadataAsync`, `ReplaceFileAsync`, `DeleteAsync`, `ShareAsync`, `GetSharedWithMeAsync`, `GetRecentAsync`, `GetActivityReportAsync`)
- [ ] T016 Create the `DocumentService` skeleton in `ContosoDashboard/Services/DocumentService.cs` implementing `IDocumentService`, constructor-injecting `ApplicationDbContext`, `IFileStorageService`, `IVirusScanner`, and `IHttpContextAccessor` (matching this app's existing service conventions, e.g. `UserService`) — method bodies filled in by later story tasks; unimplemented methods throw `NotImplementedException` (depends on T004–T015)
- [ ] T017 Register `IFileStorageService→LocalFileStorageService`, `IVirusScanner→ClamAvVirusScanner` (or `NoOpVirusScanner` when ClamAV isn't configured), and `IDocumentService→DocumentService` as scoped services in `ContosoDashboard/Program.cs`, alongside the existing `AddScoped<I...Service, ...Service>()` registrations (depends on T011, T013/T014, T016)
- [ ] T018 Add the two authorized Minimal API endpoints to `ContosoDashboard/Program.cs` per contracts/document-download-endpoint.md: `GET /api/documents/{id}/download` (`Content-Disposition: attachment`, records a `Download` activity) and `GET /api/documents/{id}/preview` (`Content-Disposition: inline`, `400` for non-PDF/image types, no activity log), both calling `IDocumentService.GetByIdAsync` for the authorization decision and returning `404` — never `403` — when it returns nothing (depends on T016)
- [ ] T019 [P] Create the shared `DocumentServiceAuthorizationTests` fixture in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: an EF Core SQLite in-memory `ApplicationDbContext`, seeded with the same four users/roles as `ApplicationDbContext.SeedData` plus a sample project/task, reused by every authorization test in the phases below (depends on T002, T007)

**Checkpoint**: Foundation ready — schema, storage, scanning, the service contract, and download
endpoints exist. User story implementation can now begin.

---

## Phase 3: User Story 1 - Upload a document securely (Priority: P1) 🎯 MVP

**Goal**: An employee can upload a valid work file with required metadata and see it stored;
invalid uploads (too large, unsupported type, infected) are clearly rejected.

**Independent Test**: Upload a single valid PDF under 25 MB with a title and category as a seeded
user; confirm success and that the document appears in that user's own list. Then attempt a 30 MB
file, an unsupported file type, and an infected file, confirming each is rejected with a clear,
specific message.

### Tests for User Story 1

- [ ] T020 [P] [US1] Test in `ContosoDashboard.Tests/Services/DocumentServiceTests.cs`: `UploadAsync` rejects a file larger than 25 MB with a size-limit-specific error (FR-003)
- [ ] T021 [P] [US1] Test in `ContosoDashboard.Tests/Services/DocumentServiceTests.cs`: `UploadAsync` rejects a file type outside PDF/Word/Excel/PowerPoint/plain-text/JPEG/PNG with a type-specific error (FR-002)
- [ ] T022 [P] [US1] Test in `ContosoDashboard.Tests/Services/DocumentServiceTests.cs`: `UploadAsync` rejects a file the injected `IVirusScanner` flags as infected, and no `Document` row or stored file is created (FR-007)

### Implementation for User Story 1

- [ ] T023 [US1] Implement `DocumentService.UploadAsync` in `ContosoDashboard/Services/DocumentService.cs`: enforce `Title` and `Category` required (`Category` ∈ {Project Documents, Team Resources, Personal Files, Reports, Presentations, Other}); enforce file size ≤ 25 MB (FR-003); enforce file type whitelist (FR-002); call `IVirusScanner.ScanAsync` and reject on failure before any storage write (FR-007); if `ProjectId` is supplied, confirm the uploader is a current member of that project; generate the GUID-based path, call `IFileStorageService.UploadAsync`, then insert the `Document` row (research.md #4 ordering) (depends on T016, T023's tests T020–T022 failing first)
- [ ] T024 [US1] In `DocumentService.UploadAsync`, insert a `DocumentActivityLog` row with `ActionType = Upload` and `DocumentTitleSnapshot` set on every successful upload (FR-019)
- [ ] T025 [US1] Create `ContosoDashboard/Pages/Documents.razor` with an `InputFile` component (`maxFileSize` = 25 MB) using the `MemoryStream`-buffering pattern from research.md #3 (capture name/size/content-type before opening the stream, clear the `IBrowserFile` reference after copying), a form for title/description/category/associated project/tags, an upload-progress indicator, and a clear success/failure message (FR-004, FR-005)
- [ ] T026 [US1] Add a "Documents" link (policy `Employee`) to `ContosoDashboard/Shared/NavMenu.razor` pointing at `/documents`

**Checkpoint**: User Story 1 is independently functional — upload works, invalid uploads are
rejected with clear messages.

---

## Phase 4: User Story 2 - Find my documents and project documents (Priority: P2)

**Goal**: A user can sort, filter, and search their own and their projects' documents, seeing only
what they're authorized to access, and can download/preview what they find.

**Independent Test**: With several documents seeded across categories/projects, filter and search
from "My Documents" and confirm only authorized, matching results appear within 2 seconds; open a
project and confirm its Documents tab lists every associated document.

### Tests for User Story 2

- [ ] T027 [P] [US2] Test in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: `GetMyDocumentsAsync(userId)` never returns a document uploaded by a different user (IDOR check, FR-008)
- [ ] T028 [P] [US2] Test in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: `SearchAsync` excludes a matching document the caller is not authorized to access (FR-010)

### Implementation for User Story 2

- [ ] T029 [P] [US2] Implement `DocumentService.GetMyDocumentsAsync(userId, sortBy, filters)` in `ContosoDashboard/Services/DocumentService.cs`, scoped to `WHERE UploadedByUserId == userId`, supporting sort by title/upload date/category/file size and filter by category/associated project/date range (FR-008)
- [ ] T030 [P] [US2] Implement `DocumentService.GetProjectDocumentsAsync(userId, projectId)` in `ContosoDashboard/Services/DocumentService.cs`, returning rows only after confirming `userId` is a current member of `projectId` (FR-009)
- [ ] T031 [US2] Implement `DocumentService.SearchAsync(userId, query)` in `ContosoDashboard/Services/DocumentService.cs`, matching title/description/tags/uploader name/associated project, applying the same authorization check as `GetByIdAsync` (T032) to every candidate row before including it (FR-010)
- [ ] T032 [US2] Implement `DocumentService.GetByIdAsync(userId, documentId)` in `ContosoDashboard/Services/DocumentService.cs` as the single central authorization check reused by search, download/preview (T018), and every later story: returns the document only if `userId` is the uploader, a current member of its `ProjectId` (if set), an Administrator, or an active share recipient (individual or live team share); otherwise returns nothing — callers must translate "nothing" to "not found," never "forbidden" (contracts/document-service.md)
- [ ] T033 [US2] Build the "My Documents" table in `ContosoDashboard/Pages/Documents.razor`: sortable/filterable columns for title, category, upload date, file size, associated project, calling `GetMyDocumentsAsync`
- [ ] T034 [US2] Add a search box to `ContosoDashboard/Pages/Documents.razor` calling `SearchAsync`, verified to return within 2 seconds (SC-007)
- [ ] T035 [US2] Add a "Documents" tab to `ContosoDashboard/Pages/ProjectDetails.razor` listing `GetProjectDocumentsAsync` results with download/preview links pointing at the T018 endpoints (FR-011)

**Checkpoint**: User Stories 1 AND 2 both work independently.

---

## Phase 5: User Story 3 - Manage and remove documents (Priority: P3)

**Goal**: A document owner can edit metadata, replace the file, or permanently delete a document
they own; a Project Manager can additionally delete any document on their project.

**Independent Test**: As the uploader, edit a document's metadata and confirm it updates
everywhere it's listed; replace its file; delete it and confirm it's gone everywhere, including
from anyone it was shared with. As a Project Manager, delete a document on your project that you
didn't upload.

### Tests for User Story 3

- [ ] T036 [P] [US3] Test in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: `UpdateMetadataAsync`/`ReplaceFileAsync` succeed only for the uploader; any other caller's attempt has no effect and the document is unchanged (FR-012, IDOR)
- [ ] T037 [P] [US3] Test in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: `DeleteAsync` succeeds for the uploader or for a Project Manager of the document's associated project, and fails for every other caller (FR-013)

### Implementation for User Story 3

- [ ] T038 [P] [US3] Implement `DocumentService.UpdateMetadataAsync(userId, documentId, changes)` in `ContosoDashboard/Services/DocumentService.cs`, uploader-only, updating title/description/category/tags and bumping `UpdatedDate` (FR-012)
- [ ] T039 [P] [US3] Implement `DocumentService.ReplaceFileAsync(userId, documentId, newFileBytes, newFileMetadata)` in `ContosoDashboard/Services/DocumentService.cs`, uploader-only, re-running the FR-002/FR-003/FR-007 checks from T023 on the new file and bumping `UpdatedDate`; no prior version is retained (FR-012)
- [ ] T040 [US3] Implement `DocumentService.DeleteAsync(userId, documentId)` in `ContosoDashboard/Services/DocumentService.cs`, allowed for the uploader or a Project Manager of the associated project, permanently removing the `Document` row (cascading `DocumentShare` rows per T007's `DeleteBehavior.Cascade`) after the caller has confirmed (FR-013)
- [ ] T041 [US3] Add an edit-metadata form and a delete-with-confirmation control to `ContosoDashboard/Pages/Documents.razor`, and a Project-Manager-only delete control to the Documents tab in `ContosoDashboard/Pages/ProjectDetails.razor` (T035)
- [ ] T042 [US3] Add a replace-file control to the edit UI in `ContosoDashboard/Pages/Documents.razor`, reusing the `InputFile`/`MemoryStream` pattern from T025

**Checkpoint**: User Stories 1, 2, AND 3 all work independently.

---

## Phase 6: User Story 4 - Share documents and get notified (Priority: P4)

**Goal**: A document owner can share with a specific user or with a project's current members
("team"), recipients are notified in-app, and team-share access stays live against project
membership.

**Independent Test**: Share a document with a specific user and confirm they're notified and see
it under "Shared with Me." Share as a team against a project; confirm current members see it, a
newly added member gains access without re-sharing, and a removed member immediately loses access.

### Tests for User Story 4

- [ ] T043 [P] [US4] Test in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: `ShareAsync` rejects a caller who does not own the document, and rejects a call that supplies both or neither of `recipientUserId`/`teamProjectId` (FR-014)
- [ ] T044 [P] [US4] Test in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: `GetSharedWithMeAsync` includes a team-shared document for a user who just joined the team project and excludes it the moment that user is removed from the project (FR-014/FR-022, dynamic membership per research.md #6)

### Implementation for User Story 4

- [ ] T045 [US4] Implement `DocumentService.ShareAsync(userId, documentId, recipientUserId?, teamProjectId?)` in `ContosoDashboard/Services/DocumentService.cs`: uploader-only, requires exactly one of `recipientUserId`/`teamProjectId`, confirms the uploader has access to the chosen project for team shares (FR-014)
- [ ] T046 [US4] Implement `DocumentService.GetSharedWithMeAsync(userId)` in `ContosoDashboard/Services/DocumentService.cs`, returning documents with a direct individual share to `userId` OR a team share whose `TeamProjectId` currently lists `userId` as a member (live join against `ProjectMember`, research.md #6) (FR-015)
- [ ] T047 [US4] In `DocumentService.ShareAsync`, send an in-app notification via the existing `INotificationService` to the recipient (individual share) or to all current members of the team project (team share) (FR-015)
- [ ] T048 [P] [US4] In `DocumentService.UploadAsync` (T023), send an in-app notification via `INotificationService` to all current members of the document's associated project, if any, when the upload succeeds (FR-018)
- [ ] T049 [US4] Create `ContosoDashboard/Pages/SharedWithMe.razor` listing `GetSharedWithMeAsync` results, visually distinct from the uploader's own "My Documents" list (FR-015)
- [ ] T050 [US4] Add a "Share" action to `ContosoDashboard/Pages/Documents.razor` with an individual-user picker and a project/team picker, calling `ShareAsync`
- [ ] T051 [US4] Add a "Shared with Me" link to `ContosoDashboard/Shared/NavMenu.razor` pointing at `/shared-with-me`

**Checkpoint**: User Stories 1–4 all work independently.

---

## Phase 7: User Story 5 - Attach documents to tasks and see them on the dashboard (Priority: P5)

**Goal**: A user can attach/upload documents directly from a task's detail view (auto-linked to
the task's project), and sees a "Recent Documents" widget plus a document count on the dashboard.

**Independent Test**: From a task's detail page, attach an existing document and upload a new one;
confirm both appear on the task and are linked to the task's project. On the dashboard home page,
confirm the 5 most recent uploads and a total count display correctly.

### Tests for User Story 5

- [ ] T052 [P] [US5] Test in `ContosoDashboard.Tests/Services/DocumentServiceTests.cs`: uploading via `UploadAsync` with a `taskId` automatically sets `Document.ProjectId` to that task's `ProjectId` (FR-016)

### Implementation for User Story 5

- [ ] T053 [US5] Implement `DocumentService.GetRecentAsync(userId, count = 5)` in `ContosoDashboard/Services/DocumentService.cs`, scoped to the user's own uploads, reusing `GetByIdAsync`'s visibility rule (FR-017)
- [ ] T054 [US5] Extend `DocumentService.UploadAsync` (T023) to accept an optional `taskId`, and when supplied, auto-set `Document.ProjectId` from that `TaskItem.ProjectId` and set `Document.TaskId` (FR-016)
- [ ] T055 [US5] Add "Attach existing document" and "Upload new document" controls to the task detail view in `ContosoDashboard/Pages/Tasks.razor` (FR-016)
- [ ] T056 [US5] Add a "Recent Documents" widget (5 most recent, via `GetRecentAsync`) and a document-count summary card to `ContosoDashboard/Pages/Index.razor` (FR-017)

**Checkpoint**: User Stories 1–5 all work independently.

---

## Phase 8: User Story 6 - Audit and reporting for compliance (Priority: P6)

**Goal**: Administrators can access any document regardless of ownership/sharing, and can view a
report of upload/download/delete/share activity across the organization.

**Independent Test**: As the Administrator, open a document you don't own and weren't shared, and
confirm access succeeds. Open the activity report and confirm every action type appears with who
performed it and when, including an entry for a document that has since been deleted.

### Tests for User Story 6

- [ ] T057 [P] [US6] Test in `ContosoDashboard.Tests/Services/DocumentServiceAuthorizationTests.cs`: `GetByIdAsync` grants an Administrator access to a document they neither uploaded nor were shared, and `GetActivityReportAsync` rejects a non-Administrator caller (FR-019/FR-020)

### Implementation for User Story 6

- [ ] T058 [US6] Extend `DocumentService.GetByIdAsync` (T032) to grant unconditional access when the caller has the Administrator role (FR-020)
- [ ] T059 [US6] Implement `DocumentService.GetActivityReportAsync(adminUserId, filters)` in `ContosoDashboard/Services/DocumentService.cs`, Administrator-only, summarizing most-uploaded document types, most active uploaders, and access patterns from `DocumentActivityLog` (FR-019)
- [ ] T060 [US6] Confirm the T018 download/preview endpoints and the `DeleteAsync` (T040)/`ShareAsync` (T045) implementations each write a `DocumentActivityLog` row with the correct `ActionType` and `DocumentTitleSnapshot`, so the report in T059 has complete history even for since-deleted documents (FR-019, data-model.md)
- [ ] T061 [US6] Create `ContosoDashboard/Pages/DocumentReports.razor`, restricted to `[Authorize(Policy = "Administrator")]`, rendering `GetActivityReportAsync`'s output
- [ ] T062 [US6] Add a "Document Reports" link, visible only to Administrators, to `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: All six user stories are independently functional.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final housekeeping once every desired story is complete.

- [ ] T063 [P] Ensure `ContosoDashboard/AppData/uploads/` exists on a fresh clone (e.g., a `.gitkeep` file, since the directory itself is gitignored per T003)
- [ ] T064 [P] Update the "✅ Implemented Features" list in `README.md` to include Document Upload and Management
- [ ] T065 Run through every scenario in `quickstart.md` end-to-end (all 6 stories plus the authorization and performance spot-checks) against a running `dotnet run` instance
- [ ] T066 [P] Seed ~500 documents for one test user and confirm `GetMyDocumentsAsync`/`SearchAsync` (T029/T031) return in under 2 seconds and preview (T018) begins in under 3 seconds (SC-007, SC-008)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Stories (Phase 3–8)**: All depend on Foundational completion.
  - US1 has no dependency on any other story.
  - US2's `GetByIdAsync` (T032) is reused by US4 (T046), US5 (T053), and US6 (T058) — those later
    stories depend on T032 existing, but US2 itself remains independently testable.
  - US3, US4, US5, US6 can otherwise proceed in priority order or in parallel once Foundational and
    US1/US2 (for shared plumbing) are done.
- **Polish (Phase 9)**: Depends on all desired user stories being complete.

### Parallel Opportunities

- All `[P]` tasks within Phase 1 and Phase 2 can run in parallel (different files).
- Within each story phase, `[P]`-marked test tasks can run in parallel with each other, and
  `[P]`-marked model/service tasks touching different files can run in parallel.
- Once Foundational (Phase 2) is done, different developers could take US1 and US2 in parallel,
  though US2's `GetByIdAsync` (T032) should land before US4/US5/US6 start relying on it.

---

## Parallel Example: User Story 1

```bash
# Launch all three tests for User Story 1 together (must fail before T023 is implemented):
Task: "Test: UploadAsync rejects a file larger than 25 MB in ContosoDashboard.Tests/Services/DocumentServiceTests.cs"
Task: "Test: UploadAsync rejects an unsupported file type in ContosoDashboard.Tests/Services/DocumentServiceTests.cs"
Task: "Test: UploadAsync rejects a scanner-flagged infected file in ContosoDashboard.Tests/Services/DocumentServiceTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run the US1 section of quickstart.md independently
5. Demo: users can upload documents to ContosoDashboard

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. + US1 (Upload) → validate → demo (MVP)
3. + US2 (Find) → validate → demo
4. + US3 (Manage) → validate → demo
5. + US4 (Share) → validate → demo
6. + US5 (Task/dashboard integration) → validate → demo
7. + US6 (Audit/reporting) → validate → demo
8. Polish

## Notes

- `[P]` tasks touch different files with no unmet dependencies.
- Every service-layer test lives in `ContosoDashboard.Tests/Services/` and must fail before its
  corresponding implementation task, per Constitution Principle III's non-negotiable
  authorization requirement.
- Commit after each task or logical group; stop at any checkpoint to validate a story
  independently before moving to the next.
