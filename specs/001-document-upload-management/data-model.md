# Data Model: Document Upload and Management

New entities added to `ContosoDashboard.Models` and `ApplicationDbContext`, following the same
conventions as existing entities (integer PKs, `[Key]`/`[Required]`/`[MaxLength]` data annotations,
`virtual` navigation properties, UTC `DateTime` timestamps).

## Document

Represents one uploaded file's metadata and where its bytes are stored (spec: Key Entities /
FR-001–FR-013, FR-016–FR-017).

| Field | Type | Rules |
|---|---|---|
| `DocumentId` | `int` (PK) | Integer key, per stakeholder constraint (consistent with `User`/`Project`). |
| `Title` | `string` | Required, max 255. |
| `Description` | `string?` | Optional, max 2000. |
| `Category` | `string` | Required, max 50. Stored as text (not an enum) per stakeholder constraint; validated against the fixed list (Project Documents, Team Resources, Personal Files, Reports, Presentations, Other) in `DocumentService`, not via a DB enum, so the list can change without a migration. |
| `Tags` | `string?` | Optional, max 500; comma-delimited free-form tags. A single delimited column is sufficient at this scale (≤500 docs/user, FR-010's tag search is a substring match) and avoids a many-to-many table per Principle V. |
| `FileName` | `string` | Required, max 255. Original filename shown to users on download; never used to build the storage path (prevents path traversal). |
| `FilePath` | `string` | Required, max 500. GUID-based relative path (`{userId}/{projectId-or-"personal"}/{guid}.{ext}`, see research.md #4). Unique index — by the time a row is inserted the path is always populated and unique (path generated before file write, per research.md #4), so a unique index catches bugs rather than causing legitimate conflicts. |
| `FileSizeBytes` | `long` | Required. Enforced ≤ 25 MB (FR-003) in `DocumentService` before storage. |
| `FileType` | `string` | Required, max 255 (stakeholder-mandated width to fit Office MIME types, e.g. `application/vnd.openxmlformats-officedocument.wordprocessingml.document`). |
| `UploadedByUserId` | `int` (FK → `User`) | Required. `DeleteBehavior.Restrict` (matches existing `TaskItem`/`Project` FK-to-`User` convention). |
| `ProjectId` | `int?` (FK → `Project`) | Optional. Set directly by the uploader (FR-005) or automatically when uploaded from a task (FR-016). `DeleteBehavior.Restrict`. |
| `TaskId` | `int?` (FK → `TaskItem`) | Optional. Set when the document was attached/uploaded from a task's detail view (FR-016). `DeleteBehavior.Restrict`. |
| `UploadedDate` | `DateTime` | Default `DateTime.UtcNow`. Immutable after creation. |
| `UpdatedDate` | `DateTime` | Default `DateTime.UtcNow`; bumped on metadata edit or file replacement (FR-012). |

**Navigation**: `UploadedByUser` (`User`), `Project?`, `Task?` (`TaskItem`), `Shares`
(`ICollection<DocumentShare>`), `ActivityRecords` (`ICollection<DocumentActivityLog>`).

**Lifecycle**: Create → zero or more metadata edits / file replacements → permanent delete (hard
delete; no soft-delete/trash and no version history, per spec Out of Scope). There is no status
field — the entity either exists or has been deleted.

## DocumentShare

Represents document access granted beyond the owner/project-member baseline (spec: Key Entities /
FR-014–FR-015, Clarifications session).

| Field | Type | Rules |
|---|---|---|
| `DocumentShareId` | `int` (PK) | |
| `DocumentId` | `int` (FK → `Document`) | Required. `DeleteBehavior.Cascade` — deleting a document removes its shares (matches FR-013: shared recipients simply no longer see it). |
| `RecipientUserId` | `int?` (FK → `User`) | Set for an **individual share**. `DeleteBehavior.Restrict`. |
| `TeamProjectId` | `int?` (FK → `Project`) | Set for a **team share**. `DeleteBehavior.Restrict`. Access for a team share is evaluated at query time against that project's *current* `ProjectMember` rows (research.md #6) — joining or leaving the project immediately changes access, satisfying FR-022. |
| `SharedByUserId` | `int` (FK → `User`) | Required. Who created the share. `DeleteBehavior.Restrict`. |
| `CreatedDate` | `DateTime` | Default `DateTime.UtcNow`. |

**Invariant** (enforced in `DocumentService`, not a DB constraint, consistent with this app's
existing style of service-layer validation): exactly one of `RecipientUserId` or `TeamProjectId`
must be set per row — never both, never neither.

## DocumentActivityLog

Represents one audit-relevant action taken on a document (spec: Key Entities / FR-019).

| Field | Type | Rules |
|---|---|---|
| `DocumentActivityLogId` | `int` (PK) | |
| `DocumentId` | `int?` (FK → `Document`) | Nullable, `DeleteBehavior.SetNull`. A document can be hard-deleted (FR-013) while its history must remain reportable (FR-019), so the FK is allowed to go null on delete rather than cascading the log away. |
| `DocumentTitleSnapshot` | `string` | Required, max 255. Copied from `Document.Title` at the time of the action so audit reports stay meaningful after the source document is deleted. |
| `ActionType` | `enum DocumentActivityType { Upload, Download, Delete, Share }` | Required. |
| `PerformedByUserId` | `int` (FK → `User`) | Required. `DeleteBehavior.Restrict`. |
| `OccurredDate` | `DateTime` | Default `DateTime.UtcNow`. |

## Relationships to existing entities

- `User.UploadedDocuments : ICollection<Document>` (new navigation, mirrors existing
  `AssignedTasks`/`CreatedTasks` pattern).
- `Project.Documents : ICollection<Document>` (new navigation, mirrors existing `Tasks`).
- `TaskItem.Documents : ICollection<Document>` (new navigation) — supports FR-016.
- No changes to `User`, `Project`, or `TaskItem` primary keys, existing fields, or existing
  relationships — this feature is purely additive to the schema.

## Indexes (mirroring the existing `OnModelCreating` index conventions)

- `Document`: index on `UploadedByUserId`; index on `ProjectId`; index on `Category`; unique index
  on `FilePath`.
- `DocumentShare`: index on `DocumentId`; index on `RecipientUserId`; index on `TeamProjectId`.
- `DocumentActivityLog`: index on `DocumentId`; index on `(ActionType, OccurredDate)` (supports the
  FR-019 admin reports).

## Validation rules summary (from spec Functional Requirements)

- File size ≤ 25 MB (FR-003) — checked before any disk write.
- File extension/MIME type in the allowed set (FR-002) — checked before any disk write.
- Malware scan must pass (FR-007) — checked before any disk write (research.md #1).
- `Title` and `Category` required on upload (FR-005); `Category` must be one of the six predefined
  values.
- `FileType` column must accommodate long Office MIME type strings (stakeholder constraint) → max
  255, not a shorter conventional MIME-type length.
