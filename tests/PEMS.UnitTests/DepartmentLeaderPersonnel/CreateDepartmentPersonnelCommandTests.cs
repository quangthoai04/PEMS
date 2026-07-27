using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.CreateDepartmentPersonnel;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// Creating department personnel (spec §10). Two properties under test: the SERVER owns every
/// structural field, and a failed confirmation email never destroys the created account.
/// </summary>
public class CreateDepartmentPersonnelCommandTests
{
    private static CreateDepartmentPersonnelCommandHandler Handler(DepartmentLeaderTestHarness h)
        => new(h.Db, h.Scope, h.Locks, h.Confirmations.Object, h.Email.Object, h.Clock);

    private static CreateDepartmentPersonnelCommand Command(
        string email = "moi@fpt.edu.vn", string gender = "MALE") => new()
    {
        FullName = "Nguyen Van Moi",
        Email = email,
        Phone = "0912345678",
        Gender = gender,
    };

    private static Task<CreateDepartmentPersonnelResponse> Run(
        DepartmentLeaderTestHarness h, CreateDepartmentPersonnelCommand command)
        => Handler(h).Handle(command, CancellationToken.None);

    [Fact]
    public async Task Creates_a_department_staff_account_pending_email_confirmation()
    {
        var h = DepartmentLeaderTestHarness.Create();

        var result = await Run(h, Command());

        var created = h.Db.Users.Single(u => u.Email == "moi@fpt.edu.vn");
        Assert.Equal(RoleCodes.Department, h.Db.Roles.Single(r => r.RoleId == created.RoleId).RoleCode);
        Assert.Equal(UserSubRoles.Staff, created.SubRole);
        Assert.Equal(DepartmentLeaderTestHarness.DepartmentId, created.DepartmentId);
        Assert.Equal(DepartmentLeaderTestHarness.CampusId, created.PrimaryCampusId);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, created.Status);
        Assert.Equal(CreatedViaValues.ManualCreated, created.CreatedVia);
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, created.CreatedBy);
        Assert.True(result.Success);
        Assert.Equal("SENT", result.EmailNotificationStatus);
    }

    /// <summary>A Department Leader must not be able to mint a second Leader (spec §10).</summary>
    [Fact]
    public async Task Never_creates_a_second_leader()
    {
        var h = DepartmentLeaderTestHarness.Create();

        await Run(h, Command());

        var created = h.Db.Users.Single(u => u.Email == "moi@fpt.edu.vn");
        Assert.Equal(UserSubRoles.Staff, created.SubRole);
        // The seat is untouched.
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task Email_is_normalized_before_it_is_stored()
    {
        var h = DepartmentLeaderTestHarness.Create();

        await Run(h, Command(email: "  MOI@FPT.EDU.VN  "));

        Assert.Single(h.Db.Users.Where(u => u.Email == "moi@fpt.edu.vn"));
    }

    [Fact]
    public async Task Duplicate_email_is_a_409_conflict()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, email: "taken@fpt.edu.vn");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(h, Command(email: "taken@fpt.edu.vn")));
        Assert.Equal(DepartmentLeaderErrorCodes.AccountEmailAlreadyExists, ex.ErrorCode);
    }

    [Fact]
    public async Task Gender_is_stored_from_the_canonical_wire_value()
    {
        var h = DepartmentLeaderTestHarness.Create();

        await Run(h, Command(gender: "FEMALE"));

        Assert.Equal(Gender.Female, h.Db.Users.Single(u => u.Email == "moi@fpt.edu.vn").Gender);
    }

    [Theory]
    [InlineData("Nam")]      // display label
    [InlineData("male ")]    // handled: trimmed + upper-cased → valid, see the passing case below
    [InlineData("0")]        // enum ordinal
    [InlineData("UNKNOWN")]
    public async Task Non_canonical_gender_values_are_rejected_rather_than_defaulted(string gender)
    {
        var h = DepartmentLeaderTestHarness.Create();

        if (gender.Trim().ToUpperInvariant() == "MALE")
        {
            // Whitespace/case tolerance is intentional; the value still resolves to MALE, never OTHER.
            await Run(h, Command(gender: gender));
            Assert.Equal(Gender.Male, h.Db.Users.Single(u => u.Email == "moi@fpt.edu.vn").Gender);
            return;
        }

        await Assert.ThrowsAsync<ValidationException>(() => Run(h, Command(gender: gender)));
        Assert.Empty(h.Db.Users.Where(u => u.Email == "moi@fpt.edu.vn"));
    }

    [Fact]
    public async Task Inactive_department_blocks_creation()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var department = h.GetDepartment();
        department.Status = EntityStatuses.Inactive;
        h.Db.SaveChanges();
        h.Detach();

        // The scope gate refuses first — the account is never created either way.
        await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h, Command()));
        Assert.Empty(h.Db.Users.Where(u => u.Email == "moi@fpt.edu.vn"));
    }

    /// <summary>
    /// A delivery failure must NOT roll the account back: the operator resends rather than losing the
    /// record and re-typing it (spec §10).
    /// </summary>
    [Fact]
    public async Task Failed_confirmation_email_keeps_the_pending_account_and_reports_truthfully()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.MakeEmailFail();

        var result = await Run(h, Command());

        var created = h.Db.Users.Single(u => u.Email == "moi@fpt.edu.vn");
        Assert.Equal(UserStatuses.PendingEmailConfirmation, created.Status);
        Assert.True(result.Success);
        Assert.Equal("FAILED", result.EmailNotificationStatus);
        Assert.Contains("gửi lại", result.Message);
    }

    [Fact]
    public async Task Confirmation_is_issued_against_the_new_address_and_audited_with_a_masked_email()
    {
        var h = DepartmentLeaderTestHarness.Create();

        await Run(h, Command());

        var created = h.Db.Users.Single(u => u.Email == "moi@fpt.edu.vn");
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            created.UserId, "moi@fpt.edu.vn", false, It.IsAny<CancellationToken>()), Times.Once);

        var audit = h.Db.AuditLogs.Single();
        Assert.Equal(DepartmentPersonnelAuditActions.AddPersonnel, audit.Action);
        var newValues = audit.Changes.Single().NewValueText!;
        Assert.DoesNotContain("moi@fpt.edu.vn", newValues);   // masked, not stored verbatim
        Assert.Contains("@fpt.edu.vn", newValues);            // domain preserved
    }

    [Fact]
    public async Task Department_row_is_locked_so_a_concurrent_transfer_serializes()
    {
        var h = DepartmentLeaderTestHarness.Create();

        await Run(h, Command());

        Assert.Contains(
            h.Locks.LockedDepartmentBatches,
            batch => batch.Contains(DepartmentLeaderTestHarness.DepartmentId));
    }

    [Fact]
    public async Task Department_staff_caller_is_refused()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.SubRole = UserSubRoles.Staff;

        await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h, Command()));
        Assert.Empty(h.Db.Users.Where(u => u.Email == "moi@fpt.edu.vn"));
    }
}
