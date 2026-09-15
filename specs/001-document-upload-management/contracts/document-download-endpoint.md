# Contract: Document download/preview HTTP endpoints

Files live outside `wwwroot` (research.md #4), so they cannot be served as static files. These two
Minimal API endpoints (registered in `Program.cs` alongside the existing Blazor Hub mapping) are
the only HTTP-level access path to file bytes, and both require authentication.

## `GET /api/documents/{id}/download`

- **Auth**: Requires an authenticated session (existing cookie auth). Additionally delegates the
  authorization decision to `IDocumentService.GetByIdAsync(currentUserId, id)` — if that returns
  nothing, this endpoint returns `404 Not Found` (never `403`, per the document-service contract's
  non-disclosure rule).
- **Success**: `200 OK`, body = file bytes from `IFileStorageService.DownloadAsync`,
  `Content-Type` = the document's stored `FileType`, `Content-Disposition: attachment;
  filename="{Document.FileName}"` (original filename restored for the user; never the internal
  GUID-based storage path).
- **Side effect**: Records a `DocumentActivityLog` row with `ActionType = Download` (FR-019).

## `GET /api/documents/{id}/preview`

- **Auth**: Identical authorization check to `download`.
- **Applicability**: Only meaningful for PDF and image (`JPEG`/`PNG`) file types (FR-011); for any
  other file type this endpoint returns `400 Bad Request` and the UI should not offer a preview
  affordance for those types.
- **Success**: `200 OK`, same body/`Content-Type` as `download`, but
  `Content-Disposition: inline` so the browser renders the file instead of downloading it.
- **Side effect**: None — previewing is not logged as a `Download` activity (distinct from
  actually downloading the file).

## Shared error responses

| Status | When |
|---|---|
| `401 Unauthorized` | No authenticated session (existing cookie auth middleware). |
| `404 Not Found` | Document does not exist, or the caller is not authorized to see it (identical response in both cases — no existence disclosure). |
| `400 Bad Request` | `/preview` requested for a non-previewable file type. |
