using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.CreateAccount;

/// <summary>
/// Unit tests for the MSSV (student_code) handling added to <see cref="CreateAccountCommandHandler"/>
/// (spec §5 / §10.2). The caller is the default Staff Leader (id 900, campus 1). Runs on EF InMemory
/// with mocked auth/notification/email collaborators.
/// </summary>
public class CreateAccountStudentCodeTests
{
    private const ulong Campus = Uc106TestData.CampusId;

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();     // Staff Leader, id 900, campus 1
        public FakeDateTimeService Clock { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<PEMS.Application.Accounts.Common.IAccountEmailConfirmationService> Confirmations { get; } = new();
        public CreateAccountCommandHandler Handler { get; }

        public Harness()
        {
            Confirmations.Setup(c => c.IssuePendingAsync(
                    It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("raw-token");
            Confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>()))
                .Returns("http://localhost:5173/confirm-email?token=raw-token");
            Confirmations.Setup(c => c.ExpiryHours).Returns(24);
            Notifications.Setup(n => n.CreateAsync(
                    It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new CreateAccountCommandHandler(
                Db, Actor, Hasher.Object, Clock, new AuthOptions(), Dispatcher, Notifications.Object, Confirmations.Object);
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

    private static CreateAccountCommand StudentCmd(string? code, string email = "new.student@fpt.edu.vn") => new()
    {
        RoleCode = RoleCodes.Student,
        FullName = "Trần Văn C",
        Email = email,
        StudentCode = code,
    };

    [Fact]
    public async Task Student_WithCode_SavesTrimmed_SubRoleAndDepartmentNull_CampusFromCaller()
    {
        var h = CreateHarness();

        var result = await h.Run(StudentCmd("  SE123456  "));

        var user = await h.Db.Users.SingleAsync(u => u.Email == "new.student@fpt.edu.vn");
        Assert.Equal(RoleCodes.Student, result.RoleCode);
        Assert.Equal("SE123456", user.StudentCode);
        Assert.Null(user.SubRole);
        Assert.Null(user.DepartmentId);
        Assert.Equal(Campus, user.PrimaryCampusId);
    }

    [Fact]
    public async Task Student_WithoutCode_Throws()
    {
        var h = CreateHarness();
        await Assert.ThrowsAsync<ValidationException>(() => h.Run(StudentCmd(null)));
    }

    [Fact]
    public async Task Student_WhitespaceOnlyCode_Throws()
    {
        var h = CreateHarness();
        await Assert.ThrowsAsync<ValidationException>(() => h.Run(StudentCmd("   ")));
    }

    [Fact]
    public async Task Student_CodeOver30Chars_Throws()
    {
        var h = CreateHarness();
        await Assert.ThrowsAsync<ValidationException>(() => h.Run(StudentCmd(new string('X', 31))));
    }

    [Fact]
    public async Task Student_DuplicateCode_Throws()
    {
        var h = CreateHarness();
        var existing = Uc106TestData.CreateUser(200, Uc106TestData.StudentRoleId, null, null);
        existing.StudentCode = "SE999999";
        existing.Email = "existing@fpt.edu.vn";
        h.Db.Users.Add(existing);
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => h.Run(StudentCmd("SE999999")));
    }
}
