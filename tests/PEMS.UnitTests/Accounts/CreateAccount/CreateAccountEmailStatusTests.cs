using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.CreateAccount;

/// <summary>
/// P0 #3b — <see cref="CreateAccountCommandHandler"/> maps the email delivery outcome TRUTHFULLY into
/// <c>EmailNotificationStatus</c>: a Skipped send is reported as SKIPPED (never SENT), a Failed send as
/// FAILED, and neither rolls back the already-committed account (the operator can re-notify).
/// </summary>
public class CreateAccountEmailStatusTests
{
    private static (CreateAccountCommandHandler handler, TestApplicationDbContext db) Build(EmailDeliveryResult emailResult)
    {
        var db = TestApplicationDbContext.Create();
        db.Campuses.Add(Uc106TestData.CreateCampus());
        db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        db.SaveChanges();

        var email = new Mock<IEmailService>();
        email.Setup(e => e.TrySendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailResult);
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateAccountCommandHandler(
            db, new FakeCurrentUserService(), new Mock<IPasswordHasher>().Object, new FakeDateTimeService(),
            new AuthOptions(), email.Object, notifications.Object);
        return (handler, db);
    }

    private static CreateAccountCommand StudentCmd() => new()
    {
        RoleCode = RoleCodes.Student,
        FullName = "Tran Van C",
        Email = "new.student@fpt.edu.vn",
        StudentCode = "SE123456",
    };

    [Fact]
    public async Task Skipped_email_is_reported_as_SKIPPED_and_account_is_still_created()
    {
        var (handler, db) = Build(EmailDeliveryResult.Skipped("SMTP_DISABLED"));

        var res = await handler.Handle(StudentCmd(), CancellationToken.None);

        Assert.Equal("SKIPPED", res.EmailNotificationStatus);       // never "SENT"
        Assert.True(await db.Users.AnyAsync(u => u.Email == "new.student@fpt.edu.vn"));  // not rolled back
    }

    [Fact]
    public async Task Failed_email_is_reported_as_FAILED_and_account_is_still_created()
    {
        var (handler, db) = Build(EmailDeliveryResult.Failed("SMTP_SEND_FAILED", "Email delivery failed."));

        var res = await handler.Handle(StudentCmd(), CancellationToken.None);

        Assert.Equal("FAILED", res.EmailNotificationStatus);
        Assert.True(await db.Users.AnyAsync(u => u.Email == "new.student@fpt.edu.vn"));
    }

    [Fact]
    public async Task Sent_email_is_reported_as_SENT()
    {
        var (handler, _) = Build(EmailDeliveryResult.Sent());

        var res = await handler.Handle(StudentCmd(), CancellationToken.None);

        Assert.Equal("SENT", res.EmailNotificationStatus);
    }
}
