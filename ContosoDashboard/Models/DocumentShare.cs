using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

/// <summary>
/// Exactly one of RecipientUserId (individual share) or TeamProjectId (team share) must be set;
/// enforced in DocumentService, not by a database constraint (data-model.md).
/// </summary>
public class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    public int? RecipientUserId { get; set; }

    public int? TeamProjectId { get; set; }

    [Required]
    public int SharedByUserId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("DocumentId")]
    public virtual Document Document { get; set; } = null!;

    [ForeignKey("RecipientUserId")]
    public virtual User? RecipientUser { get; set; }

    [ForeignKey("TeamProjectId")]
    public virtual Project? TeamProject { get; set; }

    [ForeignKey("SharedByUserId")]
    public virtual User SharedByUser { get; set; } = null!;
}
