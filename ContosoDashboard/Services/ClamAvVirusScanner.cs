using nClam;

namespace ContosoDashboard.Services;

/// <summary>
/// Scans files with a locally-running ClamAV daemon (clamd) over TCP — no outbound network
/// call, satisfying Constitution Principle I (research.md #1).
/// </summary>
public class ClamAvVirusScanner : IVirusScanner
{
    private readonly string _host;
    private readonly int _port;

    public ClamAvVirusScanner(IConfiguration configuration)
    {
        _host = configuration["ClamAv:Host"] ?? "localhost";
        _port = int.TryParse(configuration["ClamAv:Port"], out var port) ? port : 3310;
    }

    public async Task<bool> ScanAsync(Stream fileStream)
    {
        var clam = new ClamClient(_host, _port);
        var result = await clam.SendAndScanFileAsync(fileStream);

        return result.Result == ClamScanResults.Clean;
    }
}
