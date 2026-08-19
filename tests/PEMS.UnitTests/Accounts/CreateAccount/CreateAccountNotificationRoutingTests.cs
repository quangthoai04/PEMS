using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.CreateAccount;

/// <summary>
/// Closes two gaps from the notification-system audit (2026-08-19,
/// docs/CanhIter3FixBug/GopYCQuyen/PEMS_Fix_Notification_Presentation_Semantic_Routing.md §20):
///
/// 1. Every recipient role now gets the ACCOUNT_CREATED eventKey, not only VISITOR — an EN-language
///    Staff/Student/Department reader used to see the generic "You have a new notification." placeholder.
/// 2. '/dashboard/accounts' is only on the menu for ADMIN/HO/Staff-Leader (dashboardRouteAccess.ts). A
///    recipient outside that set clicking a notification about their OWN new account used to be routed
///    into a page their role's route guard blocks — it now routes to their own Profile page instead.
/// </summary>
public class CreateAccountNotificationRoutingTests
{
    private static (CreateAccountCommandHandler handler, TestApplicationDbContext db, List<CreateNotificationRequest> sent)
        Build()
    {
        var db = TestApplicationDbContext.Create();
        db.Campuses.Add(Uc106TestData.CreateCampus());
        db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        db.SaveChanges();

        var sent = new List<CreateNotificationRequest>();
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateNotificationRequest, CancellationToken>((req, _) => sent.Add(req))
            .Returns(Task.CompletedTask);

        var confirmations = new Mock<IAccountEmailConfirmationService>();
        confirmations.Setup(c => c.IssuePendingAsync(
                It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("raw-token");
        confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>())).Returns("http://localhost:5173/confirm-email?token=raw-token");
        confirmations.Setup(c => c.ExpiryHours).Returns(24);

        // Default FakeCurrentUserService is a Staff Leader on Uc106TestData.CampusId — able to create
        // a Student (AccountProvisioningRules.ResolveStaffLeaderTargetAsync), a non-privileged role.
        var handler = new CreateAccountCommandHandler(
            db, new FakeCurrentUserService(), new Mock<IPasswordHasher>().Object, new FakeDateTimeService(),
            new AuthOptions(), new FakeSystemEmailDispatcher { Outcome = EmailDeliveryResult.Sent() },
            notifications.Object, confirmations.Object);
        return (handler, db, sent);
    }

    private static CreateAccountCommand StudentCmd() => new()
    {
        RoleCode = RoleCodes.Student,
        FullName = "Tran Van C",
        Email = "new.student.routing@fpt.edu.vn",
        StudentCode = "SE123999",
    };

    [Fact]
    public async Task StudentRecipient_GetsAccountCreatedEventKey_NotOnlyVisitor()
    {
        var (handler, _, sent) = Build();

        await handler.Handle(StudentCmd(), CancellationToken.None);

        var notification = Assert.Single(sent);
        Assert.NotNull(notification.MetadataJson);
        var meta = System.Text.Json.JsonDocument.Parse(notification.MetadataJson!).RootElement;
        Assert.Equal(NotificationEventKeys.AccountCreated, meta.GetProperty("eventKey").GetString());
    }

    [Fact]
    public async Task StudentRecipient_RoutesToProfile_NotTheAccountListRoleGuardBlocks()
    {
        var (handler, _, sent) = Build();

        await handler.Handle(StudentCmd(), CancellationToken.None);

        var notification = Assert.Single(sent);
        Assert.Equal("/dashboard/profile", notification.ActionUrl);
    }
}
