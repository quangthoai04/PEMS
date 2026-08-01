using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PEMS.Application.Common;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.Delegations;

/// <summary>
/// The three setup-progress endpoints over real HTTP, against a real database, with a real renderer and
/// a real MIME writer (§10.3).
///
/// <para>
/// The handler-level suite already covers the decision logic. What only this level can answer is whether
/// the route, the auth filter, the exception middleware and the serializer agree with it: a guard that
/// throws <c>ForbiddenException</c> is worth nothing if the middleware maps it to 500, and a body the
/// handler built correctly is worth nothing if it does not survive JSON.
/// </para>
/// <para>
/// The seeded instance carries internal data ON PURPOSE — a preparation note, an offline coordination
/// note, a note to FPTU, a media-consent note — with recognisable markers. Several tests below assert
/// those markers appear in neither the response body, nor the stored draft, nor the produced
/// <c>.eml</c>, nor the attached PDF. Seeding an instance without them would make the leak tests pass
/// for the wrong reason.
/// </para>
/// </summary>
public sealed class VisitSetupProgressEmailApiTests : IAsyncLifetime
{
    private readonly PemsWebApplicationFactory _factory = new();
    private readonly FakeGoogleDriveStorage _drive = new();
    private readonly string _pickup =
        Path.Combine(Path.GetTempPath(), "pems-setup-progress-eml-" + Guid.NewGuid().ToString("N"));

    /// <summary>Markers for data that must never leave the FPT side. Distinctive so a substring match is proof.</summary>
    private const string InternalPrepNote = "NOIBOPREP-briefing-hieu-truong-ghe-qua";
    private const string InternalOfflineNote = "NOIBOOFFLINE-da-goi-dien-anh-Tuan";
    private const string InternalNoteToFptu = "NOIBOFPTU-khach-hay-phan-nan";
    private const string InternalMediaNote = "NOIBOMEDIA-khong-duoc-chup-ong-Tanaka";

    private ulong _hostId, _hostSessionId;
    private ulong _newHostId, _newHostSessionId;
    private ulong _outsiderId, _outsiderSessionId;
    private ulong _participantId;
    private ulong _campusId;

    private string _guestContactEmail = "";
    private string _registrantEmail = "";
    private string _participantEmail = "";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_pickup);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _hostId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Staff);
        _hostSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _hostId, EffectiveRole.Staff);

        _newHostId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        _newHostSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _newHostId, EffectiveRole.StaffLeader);

        _outsiderId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Visitor);
        _outsiderSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _outsiderId, EffectiveRole.Visitor);

        var host = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _hostId);
        _campusId = host.PrimaryCampusId!.Value;

        // The CC side: an accepted, non-host participant with a real address on their user row.
        _participantId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Student);
        _participantEmail = await db.Users.AsNoTracking()
            .Where(u => u.UserId == _participantId).Select(u => u.Email).FirstAsync();

        var stamp = Guid.NewGuid().ToString("N")[..8];
        _guestContactEmail = $"setupprog-contact-{stamp}@partner.example.com";
        _registrantEmail = $"setupprog-registrant-{stamp}@partner.example.com";
    }

    public async Task DisposeAsync()
    {
        // Released explicitly: an undisposed factory keeps its connection pool open, and this suite is
        // one of several running in parallel against one MySQL server.
        if (_configured is not null) await _configured.DisposeAsync();
        await _factory.DisposeAsync();

        try { if (Directory.Exists(_pickup)) Directory.Delete(_pickup, recursive: true); }
        catch (IOException) { /* a leftover temp dir must never fail a run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private WebApplicationFactory<PEMS.Api.Controllers.FaqsController>? _configured;

    /// <summary>
    /// The real host with two things swapped: SMTP writes <c>.eml</c> files to a pickup directory
    /// instead of talking to a server, and Google Drive is the on-disk double. Everything the flow
    /// itself is made of — handlers, guards, renderer, MIME builder, file/document rows — is real.
    ///
    /// <para>
    /// Built ONCE per test. <see cref="WebApplicationFactory{T}.WithWebHostBuilder"/> stands up a whole
    /// new host — DI container, EF model, connection pool — every time it is called, and calling it per
    /// HTTP client left dozens alive at once. Their pools exhausted the MySQL connection limit and made
    /// unrelated suites running beside this one fail, which looked like a defect in those suites.
    /// </para>
    /// </summary>
    private WebApplicationFactory<PEMS.Api.Controllers.FaqsController> ConfiguredFactory()
        => _configured ??= _factory
            .WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Smtp:Enabled"] = "true",
                        ["Smtp:FromEmail"] = "no-reply@pems.test",
                        ["Smtp:FromName"] = "PEMS",
                        ["Smtp:PickupDirectory"] = _pickup,
                        // The double treats these as root paths and never calls out. Both are needed:
                        // the visit-photo tree is where the delegation folder hangs, and the document
                        // folder is where the report itself is filed. Neither falls back to the root
                        // id — the resolver refuses a missing dedicated folder rather than guess.
                        ["GoogleDrive:RootFolderId"] = "fake-root",
                        ["GoogleDrive:VisitRequestPhotoFolderId"] = "fake-root/visit-photos",
                        ["GoogleDrive:VisitRequestDocumentFolderId"] = "fake-root/visit-documents",
                    }));

                b.ConfigureServices(services =>
                    services.AddScoped<PEMS.Application.Common.Interfaces.IGoogleDriveStorageService>(_ => _drive));
            });

    private HttpClient Client(ulong userId, string roleCode, ulong sessionId)
    {
        var client = ConfiguredFactory().CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, roleCode);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());
        return client;
    }

    private HttpClient HostClient() => Client(_hostId, "STAFF", _hostSessionId);
    private HttpClient NewHostClient() => Client(_newHostId, "STAFF_LEADER", _newHostSessionId);
    private HttpClient OutsiderClient() => Client(_outsiderId, "VISITOR", _outsiderSessionId);

    private static string Draft(ulong r, ulong i) =>
        $"/api/delegations/{r}/campuses/{i}/setup-progress-email/draft";
    private static string Refresh(ulong r, ulong i, ulong d) =>
        $"/api/delegations/{r}/campuses/{i}/setup-progress-email/drafts/{d}/refresh-report";
    private static string Send(ulong r, ulong i, ulong d) =>
        $"/api/delegations/{r}/campuses/{i}/setup-progress-email/drafts/{d}/send";

    /// <summary>
    /// One approved campus instance in the preparation window, hosted by <c>_hostId</c>, complete enough
    /// for the report and the tables: a named delegation, two guests, an accepted participant, an agenda
    /// item with a party in charge, a preparation item — and four pieces of internal text.
    /// </summary>
    private async Task<(ulong RequestId, ulong InstanceId)> SeedAsync(
        string instanceStatus = VisitInstanceStatus.BeforeVisit,
        string requestStatus = VisitRequestStatuses.Approved)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = VietnamTime.Now();
        var start = now.AddDays(5);

        var visit = new VisitRequest
        {
            RequestCode = $"UT-SETUPMAIL-{Guid.NewGuid().ToString("N")[..6]}",
            VisitorUserId = _outsiderId,
            RegistrantUserId = _outsiderId,
            PrimaryContactAccessStatus = "ACTIVE",
            RegistrantFullName = "Nguyen Van Dang Ky",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Kyoto University",
            RegistrantJobTitle = "Coordinator",
            RegistrantPhone = "0999999901",
            RegistrantEmail = _registrantEmail,
            VisitScope = "SINGLE_CAMPUS",
            ContactPersonFullName = "Tanaka Hiro",
            ContactPersonOrganization = "Kyoto University",
            ContactPersonPhone = "0888888801",
            ContactPersonEmail = _guestContactEmail,
            Status = requestStatus,
            ResubmissionCount = 0,
            CreatedAt = now,
            SubmittedAt = now,
            CampusInstances = new List<VisitRequestCampus>
            {
                new()
                {
                    CampusId = _campusId,
                    // Always born in the preparation window, then moved below. A trigger refuses an
                    // instance at DURING_VISIT or later with no agenda, and a real visit reaches those
                    // states by transitioning after its schedule exists — not by being created there.
                    Status = VisitInstanceStatus.BeforeVisit,
                    PlannedStartAt = start,
                    PlannedEndAt = start.AddHours(3),
                    CurrentHostUserId = _hostId,
                    HostAssignedBy = _newHostId,
                    HostAssignedAt = now,
                    CoordinatorUserId = _newHostId,
                    CoordinatorAssignedBy = _newHostId,
                    CoordinatorAssignedAt = now,
                    DecidedBy = _newHostId,
                    DecidedAt = now,
                    DecisionActorRole = "STAFF_LEADER",
                    DecisionSource = "STANDARD_CAMPUS_REVIEW",
                    // INTERNAL — must never be quoted to the guest.
                    PreparationNote = InternalPrepNote,
                    CreatedAt = now,
                },
            },
        };

        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();

        var instance = visit.CampusInstances.First();

        db.Set<VisitInstanceFormDetail>().Add(new VisitInstanceFormDetail
        {
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = "Doan Dai hoc Kyoto",
            VisitType = "CAMPUS_TOUR",
            Purpose = "Tham quan co so va ky ket hop tac",
            WorkingContent = "Trao doi hop tac dao tao",
            OperationalContactFullName = "Tanaka Hiro",
            OperationalContactEmail = _guestContactEmail,
            WorkingLanguage = "EN",
            TransportationNote = "Doan can xe 16 cho don tai san bay",
            MediaConsentStatus = "AGREED",
            // INTERNAL — both of these sit on the same row as the shareable fields.
            MediaConsentNote = InternalMediaNote,
            NoteToFptu = InternalNoteToFptu,
            CreatedAt = now,
        });

        var guest = new VisitGuestMember
        {
            VisitRequestId = visit.VisitRequestId,
            MemberType = "GUEST",
            DisplayOrder = 1,
            FullName = "Tanaka Hiro",
            Organization = "Kyoto University",
            JobTitle = "Professor",
            Nationality = "JP",
            CreatedAt = now,
        };
        db.Set<VisitGuestMember>().Add(guest);
        await db.SaveChangesAsync();

        db.Set<VisitInstanceGuestMember>().Add(new VisitInstanceGuestMember
        {
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            GuestMemberId = guest.GuestMemberId,
            DisplayOrder = 1,
            CreatedAt = now,
        });

        db.Set<VisitParticipant>().AddRange(
            new VisitParticipant
            {
                VisitInstanceId = instance.VisitInstanceId,
                UserId = _participantId,
                ParticipantRole = "IC_SUPPORT",
                IsHost = false,
                Status = ParticipantStatuses.Accepted,
                InvitedBy = _hostId,
                InvitedAt = now,
                RespondedAt = now,
                CreatedAt = now,
            },
            // Not accepted: must not be copied. Uses the outsider's account so the address is real.
            new VisitParticipant
            {
                VisitInstanceId = instance.VisitInstanceId,
                UserId = _outsiderId,
                ParticipantRole = "IC_SUPPORT",
                IsHost = false,
                Status = ParticipantStatuses.Invited,
                InvitedBy = _hostId,
                InvitedAt = now,
                CreatedAt = now,
            });

        db.Set<VisitAgenda>().Add(new VisitAgenda
        {
            VisitInstanceId = instance.VisitInstanceId,
            Title = "Don doan tai sanh Beta",
            StartTime = start,
            EndTime = start.AddMinutes(30),
            Description = "Chup anh luu niem",
            Location = "Sanh Beta",
            ResponsibleName = "Phong Hop tac Quoc te",
            SequenceOrder = 1,
            CreatedAt = now,
        });

        db.Set<VisitLogisticsItem>().Add(new VisitLogisticsItem
        {
            VisitInstanceId = instance.VisitInstanceId,
            ItemType = "ROOM",
            Title = "Phong hop Alpha",
            Quantity = 1,
            UsageStartAt = start,
            UsageEndAt = start.AddHours(2),
            Status = "ACCEPTED",
            CoordinationMode = "OFFLINE_COORDINATED",
            // INTERNAL — who was phoned about the room is not the guest's business.
            OfflineCoordinationNote = InternalOfflineNote,
            RequestedBy = _hostId,
            RequestedAt = now,
            CreatedAt = now,
        });

        await db.SaveChangesAsync();

        // Now that the schedule exists, the instance may legally hold a later status.
        if (instanceStatus != VisitInstanceStatus.BeforeVisit)
        {
            instance.Status = instanceStatus;
            await db.SaveChangesAsync();
        }

        return (visit.VisitRequestId, instance.VisitInstanceId);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Prepares a draft as the host and returns the parsed response. Fails unless it succeeded.</summary>
    private async Task<JsonElement> PrepareAsync(ulong requestId, ulong instanceId)
    {
        var response = await HostClient().PostAsJsonAsync(Draft(requestId, instanceId), new { languageCode = "vi" });
        var payload = await response.Content.ReadAsStringAsync();

        // The body is in the message on purpose: a bare "expected OK, got 422" from a prepare that every
        // other test depends on says nothing about which rule refused it.
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Prepare failed with {(int)response.StatusCode} {response.StatusCode}: {payload}");

        return JsonDocument.Parse(payload).RootElement;
    }

    /// <summary>
    /// Copies a produced artefact into <c>.testout/evidence/setup-progress/</c> at the repository root
    /// and returns where it landed, so the message and the report a run actually produced can be opened
    /// and read afterwards rather than only described. Gitignored; overwritten each run.
    /// </summary>
    private static string SaveEvidence(string fileName, byte[] content)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
               !(Directory.Exists(Path.Combine(dir.FullName, "backend")) &&
                 Directory.Exists(Path.Combine(dir.FullName, "tests"))))
            dir = dir.Parent;

        var target = Path.Combine(
            dir?.FullName ?? Path.GetTempPath(), ".testout", "evidence", "setup-progress");
        Directory.CreateDirectory(target);

        var path = Path.Combine(target, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static IEnumerable<string> InternalMarkers()
    {
        yield return InternalPrepNote;
        yield return InternalOfflineNote;
        yield return InternalNoteToFptu;
        yield return InternalMediaNote;
    }

    // ── Prepare ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_current_host_gets_a_draft_with_the_report_attached_and_the_setup_tables_in_the_body()
    {
        var (requestId, instanceId) = await SeedAsync();

        var body = await PrepareAsync(requestId, instanceId);

        var draftId = body.GetProperty("draftId").GetUInt64();
        Assert.True(draftId > 0);
        Assert.True(body.GetProperty("reportFileId").GetUInt64() > 0);
        Assert.EndsWith(".pdf", body.GetProperty("reportFileName").GetString());

        var html = body.GetProperty("bodyHtml").GetString()!;
        // The tables, not just any body: each of these is one of the sections §1 requires.
        Assert.Contains("<table", html);
        Assert.Contains("Doan Dai hoc Kyoto", html);
        Assert.Contains("Trao doi hop tac dao tao", html);
        Assert.Contains("Tanaka Hiro", html);
        Assert.Contains("Don doan tai sanh Beta", html);
        Assert.Contains("Phong Hop tac Quoc te", html);      // the party in charge, per §1
        Assert.Contains("Phong hop Alpha", html);
        Assert.Contains("Doan can xe 16 cho don tai san bay", html);
        Assert.Contains("Dữ liệu được cập nhật lúc", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.EmailDrafts.AsNoTracking().FirstAsync(d => d.EmailDraftId == draftId);
        Assert.Equal(html, stored.BodyContent);
    }

    [Fact]
    public async Task The_default_envelope_puts_the_guest_addresses_in_to_and_accepted_participants_in_cc_with_no_bcc()
    {
        var (requestId, instanceId) = await SeedAsync();

        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.EmailDraftRecipients.AsNoTracking()
            .Where(r => r.EmailDraftId == draftId).ToListAsync();

        var to = rows.Where(r => r.RecipientType == "TO").Select(r => r.RecipientEmail).ToList();
        var cc = rows.Where(r => r.RecipientType == "CC").Select(r => r.RecipientEmail).ToList();
        var bcc = rows.Where(r => r.RecipientType == "BCC").ToList();

        // TO is the two addresses PEMS actually holds for the guest side. No address is invented for the
        // named delegation roster, which has no email column at all (§3).
        Assert.Contains(_guestContactEmail, to);
        Assert.Contains(_registrantEmail, to);
        Assert.Contains(_participantEmail, cc);
        Assert.Empty(bcc);

        // The INVITED participant has not agreed to be presented to the guest as part of the reception.
        var outsiderEmail = await db.Users.AsNoTracking()
            .Where(u => u.UserId == _outsiderId).Select(u => u.Email).FirstAsync();
        Assert.DoesNotContain(outsiderEmail, cc);
    }

    [Fact]
    public async Task An_address_that_is_both_the_guest_contact_and_a_participant_stays_in_to_and_appears_once()
    {
        var (requestId, instanceId) = await SeedAsync();

        // Give the accepted participant the guest contact's address: the same person, two roles.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == requestId);
            visit.ContactPersonEmail = _participantEmail;
            await db.SaveChangesAsync();
        }

        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db2.EmailDraftRecipients.AsNoTracking()
            .Where(r => r.EmailDraftId == draftId).ToListAsync();

        Assert.Single(rows, r => string.Equals(r.RecipientEmail, _participantEmail, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("TO", rows.Single(r =>
            string.Equals(r.RecipientEmail, _participantEmail, StringComparison.OrdinalIgnoreCase)).RecipientType);
    }

    [Fact]
    public async Task Nothing_internal_reaches_the_prepared_body()
    {
        var (requestId, instanceId) = await SeedAsync();

        var html = (await PrepareAsync(requestId, instanceId)).GetProperty("bodyHtml").GetString()!;

        foreach (var marker in InternalMarkers())
            Assert.DoesNotContain(marker, html);
    }

    [Fact]
    public async Task Somebody_who_is_not_the_host_is_refused()
    {
        var (requestId, instanceId) = await SeedAsync();

        var response = await OutsiderClient().PostAsJsonAsync(Draft(requestId, instanceId), new { languageCode = "vi" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_rejected_before_any_draft_exists()
    {
        var (requestId, instanceId) = await SeedAsync();

        var anonymous = ConfiguredFactory().CreateClient();
        var response = await anonymous.PostAsJsonAsync(Draft(requestId, instanceId), new { languageCode = "vi" });

        // 401 or 403 — which one is a property of the AUTH SCHEME, and this host runs TestAuthHandler in
        // place of JwtBearer, so pinning the exact code would assert the test rig rather than the
        // endpoint. What matters is that an unauthenticated caller is stopped and no draft exists.
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected the anonymous caller to be refused, got {(int)response.StatusCode}.");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.EmailDrafts.AnyAsync(d => d.RelatedId == instanceId));
    }

    [Fact]
    public async Task An_instance_that_does_not_belong_to_the_named_request_is_not_found()
    {
        var (_, instanceId) = await SeedAsync();
        var (otherRequestId, _) = await SeedAsync();

        var response = await HostClient().PostAsJsonAsync(Draft(otherRequestId, instanceId), new { languageCode = "vi" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(VisitInstanceStatus.DuringVisit)]
    [InlineData(VisitInstanceStatus.AfterVisit)]
    [InlineData(VisitInstanceStatus.Closed)]
    public async Task A_visit_past_the_preparation_window_is_refused(string status)
    {
        var (requestId, instanceId) = await SeedAsync(instanceStatus: status);

        var response = await HostClient().PostAsJsonAsync(Draft(requestId, instanceId), new { languageCode = "vi" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Handover: the flag the browser read is not the authority ────────────

    /// <summary>
    /// The schema will not let this suite build the "replaced host" state: the canonical
    /// <c>trg_visit_campuses_assignment_validate_bu</c> raises "Official host cannot be changed after
    /// first assignment" on any UPDATE that moves <c>current_host_user_id</c> once it is set, so an
    /// approved instance cannot change hands at all in this database. The old-host case therefore stays
    /// covered by the handler-level suite, which can construct it.
    ///
    /// <para>
    /// What IS reachable here, and worth pinning, is the neighbouring guarantee: knowing a valid draft
    /// id buys a non-host nothing. Both write routes re-derive the caller's standing from the database
    /// on every call rather than trusting that the draft exists and was addressed to them.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Knowing_the_draft_id_does_not_let_a_non_host_refresh_or_send_it()
    {
        var (requestId, instanceId) = await SeedAsync();
        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        var refresh = await NewHostClient().PostAsJsonAsync(Refresh(requestId, instanceId, draftId), new { });
        var send = await NewHostClient().PostAsync(Send(requestId, instanceId, draftId), null);
        var outsiderSend = await OutsiderClient().PostAsync(Send(requestId, instanceId, draftId), null);

        Assert.Equal(HttpStatusCode.Forbidden, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderSend.StatusCode);

        // Refused is not the same as consumed: the draft is untouched and nothing was delivered.
        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db2.EmailDrafts.AsNoTracking().FirstAsync(d => d.EmailDraftId == draftId);
        Assert.Equal(EmailDraftStatus.DRAFT, stored.Status);
        Assert.Empty(Directory.GetFiles(_pickup, "*.eml"));
    }

    [Fact]
    public async Task A_visit_that_started_after_composing_cannot_be_sent_about()
    {
        var (requestId, instanceId) = await SeedAsync();
        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var instance = await db.VisitRequestCampuses.FirstAsync(c => c.VisitInstanceId == instanceId);
            instance.Status = VisitInstanceStatus.DuringVisit;
            await db.SaveChangesAsync();
        }

        var response = await HostClient().PostAsync(Send(requestId, instanceId, draftId), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Refresh: both halves, one snapshot ──────────────────────────────────

    [Fact]
    public async Task Refreshing_rebuilds_the_body_and_the_pdf_from_one_newer_snapshot()
    {
        var (requestId, instanceId) = await SeedAsync();
        var prepared = await PrepareAsync(requestId, instanceId);
        var draftId = prepared.GetProperty("draftId").GetUInt64();
        var firstFileId = prepared.GetProperty("reportFileId").GetUInt64();
        var firstBody = prepared.GetProperty("bodyHtml").GetString()!;

        // The setup changes underneath the open draft — the case the sync button exists for.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var agenda = await db.Set<VisitAgenda>().FirstAsync(a => a.VisitInstanceId == instanceId);
            agenda.Title = "Doi lich: hop tai phong Gamma";
            agenda.ResponsibleName = "Phong Dao tao";
            await db.SaveChangesAsync();
        }

        var response = await HostClient().PostAsJsonAsync(Refresh(requestId, instanceId, draftId), new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await JsonAsync(response);

        var secondFileId = refreshed.GetProperty("reportFileId").GetUInt64();
        var secondBody = refreshed.GetProperty("bodyHtml").GetString()!;

        Assert.True(refreshed.GetProperty("bodyRewritten").GetBoolean());
        Assert.NotEqual(firstFileId, secondFileId);          // the PDF was rebuilt
        Assert.NotEqual(firstBody, secondBody);              // and so was the body
        Assert.Contains("Doi lich: hop tai phong Gamma", secondBody);
        Assert.Contains("Phong Dao tao", secondBody);
        Assert.DoesNotContain("Don doan tai sanh Beta", secondBody);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // The stored draft now holds the rebuilt body …
        var stored = await db2.EmailDrafts.AsNoTracking().FirstAsync(d => d.EmailDraftId == draftId);
        Assert.Equal(secondBody, stored.BodyContent);

        // … and exactly one mandatory report, the new one. Two would leave the guest choosing.
        var attachments = await db2.EmailDraftAttachments.AsNoTracking()
            .Where(a => a.EmailDraftId == draftId).ToListAsync();
        Assert.Single(attachments);
        Assert.Equal(secondFileId, attachments[0].FileId);

        // The PDF really was re-rendered, not merely re-pointed: a second render of the SAME data would
        // still differ only in its timestamp, so what this pins is that a new document was produced and
        // stored, and that the old one is still readable rather than overwritten in place.
        var newPdf = await ReadPdfBytesAsync(db2, secondFileId);
        var oldPdf = await ReadPdfBytesAsync(db2, firstFileId);
        Assert.NotEqual(oldPdf, newPdf);
    }

    [Fact]
    public async Task Refreshing_keeps_the_subject_and_the_recipient_list_the_host_chose()
    {
        var (requestId, instanceId) = await SeedAsync();
        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var draft = await db.EmailDrafts.FirstAsync(d => d.EmailDraftId == draftId);
            draft.Subject = "Tieu de Host tu sua";
            db.EmailDraftRecipients.Add(new PEMS.Domain.Entities.Emails.EmailDraftRecipient
            {
                EmailDraftId = draftId,
                RecipientEmail = "them-nguoi-nhan@partner.example.com",
                RecipientType = "TO",
                DisplayOrder = 99,
                CreatedAt = VietnamTime.Now(),
            });
            await db.SaveChangesAsync();
        }

        var response = await HostClient().PostAsJsonAsync(Refresh(requestId, instanceId, draftId), new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db2.EmailDrafts.AsNoTracking().FirstAsync(d => d.EmailDraftId == draftId);
        var emails = await db2.EmailDraftRecipients.AsNoTracking()
            .Where(r => r.EmailDraftId == draftId).Select(r => r.RecipientEmail).ToListAsync();

        Assert.Equal("Tieu de Host tu sua", stored.Subject);
        Assert.Contains("them-nguoi-nhan@partner.example.com", emails);
    }

    [Fact]
    public async Task A_draft_from_another_instance_cannot_be_refreshed_through_this_route()
    {
        var (requestId, instanceId) = await SeedAsync();
        var (otherRequestId, otherInstanceId) = await SeedAsync();
        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        var response = await HostClient()
            .PostAsJsonAsync(Refresh(otherRequestId, otherInstanceId, draftId), new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Send ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_that_lost_its_report_is_refused_rather_than_sent_bare()
    {
        var (requestId, instanceId) = await SeedAsync();
        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.EmailDraftAttachments.Where(a => a.EmailDraftId == draftId).ExecuteDeleteAsync();
        }

        var response = await HostClient().PostAsync(Send(requestId, instanceId, draftId), null);

        // The template's own words promise an attached report; sending without one makes the mail lie.
        // 400, not 409: the handler raises ValidationException, and the message names the fix ("tạo lại
        // báo cáo") — the composer's sync button — rather than describing a state clash the Host cannot
        // act on. Asserted here so the code the client actually branches on is pinned end to end.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Directory.GetFiles(_pickup, "*.eml"));
    }

    [Fact]
    public async Task An_ordinary_composed_email_cannot_be_pushed_through_the_setup_progress_send_route()
    {
        var (requestId, instanceId) = await SeedAsync();

        ulong foreignDraftId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var anyTemplate = await db.EmailTemplates.AsNoTracking()
                .Where(t => t.TemplateCode != "VISIT_SETUP_PROGRESS_UPDATE")
                .Select(t => t.EmailTemplateId).FirstAsync();

            var foreign = new PEMS.Domain.Entities.Emails.EmailDraft
            {
                EmailTemplateId = anyTemplate,
                RelatedType = "VISIT_INSTANCE",
                RelatedId = instanceId,
                Subject = "Thu thuong",
                BodyContent = "<p>khong phai cap nhat chuan bi</p>",
                BodyFormat = EmailBodyFormat.HTML,
                Status = EmailDraftStatus.DRAFT,
                CreatedBy = _hostId,
                LastEditedBy = _hostId,
                CreatedAt = VietnamTime.Now(),
            };
            db.EmailDrafts.Add(foreign);
            await db.SaveChangesAsync();
            foreignDraftId = foreign.EmailDraftId;
        }

        var response = await HostClient().PostAsync(Send(requestId, instanceId, foreignDraftId), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Directory.GetFiles(_pickup, "*.eml"));
    }

    /// <summary>
    /// The whole flow, ending in bytes: one MIME message, the envelope the draft described, the tables in
    /// the HTML part, the report as a real PDF attachment, and none of the four internal notes anywhere
    /// in it. This is the §5 evidence, produced by the API rather than described.
    /// </summary>
    [Fact]
    public async Task Sending_produces_one_mime_message_with_the_tables_the_pdf_and_no_internal_data()
    {
        var (requestId, instanceId) = await SeedAsync();
        var prepared = await PrepareAsync(requestId, instanceId);
        var draftId = prepared.GetProperty("draftId").GetUInt64();

        var response = await HostClient().PostAsync(Send(requestId, instanceId, draftId), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var files = Directory.GetFiles(_pickup, "*.eml");
        Assert.Single(files);
        var eml = new EmlMessage(await File.ReadAllTextAsync(files[0]));
        SaveEvidence("setup-progress.eml", await File.ReadAllBytesAsync(files[0]));

        // Envelope: what the composer showed is what the headers say.
        Assert.Contains(_guestContactEmail, eml.Header("To"));
        Assert.Contains(_registrantEmail, eml.Header("To"));
        Assert.Contains(_participantEmail, eml.Header("Cc"));
        Assert.Equal(string.Empty, eml.Header("Bcc"));

        // The HTML tables travelled. Read through the transfer encoding — the HTML part is base64, so
        // none of this is present as readable text in the file itself.
        var readable = eml.DecodedTextParts;
        Assert.Contains("<table", readable);
        Assert.Contains("Doan Dai hoc Kyoto", readable);
        Assert.Contains("Don doan tai sanh Beta", readable);
        Assert.Contains("Phong Hop tac Quoc te", readable);
        Assert.Contains("Phong hop Alpha", readable);

        // The PDF is attached as a PDF, and as an attachment rather than something inline. These are
        // part HEADERS, so they are read from the raw message.
        Assert.Contains("application/pdf", eml.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("attachment", eml.Raw, StringComparison.OrdinalIgnoreCase);

        // Nothing internal, in anything the recipient can read.
        foreach (var marker in InternalMarkers())
            Assert.DoesNotContain(marker, readable);

        // The attachment is the stored report and it opens as a PDF. Its TEXT is not searched here —
        // see ReadPdfBytesAsync for why that would be a false guarantee rather than a weak one.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SaveEvidence("setup-progress-report.pdf",
            await ReadPdfBytesAsync(db, prepared.GetProperty("reportFileId").GetUInt64()));

        var draft = await db.EmailDrafts.AsNoTracking().FirstAsync(d => d.EmailDraftId == draftId);
        Assert.Equal(EmailDraftStatus.SENT, draft.Status);
    }

    [Fact]
    public async Task Sending_twice_delivers_once()
    {
        var (requestId, instanceId) = await SeedAsync();
        var draftId = (await PrepareAsync(requestId, instanceId)).GetProperty("draftId").GetUInt64();

        var first = await HostClient().PostAsync(Send(requestId, instanceId, draftId), null);
        var second = await HostClient().PostAsync(Send(requestId, instanceId, draftId), null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(Directory.GetFiles(_pickup, "*.eml"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The stored report's bytes, read back the way the application reads them — through
    /// <see cref="PEMS.Application.Common.Interfaces.IFileStorageService"/>, which for a GOOGLE_DRIVE
    /// row goes out to the Drive client (here, the double). Asserts it is a real, non-empty PDF.
    ///
    /// <para>
    /// Bytes, not text. QuestPDF Flate-compresses its content streams, so the words on the page are not
    /// present as readable characters in the file. A substring search over these bytes would answer
    /// "not found" for text that IS on the page — harmless for a positive assertion that then fails
    /// loudly, but catastrophic for the negative one that matters here: "no internal note appears in
    /// the PDF" would pass no matter what the document said. Whether internal text can reach the
    /// document is therefore asserted where it can be answered honestly — at the snapshot type, which
    /// has no field to carry it (see VisitSetupEmailHtmlTests).
    /// </para>
    /// </summary>
    private async Task<byte[]> ReadPdfBytesAsync(ApplicationDbContext db, ulong fileId)
    {
        var file = await db.Files.AsNoTracking().FirstAsync(f => f.FileId == fileId);

        using var scope = ConfiguredFactory().Services.CreateScope();
        var storage = scope.ServiceProvider
            .GetRequiredService<PEMS.Application.Common.Interfaces.IFileStorageService>();

        await using var stream = await storage.OpenReadAsync(file)
            ?? throw new InvalidOperationException($"Stored report {fileId} could not be opened.");

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        Assert.True(bytes.Length > 0, "The stored report is empty.");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        return bytes;
    }
}
