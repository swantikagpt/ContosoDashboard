namespace ContosoDashboard.Services;

/// <summary>
/// Training/local implementation of IFileStorageService using System.IO under
/// ContosoDashboard/AppData/uploads/, outside wwwroot so files are never served as static
/// content (contracts/file-storage-service.md). A future AzureBlobStorageService implementation
/// would satisfy the same interface with no changes to DocumentService.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "AppData", "uploads");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string relativePath, string contentType)
    {
        var fullPath = ResolveFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(destination);

        return relativePath;
    }

    public Task DeleteAsync(string relativePath)
    {
        var fullPath = ResolveFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string relativePath)
    {
        var fullPath = ResolveFullPath(relativePath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task<string> GetUrlAsync(string relativePath, TimeSpan expiration)
    {
        // Training implementation has no signed-URL concept; the app's own authorized
        // download endpoint is the access path (contracts/document-download-endpoint.md).
        // Signature kept cloud-shaped for a future Azure Blob SAS URL implementation.
        return Task.FromResult($"/api/documents/download-by-path?path={Uri.EscapeDataString(relativePath)}");
    }

    private string ResolveFullPath(string relativePath)
    {
        // relativePath is always generated internally as {userId}/{projectId-or-"personal"}/{guid}.{ext}
        // (research.md #4) — never derived from user-supplied input — so this cannot escape _rootPath.
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        if (!fullPath.StartsWith(_rootPath, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Resolved path escapes the storage root.");
        }

        return fullPath;
    }
}
