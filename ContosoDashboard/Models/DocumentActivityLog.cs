using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentActivityLog
{
    [Key]
    public int DocumentActivityLogId { get; set; }

    public int? DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string DocumentTitleSnapshot { get; set; } = string.Empty;

    [Required]
    public DocumentActivityType ActionType { get; set; }

    [Required]
    public int PerformedByUserId { get; set; }

    public DateTime OccurredDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("DocumentId")]
    public virtual Document? Document { get; set; }

    [ForeignKey("PerformedByUserId")]
    public virtual User PerformedByUser { get; set; } = null!;
}

public enum DocumentActivityType
{
    Upload,
    Download,
    Delete,
    Share
}
