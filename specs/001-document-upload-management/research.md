# Research: Document Upload and Management

All items below resolve a "NEEDS CLARIFICATION" from the plan's Technical Context. None of these
questions were re-raised with the user — each has a single reasonable, defensible answer given the
existing ContosoDashboard codebase and constitution constraints, so they are resolved here per the
"reasonable defaults" guidance rather than sent through `/speckit.clarify`.

## 1. Malware/virus scanning approach for FR-007

- **Decision**: Scan every uploaded file with a locally-running ClamAV daemon (`clamd`), invoked
  from `DocumentService` via a thin `IVirusScanner` abstraction, before the file is written to
  permanent storage or a `Document` row is created.
- **Rationale**: ClamAV is open-source, runs entirely on the local machine with local signature
  definitions (no outbound network call at scan time), and has mature .NET client libraries
  (e.g., `nClam`) that talk to `clamd` over a local socket/TCP port. This satisfies constitution
  Principle I (offline-first, no cloud dependency) while still meeting FR-007's hard requirement
  that infected files are actually rejected, not merely type-checked.
- **Alternatives considered**:
  - *Cloud AV APIs (e.g., VirusTotal)* — rejected: requires internet access and a third-party
    account, violating Principle I and FR-023.
  - *OS-native scanners (e.g., Windows Defender via `MpCmdRun.exe`)* — rejected: Windows-only,
    and this repository already runs cross-platform (Linux dev environment confirmed working
    with SQLite in place of LocalDB), so a Windows-only scan step would break parity.
  - *Extension/MIME whitelist only, no real scan* — rejected: FR-002 already covers type
    whitelisting; FR-007 is a distinct, additional requirement for actual malware detection and
    would be unmet by type-checking alone.
- **Follow-up for tasks phase**: `IVirusScanner` MUST be its own interface (parallel to
  `IFileStorageService`) so a training environment without ClamAV installed can register a
  no-op/allow-all implementation without touching `DocumentService` — keeps the local dev
  experience unblocked if ClamAV isn't present, while production/graded environments wire up the
  real scanner.

## 2. Testing approach

- **Decision**: Add a new `ContosoDashboard.Tests` xUnit project targeting the same TFM as the
  main project, using EF Core's in-memory/SQLite-in-memory provider for `ApplicationDbContext` in
  service-layer tests.
- **Rationale**: The repository currently has zero automated tests. xUnit is the .NET ecosystem
  default and integrates with `dotnet test` out of the box. Constitution Principle III
  (service-level authorization is NON-NEGOTIABLE) is only verifiable in practice through tests
  that assert a user cannot read/modify another user's or another project's documents — this
  needs a real test project, not manual verification.
- **Alternatives considered**: *No new test project, manual QA only* — rejected: this feature
  concentrates almost all of this app's IDOR risk (file access across users/projects/teams); the
  constitution's non-negotiable authorization principle needs an enforceable regression check.

## 3. Blazor Server file upload pattern

- **Decision**: Use the built-in `InputFile` component with `maxFileSize` set to 25 MB, copy the
  selected `IBrowserFile`'s stream into a `MemoryStream` immediately (capturing name/size/content
  type into local variables first), then release the `IBrowserFile` reference before calling
  `DocumentService.UploadAsync`.
- **Rationale**: This is the standard, documented pattern for avoiding Blazor Server's known
  `IBrowserFile` stream-disposal and re-render issues, and matches the pattern already vetted in
  the stakeholder requirements document.
- **Alternatives considered**: *Direct multipart HTTP upload endpoint bypassing Blazor* —
  rejected: adds a second upload code path/UI to maintain for no benefit at this file size (25 MB
  max), when `InputFile` already handles it within the existing Blazor Server UI.

## 4. Stored file path strategy

- **Decision**: Store files under an application data directory outside `wwwroot` (e.g.,
  `AppData/uploads/`), using the path pattern `{userId}/{projectId-or-"personal"}/{guid}.{ext}`.
  Generate the GUID-based path first, write the file to disk, and only then insert the `Document`
  row referencing that path (upload sequence: generate path → save file → save metadata).
- **Rationale**: Keeping files outside `wwwroot` means they cannot be served as static files,
  forcing all access through an authorized endpoint (supports Principle III). GUID-based names
  prevent path traversal from user-supplied filenames. Writing the file before the database row
  means a failed file write never produces an orphaned/broken database record; a failed *database*
  write after a successful file write leaves an orphaned file on disk, which is a far cheaper
  problem (reclaimed by a cleanup task) than a UI showing a document whose file doesn't exist.
- **Alternatives considered**: *Save DB record first, then file* — rejected: this is exactly the
  ordering the stakeholder document flags as producing duplicate-key/orphaned-record issues during
  training exercises, and it also risks the UI listing a document with no backing file.

## 5. Serving downloads and previews for files stored outside wwwroot

- **Decision**: Add two authorized Minimal API endpoints registered in `Program.cs` — GET
  `/api/documents/{id}/download` and GET `/api/documents/{id}/preview` — both requiring
  authentication and delegating the authorization decision to `DocumentService` before streaming
  bytes from `IFileStorageService.DownloadAsync`.
- **Rationale**: The app has no MVC controllers today (only Razor Pages + Blazor Server); adding
  two small Minimal API endpoints is less new surface area than introducing a full Controllers
  folder, consistent with Principle V (simplicity). Preview reuses the same authorization path as
  download, differing only in the `Content-Disposition` header (`inline` vs. `attachment`), so
  browsers render PDFs/images natively without any third-party viewer.
- **Alternatives considered**: *Full MVC controller* — rejected as unnecessary ceremony for two
  endpoints; *streaming through a Blazor Server SignalR call* — rejected: large binary downloads
  don't belong on the interactive render circuit and browsers can't natively "download" a SignalR
  response the way they can a normal HTTP GET.

## 6. Team share evaluation strategy (dynamic membership link, per spec Clarifications)

- **Decision**: A team share is a `DocumentShare` row with `TeamProjectId` set (and
  `RecipientUserId` null); access checks join the sharer's chosen project's *current*
  `ProjectMember` rows at query time rather than snapshotting recipient user IDs when the share is
  created.
- **Rationale**: The spec's clarification session explicitly requires that joining a project later
  grants access and leaving revokes it immediately — a snapshot list of user IDs at share-creation
  time cannot satisfy that; a live join against `ProjectMember` does, and reuses data the app
  already maintains for FR-022.
- **Alternatives considered**: *Materialize/sync a per-user share row whenever project membership
  changes* — rejected: adds a background-sync mechanism and edge cases (missed events, ordering)
  for no benefit over a straightforward query-time join, violating Principle V.
