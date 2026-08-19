using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Delegations.Commands.TransferVisitHost;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;

namespace PEMS.UnitTests.Delegations.TransferVisitHost;

/// <summary>
/// Producer-level contract for TransferVisitHostCommandHandler's notification metadata: the campus's
/// operational contact (a VISITOR) carries HOST_CHANGED; the outgoing/incoming Host notifications carry
/// HOST_TRANSFER_OUTGOING/HOST_TRANSFER_INCOMING (closed 2026-08-19 — previously MetadataJson=null); the
/// campus's other Staff Leaders carry HOST_CHANGED_HO_VISIBILITY.
/// </summary>
public class TransferVisitHostNotificationMetadataTests
{
    private const ulong LeaderId = 200;
    private const ulong NewHostId = 101;
    private const ulong VisitorId = 300;
    private const ulong VisitorRoleId = 6;

    private static (DelegationsTestDbContext Db, TransferVisitHostCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks,
        List<CreateNotificationRequest> Sent) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db); // campus 1, host=100 (STAFF+STAFF), instance 10, visit request 10.

        db.Roles.Add(DelegationsTestData.CreateRole(VisitorRoleId, RoleCodes.Visitor));
        // A second IC Staff eligible to receive the handover.
        db.Users.Add(DelegationsTestData.CreateUser(NewHostId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff,
            db.Departments.First(d => d.DepartmentType == "IC").DepartmentId));
        // The campus Staff Leader who performs the transfer.
        db.Users.Add(DelegationsTestData.CreateUser(LeaderId, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, null));
        // The campus's own operational contact — a Visitor, per VisitRequestOwnership.GuestSideRecipients.
        db.Users.Add(DelegationsTestData.CreateUser(VisitorId, VisitorRoleId, null, null));
        db.SaveChanges();

        var instance = db.VisitRequestCampuses.Single(c => c.VisitInstanceId == DelegationsTestData.VisitInstanceId);
        instance.OperationalContactUserId = VisitorId;
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser
        {
            UserId = LeaderId,
            RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = DelegationsTestData.CampusId,
        };
        var mocks = new DelegationsHandlerMocks();
        // Confirmed dead field on the real handler (never called in NotifyAfterCommitAsync) — loose,
        // no setup needed.
        var formRead = new Mock<IVisitFormReadService>(MockBehavior.Loose);

        var sent = new List<CreateNotificationRequest>();
        mocks.Notifications
            .Setup(n => n.CreateManyAsync(It.IsAny<IEnumerable<CreateNotificationRequest>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<CreateNotificationRequest>, CancellationToken>((reqs, _) => sent.AddRange(reqs))
            .Returns(Task.CompletedTask);

        var handler = new TransferVisitHostCommandHandler(
            db, user, mocks.Clock, mocks.Notifications.Object, formRead.Object,
            NullLogger<TransferVisitHostCommandHandler>.Instance,
            new RecordingUserMutationLockService());

        return (db, handler, user, mocks, sent);
    }

    private static TransferVisitHostCommand Cmd(string reason = "Đổi lịch cá nhân") =>
        new(DelegationsTestData.VisitInstanceId, NewHostId, reason, ExpectedRowVersion: 0);

    [Fact]
    public async Task VisitorRecipient_GetsHostChangedMetadata_WithCampusRequestCodeAndNewHostName()
    {
        var (_, handler, _, _, sent) = CreateSut();

        await handler.Handle(Cmd(), CancellationToken.None);

        var visitorNotification = Assert.Single(sent, n => n.RecipientUserId == VisitorId);
        Assert.Equal(NotificationTypes.VisitStatusChanged, visitorNotification.NotificationType);
        Assert.NotNull(visitorNotification.MetadataJson);

        var meta = System.Text.Json.JsonDocument.Parse(visitorNotification.MetadataJson!).RootElement;
        Assert.Equal(NotificationEventKeys.HostChanged, meta.GetProperty("eventKey").GetString());
        var @params = meta.GetProperty("params");
        Assert.Equal($"Campus {DelegationsTestData.CampusId}", @params.GetProperty("campusName").GetString());
        Assert.Equal($"VR-{DelegationsTestData.VisitRequestId}", @params.GetProperty("requestCode").GetString());
        Assert.Equal($"User {NewHostId}", @params.GetProperty("hostName").GetString());

        // Never a pre-built sentence smuggled into params — every value is a relational field.
        foreach (var prop in @params.EnumerateObject())
        {
            var value = prop.Value.GetString() ?? string.Empty;
            Assert.DoesNotContain("đã đổi Host", value);
            Assert.DoesNotContain("<", value);
        }
    }

    [Fact]
    public async Task VisitorRecipient_RoutesToTheSameVisitDetailUrl_AsBeforeTheMetadataChange()
    {
        // Routing/action must not regress: the ActionUrl and ActionType a Visitor's notification click
        // depends on are untouched by adding MetadataJson.
        var (_, handler, _, _, sent) = CreateSut();

        await handler.Handle(Cmd(), CancellationToken.None);

        var visitorNotification = sent.Single(n => n.RecipientUserId == VisitorId);
        Assert.Equal(NotificationActionTypes.OpenVisitDetail, visitorNotification.ActionType);
        Assert.Equal($"/dashboard/visit?visitRequestId={DelegationsTestData.VisitRequestId}", visitorNotification.ActionUrl);
        Assert.False(visitorNotification.IsActionRequired);
    }

    [Fact]
    public async Task OutgoingAndIncomingHost_GetHostTransferMetadata()
    {
        // Closed by the notification-system audit (2026-08-19): the Staff-facing "no longer Host" /
        // "now Host" notifications used to stay on raw VI Message (MetadataJson=null), which meant an
        // EN-language Staff member saw the generic placeholder instead of who they were and who took
        // over. They now carry their own eventKeys, distinct from the Guest-facing HOST_CHANGED.
        var (_, handler, _, _, sent) = CreateSut();

        await handler.Handle(Cmd(), CancellationToken.None);

        var outgoingHost = sent.Single(n => n.RecipientUserId == DelegationsTestData.HostUserId);
        Assert.Equal(NotificationTypes.HostAssigned, outgoingHost.NotificationType);
        Assert.NotNull(outgoingHost.MetadataJson);
        var outgoingMeta = System.Text.Json.JsonDocument.Parse(outgoingHost.MetadataJson!).RootElement;
        Assert.Equal(NotificationEventKeys.HostTransferOutgoing, outgoingMeta.GetProperty("eventKey").GetString());
        Assert.Equal($"User {NewHostId}", outgoingMeta.GetProperty("params").GetProperty("newHostName").GetString());

        var incomingHost = sent.Single(n => n.RecipientUserId == NewHostId);
        Assert.Equal(NotificationTypes.HostAssigned, incomingHost.NotificationType);
        Assert.NotNull(incomingHost.MetadataJson);
        var incomingMeta = System.Text.Json.JsonDocument.Parse(incomingHost.MetadataJson!).RootElement;
        Assert.Equal(NotificationEventKeys.HostTransferIncoming, incomingMeta.GetProperty("eventKey").GetString());
    }
}
