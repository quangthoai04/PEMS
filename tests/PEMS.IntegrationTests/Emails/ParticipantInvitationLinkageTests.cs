using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 5 — the linkage an invitation depends on, proven against a real MySQL rather than an in-memory
/// stand-in.
///
/// <para>
/// An accept/decline token is only usable if the recipient can be traced back to the message that carried
/// it: <c>email_action_tokens</c> points at both the <c>sent_emails</c> row and the
/// <c>sent_email_recipients</c> row, and both are real foreign keys. That is why the invitation handler
/// records the message INSIDE its transaction (<see cref="ISystemEmailDispatcher.PrepareAsync"/>) and only
/// sends it after committing — a token written before its message exists cannot satisfy the constraint,
/// and a message sent before the commit could survive a rollback that erased the token behind it.
/// </para>
/// <para>
/// Note on the column: <c>sent_email_id</c> is NULLABLE in the schema. "Never null for an invitation" is
/// therefore an invariant the code upholds, not something the database enforces — which is exactly why it
/// is asserted here.
/// </para>
/// </summary>
public sealed class ParticipantInvitationLinkageTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("batch5-linkage@partner.example.com");

    public void Dispose() => _h.Dispose();

    private const string AcceptUrl = "https://pems.test/api/public/email-actions/RAW-LINK-ACCEPT";
    private const string DeclineUrl = "https://pems.test/api/public/email-actions/RAW-LINK-DECLINE";
    private const ulong ParticipantId = 990101;

    private SystemEmailRequest Invitation() => new(
        SystemEmailTemplates.VisitParticipantInvitation,
        new EmailRecipient(_h.Marker, "Nguyễn Văn Bình"),
        new Dictionary<string, string>
        {
            ["recipientName"] = "Nguyễn Văn Bình",
            ["delegationName"] = "Đoàn Đại học Kyoto",
            ["campusName"] = "FPT Đà Nẵng",
            ["plannedTime"] = "09:00 12/08/2026 - 11:30 12/08/2026",
            ["hostName"] = "Trần Thị Hà",
            ["roleLabel"] = "Staff hỗ trợ IC",
            ["hostMessage"] = string.Empty,
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] = EmailComposition.AcceptDeclineBlock(AcceptUrl, DeclineUrl),
        },
        RelatedType: EmailActionTargetTypes.VisitParticipant,
        RelatedId: ParticipantId);

    private static EmailActionToken Token(
        string hash, string action, string groupKey, string email, ulong sentEmailId, ulong recipientId)
        => new()
        {
            TokenHash = hash,
            ActionGroupKey = groupKey,
            ActionContext = EmailActionContexts.ParticipationResponse,
            TargetType = EmailActionTargetTypes.VisitParticipant,
            TargetId = ParticipantId,
            IntendedAction = action,
            RecipientEmail = email,
            SentEmailId = sentEmailId,
            SentEmailRecipientId = recipientId,
            ExpiresAt = DateTime.Now.AddDays(14),
            ResultStatus = EmailActionResultStatuses.Pending,
            CreatedAt = DateTime.Now,
        };

    [Fact]
    public async Task The_message_and_its_tokens_are_written_in_one_transaction_and_sent_after_it_commits()
    {
        EmailEvidenceHarness.RequireDb();
        var groupKey = Guid.NewGuid().ToString("N");
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var dispatcher = _h.Dispatcher(db);

            PreparedSystemEmail prepared;
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                prepared = await dispatcher.PrepareAsync(Invitation());

                // Nothing has been handed to SMTP yet — that is the whole point of preparing separately.
                Assert.Empty(_h.Messages());

                db.EmailActionTokens.Add(Token($"hash-accept-{groupKey}", EmailIntendedActions.Accept,
                    groupKey, _h.Marker, prepared.SentEmailId, prepared.SentEmailRecipientId));
                db.EmailActionTokens.Add(Token($"hash-decline-{groupKey}", EmailIntendedActions.Decline,
                    groupKey, _h.Marker, prepared.SentEmailId, prepared.SentEmailRecipientId));
                await db.SaveChangesAsync();

                await tx.CommitAsync();
            }

            var delivery = await dispatcher.DeliverAsync(prepared);
            Assert.Equal(EmailDeliveryStatus.Sent, delivery.Status);

            // The recipient really got a working link…
            var body = _h.OnlyMessage().Body;
            Assert.Contains(AcceptUrl, body);
            Assert.Contains(DeclineUrl, body);

            using var verify = EmailEvidenceHarness.NewContext();
            var tokens = await verify.EmailActionTokens.AsNoTracking()
                .Where(t => t.ActionGroupKey == groupKey).ToListAsync();

            Assert.Equal(2, tokens.Count);
            Assert.All(tokens, t =>
            {
                Assert.Equal(prepared.SentEmailId, t.SentEmailId);
                Assert.Equal(prepared.SentEmailRecipientId, t.SentEmailRecipientId);
                Assert.Equal(EmailActionResultStatuses.Pending, t.ResultStatus);
                Assert.Null(t.UsedAt);
            });
            Assert.Equal(
                new[] { EmailIntendedActions.Accept, EmailIntendedActions.Decline },
                tokens.Select(t => t.IntendedAction).OrderBy(a => a, StringComparer.Ordinal).ToArray());

            // …and the message it came from is recorded as sent, with the link stripped from the record.
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == prepared.SentEmailId);
            Assert.Equal("SENT", stored.Status);
            Assert.DoesNotContain("RAW-LINK", stored.BodySnapshot ?? string.Empty);
        }
        finally { await CleanupAsync(groupKey); }
    }

    [Fact]
    public async Task A_rollback_leaves_neither_the_message_nor_the_tokens_nor_a_sent_email()
    {
        EmailEvidenceHarness.RequireDb();
        var groupKey = Guid.NewGuid().ToString("N");
        ulong sentEmailId;
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var prepared = await _h.Dispatcher(db).PrepareAsync(Invitation());
                sentEmailId = prepared.SentEmailId;

                db.EmailActionTokens.Add(Token($"hash-rollback-{groupKey}", EmailIntendedActions.Accept,
                    groupKey, _h.Marker, prepared.SentEmailId, prepared.SentEmailRecipientId));
                await db.SaveChangesAsync();

                await tx.RollbackAsync();
            }

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.False(await verify.SentEmails.AsNoTracking().AnyAsync(e => e.SentEmailId == sentEmailId));
            Assert.False(await verify.EmailActionTokens.AsNoTracking().AnyAsync(t => t.ActionGroupKey == groupKey));

            // Because delivery happens only after the commit, a rolled-back invitation is one that was
            // never sent — rather than a live accept link with nothing behind it.
            Assert.Empty(_h.Messages());
        }
        finally { await CleanupAsync(groupKey); }
    }

    [Fact]
    public async Task A_token_cannot_point_at_a_message_that_does_not_exist()
    {
        EmailEvidenceHarness.RequireDb();
        var groupKey = Guid.NewGuid().ToString("N");
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            db.EmailActionTokens.Add(Token($"hash-orphan-{groupKey}", EmailIntendedActions.Accept,
                groupKey, _h.Marker, sentEmailId: 999_999_999, recipientId: 999_999_999));

            // The foreign key is what makes "which message carried this token?" always answerable.
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally { await CleanupAsync(groupKey); }
    }

    private async Task CleanupAsync(string groupKey)
    {
        using var db = EmailEvidenceHarness.NewContext();
        await db.EmailActionTokens.Where(t => t.ActionGroupKey == groupKey).ExecuteDeleteAsync();
        await _h.CleanupAsync();
    }
}
