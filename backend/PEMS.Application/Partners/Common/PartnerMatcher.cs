using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Partners.Common;

/// <summary>
/// Shared partner-matching pipeline (01 prompt §7). Never a naive Contains:
///  1. normalize the organization name,
///  2. exact alias key match,
///  3. exact normalized partners.name / short_name match,
///  4. email-domain vs website domain,
///  5. fuzzy token match with org stop-words stripped.
/// Confidence: >=90 strong, 70–89 suggested (user must confirm), &lt;70 NONE.
/// </summary>
public static class PartnerMatcher
{
    public static async Task<PartnerMatchDto> MatchAsync(
        IApplicationDbContext db,
        string? organizationName,
        string? contactEmail,
        CancellationToken cancellationToken)
    {
        var key = PartnerNormalization.NormalizeKey(organizationName);
        var emailDomain = PartnerNormalization.EmailDomain(contactEmail);

        if (string.IsNullOrEmpty(key) && emailDomain is null)
            return None();

        // 2) Exact alias match
        if (!string.IsNullOrEmpty(key))
        {
            var alias = await db.PartnerAliases
                .Where(a => a.Status == "ACTIVE" && a.AliasNameKey == key)
                .Select(a => new { a.PartnerId })
                .FirstOrDefaultAsync(cancellationToken);
            if (alias is not null)
                return await Build(db, alias.PartnerId, 95m, "Matched by alias", cancellationToken);
        }

        // 3) Exact normalized name / short name match (normalize in-memory over candidate set)
        if (!string.IsNullOrEmpty(key))
        {
            var probe = key.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? key;
            var candidates = await db.Partners
                .Where(p => EF.Functions.Like(p.Name, $"%{probe}%")
                            || (p.ShortName != null && EF.Functions.Like(p.ShortName, $"%{probe}%")))
                .Select(p => new { p.PartnerId, p.Name, p.ShortName })
                .Take(200)
                .ToListAsync(cancellationToken);

            // The LIKE probe misses accent variants — widen with a bounded scan when nothing came back.
            if (candidates.Count == 0)
            {
                candidates = await db.Partners
                    .OrderByDescending(p => p.PartnerId)
                    .Select(p => new { p.PartnerId, p.Name, p.ShortName })
                    .Take(500)
                    .ToListAsync(cancellationToken);
            }

            foreach (var c in candidates)
            {
                if (PartnerNormalization.NormalizeKey(c.Name) == key
                    || PartnerNormalization.NormalizeKey(c.ShortName) == key)
                    return await Build(db, c.PartnerId, 92m, "Matched by normalized name", cancellationToken);
            }

            // 5) Fuzzy: compare with org stop-words stripped
            var stripped = PartnerNormalization.StripOrgWords(key);
            if (!string.IsNullOrEmpty(stripped))
            {
                foreach (var c in candidates)
                {
                    var candKey = PartnerNormalization.StripOrgWords(PartnerNormalization.NormalizeKey(c.Name));
                    var candShort = PartnerNormalization.StripOrgWords(PartnerNormalization.NormalizeKey(c.ShortName));
                    if (candKey == stripped || (candShort.Length > 0 && candShort == stripped))
                        return await Build(db, c.PartnerId, 78m, "Possible fuzzy match", cancellationToken);
                }
            }
        }

        // 4) Email domain vs partner website domain
        if (emailDomain is not null && !PartnerNormalization.IsGenericMailDomain(emailDomain))
        {
            var withSites = await db.Partners
                .Where(p => p.WebsiteUrl != null && p.WebsiteUrl != "")
                .Select(p => new { p.PartnerId, p.WebsiteUrl })
                .Take(1000)
                .ToListAsync(cancellationToken);

            foreach (var p in withSites)
            {
                var site = PartnerNormalization.WebsiteDomain(p.WebsiteUrl);
                if (site is null) continue;
                if (site == emailDomain || emailDomain.EndsWith("." + site, StringComparison.Ordinal))
                    return await Build(db, p.PartnerId, 85m, "Matched by email domain", cancellationToken);
            }
        }

        return None();
    }

    private static PartnerMatchDto None() => new()
    {
        MatchStatus = "NONE",
        Reason = "No matching partner found",
    };

    private static async Task<PartnerMatchDto> Build(
        IApplicationDbContext db, ulong partnerId, decimal confidence, string reason, CancellationToken ct)
    {
        var partner = await db.Partners
            .Where(p => p.PartnerId == partnerId)
            .Select(p => new { p.PartnerId, p.Name, p.ProfileStatus })
            .FirstAsync(ct);

        var matchStatus = confidence < 70m
            ? "NONE"
            : partner.ProfileStatus switch
            {
                PartnerProfileStatuses.Approved => confidence >= 90m ? "APPROVED" : "SUGGESTED",
                PartnerProfileStatuses.PendingApproval => "PENDING_APPROVAL",
                _ => "SUGGESTED",
            };

        return new PartnerMatchDto
        {
            MatchStatus = matchStatus,
            PartnerId = partner.PartnerId,
            PartnerName = partner.Name,
            ProfileStatus = partner.ProfileStatus,
            Confidence = confidence,
            Reason = reason,
        };
    }
}
