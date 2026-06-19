using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("user_sessions")]
public class UserSession
{
    [Key]
    [Column("session_id")]
    public ulong SessionId { get; set; }

    [Column("user_id")]
    public ulong UserId { get; set; }

    [Column("login_portal")]
    public string LoginPortal { get; set; } = null!;

    [Column("selected_campus_id")]
    public ulong? SelectedCampusId { get; set; }

    [Column("auth_provider_id")]
    public ulong? AuthProviderId { get; set; }

    [Column("refresh_token_hash")]
    public string? RefreshTokenHash { get; set; }

    [Column("refresh_expires_at")]
    public DateTime? RefreshExpiresAt { get; set; }

    [Column("refresh_revoked_at")]
    public DateTime? RefreshRevokedAt { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("revoked_by")]
    public ulong? RevokedBy { get; set; }

    [Column("revoked_reason")]
    public string? RevokedReason { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual UserAuthProvider? AuthProvider { get; set; }
}
