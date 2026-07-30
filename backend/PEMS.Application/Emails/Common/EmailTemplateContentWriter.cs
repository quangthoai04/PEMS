using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>The six operator-editable content fields, as one value to be written.</summary>
public sealed record EmailTemplateContentWrite(
    string Name,
    string? Description,
    string? SubjectVi,
    string? BodyVi,
    string? SubjectEn,
    string? BodyEn);

/// <summary>What a successful content write produced.</summary>
public sealed record EmailTemplateWriteResult(uint Revision, DateTime UpdatedAt);

/// <summary>
/// The single way template content is written, shared by UC-44 update and restore-to-default.
///
/// <para>
/// <b>Why one writer.</b> Update and restore differ only in where the six field values come from. Giving
/// them separate SQL would mean two chances to get the concurrency condition wrong, and a test proving
/// one says nothing about the other.
/// </para>
/// <para>
/// <b>Why raw SQL instead of the change tracker.</b> The version condition has to be evaluated by the
/// database, in the same statement as the write. A handler that loads the row, compares revisions in C#
/// and then saves has a window between the comparison and the write, and two requests inside that window
/// both pass the check and the later one wins silently — the lost update this column exists to prevent.
/// Here the database decides: only one statement can match <c>revision = :expected</c>, and the loser
/// gets <c>rowsAffected = 0</c>, having written nothing at all.
/// </para>
/// <para>
/// EF's own concurrency-token support was considered and rejected: it puts the value it LOADED into the
/// WHERE clause, but the value that must be checked is the one the operator was shown. Somebody who
/// opened revision 4, left the form open while a colleague saved revision 5, and then submits has to be
/// refused — and with EF's token they would not be, because the handler's own fresh read already sees 5.
/// </para>
/// </summary>
public static class EmailTemplateContentWriter
{
    /// <summary>
    /// Writes the six content fields plus the registry-derived <c>variables_text</c>, if and only if the
    /// stored revision is still <paramref name="expectedRevision"/>, and bumps the revision by one.
    /// </summary>
    /// <exception cref="ConflictException">
    /// <c>EMAIL_TEMPLATE_CONCURRENCY_CONFLICT</c> when no row matched. The payload carries the revision
    /// actually stored, so the screen can say what the editor has to reload to.
    /// </exception>
    public static async Task<EmailTemplateWriteResult> WriteAsync(
        IApplicationDbContext db,
        ulong emailTemplateId,
        uint expectedRevision,
        EmailTemplateContentWrite content,
        string variablesText,
        ulong? actorUserId,
        CancellationToken cancellationToken)
    {
        // Truncated to the second before it is stored. The column is DATETIME with no fractional part and
        // MySQL ROUNDS on insert, so …:00.600 comes back as …:01 — a value one second away from the one
        // handed to the caller. updated_at is no longer the concurrency token, but it is still displayed,
        // and "saved at a time one second in the future" is a defect on its own.
        var now = Truncate(VietnamTime.Now());

        // revision = revision + 1 is computed by the server, not passed in: reading it, adding one in C#
        // and writing the result back is the very read-modify-write this guards against.
        //
        // NULLIF rather than DBNull.Value parameters: EF's raw-SQL parameter builder has no store type
        // for DBNull and throws before the statement is ever sent. Empty string is a safe stand-in for
        // NULL in these five columns because every reader in the system already treats '' and NULL
        // identically (the seed's own stage check tests `col IS NULL OR col = ''`), and updated_by uses 0
        // because user ids are BIGINT UNSIGNED starting at 1, so 0 can never collide with a real actor.
        var affected = await db.Database.ExecuteSqlRawAsync(
            @"UPDATE email_templates
                 SET name           = {1},
                     description    = NULLIF({2}, ''),
                     subject_vi     = NULLIF({3}, ''),
                     body_vi        = NULLIF({4}, ''),
                     subject_en     = NULLIF({5}, ''),
                     body_en        = NULLIF({6}, ''),
                     variables_text = {7},
                     revision       = revision + 1,
                     updated_at     = {8},
                     updated_by     = NULLIF({9}, 0)
               WHERE email_template_id = {0}
                 AND revision = {10}",
            new object[]
            {
                emailTemplateId,
                content.Name,
                content.Description ?? string.Empty,
                content.SubjectVi ?? string.Empty,
                content.BodyVi ?? string.Empty,
                content.SubjectEn ?? string.Empty,
                content.BodyEn ?? string.Empty,
                variablesText,
                now,
                actorUserId ?? 0UL,
                expectedRevision,
            },
            cancellationToken);

        if (affected == 1)
            return new EmailTemplateWriteResult(expectedRevision + 1, now);

        // Nothing was written. Report the revision that IS stored so the screen can be specific; a
        // deleted row (revision null) is reported the same way rather than as a 404, because from the
        // editor's point of view both mean "reload, what you have is stale".
        var current = await db.EmailTemplates
            .AsNoTracking()
            .Where(t => t.EmailTemplateId == emailTemplateId)
            .Select(t => (uint?)t.Revision)
            .FirstOrDefaultAsync(cancellationToken);

        throw new ConflictException(
            "Mẫu email này đã được người khác cập nhật sau khi bạn mở nó. " +
            "Vui lòng tải lại để xem nội dung mới nhất rồi áp dụng lại thay đổi của bạn.",
            EmailErrorCodes.TemplateConcurrencyConflict,
            new { expectedRevision, currentRevision = current });
    }

    private static DateTime Truncate(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);
}
