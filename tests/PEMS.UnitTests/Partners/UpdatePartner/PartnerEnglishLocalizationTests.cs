using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;
using PEMS.Application.Partners.Commands.UpdatePartner;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Partners;
using PEMS.UnitTests.Partners.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Partners.UpdatePartner;

/// <summary>
/// Guards the English country/city localization on <c>partner_translations</c>.
///
/// <para>
/// The EN row holds a country of its own — "South Korea" where the base row says "Hàn Quốc",
/// "India" where it says "Ấn Độ" — and 15 of the 42 rows in the working database differ that way.
/// <c>UpdatePartnerCommandHandler</c> used to assign the Vietnamese <c>countryVal</c> straight onto
/// the EN row whenever the EN panel was open on save, so editing a description silently rewrote
/// "South Korea" to "Hàn Quốc" and the English public page started showing Vietnamese country names.
/// Nothing caught it: the request carries no EnglishCountry, so there was no field to diff, and the
/// only visible symptom was on a page nobody re-checked after an unrelated edit.
/// </para>
/// <para>
/// EVERY test here except <see cref="Changing_The_Base_Country_Clears_The_Stale_English_Name"/>
/// fails against the pre-fix handler with "expected: South Korea, actual: Hàn Quốc".
/// </para>
/// </summary>
public sealed class PartnerEnglishLocalizationTests
{
    private const ulong CampusId = 1;
    private const ulong PartnerId = 4242;
    private const ulong ActorId = 7;

    private static PartnersTestDbContext NewDb()
    {
        var db = PartnersTestDbContext.Create();
        db.Campuses.Add(new Campus { CampusId = CampusId, CampusCode = "HN", Name = "Hà Nội", Status = "ACTIVE" });
        db.SaveChanges();
        return db;
    }

    /// <summary>A partner whose English translation is genuinely localized, as the seed data is.</summary>
    private static void SeedKoreanPartner(
        PartnersTestDbContext db, string baseCountry = "Hàn Quốc", string baseCity = "Seoul",
        string enCountry = "South Korea", string enCity = "Seoul")
    {
        db.Partners.Add(new Partner
        {
            PartnerId = PartnerId,
            OwnerCampusId = CampusId,
            PartnerCode = "P-SEOULTECH",
            Name = "SeoulTech Global Engagement Center",
            PartnerType = "UNIVERSITY",
            CooperationStatus = "ACTIVE",
            ProfileStatus = "APPROVED",
            Visibility = "INTERNAL",
            Country = baseCountry,
            City = baseCity,
            Description = "Đối tác trọng tâm cho trao đổi sinh viên kỹ thuật.",
            CreatedAt = new DateTime(2026, 3, 1),
        });
        db.PartnerTranslationsSet.Add(new PartnerTranslation
        {
            PartnerTranslationId = 1, PartnerId = PartnerId, LanguageCode = "vi",
            Name = "SeoulTech Global Engagement Center",
            Country = baseCountry, City = baseCity,
            Description = "Đối tác trọng tâm cho trao đổi sinh viên kỹ thuật.",
            TranslationSource = "LEGACY", TranslationStatus = "READY",
            CreatedAt = new DateTime(2026, 3, 1),
        });
        db.PartnerTranslationsSet.Add(new PartnerTranslation
        {
            PartnerTranslationId = 2, PartnerId = PartnerId, LanguageCode = "en",
            Name = "SeoulTech Global Engagement Center",
            Country = enCountry, City = enCity,
            Description = "Key partner for engineering student exchange.",
            TranslationSource = "MANUAL", TranslationStatus = "READY",
            CreatedAt = new DateTime(2026, 3, 1),
        });
        db.SaveChanges();
    }

    private static UpdatePartnerCommandHandler NewHandler(PartnersTestDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        currentUser.SetupGet(u => u.UserId).Returns(ActorId);
        currentUser.SetupGet(u => u.RoleCode).Returns("STAFF");
        currentUser.SetupGet(u => u.SubRole).Returns("LEADER");
        currentUser.SetupGet(u => u.PrimaryCampusId).Returns(CampusId);

        var clock = new Mock<IDateTimeService>();
        clock.SetupGet(c => c.VietnamNow).Returns(new DateTime(2026, 8, 11, 12, 0, 0));

        var sanitizer = new Mock<IHtmlSanitizerService>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        // Never consulted in these scenarios (the EN row already exists), but a throwing double would
        // hide a regression that started calling it, so it returns the input unchanged instead.
        var translator = new Mock<INewsTranslationService>();
        translator
            .Setup(t => t.TranslateTextAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> xs, string _, string _, CancellationToken _) => xs);

        return new UpdatePartnerCommandHandler(
            db, currentUser.Object, clock.Object, translator.Object, sanitizer.Object,
            NullLogger<UpdatePartnerCommandHandler>.Instance);
    }

    private static UpdatePartnerCommand BaseCommand(string country = "Hàn Quốc", string city = "Seoul") => new()
    {
        PartnerId = PartnerId,
        PartnerCode = "P-SEOULTECH",
        Name = "SeoulTech Global Engagement Center",
        PartnerType = "UNIVERSITY",
        CooperationStatus = "ACTIVE",
        Visibility = "INTERNAL",
        Country = country,
        City = city,
    };

    private static PartnerTranslation EnRow(PartnersTestDbContext db) =>
        db.PartnerTranslationsSet.AsNoTracking().Single(t => t.PartnerId == PartnerId && t.LanguageCode == "en");

    private static PartnerTranslation ViRow(PartnersTestDbContext db) =>
        db.PartnerTranslationsSet.AsNoTracking().Single(t => t.PartnerId == PartnerId && t.LanguageCode == "vi");

    // ── TC-PARTNER-EN-01 ──────────────────────────────────────────────────────

    [Fact]
    public async Task VietnameseOnly_Update_Preserves_The_English_Country()
    {
        using var db = NewDb();
        SeedKoreanPartner(db);

        // The EN panel was never opened: no English* field is sent at all.
        var cmd = BaseCommand();
        cmd.Description = "Mô tả đã cập nhật bằng tiếng Việt.";

        await NewHandler(db).Handle(cmd, CancellationToken.None);

        Assert.Equal("South Korea", EnRow(db).Country);
        Assert.Equal("Hàn Quốc", ViRow(db).Country);
        Assert.Equal("Hàn Quốc", db.Partners.AsNoTracking().Single(p => p.PartnerId == PartnerId).Country);
    }

    // ── TC-PARTNER-EN-02 ──────────────────────────────────────────────────────

    [Fact]
    public async Task Editing_Only_The_English_Description_Preserves_The_English_Country()
    {
        using var db = NewDb();
        SeedKoreanPartner(db, baseCountry: "Ấn Độ", enCountry: "India");

        // This is the exact path that used to corrupt the data: the EN panel IS open (EnglishName is
        // present), so the handler took the branch that reassigned Country from the Vietnamese value.
        var cmd = BaseCommand(country: "Ấn Độ");
        cmd.EnglishName = "SeoulTech Global Engagement Center";
        cmd.EnglishDescription = "Updated English description only.";

        await NewHandler(db).Handle(cmd, CancellationToken.None);

        var en = EnRow(db);
        Assert.Equal("India", en.Country);
        Assert.Equal("Updated English description only.", en.Description);
    }

    // ── TC-PARTNER-EN-04 ──────────────────────────────────────────────────────

    [Fact]
    public async Task English_City_Is_Preserved_By_An_Unrelated_Update()
    {
        using var db = NewDb();
        SeedKoreanPartner(db, baseCity: "Thành phố Seoul", enCity: "Seoul City");

        var cmd = BaseCommand(city: "Thành phố Seoul");
        cmd.EnglishName = "SeoulTech Global Engagement Center";
        cmd.EnglishDescription = "Another English edit.";

        await NewHandler(db).Handle(cmd, CancellationToken.None);

        Assert.Equal("Seoul City", EnRow(db).City);
        Assert.Equal("Thành phố Seoul", ViRow(db).City);
    }

    // ── TC-PARTNER-EN-06 ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_Request_That_Omits_English_Fields_Entirely_Preserves_English_Localization()
    {
        using var db = NewDb();
        SeedKoreanPartner(db, baseCountry: "Đức", baseCity: "München", enCountry: "Germany", enCity: "Munich");

        // An older API client that predates any English handling — it sends the base fields only.
        await NewHandler(db).Handle(BaseCommand(country: "Đức", city: "München"), CancellationToken.None);

        var en = EnRow(db);
        Assert.Equal("Germany", en.Country);
        Assert.Equal("Munich", en.City);
    }

    // ── TC-PARTNER-EN-07 (data half; the HTTP half is covered by the runtime smoke) ──

    [Fact]
    public async Task After_An_Update_The_Two_Languages_Still_Disagree_As_They_Should()
    {
        using var db = NewDb();
        SeedKoreanPartner(db);

        var cmd = BaseCommand();
        cmd.EnglishName = "SeoulTech Global Engagement Center";
        cmd.EnglishDescription = "Key partner for engineering student exchange.";
        await NewHandler(db).Handle(cmd, CancellationToken.None);

        // This is what the public detail endpoint reads: `chosen?.Country ?? partner.Country`.
        Assert.Equal("Hàn Quốc", ViRow(db).Country);
        Assert.Equal("South Korea", EnRow(db).Country);
        Assert.NotEqual(ViRow(db).Country, EnRow(db).Country);
    }

    // ── Staleness rule (the other half of "preserve") ─────────────────────────

    [Fact]
    public async Task Changing_The_Base_Country_Clears_The_Stale_English_Name()
    {
        using var db = NewDb();
        SeedKoreanPartner(db);

        // The partner genuinely moved. "South Korea" is no longer a localization of this partner's
        // country, so keeping it would be worse than dropping it: the public reader falls back to the
        // base value and shows the real new country.
        var cmd = BaseCommand(country: "Nhật Bản", city: "Kyoto");
        cmd.EnglishName = "SeoulTech Global Engagement Center";

        await NewHandler(db).Handle(cmd, CancellationToken.None);

        var en = EnRow(db);
        Assert.Null(en.Country);
        Assert.Null(en.City);
        Assert.Equal("Nhật Bản", ViRow(db).Country);
    }
}
