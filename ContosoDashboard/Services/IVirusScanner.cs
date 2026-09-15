namespace ContosoDashboard.Services;

/// <summary>
/// Abstraction over malware scanning (research.md #1). Kept separate from
/// IFileStorageService so a training environment without ClamAV installed can register
/// NoOpVirusScanner without touching DocumentService.
/// </summary>
public interface IVirusScanner
{
    /// <returns>true when the file is clean and safe to store; false when it should be rejected.</returns>
    Task<bool> ScanAsync(Stream fileStream);
}
