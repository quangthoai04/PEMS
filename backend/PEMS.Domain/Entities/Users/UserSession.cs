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

    [Column("auth_provider_id")]
    public ulong? AuthProviderId { get; set; }

    [Column("refresh_token_hash")]
    public string? RefreshTokenHash { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The single expiry of the session AND of its refresh token — they are the same lifetime,
    /// so there is no separate refresh_expires_at to drift out of sync with.
    /// </summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// The single revocation instant of the session AND of its refresh token. Once set, both
    /// the access-token session check and the refresh lookup reject this row.
    /// </summary>
    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("revoked_by")]
    public ulong? RevokedBy { get; set; }

    [Column("revoked_reason")]
    public string? RevokedReason { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual UserAuthProvider? AuthProvider { get; set; }
}
