namespace PEMS.Domain.Enums;

/// <summary>
/// Lifecycle of an editable email draft. Names map 1:1 to the SQL
/// ENUM('DRAFT','SENT','DISCARDED') strings. A draft is never hard-deleted: it moves to SENT
/// (linked to the produced sent_email) or DISCARDED.
/// </summary>
public enum EmailDraftStatus
{
    DRAFT,
    SENT,
    DISCARDED,
}
