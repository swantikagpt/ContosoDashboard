# Contract: IFileStorageService

`ContosoDashboard.Services.IFileStorageService` — the infrastructure abstraction required by
Constitution Principle IV so the training-only local implementation can be swapped for a cloud
implementation later with no change to `DocumentService`, Pages, or the database schema.

```csharp
public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string relativePath, string contentType);
    Task DeleteAsync(string relativePath);
    Task<Stream> DownloadAsync(string relativePath);
    Task<string> GetUrlAsync(string relativePath, TimeSpan expiration);
}
```

- **`UploadAsync`** — writes `fileStream` to `relativePath` (the caller — `DocumentService` —
  already generated the GUID-based path per research.md #4) and returns the path actually stored
  (allows a future cloud implementation to return a blob name/URL if it differs). Training
  implementation (`LocalFileStorageService`) writes under `AppData/uploads/` using
  `System.IO.File`; no network or cloud SDK calls.
- **`DeleteAsync`** — removes the file at `relativePath`. Training implementation deletes the
  local file; a production `AzureBlobStorageService` would delete the corresponding blob.
- **`DownloadAsync`** — returns a readable `Stream` for the file at `relativePath`, used by the
  download/preview endpoints (contracts/document-download-endpoint.md). Training implementation
  opens a local `FileStream`.
- **`GetUrlAsync`** — returns a URL that can be used to fetch the file directly, with an
  expiration. Training implementation returns the app's own authorized download endpoint path
  (expiration is not meaningful locally, but the signature stays cloud-shaped for the eventual
  swap to Azure Blob Storage SAS URLs).

## Compliance requirement

`DocumentService` MUST depend only on `IFileStorageService` (injected via DI), never on
`System.IO` directly and never on any cloud SDK type. `Program.cs` registers the concrete
`LocalFileStorageService` for this feature; swapping to a cloud implementation is a one-line DI
registration change per Constitution Principle IV.
