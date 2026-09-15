using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public class DocumentService : IDocumentService
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // FR-003

    private static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png"
    };

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IVirusScanner _virusScanner;

    public DocumentService(ApplicationDbContext context, IFileStorageService fileStorageService, IVirusScanner virusScanner)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _virusScanner = virusScanner;
    }

    public async Task<DocumentUploadResult> UploadAsync(int uploaderUserId, DocumentUploadRequest request)
    {
        // FR-005: title and category are required
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return DocumentUploadResult.Fail("A document title is required.");
        }

        if (!DocumentCategories.AllowedValues.Contains(request.Category))
        {
            return DocumentUploadResult.Fail("Select a valid category.");
        }

        // FR-003: reject files over 25 MB
        if (request.FileSizeBytes > MaxFileSizeBytes)
        {
            return DocumentUploadResult.Fail("This file exceeds the 25 MB size limit.");
        }

        // FR-002: reject unsupported file types
        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return DocumentUploadResult.Fail("This file type is not supported.");
        }

        // If uploading to a project (directly or via a task), the uploader must be a member or the project manager
        var effectiveProjectId = request.ProjectId;
        if (request.TaskId.HasValue)
        {
            var task = await _context.Tasks.FindAsync(request.TaskId.Value);
            effectiveProjectId = task?.ProjectId;
        }

        if (effectiveProjectId.HasValue)
        {
            var hasProjectAccess = await _context.Projects.AnyAsync(p =>
                p.ProjectId == effectiveProjectId.Value &&
                (p.ProjectManagerId == uploaderUserId || p.ProjectMembers.Any(pm => pm.UserId == uploaderUserId)));

            if (!hasProjectAccess)
            {
                return DocumentUploadResult.Fail("You are not a member of the selected project.");
            }
        }

        // FR-007: scan before any storage write
        var isClean = await _virusScanner.ScanAsync(request.FileStream);
        if (!isClean)
        {
            return DocumentUploadResult.Fail("This file failed a malware scan and was not uploaded.");
        }

        // research.md #4: generate the path, write the file, THEN insert the database row
        var relativePath = BuildRelativePath(uploaderUserId, effectiveProjectId, extension);
        request.FileStream.Position = 0;
        await _fileStorageService.UploadAsync(request.FileStream, relativePath, request.ContentType);

        var document = new Document
        {
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Tags = request.Tags,
            FileName = request.FileName,
            FilePath = relativePath,
            FileSizeBytes = request.FileSizeBytes,
            FileType = request.ContentType,
            UploadedByUserId = uploaderUserId,
            ProjectId = effectiveProjectId,
            TaskId = request.TaskId,
            UploadedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // FR-019: record the upload in the activity log
        _context.DocumentActivityLogs.Add(new DocumentActivityLog
        {
            DocumentId = document.DocumentId,
            DocumentTitleSnapshot = document.Title,
            ActionType = DocumentActivityType.Upload,
            PerformedByUserId = uploaderUserId,
            OccurredDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return DocumentUploadResult.Ok(document);
    }

    public async Task<List<Document>> GetMyDocumentsAsync(int userId, DocumentSortBy sortBy = DocumentSortBy.UploadDate, DocumentFilter? filter = null)
    {
        // FR-008: scoped to the caller's own uploads only — cannot be parameterized to another user's ID
        var query = _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d => d.UploadedByUserId == userId);

        query = ApplyFilter(query, filter);

        return await ApplySort(query, sortBy).ToListAsync();
    }

    public async Task<List<Document>> GetProjectDocumentsAsync(int userId, int projectId)
    {
        // FR-009: only current project members (or the project manager) can list a project's documents
        var hasAccess = await _context.Projects.AnyAsync(p =>
            p.ProjectId == projectId &&
            (p.ProjectManagerId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId)));

        if (!hasAccess)
        {
            return new List<Document>();
        }

        return await _context.Documents
            .Include(d => d.UploadedByUser)
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.UploadedDate)
            .ToListAsync();
    }

    public async Task<List<Document>> SearchAsync(int userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<Document>();
        }

        var lowered = query.ToLower();

        // FR-010: candidate matches, authorization applied per-row below (never disclose existence)
        var candidates = await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d =>
                d.Title.ToLower().Contains(lowered) ||
                (d.Description != null && d.Description.ToLower().Contains(lowered)) ||
                (d.Tags != null && d.Tags.ToLower().Contains(lowered)) ||
                d.UploadedByUser.DisplayName.ToLower().Contains(lowered) ||
                (d.Project != null && d.Project.Name.ToLower().Contains(lowered)))
            .ToListAsync();

        var authorized = new List<Document>();
        foreach (var candidate in candidates)
        {
            if (await IsAuthorizedAsync(userId, candidate))
            {
                authorized.Add(candidate);
            }
        }

        return authorized;
    }

    public async Task<Document?> GetByIdAsync(int userId, int documentId)
    {
        var document = await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null)
        {
            return null;
        }

        return await IsAuthorizedAsync(userId, document) ? document : null;
    }

    /// <summary>
    /// Central authorization check reused by SearchAsync, GetByIdAsync, and (in later phases)
    /// the download/preview endpoints. Team shares are evaluated live against current
    /// ProjectMember rows, never a snapshot (research.md #6).
    /// </summary>
    private async Task<bool> IsAuthorizedAsync(int userId, Document document)
    {
        if (document.UploadedByUserId == userId)
        {
            return true;
        }

        var callingUser = await _context.Users.FindAsync(userId);
        if (callingUser?.Role == UserRole.Administrator)
        {
            return true;
        }

        if (document.ProjectId.HasValue)
        {
            var isProjectMember = await _context.Projects.AnyAsync(p =>
                p.ProjectId == document.ProjectId.Value &&
                (p.ProjectManagerId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId)));

            if (isProjectMember)
            {
                return true;
            }
        }

        var isSharedWithMe = await _context.DocumentShares.AnyAsync(s =>
            s.DocumentId == document.DocumentId &&
            (s.RecipientUserId == userId ||
             (s.TeamProjectId != null && _context.ProjectMembers.Any(pm => pm.ProjectId == s.TeamProjectId && pm.UserId == userId))));

        return isSharedWithMe;
    }

    private static IQueryable<Document> ApplyFilter(IQueryable<Document> query, DocumentFilter? filter)
    {
        if (filter == null)
        {
            return query;
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(d => d.Category == filter.Category);
        }

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(d => d.ProjectId == filter.ProjectId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(d => d.UploadedDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(d => d.UploadedDate <= filter.ToDate.Value);
        }

        return query;
    }

    private static IQueryable<Document> ApplySort(IQueryable<Document> query, DocumentSortBy sortBy)
    {
        return sortBy switch
        {
            DocumentSortBy.Title => query.OrderBy(d => d.Title),
            DocumentSortBy.Category => query.OrderBy(d => d.Category),
            DocumentSortBy.FileSize => query.OrderByDescending(d => d.FileSizeBytes),
            _ => query.OrderByDescending(d => d.UploadedDate)
        };
    }

    private static string BuildRelativePath(int userId, int? projectId, string extension)
    {
        var scope = projectId.HasValue ? projectId.Value.ToString() : "personal";
        return Path.Combine(userId.ToString(), scope, $"{Guid.NewGuid()}{extension}");
    }
}
