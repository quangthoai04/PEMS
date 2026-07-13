using PEMS.Application.Partners.Queries.GetPublicPartnerCountries;
using PEMS.UnitTests.Partners.TestInfrastructure;

namespace PEMS.UnitTests.Partners.GetPublicPartnerCountries;

/// <summary>
/// Unit tests for <see cref="GetPublicPartnerCountriesQueryHandler"/> — the endpoint both the
/// Homepage globe and the public /partners page call for their country list. `partners.country`
/// is free text (no FK/enum), so these tests guard the fix for country strings that differ only
/// by casing/diacritics/whitespace being counted as separate countries, and confirm the public
/// visibility/approval rules and the "Vietnam is a hub, not a partner country" rule.
/// </summary>
public class GetPublicPartnerCountriesQueryHandlerTests
{
    private static async Task<List<PublicPartnerCountryDto>> RunAsync(PartnersTestDbContext db)
    {
        var handler = new GetPublicPartnerCountriesQueryHandler(db);
        return await handler.Handle(new GetPublicPartnerCountriesQuery(), CancellationToken.None);
    }

    private static PartnersTestDbContext SeededDb()
    {
        var db = PartnersTestDbContext.Create();
        db.Campuses.Add(PartnersTestData.CreateCampus());
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task MultiplePartnersSameCountry_AreCountedTogether()
    {
        using var db = SeededDb();
        db.Partners.AddRange(
            PartnersTestData.CreatePartner(1, "A Corp", "Nhật Bản"),
            PartnersTestData.CreatePartner(2, "B Corp", "Nhật Bản"),
            PartnersTestData.CreatePartner(3, "C Corp", "Nhật Bản"));
        db.SaveChanges();

        var result = await RunAsync(db);

        var jp = Assert.Single(result);
        Assert.Equal("Nhật Bản", jp.Value);
        Assert.Equal(3, jp.Count);
    }

    [Fact]
    public async Task NewCountryNeverSeenBefore_StillAppears()
    {
        using var db = SeededDb();
        db.Partners.Add(PartnersTestData.CreatePartner(1, "Nairobi Ltd", "Kenya"));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Contains(result, c => c.Value == "Kenya" && c.Count == 1);
    }

    [Fact]
    public async Task CountryNameDifferingOnlyByCaseOrDiacritics_CollapsesIntoOneRow()
    {
        using var db = SeededDb();
        db.Partners.AddRange(
            PartnersTestData.CreatePartner(1, "A Corp", "Việt Nam"),
            PartnersTestData.CreatePartner(2, "B Corp", "viet nam"),
            PartnersTestData.CreatePartner(3, "C Corp", "VIỆT NAM"),
            PartnersTestData.CreatePartner(4, "D Corp", "  Việt Nam  "));
        db.SaveChanges();

        var result = await RunAsync(db);

        var vn = Assert.Single(result);
        Assert.Equal(4, vn.Count);
        // Representative label picked is the most frequent raw spelling ("Việt Nam" ×2 after trim).
        Assert.Equal("Việt Nam", vn.Value);
    }

    [Fact]
    public async Task ApprovedButNotPublic_IsExcluded()
    {
        using var db = SeededDb();
        db.Partners.Add(PartnersTestData.CreatePartner(
            1, "Internal Corp", "Laos", profileStatus: "APPROVED", visibility: "INTERNAL"));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PublicButNotApproved_IsExcluded()
    {
        using var db = SeededDb();
        db.Partners.Add(PartnersTestData.CreatePartner(
            1, "Pending Corp", "Laos", profileStatus: "PENDING_APPROVAL", visibility: "PUBLIC"));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Empty(result);
    }

    [Fact]
    public async Task VietnamAsHub_IsOnlyCountedWhenARealPublicPartnerIsFromVietnam()
    {
        // The 3D globe always shows a Vietnam hub pin for FPT University, independent of this
        // endpoint (GlobeShowcase.tsx VIETNAM_HUB is a hardcoded display point, not sourced from
        // here). This handler must never invent a Vietnam row on its own — it only appears when a
        // real APPROVED+PUBLIC partner's country is Vietnam, exactly like any other country.
        using var db = SeededDb();
        db.Partners.Add(PartnersTestData.CreatePartner(1, "Foreign Corp", "Hàn Quốc"));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.DoesNotContain(result, c => c.Value.Contains("Việt", StringComparison.OrdinalIgnoreCase)
            || c.Value.Contains("Vietnam", StringComparison.OrdinalIgnoreCase));
        Assert.Single(result);
    }

    [Fact]
    public async Task NullOrEmptyCountry_IsExcluded()
    {
        using var db = SeededDb();
        db.Partners.AddRange(
            PartnersTestData.CreatePartner(1, "No Country Corp", null),
            PartnersTestData.CreatePartner(2, "Empty Country Corp", ""));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ResultIsNotLimitedToASmallFixedCount()
    {
        // Regression guard for the reported bug: no slice/take/pageSize limiting the country list
        // to a fixed 6 or 7 — every distinct real country among public partners must appear.
        using var db = SeededDb();
        var countries = new[]
        {
            "Nhật Bản", "Hàn Quốc", "Trung Quốc", "Singapore", "Thái Lan",
            "Malaysia", "Indonesia", "Philippines", "Ấn Độ", "Đức",
        };
        ulong id = 1;
        foreach (var country in countries)
        {
            db.Partners.Add(PartnersTestData.CreatePartner(id++, $"Partner {id}", country));
        }
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Equal(countries.Length, result.Count);
    }
}
