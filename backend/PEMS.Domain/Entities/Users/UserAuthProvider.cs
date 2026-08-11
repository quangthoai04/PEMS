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

    /// <summary>LOCAL_PASSWORD or GOOGLE_SSO (see <c>ProviderTypes</c>).</summary>
    [Column("provider_type")]
    public string ProviderType { get; set; } = null!;

    /// <summary>
    /// Provider-issued subject id. Required for GOOGLE_SSO (enforced by
    /// <c>trg_auth_providers_validate_bi/bu</c>); always NULL for LOCAL_PASSWORD.
    /// </summary>
    [Column("provider_subject")]
    public string? ProviderSubject { get; set; }

    [Column("linked_at")]
    public DateTime LinkedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
