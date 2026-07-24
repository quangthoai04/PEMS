using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.CreateAccount;

/// <summary>
/// Identity (full name / email) behaviour of <see cref="CreateAccountCommandHandler"/> — the write
/// side of PEMS_HO_ACCOUNT_IDENTITY_VALIDATION_IMPLEMENTATION_SPEC §10.1/§12.2 and AC-10. The
/// handler is reached directly here, i.e. exactly like a client that skips the modal and the
/// FluentValidation pipeline: it must still normalize and reject on its own.
/// The caller is the default Staff Leader (id 900, campus 1) creating a STUDENT account.
/// </summary>
public class CreateAccountIdentityTests
{
    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();     // Staff Leader, id 900, campus 1
        public FakeDateTimeService Clock { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public CreateAccountCommandHandler Handler { get; }

        public Harness()
        {
            Email.Setup(e => e.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Email.Setup(e => e.TrySendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmailDeliveryResult.Sent());
            Notifications.Setup(n => n.CreateAsync(
                    It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new CreateAccountCommandHandler(
                Db, Actor, Hasher.Object, Clock, new AuthOptions(), Email.Object, Notifications.Object);
        }

        public Task<CreateAccountResponse> Run(CreateAccountCommand cmd) => Handler.Handle(cmd, CancellationToken.None);
    }

    private static Harness CreateHarness()
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        h.Db.SaveChanges();
        return h;
    }

    private static CreateAccountCommand Cmd(string fullName, string email) => new()
    {
        RoleCode = RoleCodes.Student,
        FullName = fullName,
        Email = email,
        StudentCode = "SE123456",
    };

    [Theory]
    [InlineData("new.student@gmail.com")]
    [InlineData("new.student@fpt.edu.vn")]
    [InlineData("new.student@fe.edu.vn")]
    public async Task AllowedDomains_AreAccepted(string email)
    {
        var h = CreateHarness();
        var result = await h.Run(Cmd("Trần Văn C", email));
        Assert.Equal(email, result.Email);
    }

    [Fact]
    public async Task Identity_IsNormalizedBeforeItIsStored()
    {
        var h = CreateHarness();

        await h.Run(Cmd("  Nguyễn   Văn   An  ", "  NEW.Student@FPT.EDU.VN  "));

        var user = await h.Db.Users.SingleAsync(u => u.Email == "new.student@fpt.edu.vn");
        Assert.Equal("Nguyễn Văn An", user.FullName);   // collapsed, casing preserved
    }

    [Fact]
    public async Task DuplicateEmail_IsDetectedCaseInsensitively()
    {
        var h = CreateHarness();
        var existing = Uc106TestData.CreateUser(200, Uc106TestData.StudentRoleId, null, null);
        existing.Email = "taken@fpt.edu.vn";
        existing.StudentCode = "SE000001";
        h.Db.Users.Add(existing);
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Run(Cmd("Trần Văn C", "  TAKEN@FPT.EDU.VN  ")));
        Assert.Equal(AccountErrorCodes.EmailAlreadyExists, ex.ErrorCode);
        Assert.Equal(AccountIdentityRules.EmailAlreadyUsedMessage, ex.Message);
    }

    // ── AC-10: a payload that skips the modal is still rejected by the handler ──

    [Theory]
    [InlineData("Trần Văn C", "student@yahoo.com")]                  // domain not allowed
    [InlineData("Trần Văn C", "student@student.fpt.edu.vn")]         // subdomain, not an exact match
    [InlineData("Trần Văn C", "student+test@gmail.com")]             // plus addressing
    [InlineData("Trần Văn C", "abc..def@gmail.com")]                 // malformed local-part
    [InlineData("Trần Văn C", "abc")]                                // not an email at all
    [InlineData("Nguyễn Văn 123", "student@gmail.com")]              // digits in the name
    [InlineData("A", "student@gmail.com")]                           // name too short
    [InlineData("<script>alert(1)</script>", "student@gmail.com")]   // markup in the name
    public async Task InvalidIdentity_IsRejected_AndNothingIsWritten(string fullName, string email)
    {
        var h = CreateHarness();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(Cmd(fullName, email)));

        Assert.False(await h.Db.Users.AnyAsync());
    }

    [Fact]
    public async Task FullNameOver150Characters_IsRejected()
    {
        var h = CreateHarness();
        await Assert.ThrowsAsync<ValidationException>(
            () => h.Run(Cmd(new string('a', 151), "student@gmail.com")));
    }
}
