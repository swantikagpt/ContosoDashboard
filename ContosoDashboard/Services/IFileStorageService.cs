namespace ContosoDashboard.Services;

/// <summary>
/// Infrastructure abstraction (Constitution Principle IV): DocumentService depends only on this
/// interface, never on System.IO or a cloud SDK directly, so the training-only local
/// implementation can be swapped for a cloud implementation via DI registration alone.
/// </summary>
public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string relativePath, string contentType);
    Task DeleteAsync(string relativePath);
    Task<Stream> DownloadAsync(string relativePath);
    Task<string> GetUrlAsync(string relativePath, TimeSpan expiration);
}
