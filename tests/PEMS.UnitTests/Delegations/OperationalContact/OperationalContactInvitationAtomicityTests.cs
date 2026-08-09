using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.OperationalContact;

/// <summary>
/// One rule, checked on each of the three commands that issue an operational-contact link: the link is
/// written in the SAME unit of work as the invitation it answers, and the email goes out afterwards.
///
/// <para>
/// What these tests can prove, and it is the part that used to be wrong, is WHICH SIDE OF THE COMMIT
/// each step falls on: the mint between <c>tx-begin</c> and <c>tx-commit</c>, the email strictly after
/// <c>tx-commit</c>. That distinction is the whole fix — the old code also minted, saved and sent in
/// that order, only it did all three AFTER the commit, so an ordering assertion that ignored the
/// transaction boundary would have passed on the bug.
/// </para>
/// <para>
/// What they CANNOT prove is rollback. The InMemory provider ignores transactions, so a mint failure
/// here leaves the earlier writes in the store no matter how the handler is written. Every assertion
/// about a failed mint is therefore about what the handler DOES — propagate, and send nothing — never
/// about what the database keeps. That half needs a real MySQL and is out of scope by agreement.
/// </para>
/// </summary>
public class OperationalContactInvitationAtomicityTests
{
    private static readonly DateTime Now = new(2026, 8, 9, 10, 0, 0);

    private const ulong RegistrantId = 500;
    private const ulong RequestId = DelegationsTestData.VisitRequestId;
    private const ulong InstanceId = DelegationsTestData.VisitInstanceId;

    // ─────────────────────────── Resend ───────────────────────────

    [Fact]
    public async Task Resend_mints_the_new_link_inside_the_transaction_and_mails_after_it()
    {
        var (db, invitations) = await ArrangeWithPendingInvitationAsync();

        await ResendHandler(db, invitations).Handle(
            new ResendOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        AssertMintedInsideTransactionAndSentAfter(db.Journal);
    }

    [Fact]
    public async Task Resend_mints_for_the_bumped_version_and_counts_the_send()
    {
        var (db, invitations) = await ArrangeWithPendingInvitationAsync();

        var response = await ResendHandler(db, invitations).Handle(
            new ResendOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        // v1 was the invitation as arranged; the new link belongs to v2, never to the version whose
        // links this command has just killed.
        var mint = Assert.Single(invitations.Mints);
        Assert.Equal(2u, mint.TokenVersion);
        Assert.Equal(2u, response.TokenVersion);
        Assert.Equal(1u, response.ResendCount);

        var change = await db.VisitRequestIdentityChanges.SingleAsync();
        Assert.Equal(2u, change.TokenVersion);
        Assert.Equal(1u, change.ResendCount);
    }

    [Fact]
    public async Task Resend_kills_the_old_links_and_leaves_exactly_one_live_group()
    {
        var (db, invitations) = await ArrangeWithPendingInvitationAsync();
        await SeedLiveTokensAsync(db, tokenVersion: 1);

        await ResendHandler(db, invitations).Handle(
            new ResendOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        var tokens = await db.EmailActionTokens.AsNoTracking().ToListAsync();
        var live = tokens.Where(t => t.ResultStatus == EmailActionResultStatuses.Pending && t.UsedAt is null).ToList();

        // Two links (accept + decline), one group, and it is the NEW version's.
        Assert.Equal(2, live.Count);
        Assert.Single(live.Select(t => t.ActionGroupKey).Distinct());
        Assert.All(live, t => Assert.EndsWith(":2", t.ActionGroupKey));
        Assert.All(tokens.Where(t => t.ActionGroupKey!.EndsWith(":1")),
            t => Assert.Equal(EmailActionResultStatuses.Invalid, t.ResultStatus));
    }

    [Fact]
    public async Task Resend_sends_one_email_and_only_one()
    {
        var (db, invitations) = await ArrangeWithPendingInvitationAsync();

        await ResendHandler(db, invitations).Handle(
            new ResendOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        Assert.Equal(new[] { InvitationIdOf(db) }, invitations.Dispatches);
    }

    [Fact]
    public async Task Resend_propagates_a_token_failure_and_sends_nothing()
    {
        var (db, invitations) = await ArrangeWithPendingInvitationAsync();
        invitations.FailMintWith = new InvalidOperationException("token store unavailable");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResendHandler(db, invitations).Handle(
                new ResendOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None));

        // Not swallowed, not converted into a "sent" answer: the caller is told, the transaction is
        // never committed, and nobody is emailed a link that was never made. (That an uncommitted
        // transaction actually UNDOES the version bump is a database property this provider cannot
        // show — see the class remarks.)
        Assert.Equal("token store unavailable", thrown.Message);
        Assert.DoesNotContain("tx-commit", db.Journal);
        Assert.Empty(invitations.Dispatches);
    }

    [Fact]
    public async Task Resend_keeps_the_committed_state_when_the_mail_provider_fails()
    {
        var (db, invitations) = await ArrangeWithPendingInvitationAsync();
        invitations.FailDispatchWith = new InvalidOperationException("smtp down");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResendHandler(db, invitations).Handle(
                new ResendOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None));

        // The delivery is the ONLY thing that failed. The bump, the events and the new links were
        // already saved before the dispatcher was called — a mail outage is not a rollback.
        var change = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync();
        Assert.Equal(2u, change.TokenVersion);
        Assert.Equal(IdentityChangeStatuses.Pending, change.Status);
        Assert.Equal(2, await db.EmailActionTokens
            .CountAsync(t => t.ResultStatus == EmailActionResultStatuses.Pending && t.UsedAt == null));
    }

    // ─────────────────────────── Reinvite ───────────────────────────

    [Fact]
    public async Task Reinvite_mints_inside_the_transaction_and_mails_after_it()
    {
        var (db, invitations) = await ArrangeAwaitingContactAsync();

        await ReinviteHandler(db, invitations).Handle(
            new ReinviteOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        AssertMintedInsideTransactionAndSentAfter(db.Journal);

        // The links belong to the invitation this command just wrote, and there are exactly two.
        var invitation = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync();
        Assert.Equal(new[] { invitation.IdentityChangeId }, invitations.Dispatches);
        Assert.Equal(2, await db.EmailActionTokens.CountAsync(t => t.TargetId == invitation.IdentityChangeId));
    }

    [Fact]
    public async Task Reinvite_propagates_a_token_failure_and_sends_nothing()
    {
        var (db, invitations) = await ArrangeAwaitingContactAsync();
        invitations.FailMintWith = new InvalidOperationException("token store unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReinviteHandler(db, invitations).Handle(
                new ReinviteOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None));

        Assert.DoesNotContain("tx-commit", db.Journal);
        Assert.Empty(invitations.Dispatches);
    }

    // ─────────────────────────── Replace ───────────────────────────

    [Fact]
    public async Task Replace_mints_inside_the_transaction_and_mails_after_it()
    {
        var (db, invitations) = await ArrangeAwaitingContactAsync();

        await ReplaceHandler(db, invitations).Handle(
            NewContactCommand("nguoi.moi@doitac.local"), CancellationToken.None);

        AssertMintedInsideTransactionAndSentAfter(db.Journal);

        var invitation = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync();
        Assert.Equal(IdentityChangeKinds.InitialConfirmation, invitation.ChangeKind);
        Assert.Equal(new[] { invitation.IdentityChangeId }, invitations.Dispatches);
        Assert.Equal(2, await db.EmailActionTokens.CountAsync(t => t.TargetId == invitation.IdentityChangeId));
    }

    [Fact]
    public async Task Replace_propagates_a_token_failure_and_sends_nothing()
    {
        var (db, invitations) = await ArrangeAwaitingContactAsync();
        invitations.FailMintWith = new InvalidOperationException("token store unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReplaceHandler(db, invitations).Handle(
                NewContactCommand("nguoi.moi@doitac.local"), CancellationToken.None));

        Assert.DoesNotContain("tx-commit", db.Journal);
        Assert.Empty(invitations.Dispatches);
    }

    [Fact]
    public async Task Replace_with_the_registrants_own_address_mints_nothing_and_sends_nothing()
    {
        var (db, invitations) = await ArrangeAwaitingContactAsync();

        // The registrant proved this address at submit time, so they are linked outright — there is no
        // invitation to answer, hence no link to mint and no email to fail on.
        var response = await ReplaceHandler(db, invitations).Handle(
            NewContactCommand("guest@test.local"), CancellationToken.None);

        Assert.True(response.ContactConfirmed);
        Assert.Empty(invitations.Mints);
        Assert.Empty(invitations.Dispatches);
        Assert.Empty(await db.VisitRequestIdentityChanges.ToListAsync());
    }

    // ─────────────────────────── Fixtures ───────────────────────────

    private static ReplaceOperationalContactCommand NewContactCommand(string email) =>
        new(RequestId, InstanceId, "Người Mới", "Đối tác", "Trưởng phòng", "0900000009", email);

    private static int IndexOfPrefix(IReadOnlyList<string> journal, string prefix)
    {
        for (var i = 0; i < journal.Count; i++)
            if (journal[i].StartsWith(prefix, StringComparison.Ordinal))
                return i;
        return -1;
    }

    /// <summary>
    /// The invariant itself: the link was minted inside the business transaction, a flush made it
    /// durable with the change, the transaction committed, and only then did anything try to send an
    /// email. Reads the whole journal so a failure message shows the order that actually happened.
    /// </summary>
    private static void AssertMintedInsideTransactionAndSentAfter(IReadOnlyList<string> journal)
    {
        var trace = $"journal was [{string.Join(" → ", journal)}]";
        var begin = IndexOfPrefix(journal, "tx-begin");
        var mint = IndexOfPrefix(journal, "mint:");
        var commit = IndexOfPrefix(journal, "tx-commit");
        var dispatch = IndexOfPrefix(journal, "dispatch:");

        Assert.True(begin >= 0, $"the handler opened no transaction — {trace}");
        Assert.True(mint >= 0, $"no link was minted — {trace}");
        Assert.True(commit >= 0, $"the transaction never committed — {trace}");
        Assert.True(dispatch >= 0, $"no invitation email was sent — {trace}");

        Assert.True(begin < mint && mint < commit,
            $"the link must be minted INSIDE the business transaction — {trace}");
        Assert.Contains("save", journal.Skip(mint + 1).Take(commit - mint - 1));
        Assert.True(commit < dispatch,
            $"the email must be sent only after the commit — {trace}");
    }

    private static ulong InvitationIdOf(OperationalContactTestDbContext db) =>
        db.VisitRequestIdentityChanges.AsNoTracking().Single().IdentityChangeId;

    /// <summary>A campus still at the contact gate, whose contact is somebody other than the registrant.</summary>
    private static async Task<(OperationalContactTestDbContext Db, RecordingOperationalContactInvitationService Invitations)>
        ArrangeAwaitingContactAsync()
    {
        var db = OperationalContactTestDbContext.Create();

        db.Campuses.Add(DelegationsTestData.CreateCampus());
        var visit = DelegationsTestData.CreateVisitRequest();
        visit.Status = VisitRequestStatuses.PendingContactConfirmation;
        visit.RegistrantUserId = RegistrantId;
        visit.EmailVerifiedAt = Now.AddDays(-1);

        var instance = DelegationsTestData.CreateVisitInstance(
            status: VisitInstanceStatuses.WaitingContactConfirmation, currentHostUserId: null);
        instance.OperationalContactUserId = null;
        visit.CampusInstances.Add(instance);
        db.VisitRequests.Add(visit);

        await db.SaveChangesAsync(CancellationToken.None);
        db.Journal.Clear();          // arrangement saves are not part of what the handler did
        db.ChangeTracker.Clear();

        return (db, new RecordingOperationalContactInvitationService(db, Now));
    }

    /// <summary>The same campus, plus the PENDING invitation a resend acts on.</summary>
    private static async Task<(OperationalContactTestDbContext Db, RecordingOperationalContactInvitationService Invitations)>
        ArrangeWithPendingInvitationAsync()
    {
        var (db, invitations) = await ArrangeAwaitingContactAsync();

        db.VisitRequestIdentityChanges.Add(new VisitRequestIdentityChange
        {
            VisitRequestId = RequestId,
            VisitInstanceId = InstanceId,
            ChangeKind = IdentityChangeKinds.InitialConfirmation,
            TokenVersion = 1,
            ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
            NewEmailNormalized = "op@test.local",
            NewEmailMasked = "o**@test.local",
            Status = IdentityChangeStatuses.Pending,
            ExpectedRequestRowVersion = 0,
            RequestedBy = RegistrantId,
            RequestedAt = Now.AddHours(-2),
            ExpiresAt = Now.AddHours(70),
            ResendCount = 0,
            CreatedAt = Now.AddHours(-2),
        });
        await db.SaveChangesAsync(CancellationToken.None);
        db.Journal.Clear();
        db.ChangeTracker.Clear();

        return (db, invitations);
    }

    /// <summary>
    /// The links a previous send left behind. Seeded with a CreatedAt outside the resend cooldown, so
    /// the test exercises the invalidation rather than the rate limit.
    /// </summary>
    private static async Task SeedLiveTokensAsync(OperationalContactTestDbContext db, uint tokenVersion)
    {
        var changeId = InvitationIdOf(db);
        foreach (var action in new[] { EmailIntendedActions.Accept, EmailIntendedActions.Decline })
            db.EmailActionTokens.Add(new PEMS.Domain.Entities.Emails.EmailActionToken
            {
                TokenHash = $"old-{action}",
                ActionContext = EmailActionContexts.VisitContactClaim,
                ActionGroupKey = $"OP_CONTACT_CONFIRM:{changeId}:{tokenVersion}",
                TargetType = EmailActionTargetTypes.VisitRequestIdentityChange,
                TargetId = changeId,
                IntendedAction = action,
                RecipientEmail = "op@test.local",
                ExpiresAt = Now.AddHours(70),
                ResultStatus = EmailActionResultStatuses.Pending,
                CreatedAt = Now.AddHours(-2),
            });
        await db.SaveChangesAsync(CancellationToken.None);
        db.Journal.Clear();
        db.ChangeTracker.Clear();
    }

    // ─────────────────────────── Handlers under test ───────────────────────────

    private static ICurrentUserService Registrant()
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.IsAuthenticated).Returns(true);
        m.SetupGet(c => c.UserId).Returns(RegistrantId);
        return m.Object;
    }

    private static IDateTimeService Clock()
    {
        var m = new Mock<IDateTimeService>();
        m.SetupGet(c => c.VietnamNow).Returns(Now);
        m.SetupGet(c => c.UtcNow).Returns(Now.AddHours(-7));
        return m.Object;
    }

    private static ResendOperationalContactConfirmationCommandHandler ResendHandler(
        OperationalContactTestDbContext db, IOperationalContactInvitationService invitations)
        => new(db, Registrant(), Clock(), invitations, new PerCampusFormV2WriteOptions());

    private static ReinviteOperationalContactConfirmationCommandHandler ReinviteHandler(
        OperationalContactTestDbContext db, IOperationalContactInvitationService invitations)
        => new(db, Registrant(), Clock(), invitations,
            // Re-inviting puts a campus back behind the contact gate, so it has to re-derive the
            // request's aggregate status through the same service every other contact command uses.
            new VisitRequestAggregateStatusService(db),
            new PerCampusFormV2WriteOptions());

    private static ReplaceOperationalContactCommandHandler ReplaceHandler(
        OperationalContactTestDbContext db, IOperationalContactInvitationService invitations)
        => new(db, Registrant(), Clock(), invitations,
            new VisitRequestAggregateStatusService(db),
            Mock.Of<INotificationService>(),
            NullLogger<ReplaceOperationalContactCommandHandler>.Instance,
            new PerCampusFormV2WriteOptions());
}
