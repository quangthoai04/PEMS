using Moq;
using PEMS.Application.Accounts.Commands.ManageAccountStatus;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;
using NotificationRelatedTypes = PEMS.Application.Notifications.Common.NotificationRelatedTypes;

namespace PEMS.UnitTests.Accounts.ManageAccountStatus;

/// <summary>
/// Producer-level contract for the semantic-notification gap closed on
/// <see cref="ManageAccountStatusCommandHandler"/>: only ADMIN's two security actions
/// (ACTIVE→LOCKED, LOCKED→ACTIVE) are reachable for a VISITOR target — HO and Staff Leader's
/// business-status branch (ACTIVE↔INACTIVE) explicitly excludes VISITOR from targetInScope, so it
/// is deliberately NOT covered here, same as every other non-Visitor-reachable notification branch
/// in this architecture.
/// </summary>
public class ManageAccountStatusNotificationMetadataTests
{
    private const ulong Campus = Uc106TestData.CampusId;
    private const ulong AdminRoleId = 1;
    private const ulong VisitorRoleId = 6;
    private const ulong ActorId = 700;
    private const ulong TargetId = 810;
    private const string Reason = "Nghi ngờ tài khoản bị xâm nhập";

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new()
        {
            UserId = ActorId, RoleCode = RoleCodes.Admin, SubRole = null, RoleId = AdminRoleId,
            PrimaryCampusId = Campus,
        };
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public Mock<ISecurityAuditService> Audit { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public List<CreateNotificationRequest> Sent { get; } = new();
        public ManageAccountStatusCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Notifications
                .Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .Callback<CreateNotificationRequest, CancellationToken>((req, _) => Sent.Add(req))
                .Returns(Task.CompletedTask);
            Handler = new ManageAccountStatusCommandHandler(
                Db, Actor, Sessions, Audit.Object, Clock, Notifications.Object);
        }

        public Task<ManageAccountStatusResponse> Run(ManageAccountStatusCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    private static Harness CreateHarness(string targetStatus = EntityStatuses.Active)
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(AdminRoleId, RoleCodes.Admin),
            Uc106TestData.CreateRole(VisitorRoleId, RoleCodes.Visitor));

        var admin = Uc106TestData.CreateUser(ActorId, AdminRoleId, null, null, Campus);
        h.Db.Users.Add(admin);

        // The VISITOR target — ADMIN's security actions carry no campus/role scoping, unlike
        // HO/Staff-Leader's business-status branch (which excludes VISITOR from targetInScope
        // entirely, confirmed by reading ManageAccountStatusCommandHandler.cs).
        var target = Uc106TestData.CreateUser(TargetId, VisitorRoleId, null, null, Campus);
        target.Status = targetStatus;
        h.Db.Users.Add(target);
        h.Db.SaveChanges();
        return h;
    }

    private static ManageAccountStatusCommand Cmd(string status, string? reason = Reason) =>
        new() { UserId = TargetId, Status = status, Reason = reason };

    private static System.Text.Json.JsonElement MetadataOf(CreateNotificationRequest req)
    {
        Assert.NotNull(req.MetadataJson);
        return System.Text.Json.JsonDocument.Parse(req.MetadataJson!).RootElement;
    }

    [Fact]
    public async Task VisitorTarget_SecurityLock_GetsAccountLockedMetadata_WithNoParams()
    {
        var h = CreateHarness();

        await h.Run(Cmd(UserStatuses.Locked));

        var sent = Assert.Single(h.Sent, n => n.RecipientUserId == TargetId);
        Assert.Equal(NotificationTypes.AccountStatusChanged, sent.NotificationType);
        var meta = MetadataOf(sent);
        Assert.Equal(NotificationEventKeys.AccountLocked, meta.GetProperty("eventKey").GetString());
        Assert.Empty(meta.GetProperty("params").EnumerateObject());
        // The lock REASON (an admin-entered, potentially sensitive security note) must never leak
        // into the Visitor-facing payload — the original VI Message never showed it either.
        Assert.DoesNotContain(Reason, sent.MetadataJson);
    }

    [Fact]
    public async Task VisitorTarget_SecurityUnlock_GetsAccountUnlockedMetadata_WithNoParams()
    {
        var h = CreateHarness(UserStatuses.Locked);

        await h.Run(Cmd(UserStatuses.Active, "Điều tra hoàn tất"));

        var sent = Assert.Single(h.Sent, n => n.RecipientUserId == TargetId);
        var meta = MetadataOf(sent);
        Assert.Equal(NotificationEventKeys.AccountUnlocked, meta.GetProperty("eventKey").GetString());
        Assert.Empty(meta.GetProperty("params").EnumerateObject());
    }

    [Fact]
    public async Task VisitorTarget_RoutesToProfile_NotTheAccountListRoleGuardBlocks()
    {
        // Closed by the notification-system audit (2026-08-19): '/dashboard/accounts' is only on the
        // menu for ADMIN/HO/Staff-Leader (dashboardRouteAccess.ts). A VISITOR clicking a notification
        // about their OWN account used to be routed into a page their role's route guard blocks —
        // now it routes to their own Profile page, which every role can reach.
        var h = CreateHarness();

        await h.Run(Cmd(UserStatuses.Locked));

        var sent = Assert.Single(h.Sent, n => n.RecipientUserId == TargetId);
        Assert.Equal(NotificationActionTypes.OpenAccountDetail, sent.ActionType);
        Assert.Equal("/dashboard/profile", sent.ActionUrl);
        Assert.Equal(NotificationRelatedTypes.Account, sent.RelatedType);
        Assert.Equal(TargetId, sent.RelatedId);
    }
}
