using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

/// <summary>
/// contracts/document-service.md — the sole entry point Pages call for document business logic
/// (Constitution Principle II). Every method independently enforces authorization
/// (Constitution Principle III); callers must never rely on page-level [Authorize] alone.
/// Authorization failures on a read of a specific document return null/empty — never an
/// exception — so callers can render "not found" without confirming the document's existence
/// to an unauthorized caller.
/// </summary>
public interface IDocumentService
{
    Task<DocumentUploadResult> UploadAsync(int uploaderUserId, DocumentUploadRequest request);

    Task<List<Document>> GetMyDocumentsAsync(int userId, DocumentSortBy sortBy = DocumentSortBy.UploadDate, DocumentFilter? filter = null);

    Task<List<Document>> GetProjectDocumentsAsync(int userId, int projectId);

    Task<List<Document>> SearchAsync(int userId, string query);

    Task<Document?> GetByIdAsync(int userId, int documentId);
}

public class DocumentUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public int? ProjectId { get; set; }
    public int? TaskId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public Stream FileStream { get; set; } = Stream.Null;
}

public class DocumentUploadResult
{
    public bool Success { get; set; }
    public Document? Document { get; set; }
    public string? ErrorMessage { get; set; }

    public static DocumentUploadResult Ok(Document document) => new() { Success = true, Document = document };
    public static DocumentUploadResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

public enum DocumentSortBy
{
    Title,
    UploadDate,
    Category,
    FileSize
}

public class DocumentFilter
{
    public string? Category { get; set; }
    public int? ProjectId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
