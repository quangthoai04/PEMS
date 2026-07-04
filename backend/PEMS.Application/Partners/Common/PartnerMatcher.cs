using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Partners.Common;

/// <summary>
/// Shared partner-matching pipeline (01 prompt §7). Never a naive Contains:
///  1. normalize the organization name,
///  2. exact alias key match           (score 95),
///  3. exact normalized name/short_name (score 92),
///  4. email-domain vs website domain   (score 85),
///  5. fuzzy token match, org stop-words stripped (score 78),
///  6. Levenshtein similarity on the normalized name/short_name (suggest-only):
///       sim &gt;=92% → 88, 85–91% → 78, 78–84% → 68, &lt;78% dropped.
///     Short names (&lt;8 chars) and very different lengths are skipped so it never
///     suggests unrelated partners; it also never outscores alias/exact/email-domain
///     because <see cref="PartnerMatcher"/> keeps the highest score per partner.
/// Scores: >=90 strong, 70–89 possible, &lt;70 dropped. Instead of returning the first hit,
/// all strategies contribute candidates (deduped by partner, keeping the highest score);
/// the best one drives the legacy top-level fields and the full ranked list is returned in
/// <see cref="PartnerMatchDto.Candidates"/> (top 5) for the "create or link" picker.
/// </summary>
public static class PartnerMatcher
{
    private const int MaxCandidates = 5;

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

        // partnerId → best (score, reason) seen so far.
        var scores = new Dictionary<ulong, (decimal Score, string Reason)>();
        void Consider(ulong partnerId, decimal score, string reason)
        {
            if (!scores.TryGetValue(partnerId, out var existing) || score > existing.Score)
                scores[partnerId] = (score, reason);
        }

        // 2) Exact alias match
        if (!string.IsNullOrEmpty(key))
        {
            var aliasPartnerIds = await db.PartnerAliases
                .Where(a => a.Status == "ACTIVE" && a.AliasNameKey == key)
                .Select(a => a.PartnerId)
                .ToListAsync(cancellationToken);
            foreach (var id in aliasPartnerIds)
                Consider(id, 95m, "Khớp theo tên gọi khác (alias)");
        }

        // 3) Exact normalized name/short_name + 5) fuzzy (over the same candidate set)
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

            var stripped = PartnerNormalization.StripOrgWords(key);
            foreach (var c in candidates)
            {
                if (PartnerNormalization.NormalizeKey(c.Name) == key
                    || PartnerNormalization.NormalizeKey(c.ShortName) == key)
                {
                    Consider(c.PartnerId, 92m, "Khớp theo tên tổ chức");
                    continue;
                }

                if (!string.IsNullOrEmpty(stripped))
                {
                    var candKey = PartnerNormalization.StripOrgWords(PartnerNormalization.NormalizeKey(c.Name));
                    var candShort = PartnerNormalization.StripOrgWords(PartnerNormalization.NormalizeKey(c.ShortName));
                    if (candKey == stripped || (candShort.Length > 0 && candShort == stripped))
                        Consider(c.PartnerId, 78m, "Có thể trùng theo tên rút gọn");
                }

                // 6) Levenshtein similarity on the full normalized name/short_name — suggest-only.
                // Consider() keeps the highest score, so an alias/exact/email-domain hit for the
                // same partner always wins over this lower fuzzy score.
                var lev = LevenshteinSuggestion(key, c.Name, c.ShortName);
                if (lev is { } l)
                    Consider(c.PartnerId, l.Score, $"Tên gần giống · {l.Similarity}%");
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
                    Consider(p.PartnerId, 85m, "Khớp theo tên miền email");
            }
        }

        if (scores.Count == 0)
            return None();

        // Load display fields for the strongest candidates only.
        var topIds = scores
            .OrderByDescending(kv => kv.Value.Score)
            .Take(MaxCandidates)
            .Select(kv => kv.Key)
            .ToList();

        var partners = await db.Partners
            .Where(p => topIds.Contains(p.PartnerId))
            .Select(p => new
            {
                p.PartnerId, p.Name, p.ShortName, p.ProfileStatus, p.Visibility,
                p.OwnerCampusId, p.Country, p.City,
            })
            .ToListAsync(cancellationToken);

        var campusIds = partners.Select(p => p.OwnerCampusId).Distinct().ToList();
        var campusNames = await db.Campuses
            .Where(c => campusIds.Contains(c.CampusId))
            .Select(c => new { c.CampusId, c.Name })
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var ranked = partners
            .Select(p =>
            {
                var (score, reason) = scores[p.PartnerId];
                return new PartnerMatchCandidateDto
                {
                    PartnerId = p.PartnerId,
                    Name = p.Name,
                    ShortName = p.ShortName,
                    ProfileStatus = p.ProfileStatus,
                    Visibility = p.Visibility,
                    OwnerCampusId = p.OwnerCampusId,
                    OwnerCampusName = campusNames.TryGetValue(p.OwnerCampusId, out var cn) ? cn : null,
                    Country = p.Country,
                    City = p.City,
                    MatchScore = score,
                    MatchReason = reason,
                };
            })
            .OrderByDescending(c => c.MatchScore)
            .ThenBy(c => c.Name)
            .ToList();

        var best = ranked[0];
        var matchStatus = best.MatchScore < 70m
            ? "NONE"
            : best.ProfileStatus switch
            {
                PartnerProfileStatuses.Approved => best.MatchScore >= 90m ? "APPROVED" : "SUGGESTED",
                PartnerProfileStatuses.PendingApproval => "PENDING_APPROVAL",
                _ => "SUGGESTED",
            };

        return new PartnerMatchDto
        {
            MatchStatus = matchStatus,
            PartnerId = best.PartnerId,
            PartnerName = best.Name,
            ProfileStatus = best.ProfileStatus,
            Confidence = best.MatchScore,
            Reason = best.MatchReason,
            Candidates = ranked,
        };
    }

    private static PartnerMatchDto None() => new()
    {
        MatchStatus = "NONE",
        Reason = "No matching partner found",
    };

    /// <summary>
    /// Fuzzy-string suggestion for one candidate: the strongest Levenshtein similarity of the
    /// normalized input against the candidate's normalized name and short_name, mapped to a score.
    /// Returns <c>null</c> when the pair is unsafe to compare (short/very different lengths) or the
    /// similarity is below the 78% floor — those must NOT be suggested.
    /// </summary>
    private static (decimal Score, int Similarity)? LevenshteinSuggestion(
        string normalizedInput, string? candidateName, string? candidateShortName)
    {
        var best = -1;
        var nameSim = PartnerNormalization.FuzzySimilarity(
            normalizedInput, PartnerNormalization.NormalizeKey(candidateName));
        if (nameSim is int ns && ns > best) best = ns;
        var shortSim = PartnerNormalization.FuzzySimilarity(
            normalizedInput, PartnerNormalization.NormalizeKey(candidateShortName));
        if (shortSim is int ss && ss > best) best = ss;

        if (best < 78) return null; // below floor → do not suggest

        var score = best >= 92 ? 88m : best >= 85 ? 78m : 68m;
        return (score, best);
    }
}
