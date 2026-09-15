# Contract: IDocumentService

`ContosoDashboard.Services.IDocumentService` — the sole entry point Pages are allowed to call for
document business logic (Constitution Principle II). Every method independently enforces
authorization (Principle III); a caller's `[Authorize]`-gated page is not sufficient on its own.

All methods take the acting user's ID (or read it from `IHttpContextAccessor`/claims, matching this
app's existing service conventions) so authorization can be checked against the *caller*, not just
whatever ID the caller happens to pass in — this is what prevents IDOR.

| Method | Authorization performed before returning/mutating data | Maps to |
|---|---|---|
| `UploadAsync(uploaderUserId, fileMetadata, fileBytes)` | Validates size/type; runs `IVirusScanner`; if `ProjectId` is set, confirms uploader is a member of that project. | FR-001–FR-007 |
| `GetMyDocumentsAsync(userId, sortBy, filters)` | Scoped to `WHERE UploadedByUserId == userId` — cannot be parameterized to another user's ID. | FR-008 |
| `GetProjectDocumentsAsync(userId, projectId)` | Confirms `userId` is a current member of `projectId` before returning any rows. | FR-009 |
| `SearchAsync(userId, query)` | Applies the same per-row authorization as `GetByIdAsync` to every candidate result before including it — a match the user isn't authorized for is silently excluded, never surfaced as "access denied" (avoids leaking existence). | FR-010 |
| `GetByIdAsync(userId, documentId)` | Returns the document only if `userId` is the uploader, a current member of its `ProjectId` (if set), an Administrator, a Team Lead of a project the document is associated with (FR-021 scope), or an active individual/team share recipient (FR-014, evaluated live per research.md #6). Otherwise returns "not found" (never "forbidden" — do not confirm the document's existence to unauthorized callers). | FR-011, IDOR prevention |
| `UpdateMetadataAsync(userId, documentId, changes)` | Only the uploader may call this successfully; anyone else gets "not found" per the same non-disclosure rule as `GetByIdAsync`. | FR-012 |
| `ReplaceFileAsync(userId, documentId, newFileBytes, newFileMetadata)` | Same authorization as `UpdateMetadataAsync`; re-runs FR-002/FR-003/FR-007 checks on the new file. | FR-012 |
| `DeleteAsync(userId, documentId)` | Allowed for the uploader, or a Project Manager whose project the document is associated with; deletes cascade to `DocumentShare` rows (data-model.md). | FR-013 |
| `ShareAsync(userId, documentId, recipientUserId? , teamProjectId?)` | Only the uploader may share; if `teamProjectId` is set, confirms the uploader has access to that project first. Exactly one of `recipientUserId`/`teamProjectId` must be supplied. | FR-014 |
| `GetSharedWithMeAsync(userId)` | Returns documents with an individual share to `userId` OR a team share whose `TeamProjectId` currently has `userId` as a member (live join, research.md #6). | FR-015 |
| `GetRecentAsync(userId, count = 5)` | Same visibility rule as `GetByIdAsync`, applied to the user's own uploads only (dashboard widget scope per FR-017). | FR-017 |
| `GetActivityReportAsync(adminUserId, filters)` | Restricted to callers with the Administrator role (checked here, not just at the page level). | FR-019 |

## Error semantics

- Authorization failures on a **read** of a specific document return "not found", never
  "forbidden" — this matches this app's existing IDOR-prevention convention and avoids confirming
  a document's existence to a user who shouldn't know about it.
- Validation failures on **upload/replace** (size, type, failed scan) return a specific, distinct
  error per FR-002/FR-003/FR-007 so the UI can show the right message — these are not folded into
  a generic "not found"/"forbidden" pair.
