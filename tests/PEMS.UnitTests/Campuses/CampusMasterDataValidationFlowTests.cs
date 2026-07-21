using PEMS.Application.Campuses.Commands.AddNewCampus;
using PEMS.Application.Campuses.Commands.UpdateCampus;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Campuses;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// Create (UC-81) and edit (UC-85) flow tests for the shared master-data validation:
/// validator parity (§12.1), direct-API rejection (§17.7/§17.8, AC-10), the city whitelist with
/// legacy tolerance (§6.3, AC-05), canonical phone duplicates (§8.5, AC-03) and duplicate
/// exclusion of the campus being edited (§10.2, AC-08).
/// </summary>
public class CampusMasterDataValidationFlowTests
{
    private static readonly FakeCurrentUserService HoUser = new()
    {
        UserId = 900,
        RoleCode = RoleCodes.Ho,
        SubRole = null,
        PrimaryCampusId = 999,
    };

    private static AddNewCampusCommand ValidCreate() => new()
    {
        CampusCode = "QN",
        Name = "FPT University Quy Nhơn",
        City = "Gia Lai",
        Address = "Khu đô thị mới An Phú Thịnh, Quy Nhơn",
        Phone = "0256 7300 999",
        Email = "qn@fpt.edu.vn",
    };

    private static UpdateCampusCommand ValidUpdate(ulong campusId = 1) => new()
    {
        CampusId = campusId,
        CampusCode = "QN",
        Name = "FPT University Quy Nhơn",
        City = "Gia Lai",
        Address = "Khu đô thị mới An Phú Thịnh, Quy Nhơn",
        Phone = "0256 7300 999",
        Email = "qn@fpt.edu.vn",
    };

    private static UpdateCampusCommandHandler UpdateHandler(CampusTestDbContext db)
        => new(db, HoUser, new RoleAccessPolicy(), new FakeDateTimeService());

    private static AddNewCampusCommandHandler CreateHandler(CampusTestDbContext db)
        => new(db, HoUser, new RoleAccessPolicy(), new FakeDateTimeService());

    /// <summary>Seeds one campus so update tests have something to edit.</summary>
    private static Campus SeedCampus(CampusTestDbContext db, Action<Campus>? customize = null)
    {
        var campus = new Campus
        {
            CampusCode = "HN",
            Name = "FPT University Hà Nội",
            City = "Hà Nội",
            Address = "Km 29 Đại lộ Thăng Long, Thạch Thất, Hà Nội",
            Phone = "024 7300 5588",
            Email = "hn@fpt.edu.vn",
            Status = EntityStatuses.Active,
            IcHeadUserId = 77,
            CreatedAt = new DateTime(2024, 1, 1),
            CreatedBy = 1,
        };
        customize?.Invoke(campus);
        db.Campuses.Add(campus);
        db.SaveChanges();
        return campus;
    }

    // ── §12.1 Create and edit share one rule set ──────────────────────────────

    /// <summary>
    /// The core anti-drift test: for the same master data, create and edit must reach the same
    /// verdict. If one validator gains a rule the other lacks, this fails.
    /// </summary>
    [Theory]
    [InlineData("HN", "FPT University Hà Nội", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024 7300 5588", "hn@fpt.edu.vn", true)]
    [InlineData("H", "FPT University Hà Nội", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024 7300 5588", "hn@fpt.edu.vn", false)]
    [InlineData("HN--2", "FPT University Hà Nội", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024 7300 5588", "hn@fpt.edu.vn", false)]
    [InlineData("HN", "12", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024 7300 5588", "hn@fpt.edu.vn", false)]
    [InlineData("HN", "<script>x</script>", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024 7300 5588", "hn@fpt.edu.vn", false)]
    [InlineData("HN", "FPT University Hà Nội", "Hà Nội", "12345", "024 7300 5588", "hn@fpt.edu.vn", false)]
    [InlineData("HN", "FPT University Hà Nội", "Hà Nội", "Km 29 Đại lộ Thăng Long", "1234567", "hn@fpt.edu.vn", false)]
    [InlineData("HN", "FPT University Hà Nội", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024ABC5588", "hn@fpt.edu.vn", false)]
    [InlineData("HN", "FPT University Hà Nội", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024 7300 5588", "abc@gmail.com", false)]
    [InlineData("HN", "FPT University Hà Nội", "Hà Nội", "Km 29 Đại lộ Thăng Long", "024 7300 5588", "abc+t@fpt.edu.vn", false)]
    public void CreateAndUpdateValidators_AgreeOnEveryPayload(
        string code, string name, string city, string address, string phone, string email, bool expectedValid)
    {
        var createResult = new AddNewCampusCommandValidator().Validate(new AddNewCampusCommand
        {
            CampusCode = code, Name = name, City = city, Address = address, Phone = phone, Email = email,
        });
        var updateResult = new UpdateCampusCommandValidator().Validate(new UpdateCampusCommand
        {
            CampusId = 1,
            CampusCode = code, Name = name, City = city, Address = address, Phone = phone, Email = email,
        });

        Assert.Equal(expectedValid, createResult.IsValid);
        Assert.Equal(expectedValid, updateResult.IsValid);
    }

    // ── §17.7 / AC-10 Direct API payloads are rejected ────────────────────────

    [Theory]
    [InlineData("Hà Nội")]  // diacritics in the code
    [InlineData("-HN")]
    [InlineData("HN_")]
    [InlineData("HN@01")]
    public void CreateValidator_RejectsMalformedCode(string code)
    {
        var command = ValidCreate();
        command.CampusCode = code;
        Assert.False(new AddNewCampusCommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public void CreateValidator_RejectsCityOutsideTheWhitelist()
    {
        var command = ValidCreate();
        command.City = "Vùng đất lạ";

        var result = new AddNewCampusCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == CampusMasterRules.CityNotAllowedMessage);
    }

    [Fact]
    public void CreateValidator_AcceptsMasterDataThatOnlyBecomesValidAfterNormalization()
    {
        var command = ValidCreate();
        command.CampusCode = "  qn  ";                       // → "QN"
        command.Name = "  FPT   University   Quy Nhơn ";      // → collapsed
        command.City = "  gia lai ";                          // → canonical "Gia Lai"

        Assert.True(new AddNewCampusCommandValidator().Validate(command).IsValid);
    }

    // ── §6.3 / AC-05 City whitelist with legacy tolerance ─────────────────────

    [Fact]
    public async Task Update_KeepsAnUnmigratedLegacyCity_WhenItIsNotBeingChanged()
    {
        using var db = CampusTestDbContext.Create();
        // "Bắc Giang" was merged away in 2025 and is no longer on the whitelist.
        var campus = SeedCampus(db, c => c.City = "Bắc Giang");

        var command = ValidUpdate(campus.CampusId);
        command.City = "Bắc Giang";
        command.Name = "FPT University Bắc Giang";

        await UpdateHandler(db).Handle(command, CancellationToken.None);

        Assert.Equal("Bắc Giang", db.Campuses.Single().City);
        Assert.Equal("FPT University Bắc Giang", db.Campuses.Single().Name);
    }

    [Fact]
    public async Task Update_RejectsAChangeToACityOutsideTheWhitelist()
    {
        using var db = CampusTestDbContext.Create();
        var campus = SeedCampus(db, c => c.City = "Bắc Giang");

        var command = ValidUpdate(campus.CampusId);
        command.City = "Một tỉnh không tồn tại";

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => UpdateHandler(db).Handle(command, CancellationToken.None));

        Assert.Equal(CampusErrorCodes.CampusCityInvalid, ex.ErrorCode);
        Assert.Equal("Bắc Giang", db.Campuses.Single().City); // nothing was written
    }

    [Fact]
    public async Task Update_AcceptsAChangeFromALegacyCityOntoTheWhitelist()
    {
        using var db = CampusTestDbContext.Create();
        var campus = SeedCampus(db, c => c.City = "Bắc Giang");

        var command = ValidUpdate(campus.CampusId);
        command.City = "Bắc Ninh";

        await UpdateHandler(db).Handle(command, CancellationToken.None);

        Assert.Equal("Bắc Ninh", db.Campuses.Single().City);
    }

    // ── §8.5 / AC-03 Canonical phone duplicates ───────────────────────────────

    [Fact]
    public async Task Create_TreatsTheInternationalFormAsADuplicateOfTheStoredDomesticNumber()
    {
        using var db = CampusTestDbContext.Create();
        SeedCampus(db); // phone "024 7300 5588"

        var command = ValidCreate();
        command.Phone = "+84 24 7300 5588"; // same number, different spelling

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => CreateHandler(db).Handle(command, CancellationToken.None));

        Assert.Equal(CampusErrorCodes.CampusPhoneAlreadyExists, ex.ErrorCode);
        Assert.Single(db.Campuses); // the create rolled back
    }

    [Fact]
    public async Task Create_RollsBackTheCampusWhenTheDuplicateCheckFailsInsideTheTransaction()
    {
        using var db = CampusTestDbContext.Create();
        SeedCampus(db);

        var command = ValidCreate();
        command.Email = "HN@FPT.EDU.VN"; // same email once normalized

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateHandler(db).Handle(command, CancellationToken.None));

        Assert.Single(db.Campuses);
        Assert.Empty(db.Departments); // no orphan IC department either
    }

    // ── §10.2 / AC-08 Duplicate checks exclude the campus being edited ────────

    [Fact]
    public async Task Update_DoesNotConflictWithTheCampusBeingEdited()
    {
        using var db = CampusTestDbContext.Create();
        var campus = SeedCampus(db);

        // Same code/name/address/phone/email as stored — only the phone formatting differs.
        var command = ValidUpdate(campus.CampusId);
        command.CampusCode = "HN";
        command.Name = "FPT University Hà Nội";
        command.City = "Hà Nội";
        command.Address = "Km 29 Đại lộ Thăng Long, Thạch Thất, Hà Nội";
        command.Phone = "+84 24 7300 5588";
        command.Email = "hn@fpt.edu.vn";

        var response = await UpdateHandler(db).Handle(command, CancellationToken.None);

        Assert.Equal(campus.CampusId, response.CampusId);
        Assert.Equal("+84 24 7300 5588", db.Campuses.Single().Phone);
    }

    [Fact]
    public async Task Update_StillDetectsADuplicateAgainstADifferentCampus()
    {
        using var db = CampusTestDbContext.Create();
        SeedCampus(db);
        var other = SeedCampus(db, c =>
        {
            c.CampusCode = "HCM";
            c.Name = "FPT University TP. Hồ Chí Minh";
            c.City = "TP. Hồ Chí Minh";
            c.Address = "Lô E2a-7, Đường D1, Khu Công nghệ cao";
            c.Phone = "028 7300 5588";
            c.Email = "hcm@fpt.edu.vn";
        });

        var command = ValidUpdate(other.CampusId);
        command.CampusCode = "HCM";
        command.Name = "FPT University TP. Hồ Chí Minh";
        command.City = "TP. Hồ Chí Minh";
        command.Address = "Lô E2a-7, Đường D1, Khu Công nghệ cao";
        command.Email = "hcm@fpt.edu.vn";
        command.Phone = "(024) 7300.5588"; // belongs to the Hà Nội campus

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => UpdateHandler(db).Handle(command, CancellationToken.None));

        Assert.Equal(CampusErrorCodes.CampusPhoneAlreadyExists, ex.ErrorCode);
    }

    // ── §12.6 Update touches master data only ─────────────────────────────────

    [Fact]
    public async Task Update_LeavesStatus_IcHead_AndCreatedMetadataUntouched()
    {
        using var db = CampusTestDbContext.Create();
        var campus = SeedCampus(db, c => c.Status = EntityStatuses.Inactive);
        var createdAt = campus.CreatedAt;

        var command = ValidUpdate(campus.CampusId);
        await UpdateHandler(db).Handle(command, CancellationToken.None);

        var saved = db.Campuses.Single();
        Assert.Equal(EntityStatuses.Inactive, saved.Status);
        Assert.Equal(77ul, saved.IcHeadUserId);
        Assert.Equal(createdAt, saved.CreatedAt);
        Assert.Equal(1ul, saved.CreatedBy);
        Assert.Equal(HoUser.UserId, saved.UpdatedBy);
    }

    [Fact]
    public async Task Update_WritesNormalizedValues_NotTheRawPayload()
    {
        using var db = CampusTestDbContext.Create();
        var campus = SeedCampus(db);

        var command = ValidUpdate(campus.CampusId);
        command.CampusCode = "  qn  ";
        command.Name = "  FPT   University   Quy Nhơn ";
        command.City = "  gia lai ";
        command.Email = "  QN@FPT.EDU.VN ";

        await UpdateHandler(db).Handle(command, CancellationToken.None);

        var saved = db.Campuses.Single();
        Assert.Equal("QN", saved.CampusCode);
        Assert.Equal("FPT University Quy Nhơn", saved.Name);
        Assert.Equal("Gia Lai", saved.City);
        Assert.Equal("qn@fpt.edu.vn", saved.Email);
    }
}
