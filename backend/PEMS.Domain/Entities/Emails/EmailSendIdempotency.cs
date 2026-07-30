using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Emails;

/// <summary>
/// One reservation for one logical "gửi báo cáo/hóa đơn" action (G11 / R-103).
///
/// <para>
/// The row exists so that a retry can be recognised as the same action rather than as a new one. Before
/// it, a report send that timed out in the browser could be pressed again and the server — which never
/// saw the disconnect — would generate a second PDF, write a second history row and send a second email.
/// </para>
/// <para>
/// It stores hashes, never the things they are hashes of: not the key the client generated, not the note
/// the user typed, not the recipient's address, not a single monetary value. What it needs to know is
/// only "is this the same request as before", and a hash answers that without keeping the request.
/// </para>
/// </summary>
[Table("email_send_idempotency")]
public class EmailSendIdempotency
{
    [Key]
    [Column("email_send_idempotency_id")]
    public ulong EmailSendIdempotencyId { get; set; }

    /// <summary>The user who pressed "gửi", read from the validated JWT — never from the payload.</summary>
    [Column("actor_user_id")]
    public ulong ActorUserId { get; set; }

    /// <summary>Which of the six send actions this reservation belongs to.</summary>
    [Column("operation_code")]
    public string OperationCode { get; set; } = null!;

    /// <summary>SHA-256 (lower-case hex) of the client's Idempotency-Key. The key itself is never stored.</summary>
    [Column("idempotency_key_hash")]
    public string IdempotencyKeyHash { get; set; } = null!;

    /// <summary>
    /// SHA-256 (lower-case hex) of the canonicalised business content of the request. Same key with a
    /// different fingerprint is a client bug or a key collision, and is refused rather than sent.
    /// </summary>
    [Column("request_fingerprint")]
    public string RequestFingerprint { get; set; } = null!;

    /// <summary>See <c>EmailSendStates</c> in the Application layer for the state machine.</summary>
    [Column("state")]
    public string State { get; set; } = null!;

    /// <summary>The history row a successful send produced, for reconciliation.</summary>
    [Column("sent_email_id")]
    public ulong? SentEmailId { get; set; }

    /// <summary>The success message, replayed verbatim to a duplicate request.</summary>
    [Column("result_message")]
    public string? ResultMessage { get; set; }

    /// <summary>A stable failure code. Never an address, an amount, a token or message content.</summary>
    [Column("failure_code")]
    public string? FailureCode { get; set; }

    /// <summary>How many times the handler actually ran under this key (a retry after a clean failure).</summary>
    [Column("attempt_count")]
    public uint AttemptCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Set immediately before the outbound call. Its presence is the record that the system can no
    /// longer claim nothing was sent — which is exactly what makes a same-key retry unsafe from here on.
    /// </summary>
    [Column("dispatch_started_at")]
    public DateTime? DispatchStartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
