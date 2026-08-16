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
/// Producer-level contract for the semantic-notification gap closed on TransferVisitHostCommandHandler:
/// the campus's operational contact (a VISITOR) is notified on a host handover, and that notification
/// must now carry HOST_CHANGED metadata — the pre-existing HostAssigned notifications to the outgoing/
/// incoming Host and the campus's other Staff Leaders (Staff-facing, never i18n'd) must stay untouched.
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
    public async Task OutgoingAndIncomingHost_NeverGetMetadata_OnlyTheVisitorDoes()
    {
        // The Staff-facing HOST_ASSIGNED notifications (outgoing Host loses the role, incoming Host
        // gains it) stay on raw VI Message — they are never read through resolveNotificationPresentation,
        // and giving them metadata would be scope creep beyond the Visitor i18n gap this producer had.
        var (_, handler, _, _, sent) = CreateSut();

        await handler.Handle(Cmd(), CancellationToken.None);

        var outgoingHost = sent.Single(n => n.RecipientUserId == DelegationsTestData.HostUserId);
        Assert.Equal(NotificationTypes.HostAssigned, outgoingHost.NotificationType);
        Assert.Null(outgoingHost.MetadataJson);

        var incomingHost = sent.Single(n => n.RecipientUserId == NewHostId);
        Assert.Equal(NotificationTypes.HostAssigned, incomingHost.NotificationType);
        Assert.Null(incomingHost.MetadataJson);
    }
}
