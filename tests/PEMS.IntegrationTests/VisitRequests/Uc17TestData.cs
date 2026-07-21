using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.Application.Common;
using PEMS.Application.Delegations.Commands.InitiateVisitRequestV2;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Shared builders for the UC-17 OTP-challenge / idempotency Integration Tests.
///
/// The raw OTP code is never recoverable from the database (only hashes are stored), so
/// tests seed challenge rows DIRECTLY with a known session token + code, computing the
/// exact same hashes OtpService does:
///   challenge_token_hash = SHA256(sessionToken)
///   token_hash           = SHA256($"{email}:{purpose}:{code}:{challengeTokenHash}")
/// Issue-flow tests (rate limits, resend, recover) go through the real API instead.
/// </summary>
public static class Uc17TestData
{
    public const string Purpose = "VISIT_REQUEST_VERIFY";

    public static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static string ChallengeHash(string sessionToken) => Sha256Hex(sessionToken);

    public static string CodeHash(string email, string code, string sessionToken)
        => Sha256Hex($"{email}:{Purpose}:{code}:{ChallengeHash(sessionToken)}");

    /// <summary>Unique lowercase email per test so fingerprints/quotas never collide across runs.</summary>
    public static string UniqueEmail(string label)
        => $"it-uc17-{label}-{Guid.NewGuid():N}@it-uc17.pems.local";

    /// <summary>
    /// Seeds one challenge row exactly as OtpService.CreateChallengeAsync would have,
    /// along with a corresponding VisitRequestPendingForm snapshot.
    /// </summary>
    public static async Task<OtpToken> SeedChallengeAsync(
        ApplicationDbContext db,
        string email,
        string submissionId,
        string sessionToken,
        string code,
        int attemptCount = 0,
        int maxAttempts = 10,
        DateTime? expiresAt = null,
        DateTime? nextAttemptAllowedAt = null,
        DateTime? humanVerificationRequiredAt = null,
        DateTime? invalidatedAt = null,
        string issueReason = "INITIAL",
        string? campusCode = null,
        string? delegationName = null,
        DateTime? start = null,
        DateTime? end = null)
    {
        var now = VietnamTime.Now();
        var token = new OtpToken
        {
            Email = email,
            TokenType = OtpTokenTypes.OtpCode,
            Purpose = Purpose,
            TokenHash = CodeHash(email, code, sessionToken),
            ChallengeTokenHash = ChallengeHash(sessionToken),
            SubmissionId = submissionId,
            IssueReason = issueReason,
            ExpiresAt = expiresAt ?? now.AddMinutes(5),
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            NextAttemptAllowedAt = nextAttemptAllowedAt,
            HumanVerificationRequiredAt = humanVerificationRequiredAt,
            InvalidatedAt = invalidatedAt,
            InvalidationReason = humanVerificationRequiredAt is not null ? "MAX_ATTEMPTS" : null,
            CreatedAt = now
        };
        db.OtpTokens.Add(token);

        var s = start ?? now.AddDays(10).Date.AddHours(9);
        var e = end ?? s.AddHours(4);
        var cCode = campusCode ?? "HOLOLA";
        var dName = delegationName ?? "Đoàn Test";

        await SeedPendingFormAsync(db, email, submissionId, cCode, dName, s, e);

        await db.SaveChangesAsync();
        return token;
    }

    public static async Task SeedPendingFormAsync(
        ApplicationDbContext db,
        string email,
        string submissionId,
        string campusCode,
        string delegationName,
        DateTime start,
        DateTime end)
    {
        var now = VietnamTime.Now();
        var rawPayload = FormV2Payload(email, submissionId, campusCode, delegationName, start, end);
        var json = System.Text.Json.JsonSerializer.Serialize(rawPayload);
        var form = System.Text.Json.JsonSerializer.Deserialize<PEMS.Application.Common.DTOs.VisitRequestFormDataV2>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var snapshot = V2PendingFormSnapshot.Serialize(form);
        var fingerprint = V2PendingFormSnapshot.Fingerprint(form);

        var existingPending = await db.VisitRequestPendingForms.FirstOrDefaultAsync(p => p.SubmissionId == submissionId);
        if (existingPending is null)
        {
            db.VisitRequestPendingForms.Add(new PEMS.Domain.Entities.Delegations.VisitRequestPendingForm
            {
                SubmissionId = submissionId,
                RegistrantEmail = email.Trim().ToLowerInvariant(),
                FormSchemaVersion = 2,
                FingerprintV2 = fingerprint,
                SnapshotJson = snapshot,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(15),
                ConsumedAt = null,
            });
        }
        else
        {
            existingPending.RegistrantEmail = email.Trim().ToLowerInvariant();
            existingPending.FingerprintV2 = fingerprint;
            existingPending.SnapshotJson = snapshot;
            existingPending.ExpiresAt = now.AddMinutes(15);
            existingPending.ConsumedAt = null;
        }

        await db.SaveChangesAsync();
    }

    public static Task<string> FirstActiveCampusCodeAsync(ApplicationDbContext db)
        => db.Campuses.AsNoTracking()
            .Where(c => c.Status == "ACTIVE")
            .OrderBy(c => c.CampusId)
            .Select(c => c.CampusCode)
            .FirstAsync();

    public static Task<string> SecondActiveCampusCodeAsync(ApplicationDbContext db)
        => db.Campuses.AsNoTracking()
            .Where(c => c.Status == "ACTIVE")
            .OrderBy(c => c.CampusId)
            .Select(c => c.CampusCode)
            .Skip(1)
            .FirstAsync();

    /// <summary>
    /// Full valid single-campus verify payload (camelCase JSON via anonymous object).
    /// Datetimes are sent WITHOUT offset (wall-clock) so no timezone conversion happens
    /// in model binding and two calls with the same strings share one fingerprint.
    /// </summary>
    public static Dictionary<string, object?> VerifyPayload(
        string email,
        string submissionId,
        string sessionToken,
        string otpCode,
        string campusCode,
        string delegationName,
        DateTime start,
        DateTime end)
        => new()
        {
            ["registrantFullName"] = "IT UC17 Người đăng ký",
            ["registrantNationality"] = "Việt Nam",
            ["registrantOrganization"] = "Công ty Kiểm Thử UC17",
            ["registrantPosition"] = "QA",
            ["registrantPhone"] = "0912345678",
            ["registrantEmail"] = email,
            ["delegationName"] = delegationName,
            ["visitScope"] = "SINGLE_CAMPUS",
            ["visitType"] = "CAMPUS_TOUR",
            ["visitTypeOther"] = null,
            ["campusVisits"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["campusId"] = campusCode,
                    ["startDatetime"] = start.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                    ["endDatetime"] = end.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                }
            },
            ["purpose"] = "Tham quan và trao đổi hợp tác (integration test)",
            ["workingContent"] = null,
            ["visitors"] = Array.Empty<object>(),
            ["supportMembers"] = Array.Empty<object>(),
            ["contactPerson"] = new Dictionary<string, object?>
            {
                ["fullName"] = "IT UC17 Người đăng ký",
                ["organization"] = "Công ty Kiểm Thử UC17",
                ["phone"] = "0912345678",
                ["email"] = email,
            },
            ["isContactSelf"] = true,
            ["workingLanguage"] = "VI",
            ["transportationNote"] = null,
            ["mediaConsentStatus"] = "DECLINED",
            ["mediaConsentNote"] = null,
            ["partnerId"] = null,
            ["notes"] = null,
            ["otpCode"] = otpCode,
            ["submissionId"] = submissionId,
            ["sessionToken"] = sessionToken,
        };

    /// <summary>Initiate payload = verify payload minus otpCode/sessionToken.</summary>
    public static Dictionary<string, object?> InitiatePayload(
        string email, string submissionId, string campusCode, string delegationName,
        DateTime start, DateTime end)
    {
        var payload = VerifyPayload(email, submissionId, "unused", "000000", campusCode, delegationName, start, end);
        payload.Remove("otpCode");
        payload.Remove("sessionToken");
        return payload;
    }

    // ── V2 per-campus payload builders ────────────────────────────────────────

    /// <summary>
    /// Builds the per-campus V2 form payload (<c>VisitRequestFormDataV2</c> shape) for a
    /// single campus. The V2 structure nests delegation/visit content INSIDE each campus
    /// entry rather than at the request level.
    /// </summary>
    public static Dictionary<string, object?> FormV2Payload(
        string email,
        string submissionId,
        string campusCode,
        string delegationName,
        DateTime start,
        DateTime end)
        => new()
        {
            ["submissionId"] = submissionId,
            ["registrant"] = new Dictionary<string, object?>
            {
                ["fullName"] = "IT UC17 Người đăng ký",
                ["nationality"] = "Việt Nam",
                ["organization"] = "Công ty Kiểm Thử UC17",
                ["jobTitle"] = "QA",
                ["phone"] = "0912345678",
                ["email"] = email,
            },
            ["primaryContact"] = new Dictionary<string, object?>
            {
                ["fullName"] = "IT UC17 Người đăng ký",
                ["organization"] = "Công ty Kiểm Thử UC17",
                ["phone"] = "0912345678",
                ["email"] = email,
            },
            ["partnerId"] = null,
            ["campusVisits"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["campusId"] = campusCode,
                    ["plannedStartAt"] = start.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                    ["plannedEndAt"] = end.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                    ["delegationName"] = delegationName,
                    ["visitType"] = "CAMPUS_TOUR",
                    ["visitTypeOther"] = null,
                    ["purpose"] = "Tham quan và trao đổi hợp tác (integration test)",
                    ["workingContent"] = "Test working content",
                    ["visitors"] = Array.Empty<object>(),
                    ["externalSupportMembers"] = Array.Empty<object>(),
                    ["operationalContact"] = new Dictionary<string, object?>
                    {
                        ["fullName"] = "IT UC17 Người đăng ký",
                        ["organization"] = "Công ty Kiểm Thử UC17",
                        ["phone"] = "0912345678",
                        ["email"] = email,
                    },
                    ["workingLanguage"] = "VI",
                    ["transportationNote"] = null,
                    ["mediaConsentStatus"] = "DECLINED",
                    ["mediaConsentNote"] = null,
                    ["notes"] = null,
                    ["processing"] = null,
                }
            },
        };

    /// <summary>V2 initiate payload wraps the form in a <c>form</c> property.</summary>
    public static Dictionary<string, object?> InitiateV2Payload(
        string email, string submissionId, string campusCode, string delegationName,
        DateTime start, DateTime end)
        => new()
        {
            ["form"] = FormV2Payload(email, submissionId, campusCode, delegationName, start, end),
        };

    /// <summary>V2 verify payload adds otpCode + sessionToken alongside the form.</summary>
    public static Dictionary<string, object?> VerifyV2Payload(
        string email,
        string submissionId,
        string sessionToken,
        string otpCode,
        string campusCode,
        string delegationName,
        DateTime start,
        DateTime end)
        => new()
        {
            ["form"] = FormV2Payload(email, submissionId, campusCode, delegationName, start, end),
            ["otpCode"] = otpCode,
            ["sessionToken"] = sessionToken,
        };
}
