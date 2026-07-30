using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// Shared fixture for the Department Leader personnel slice. Builds the minimum world every handler
/// needs — a campus, the DEPARTMENT role, a GENERAL department and its seated Leader — so each test
/// only states the thing it is actually about.
///
/// The real scope service is used rather than a mock: the authorization gate is the security boundary
/// under test, and stubbing it would make every "out of scope is refused" assertion vacuous.
/// </summary>
public sealed class DepartmentLeaderTestHarness
{
    public const ulong CampusId = 1;
    public const ulong OtherCampusId = 2;
    public const ulong DepartmentId = 10;
    public const ulong OtherDepartmentId = 11;
    public const ulong LeaderId = 900;      // == FakeCurrentUserService default UserId
    public const ulong StaffId = 901;

    public TestApplicationDbContext Db { get; }
    public FakeCurrentUserService Actor { get; }
    public FakeDateTimeService Clock { get; }
    public RecordingUserMutationLockService Locks { get; }
    public RecordingSessionService Sessions { get; }
    public FakeSystemEmailDispatcher Dispatcher { get; }
    public Mock<IAccountEmailConfirmationService> Confirmations { get; }
    public IDepartmentLeaderPersonnelScopeService Scope { get; }

    /// <summary>Every address the dispatcher was asked to send to, in call order.</summary>
    public List<string> SentTo => Dispatcher.Sent.Select(r => r.To.Email).ToList();

    /// <summary>
    /// The message sent to <paramref name="toEmail"/> — asserted on by TEMPLATE CODE and VARIABLES
    /// rather than rendered HTML, because the body now lives in <c>email_templates</c>. That is the
    /// stronger check: a template declaring no variables cannot leak a value at all, whereas a body
    /// that merely happens not to contain a string today could start containing it tomorrow.
    /// </summary>
    public SystemEmailRequest MessageTo(string toEmail)
        => Dispatcher.Sent.Single(r => r.To.Email == toEmail);

    private DepartmentLeaderTestHarness()
    {
        Db = TestApplicationDbContext.Create();
        Actor = new FakeCurrentUserService
        {
            UserId = LeaderId,
            RoleCode = RoleCodes.Department,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = CampusId,
            DepartmentId = DepartmentId,
        };
        Clock = new FakeDateTimeService();
        Locks = new RecordingUserMutationLockService();
        Sessions = new RecordingSessionService(Db);
        Dispatcher = new FakeSystemEmailDispatcher();
        Confirmations = new Mock<IAccountEmailConfirmationService>();

        Confirmations.Setup(c => c.IssuePendingAsync(
                It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("raw-token");
        Confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>()))
            .Returns("http://localhost:5173/confirm-email?token=raw-token");

        Scope = new DepartmentLeaderPersonnelScopeService(Db, Actor);
    }

    /// <summary>Makes the dispatcher report a hard failure, so "email failed" paths can be tested.</summary>
    public void MakeEmailFail()
        => Dispatcher.Outcome = EmailDeliveryResult.Failed("SMTP_ERROR", "Không gửi được email.");

    /// <summary>Fails only the message addressed to <paramref name="toEmail"/>, for partial-outcome tests.</summary>
    public void MakeEmailFailFor(string toEmail)
        => Dispatcher.OutcomeFor = request => request.To.Email == toEmail
            ? EmailDeliveryResult.Failed("SMTP_ERROR", "Không gửi được email.")
            : EmailDeliveryResult.Sent();

    /// <summary>
    /// A campus + DEPARTMENT/STAFF roles + an ACTIVE GENERAL department headed by <see cref="LeaderId"/>.
    /// </summary>
    public static DepartmentLeaderTestHarness Create()
    {
        var h = new DepartmentLeaderTestHarness();

        h.Db.Campuses.Add(Uc106TestData.CreateCampus(CampusId));
        h.Db.Campuses.Add(Uc106TestData.CreateCampus(OtherCampusId));
        h.Db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.DepartmentRoleId, RoleCodes.Department));
        h.Db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff));

        var department = Uc106TestData.CreateGeneralDepartment(DepartmentId, CampusId);
        department.HeadUserId = LeaderId;
        h.Db.Departments.Add(department);

        var leader = Uc106TestData.CreateDepartmentLeader(LeaderId, DepartmentId, CampusId);
        leader.FullName = "Truong Phong";
        leader.Email = "leader@fpt.edu.vn";
        h.Db.Users.Add(leader);

        h.Db.SaveChanges();
        return h;
    }

    /// <summary>Adds a DEPARTMENT/STAFF member. Defaults to this department, this campus, ACTIVE.</summary>
    public User AddStaff(
        ulong userId = StaffId,
        string status = UserStatuses.Active,
        ulong departmentId = DepartmentId,
        ulong campusId = CampusId,
        string? email = null,
        string? fullName = null,
        string? phone = "0912345678",
        PEMS.Domain.Enums.Gender? gender = PEMS.Domain.Enums.Gender.Male)
    {
        var user = Uc106TestData.CreateDepartmentStaff(userId, departmentId, campusId);
        user.Status = status;
        user.Email = email ?? $"staff{userId}@fpt.edu.vn";
        user.FullName = fullName ?? $"Nhan Vien {userId}";
        user.Phone = phone;
        user.Gender = gender;
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    /// <summary>A second GENERAL department (different campus) used for cross-department scope tests.</summary>
    public Department AddOtherDepartment(ulong headUserId = 0)
    {
        var department = Uc106TestData.CreateGeneralDepartment(OtherDepartmentId, OtherCampusId);
        if (headUserId != 0) department.HeadUserId = headUserId;
        Db.Departments.Add(department);
        Db.SaveChanges();
        return department;
    }

    public UserSession AddActiveSession(ulong sessionId, ulong userId)
    {
        var session = Uc106TestData.CreateActiveSession(sessionId, userId);
        Db.UserSessions.Add(session);
        Db.SaveChanges();
        return session;
    }

    public UserAuthProvider AddAuthProvider(
        ulong id, ulong userId, string providerType, string? providerEmail, string? subject = "ext-subject")
    {
        var provider = new UserAuthProvider
        {
            AuthProviderId = id,
            UserId = userId,
            ProviderType = providerType,
            ProviderSubject = providerType == ProviderTypes.LocalPassword ? null : subject,
            ProviderEmail = providerEmail,
            IsEnabled = true,
            LinkedAt = new System.DateTime(2026, 1, 1),
        };
        Db.UserAuthProviders.Add(provider);
        Db.SaveChanges();
        return provider;
    }

    public AccountEmailConfirmation AddPendingConfirmation(
        ulong confirmationId, ulong userId, string targetEmail, int resendCount = 0)
    {
        var row = new AccountEmailConfirmation
        {
            ConfirmationId = confirmationId,
            UserId = userId,
            TargetEmail = targetEmail,
            TokenHash = "hash",
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = Clock.VietnamNow.AddHours(24),
            ResendCount = resendCount,
            CreatedAt = Clock.VietnamNow.AddMinutes(-10),
        };
        Db.AccountEmailConfirmations.Add(row);
        Db.SaveChanges();
        return row;
    }

    public Department GetDepartment(ulong departmentId = DepartmentId)
        => Db.Departments.Find(departmentId)!;

    public User GetUser(ulong userId) => Db.Users.Find(userId)!;

    /// <summary>Detaches everything so the next read re-materializes from the store, not the tracker.</summary>
    public void Detach() => Db.ChangeTracker.Clear();
}
