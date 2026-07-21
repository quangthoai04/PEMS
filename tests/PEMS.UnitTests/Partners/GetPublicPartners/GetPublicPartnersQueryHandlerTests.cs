using Moq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.Queries.GetPublicPartners;
using PEMS.UnitTests.Partners.TestInfrastructure;

namespace PEMS.UnitTests.Partners.GetPublicPartners;

/// <summary>
/// Unit tests for <see cref="GetPublicPartnersQueryHandler"/>'s <c>Country</c> filter — the query
/// the /partners page and the globe's pin-click deep-link both run. Because `partners.country` is
/// free text, a filter value must match partners whose stored country differs only by
/// casing/diacritics/whitespace (e.g. a globe pin emitting "nhat ban" must still find partners
/// stored as "Nhật Bản"), not just byte-identical values.
/// </summary>
public class GetPublicPartnersQueryHandlerTests
{
    private static async Task<GetPublicPartnersResponse> RunAsync(PartnersTestDbContext db, string? country = null)
    {
        var mockCache = new Mock<IPartnerDescriptionTranslationCache>();
        mockCache.Setup(m => m.TranslateAsync(It.IsAny<List<PartnerDescriptionTranslationSource>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new Dictionary<ulong, string>());
                 
        var handler = new GetPublicPartnersQueryHandler(db, mockCache.Object);
        return await handler.Handle(
            new GetPublicPartnersQuery { Country = country, Page = 1, PageSize = 24 },
            CancellationToken.None);
    }

    private static PartnersTestDbContext SeededDb()
    {
        var db = PartnersTestDbContext.Create();
        db.Campuses.Add(PartnersTestData.CreateCampus());
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task FilterByExactStoredValue_ReturnsMatchingPartners()
    {
        using var db = SeededDb();
        db.Partners.AddRange(
            PartnersTestData.CreatePartner(1, "A Corp", "Nhật Bản"),
            PartnersTestData.CreatePartner(2, "B Corp", "Hàn Quốc"));
        db.SaveChanges();

        var result = await RunAsync(db, "Nhật Bản");

        var item = Assert.Single(result.Items);
        Assert.Equal("A Corp", item.Name);
    }

    [Fact]
    public async Task FilterDifferingOnlyByCaseAndDiacritics_StillMatches()
    {
        using var db = SeededDb();
        db.Partners.Add(PartnersTestData.CreatePartner(1, "A Corp", "Việt Nam"));
        db.SaveChanges();

        var result = await RunAsync(db, "viet nam");

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task FilterMatchingMultipleRawSpellingsOfSameCountry_ReturnsAllOfThem()
    {
        using var db = SeededDb();
        db.Partners.AddRange(
            PartnersTestData.CreatePartner(1, "A Corp", "Việt Nam"),
            PartnersTestData.CreatePartner(2, "B Corp", "VIỆT NAM"),
            PartnersTestData.CreatePartner(3, "C Corp", "Hàn Quốc"));
        db.SaveChanges();

        var result = await RunAsync(db, "Việt Nam");

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, i => i.Name == "C Corp");
    }

    [Fact]
    public async Task ApprovedButNotPublic_NeverReturned()
    {
        using var db = SeededDb();
        db.Partners.Add(PartnersTestData.CreatePartner(
            1, "Internal Corp", "Laos", profileStatus: "APPROVED", visibility: "INTERNAL"));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task PublicButNotApproved_NeverReturned()
    {
        using var db = SeededDb();
        db.Partners.Add(PartnersTestData.CreatePartner(
            1, "Draft Corp", "Laos", profileStatus: "DRAFT", visibility: "PUBLIC"));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task NoCountryFilter_ReturnsAllPublicApprovedPartners()
    {
        using var db = SeededDb();
        db.Partners.AddRange(
            PartnersTestData.CreatePartner(1, "A Corp", "Nhật Bản"),
            PartnersTestData.CreatePartner(2, "B Corp", "Hàn Quốc"),
            PartnersTestData.CreatePartner(3, "C Corp", "Draft", profileStatus: "DRAFT"));
        db.SaveChanges();

        var result = await RunAsync(db);

        Assert.Equal(2, result.TotalCount);
    }
}
