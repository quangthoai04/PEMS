using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.ApiIntegrations;

[Table("api_configuration_headers")]
public class ApiConfigurationHeader
{
    [Key]
    [Column("api_configuration_header_id")]
    public ulong ApiConfigurationHeaderId { get; set; }

    [Column("api_config_id")]
    public ulong ApiConfigId { get; set; }

    [Column("header_name")]
    public string HeaderName { get; set; } = null!;

    [Column("header_value_encrypted")]
    public string? HeaderValueEncrypted { get; set; }

    [Column("is_secret")]
    public bool IsSecret { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual ApiConfiguration ApiConfiguration { get; set; } = null!;
}
