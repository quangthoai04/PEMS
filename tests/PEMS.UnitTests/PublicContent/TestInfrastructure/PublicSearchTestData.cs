using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Partners;

namespace PEMS.UnitTests.PublicContent.TestInfrastructure;

/// <summary>
/// Fixture builders for the public-search tests. Defaults describe the <b>visible</b> case for every
/// surface, so a test only states the one thing it is varying (a DRAFT status, a missing EN row, an
/// inactive area) and the rest stays valid.
/// </summary>
public static class PublicSearchTestData
{
    public static readonly DateTime Jan1 = new(2026, 1, 1);

    public static Campus Campus(ulong campusId = 1, string code = "HN", string name = "FPTU Hà Nội",
        string status = "ACTIVE", string? city = "Hà Nội") => new()
    {
        CampusId = campusId,
        CampusCode = code,
        Name = name,
        City = city,
        Status = status,
        CreatedAt = Jan1,
    };

    // ── News ──────────────────────────────────────────────────────────────────────────
    public static News News(ulong newsId, string status = "PUBLISHED", DateTime? publishedAt = null) => new()
    {
        NewsId = newsId,
        Status = status,
        AuthorUserId = 1,
        SubmittedAt = Jan1,
        PublishedAt = publishedAt ?? Jan1,
        CreatedAt = Jan1,
    };

    public static NewsTranslation NewsTranslation(
        ulong id, ulong newsId, string lang, string title, string? summary = null) => new()
    {
        NewsTranslationId = id,
        NewsId = newsId,
        LanguageCode = lang,
        Title = title,
        Slug = $"{lang}-{id}",
        Summary = summary,
        CreatedAt = Jan1,
    };

    // ── Partners ──────────────────────────────────────────────────────────────────────
    public static Partner Partner(
        ulong partnerId, string name, string? description = null, string? country = null,
        string? shortName = null, string? publicSlug = null,
        string profileStatus = "APPROVED", string visibility = "PUBLIC") => new()
    {
        PartnerId = partnerId,
        OwnerCampusId = 1,
        Name = name,
        ShortName = shortName,
        Description = description,
        Country = country,
        PublicSlug = publicSlug,
        PartnerType = "COMPANY",
        ProfileStatus = profileStatus,
        Visibility = visibility,
        CooperationStatus = "ACTIVE",
        CreatedAt = Jan1,
    };

    public static PartnerTranslation PartnerTranslation(
        ulong id, ulong partnerId, string lang, string name,
        string? description = null, string? country = null, string? shortName = null) => new()
    {
        PartnerTranslationId = id,
        PartnerId = partnerId,
        LanguageCode = lang,
        Name = name,
        ShortName = shortName,
        Description = description,
        Country = country,
        CreatedAt = Jan1,
    };

    // ── FAQs ──────────────────────────────────────────────────────────────────────────
    public static Faq Faq(
        ulong faqId, string question, string answer, string faqType = "VISIT_REQUEST",
        string status = "PUBLISHED", int displayOrder = 0) => new()
    {
        FaqId = faqId,
        Question = question,
        Answer = answer,
        FaqType = faqType,
        Status = status,
        DisplayOrder = displayOrder,
        CreatedAt = Jan1,
    };

    public static FaqTranslation FaqTranslation(
        ulong id, ulong faqId, string lang, string question, string answer) => new()
    {
        FaqTranslationId = id,
        FaqId = faqId,
        LanguageCode = lang,
        Question = question,
        Answer = answer,
        CreatedAt = Jan1,
    };

    // ── Gallery ───────────────────────────────────────────────────────────────────────
    public static GalleryArea Area(
        ulong areaId = 1, ulong campusId = 1, string name = "Khu học tập",
        string? nameEn = "Study Area", string translationStatus = "READY",
        string status = "ACTIVE") => new()
    {
        AreaId = areaId,
        CampusId = campusId,
        AreaName = name,
        AreaNameEn = nameEn,
        AreaKey = $"area-{areaId}",
        TranslationStatus = translationStatus,
        Status = status,
        CreatedAt = Jan1,
    };

    public static GalleryLocation Location(
        ulong locationId = 1, ulong areaId = 1, string name = "Thư viện",
        string? nameEn = "Library", string translationStatus = "READY",
        string status = "ACTIVE") => new()
    {
        LocationId = locationId,
        AreaId = areaId,
        LocationName = name,
        LocationNameEn = nameEn,
        LocationKey = $"loc-{locationId}",
        TranslationStatus = translationStatus,
        Status = status,
        CreatedAt = Jan1,
    };

    public static GalleryItem Item(
        ulong itemId, string title, string? titleEn = null, ulong locationId = 1,
        string status = "PUBLISHED", string translationStatus = "READY",
        DateTime? deletedAt = null, uint displayOrder = 0, string mediaKind = "IMAGE") => new()
    {
        GalleryItemId = itemId,
        LocationId = locationId,
        Title = title,
        TitleEn = titleEn,
        ItemType = "MEDIA",
        MediaKind = mediaKind,
        Status = status,
        TranslationStatus = translationStatus,
        DisplayOrder = displayOrder,
        DeletedAt = deletedAt,
        CreatedAt = Jan1,
    };

    public static GalleryItemContent ItemContent(
        ulong itemId, string descriptionVi = "", string descriptionEn = "") => new()
    {
        GalleryItemId = itemId,
        DescriptionVi = descriptionVi,
        DescriptionEn = descriptionEn,
        AudioViFileId = 0,
        AudioEnFileId = 0,
        CreatedAt = Jan1,
    };

    public static UploadedFile File(ulong fileId, string? thumbnailUrl = null) => new()
    {
        FileId = fileId,
        StorageProvider = "LOCAL",
        ObjectKey = $"key-{fileId}",
        OriginalFilename = $"file-{fileId}.jpg",
        MimeType = "image/jpeg",
        FilePurpose = "GALLERY_MEDIA",
        ThumbnailUrl = thumbnailUrl,
        UploadedAt = Jan1,
    };

    public static GalleryItemMedia Media(
        ulong mediaId, ulong itemId, ulong fileId, ulong? thumbnailFileId = null,
        bool isPrimary = true, string status = "ACTIVE", DateTime? deletedAt = null,
        uint displayOrder = 0) => new()
    {
        MediaId = mediaId,
        GalleryItemId = itemId,
        FileId = fileId,
        MediaType = "IMAGE",
        ThumbnailFileId = thumbnailFileId,
        IsPrimary = isPrimary,
        DisplayOrder = displayOrder,
        Status = status,
        DeletedAt = deletedAt,
        CreatedAt = Jan1,
    };

    /// <summary>
    /// Seeds the campus → area → location spine every gallery test needs, plus the two files a media
    /// row points at. Tests then add only their own items.
    /// </summary>
    public static async Task SeedGallerySpineAsync(PublicSearchTestDbContext db)
    {
        db.Campuses.Add(Campus());
        db.GalleryAreas.Add(Area());
        db.GalleryLocations.Add(Location());
        db.Files.Add(File(100));
        db.Files.Add(File(101, thumbnailUrl: "https://img.youtube.com/thumb.jpg"));
        await db.SaveChangesAsync();
    }

    /// <summary>Adds a fully visible gallery item (content + one ACTIVE primary media) in one call.</summary>
    public static async Task AddVisibleItemAsync(
        PublicSearchTestDbContext db, ulong itemId, string title, string? titleEn = null,
        string descriptionVi = "", string descriptionEn = "", string translationStatus = "READY",
        ulong locationId = 1, uint displayOrder = 0)
    {
        db.GalleryItems.Add(Item(itemId, title, titleEn, locationId,
            translationStatus: translationStatus, displayOrder: displayOrder));
        db.GalleryItemContents.Add(ItemContent(itemId, descriptionVi, descriptionEn));
        db.GalleryItemMedia.Add(Media(mediaId: itemId * 10, itemId: itemId, fileId: 100, thumbnailFileId: 100));
        await db.SaveChangesAsync();
    }
}
