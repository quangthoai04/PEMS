using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_request_fingerprint_guards")]
public class VisitRequestFingerprintGuard
{
    [Key]
    [Column("fingerprint", TypeName = "varchar(64)")]
    public string Fingerprint { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
