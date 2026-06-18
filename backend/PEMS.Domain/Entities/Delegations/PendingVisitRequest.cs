using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// Temporary storage for visit request form data while the registrant
/// completes OTP email verification. Cleaned up after successful verification
/// or expiry.
/// </summary>
[Table("pending_visit_requests")]
public class PendingVisitRequest
{
    [Key]
    [Column("pending_id")]
    public string PendingId { get; set; } = null!;

    /// <summary>Email used to send the OTP — must match at verification time.</summary>
    [Column("email")]
    public string Email { get; set; } = null!;

    /// <summary>Full form data serialised as JSON for reconstruction after OTP passes.</summary>
    [Column("form_data_json", TypeName = "longtext")]
    public string FormDataJson { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
