using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.ApiIntegrations;

[Table("api_request_logs")]
public class ApiRequestLog
{
    [Key]
    [Column("api_request_log_id")]
    public long ApiRequestLogId { get; set; }

    [Column("api_config_id")]
    public string ApiConfigId { get; set; } = null!;

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("requested_by")]
    public string? RequestedBy { get; set; }

    [Column("related_type")]
    public string? RelatedType { get; set; }

    [Column("related_id")]
    public string? RelatedId { get; set; }

    [Column("endpoint")]
    public string Endpoint { get; set; } = null!;

    [Column("method")]
    public string Method { get; set; } = null!;

    [Column("http_status")]
    public int? HttpStatus { get; set; }

    [Column("response_time_ms")]
    public int? ResponseTimeMs { get; set; }

    [Column("request_size_bytes")]
    public long? RequestSizeBytes { get; set; }

    [Column("response_size_bytes")]
    public long? ResponseSizeBytes { get; set; }

    [Column("success")]
    public bool Success { get; set; }

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual ApiConfiguration ApiConfiguration { get; set; } = null!;
}
