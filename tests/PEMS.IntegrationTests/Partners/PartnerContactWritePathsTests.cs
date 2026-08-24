using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Partners.VisitLinks.Commands.CreateOrUpdateVisitGuestPartnerLink;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Partners;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Partners;

/// <summary>
/// GitHub bug report (CanhIter3FixBug "Partner Contact / Business Card Data Capture") reproduced live at
/// /dashboard/visit/process/47123 → Scan Card Visit → Lưu thông tin liên hệ: a Phone value like "ádsad"
/// was rejected with "Số điện thoại người liên hệ không hợp lệ. Nhập số Việt Nam dạng...", the exact
/// <see cref="PEMS.Application.Common.Validation.PhoneNumberRules.FormatHint"/> wording — a REAL backend
/// validator rejection (unlike the earlier stale-binary "Phone field is required" investigation), because
/// <c>CreatePartnerContactCommandValidator</c>/<c>UpdatePartnerContactCommandValidator</c>/
/// <c>ConfirmBusinessCardContactCommandValidator</c> all called <c>MustBeAPhoneNumber</c>. Partner Contact
/// is external business-card/partner-supplied data, never an authentication/identity field — this suite
/// proves Create/Update go through the REAL HTTP pipeline (routing, model binding, FluentValidation,
/// handler, MySQL persistence) and accept arbitrary phone/email text, storing it EXACTLY as trimmed
/// (never reformatted/lowercased), while OCR confirm (proven at the same real-MySQL handler+persistence
/// level, in-process — the validator-level acceptance matrix for this one is in
/// PEMS.UnitTests.Validation.PartnerContactContractTests) does the same.
/// </summary>
public sealed class PartnerContactWritePathsTests : IAsyncLifetime
{
    private const string NamePrefix = "[IT-PC-WRITE] ";
    private readonly PemsWebApplicationFactory _factory = new();
    private ulong _staffLeaderId, _sessionId, _campusId;
    private ulong _partnerId;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _staffLeaderId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        _sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _staffLeaderId, EffectiveRole.StaffLeader);
        _campusId = (await db.Users.AsNoTracking().Where(u => u.UserId == _staffLeaderId)
            .Select(u => u.PrimaryCampusId).FirstAsync())!.Value;

        var partner = new Partner
        {
            OwnerCampusId = _campusId,
            Name = NamePrefix + Guid.NewGuid().ToString("N")[..8],
            PartnerType = "COMPANY",
            ProfileStatus = "APPROVED",
            Visibility = "PUBLIC",
            CreatedAt = DateTime.Now,
            CreatedBy = _staffLeaderId,
        };
        db.Partners.Add(partner);
        await db.SaveChangesAsync();
        _partnerId = partner.PartnerId;
    }

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            async Task Del(string sql, params object[] p) => await db.Database.ExecuteSqlRawAsync(sql, p);
            await Del("DELETE FROM audit_log_changes WHERE audit_log_id IN (SELECT audit_log_id FROM audit_logs WHERE entity_type = 'PartnerContact' AND entity_id IN (SELECT contact_id FROM partner_contacts WHERE partner_id = {0}))", _partnerId);
            await Del("DELETE FROM audit_logs WHERE entity_type = 'PartnerContact' AND entity_id IN (SELECT contact_id FROM partner_contacts WHERE partner_id = {0})", _partnerId);
            await Del("DELETE FROM partner_contacts WHERE partner_id = {0}", _partnerId);
            await Del("DELETE FROM partners WHERE partner_id = {0}", _partnerId);

            var jobIds = await db.BusinessCardOcrJobs.Where(j => j.ConfirmedPartnerId == _partnerId)
                .Select(j => j.OcrJobId).ToListAsync();
            foreach (var jobId in jobIds)
                await Del("DELETE FROM business_card_ocr_jobs WHERE ocr_job_id = {0}", jobId);
        }
        await _factory.DisposeAsync();
    }

    private HttpClient StaffLeaderClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _staffLeaderId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, "STAFF");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, "LEADER");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, _sessionId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PrimaryCampusIdHeader, _campusId.ToString());
        return client;
    }

    private static async Task<ulong> ContactCountAsync(ApplicationDbContext db, ulong partnerId)
        => (ulong)await db.PartnerContacts.CountAsync(c => c.PartnerId == partnerId);

    // ── §18 HTTP/model-binding boundary — the exact payload shape from the task ────────────────────

    [Fact]
    public async Task Http_boundary_create_accepts_the_exact_reported_payload()
    {
        PartnerContactTestGate.RequireDb();
        var payload = new
        {
            fullName = "International Contact",
            phone = "+1 (212) 555-1234 ext. 208",
            email = "raw-contact-value",
        };
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts", payload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("không hợp lệ", body, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking().SingleAsync(c => c.PartnerId == _partnerId);
        Assert.Equal("+1 (212) 555-1234 ext. 208", contact.Phone); // stored EXACT, never reformatted
        Assert.Equal("raw-contact-value", contact.Email); // stored EXACT, never lowercased
    }

    // ── CREATE — C1-C10 ──────────────────────────────────────────────────────────────────────────

    [Theory] // C1-C3, C5: real-world formats the old rule rejected
    [InlineData("+82 10-1234-0001")]
    [InlineData("+1 (212) 555-1234 ext. 208")]
    [InlineData("ádsad")]
    [InlineData("03-1234-5678")]
    [InlineData("Tel: +81 90 1234 5678")]
    [InlineData("Office +44 (0)20 1234 5678 x204")]
    public async Task Create_stores_arbitrary_phone_text_exactly(string phone)
    {
        PartnerContactTestGate.RequireDb();
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "Nguyễn Văn A", phone });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking()
            .Where(c => c.PartnerId == _partnerId).OrderByDescending(c => c.ContactId).FirstAsync();
        Assert.Equal(phone, contact.Phone);
        await CleanupContactAsync(db, contact.ContactId);
    }

    [Fact] // C4
    public async Task Create_blank_phone_stores_null()
    {
        PartnerContactTestGate.RequireDb();
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "Nguyễn Văn A", phone = "" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking()
            .Where(c => c.PartnerId == _partnerId).OrderByDescending(c => c.ContactId).FirstAsync();
        Assert.Null(contact.Phone);
        await CleanupContactAsync(db, contact.ContactId);
    }

    [Fact] // C5 — nonstandard email text
    public async Task Create_stores_nonstandard_email_exactly()
    {
        PartnerContactTestGate.RequireDb();
        const string email = "một giá trị user nhập";
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "Nguyễn Văn A", email });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking()
            .Where(c => c.PartnerId == _partnerId).OrderByDescending(c => c.ContactId).FirstAsync();
        Assert.Equal(email, contact.Email);
        await CleanupContactAsync(db, contact.ContactId);
    }

    [Fact] // C6
    public async Task Create_blank_email_stores_null()
    {
        PartnerContactTestGate.RequireDb();
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "Nguyễn Văn A", email = "" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking()
            .Where(c => c.PartnerId == _partnerId).OrderByDescending(c => c.ContactId).FirstAsync();
        Assert.Null(contact.Email);
        await CleanupContactAsync(db, contact.ContactId);
    }

    [Fact] // C7 — mixed-case email preserved, never lowercased at rest
    public async Task Create_preserves_email_case_exactly()
    {
        PartnerContactTestGate.RequireDb();
        const string email = "John.Smith@Partner.COM";
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "Nguyễn Văn A", email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking()
            .Where(c => c.PartnerId == _partnerId).OrderByDescending(c => c.ContactId).FirstAsync();
        Assert.Equal(email, contact.Email); // NOT "john.smith@partner.com"
        await CleanupContactAsync(db, contact.ContactId);
    }

    [Fact] // C8
    public async Task Create_blank_fullname_is_rejected()
    {
        PartnerContactTestGate.RequireDb();
        var countBefore = await CountAsync();
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(countBefore, await CountAsync());
    }

    [Fact] // C9 — overlong phone (VARCHAR(50)) is rejected gracefully, not a 500
    public async Task Create_overlong_phone_is_rejected_gracefully()
    {
        PartnerContactTestGate.RequireDb();
        var countBefore = await CountAsync();
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "Nguyễn Văn A", phone = new string('1', 51) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(countBefore, await CountAsync());
    }

    [Fact] // C10 — overlong email (VARCHAR(150)) is rejected gracefully
    public async Task Create_overlong_email_is_rejected_gracefully()
    {
        PartnerContactTestGate.RequireDb();
        var countBefore = await CountAsync();
        var response = await StaffLeaderClient().PostAsJsonAsync($"/api/partners/{_partnerId}/contacts",
            new { fullName = "Nguyễn Văn A", email = new string('a', 151) + "@x.com" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(countBefore, await CountAsync());
    }

    // ── UPDATE — U1-U10 (mirrors Create; the key regression case is JobTitle-only edit preserving
    //    an arbitrary Phone unchanged, proving Create/Update are symmetric) ─────────────────────────

    [Fact]
    public async Task Update_stores_arbitrary_phone_text_exactly()
    {
        PartnerContactTestGate.RequireDb();
        var contactId = await SeedContactAsync(phone: null, email: null);
        var response = await StaffLeaderClient().PutAsJsonAsync($"/api/partners/{_partnerId}/contacts/{contactId}",
            new { fullName = "Nguyễn Văn A", phone = "ABC-XYZ" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking().SingleAsync(c => c.ContactId == contactId);
        Assert.Equal("ABC-XYZ", contact.Phone);
    }

    [Fact] // The exact "created with a raw phone, then edit something else" regression scenario
    public async Task Update_of_jobtitle_only_leaves_an_existing_raw_phone_untouched()
    {
        PartnerContactTestGate.RequireDb();
        var contactId = await SeedContactAsync(phone: "ABC-XYZ", email: null);
        var response = await StaffLeaderClient().PutAsJsonAsync($"/api/partners/{_partnerId}/contacts/{contactId}",
            new { fullName = "Nguyễn Văn A", phone = "ABC-XYZ", jobTitle = "Giám đốc" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking().SingleAsync(c => c.ContactId == contactId);
        Assert.Equal("Giám đốc", contact.JobTitle);
        Assert.Equal("ABC-XYZ", contact.Phone); // unchanged, not reformatted
    }

    [Fact]
    public async Task Update_blank_phone_clears_it_to_null()
    {
        PartnerContactTestGate.RequireDb();
        var contactId = await SeedContactAsync(phone: "0912345678", email: null);
        var response = await StaffLeaderClient().PutAsJsonAsync($"/api/partners/{_partnerId}/contacts/{contactId}",
            new { fullName = "Nguyễn Văn A", phone = "" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking().SingleAsync(c => c.ContactId == contactId);
        Assert.Null(contact.Phone);
    }

    [Fact]
    public async Task Update_blank_fullname_is_rejected_leaving_prior_values_unchanged()
    {
        PartnerContactTestGate.RequireDb();
        var contactId = await SeedContactAsync(phone: "0912345678", email: null);
        var response = await StaffLeaderClient().PutAsJsonAsync($"/api/partners/{_partnerId}/contacts/{contactId}",
            new { fullName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = await db.PartnerContacts.AsNoTracking().SingleAsync(c => c.ContactId == contactId);
        Assert.Equal("0912345678", contact.Phone); // untouched
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private async Task<int> CountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.PartnerContacts.CountAsync(c => c.PartnerId == _partnerId);
    }

    private async Task<ulong> SeedContactAsync(string? phone, string? email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contact = new PartnerContact
        {
            PartnerId = _partnerId,
            FullName = "Seed Contact",
            Phone = phone,
            Email = email,
            SourceType = "MANUAL",
            Status = "ACTIVE",
            CreatedAt = DateTime.Now,
            CreatedBy = _staffLeaderId,
        };
        db.PartnerContacts.Add(contact);
        await db.SaveChangesAsync();
        return contact.ContactId;
    }

    private static async Task CleanupContactAsync(ApplicationDbContext db, ulong contactId)
        => await db.Database.ExecuteSqlRawAsync("DELETE FROM partner_contacts WHERE contact_id = {0}", contactId);

    // ── OCR CONFIRM — O1-O7. Real MySQL, real validator + real handler, in-process (not raw HTTP —
    //    the model-binding-boundary proof for this DTO shape is Create/Update above; this proves real
    //    persistence + the OCR-job lifecycle, which the HTTP layer does not add anything new to). ───

    private sealed class StaticUser : ICurrentUserService
    {
        private readonly ulong _id;
        private readonly ulong _campusId;
        public StaticUser(ulong id, ulong campusId) { _id = id; _campusId = campusId; }
        public bool IsAuthenticated => true;
        public ulong? UserId => _id;
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode => "STAFF";
        public string? SubRole => "LEADER";
        public ulong? PrimaryCampusId => _campusId;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class StaticClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.Now;
    }

    private async Task<ulong> SeedOcrJobAsync(ApplicationDbContext db)
    {
        var file = new UploadedFile
        {
            StorageProvider = "LOCAL",
            ObjectKey = $"it-pc-write/{Guid.NewGuid():N}.jpg",
            OriginalFilename = "card.jpg",
            UploadedAt = DateTime.Now,
        };
        db.Files.Add(file);

        var apiConfig = new ApiConfiguration
        {
            ApiCode = "IT_PC_WRITE_OCR",
            Name = NamePrefix + "OCR config",
            BaseUrl = "https://ocr.test",
            Status = "ACTIVE",
            CreatedAt = DateTime.Now,
        };
        db.ApiConfigurations.Add(apiConfig);
        await db.SaveChangesAsync();

        var job = new BusinessCardOcrJob
        {
            ScannedCardFileId = file.FileId,
            ApiConfigId = apiConfig.ApiConfigId,
            Status = BusinessCardOcrJob.StatusSucceeded,
            CreatedBy = _staffLeaderId,
            CreatedAt = DateTime.Now,
        };
        db.BusinessCardOcrJobs.Add(job);
        await db.SaveChangesAsync();
        return job.OcrJobId;
    }

    private async Task DeleteOcrJobAsync(ulong ocrJobId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        async Task Del(string sql, params object[] p) => await db.Database.ExecuteSqlRawAsync(sql, p);
        var job = await db.BusinessCardOcrJobs.AsNoTracking().SingleAsync(j => j.OcrJobId == ocrJobId);
        if (job.ConfirmedContactId is { } cid)
            await Del("DELETE FROM partner_contacts WHERE contact_id = {0}", cid);
        await Del("DELETE FROM business_card_ocr_jobs WHERE ocr_job_id = {0}", ocrJobId);
        await Del("DELETE FROM api_configurations WHERE api_config_id = {0}", job.ApiConfigId);
        await Del("DELETE FROM files WHERE file_id = {0}", job.ScannedCardFileId);
    }

    private ConfirmBusinessCardContactCommandHandler Handler(ApplicationDbContext db)
        => new(db, new StaticUser(_staffLeaderId, _campusId), new StaticClock(), new NoopSender());

    /// <summary>No visit context on any of these seeds, so the handler's post-commit
    /// visit_guest_partner_links step is a no-op — this sender is never actually invoked.</summary>
    private sealed class NoopSender : MediatR.ISender
    {
        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException("No visit context in these tests — should not be called.");
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : MediatR.IRequest
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    [Theory] // O1-O3
    [InlineData("+82 10-1234-0001")]
    [InlineData("+1 (212) 555-1234 ext. 208")]
    [InlineData("ádsad")]
    public async Task OcrConfirm_stores_arbitrary_phone_text_exactly(string phone)
    {
        PartnerContactTestGate.RequireDb();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);
        var jobId = await SeedOcrJobAsync(db);
        try
        {
            var result = await Handler(db).Handle(new ConfirmBusinessCardContactCommand
            {
                OcrJobId = jobId, PartnerId = _partnerId, FullName = "Nguyễn Văn A", Phone = phone,
            }, CancellationToken.None);

            using var scope = _factory.Services.CreateScope();
            var verify = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contact = await verify.PartnerContacts.AsNoTracking().SingleAsync(c => c.ContactId == result.ContactId);
            Assert.Equal(phone, contact.Phone);
        }
        finally { await DeleteOcrJobAsync(jobId); }
    }

    [Fact] // O4
    public async Task OcrConfirm_stores_nonstandard_email_exactly()
    {
        PartnerContactTestGate.RequireDb();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);
        var jobId = await SeedOcrJobAsync(db);
        try
        {
            const string email = "một giá trị user nhập";
            var result = await Handler(db).Handle(new ConfirmBusinessCardContactCommand
            {
                OcrJobId = jobId, PartnerId = _partnerId, FullName = "Nguyễn Văn A", Email = email,
            }, CancellationToken.None);

            using var scope = _factory.Services.CreateScope();
            var verify = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contact = await verify.PartnerContacts.AsNoTracking().SingleAsync(c => c.ContactId == result.ContactId);
            Assert.Equal(email, contact.Email);
        }
        finally { await DeleteOcrJobAsync(jobId); }
    }

    [Fact] // O5 — raw phone AND email persistence in the same confirm
    public async Task OcrConfirm_persists_raw_phone_and_email_together()
    {
        PartnerContactTestGate.RequireDb();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);
        var jobId = await SeedOcrJobAsync(db);
        try
        {
            var result = await Handler(db).Handle(new ConfirmBusinessCardContactCommand
            {
                OcrJobId = jobId, PartnerId = _partnerId, FullName = "Kim Min Jae",
                Phone = "+82 10-1234-0001", Email = "kim.minjae@seoultech.example",
            }, CancellationToken.None);

            using var scope = _factory.Services.CreateScope();
            var verify = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contact = await verify.PartnerContacts.AsNoTracking().SingleAsync(c => c.ContactId == result.ContactId);
            Assert.Equal("+82 10-1234-0001", contact.Phone);
            Assert.Equal("kim.minjae@seoultech.example", contact.Email);
            Assert.Equal("BUSINESS_CARD_OCR", contact.SourceType);
        }
        finally { await DeleteOcrJobAsync(jobId); }
    }

    [Fact] // O6
    public async Task OcrConfirm_blank_fullname_is_still_rejected()
    {
        PartnerContactTestGate.RequireDb();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);
        var jobId = await SeedOcrJobAsync(db);
        try
        {
            var validator = new ConfirmBusinessCardContactCommandValidator();
            var cmd = new ConfirmBusinessCardContactCommand { OcrJobId = jobId, PartnerId = _partnerId, FullName = "" };
            var result = await validator.ValidateAsync(cmd, CancellationToken.None);
            Assert.False(result.IsValid);
        }
        finally { await DeleteOcrJobAsync(jobId); }
    }

    [Fact] // O7 — job/partner guards retained: confirming twice is refused, second call mutates nothing
    public async Task OcrConfirm_guards_against_double_confirm()
    {
        PartnerContactTestGate.RequireDb();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);
        var jobId = await SeedOcrJobAsync(db);
        try
        {
            await Handler(db).Handle(new ConfirmBusinessCardContactCommand
            {
                OcrJobId = jobId, PartnerId = _partnerId, FullName = "Nguyễn Văn A",
            }, CancellationToken.None);

            using var db2 = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);
            var countBefore = await db2.PartnerContacts.CountAsync(c => c.PartnerId == _partnerId);
            await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.ConflictException>(() =>
                Handler(db2).Handle(new ConfirmBusinessCardContactCommand
                {
                    OcrJobId = jobId, PartnerId = _partnerId, FullName = "Nguyễn Văn B",
                }, CancellationToken.None));

            using var scope = _factory.Services.CreateScope();
            var verify = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(countBefore, await verify.PartnerContacts.CountAsync(c => c.PartnerId == _partnerId));
        }
        finally { await DeleteOcrJobAsync(jobId); }
    }

    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
}

/// <summary>Tiny shared DB-reachability gate, matching VisitSafeEditContactPhoneApiTests' own.</summary>
internal static class PartnerContactTestGate
{
    private static bool? _dbUp;
    public static void RequireDb()
    {
        if (_dbUp is null)
        {
            try
            {
                using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseMySql(
                        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None",
                        ServerVersion.AutoDetect(
                            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None"))
                    .Options);
                _dbUp = db.Database.CanConnect();
            }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }
}
