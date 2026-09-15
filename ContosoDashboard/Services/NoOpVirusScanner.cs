namespace ContosoDashboard.Services;

/// <summary>
/// Always-allow scanner for local dev environments without ClamAV installed
/// (research.md #1 follow-up). Never registered outside local development.
/// </summary>
public class NoOpVirusScanner : IVirusScanner
{
    public Task<bool> ScanAsync(Stream fileStream) => Task.FromResult(true);
}
