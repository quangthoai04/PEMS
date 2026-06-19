using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("user_auth_providers")]
public class UserAuthProvider
{
    [Key]
    [Column("auth_provider_id")]
    public ulong AuthProviderId { get; set; }

    [Column("user_id")]
    public ulong UserId { get; set; }

    [Column("provider_type")]
    public string ProviderType { get; set; } = null!;

    [Column("provider_subject")]
    public string? ProviderSubject { get; set; }

    [Column("provider_email")]
    public string? ProviderEmail { get; set; }

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("linked_at")]
    public DateTime LinkedAt { get; set; }

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
