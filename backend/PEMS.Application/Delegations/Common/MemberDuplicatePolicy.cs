using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Delegations.Common;

/// <summary>One member row as the duplicate check sees it — the four business columns plus the id.</summary>
/// <param name="Kind">
/// Which list the row arrived in. Carried so a conflict can be REPORTED as "khách vs nhân sự hỗ trợ"
/// rather than as two anonymous rows; it never affects whether two rows are the same person.
/// </param>
public readonly record struct MemberIdentityInput(
    string Kind,
    int Index,
    string? FullName,
    string? JobTitle,
    string? Organization,
    ulong? OrganizationPartnerId,
    string? Nationality);

/// <summary>Two rows of one campus that describe the same person.</summary>
public readonly record struct MemberDuplicate(MemberIdentityInput First, MemberIdentityInput Second)
{
    /// <summary>The person, as a sentence a user can act on. Never an id — ids mean nothing on a form.</summary>
    public string Describe() =>
        $"{(First.FullName ?? string.Empty).Trim()} ({KindLabel(First.Kind)} #{First.Index + 1} "
        + $"và {KindLabel(Second.Kind)} #{Second.Index + 1})";

    private static string KindLabel(string kind) =>
        kind == MemberDuplicatePolicy.SupportKind ? "nhân sự hỗ trợ" : "khách";
}

/// <summary>
/// Whether two rows of ONE campus's member lists are the same person (ID-02).
///
/// <para>
/// The delegation and the support list are two doors into the same table. Nothing used to compare
/// them: the Excel importer de-duplicated inside the list it was importing, the form validated each
/// array separately, and so the same human written into both arrived in <c>visit_guest_members</c>
/// as two rows with two different <c>guest_member_id</c>s. Every id-first rule downstream then
/// correctly concluded they were two people — the biên bản listed them twice, the headcount was
/// wrong, and no amount of de-duplication in the biên bản could fix it, because by then there really
/// were two people in the data. The fix has to be here, at the door.
/// </para>
///
/// <para><b>What counts as the same person.</b> This class implements steps 4–6 of the plan's
/// comparison order — the only steps a member row can answer. Steps 1–2 (a shared
/// <c>guest_member_id</c> / <c>ClientMemberKey</c>, a shared account) are decided by
/// <see cref="OperationalContactLink"/> and the create/edit services, which have the ids; step 3
/// (email, phone) cannot apply at all, because a delegation row carries neither.</para>
///
/// <list type="number">
///   <item>Same <c>organizationPartnerId</c> + name + job title + nationality → the same person. The
///   id is the stronger half: two rows agreeing on the partner PROFILE agree on the organisation
///   however each spelled it.</item>
///   <item>No partner id on either side: organisation TEXT + name + job title + nationality.</item>
///   <item>Name alone → no conclusion, ever. Two members of one delegation sharing a name is
///   ordinary; merging them would put a stranger in somebody else's place.</item>
/// </list>
///
/// <para><b>Why full equality and not a heuristic.</b> A pair that matches on every field the form
/// collects is not merely a "candidate": there is nothing left that could distinguish them, and the
/// person filling the form has already been offered the chance to say so — the client runs this exact
/// rule before submitting and asks. A pair that differs anywhere (a different job title, a different
/// nationality, a different employer) is two people and passes. So "add the distinguishing detail" is
/// always an available answer, which is what makes refusing the identical case safe.</para>
///
/// <para>Nothing here merges, deletes or rewrites anything. It reports; the caller refuses.</para>
/// </summary>
public static class MemberDuplicatePolicy
{
    public const string GuestKind = "GUEST";
    public const string SupportKind = "EXTERNAL_SUPPORT";

    /// <summary>Refusal code for a campus whose merged member list contains one person twice.</summary>
    public const string DuplicateCode = "DUPLICATE_MEMBER_IN_CAMPUS";

    /// <summary>
    /// The string two rows must share to be the same person, or "" for a row that cannot be compared.
    ///
    /// <para>Normalisation is <see cref="PersonIdentity.Normalize"/>'s — trim, collapse inner
    /// whitespace, lower-case, and leave Vietnamese accents ALONE. Stripping them would make "Nguyễn
    /// Văn An" and "Nguyen Van Ân" one person, which in this system is a merge waiting to happen.</para>
    ///
    /// <para>A row with no name returns "": an unnamed row matches nothing rather than matching every
    /// other unnamed row. Half-typed rows are ordinary while a form is open, and refusing a submit
    /// because two of them were equally blank would be nonsense.</para>
    /// </summary>
    public static string Fingerprint(
        string? fullName, string? jobTitle, string? organization, ulong? organizationPartnerId, string? nationality)
    {
        var name = PersonIdentity.Normalize(fullName);
        if (name.Length == 0) return string.Empty;
        // The partner id wins over the text when there is one, so "FPT University (FPTU)" and "FPT
        // University" stop being two employers the moment both rows point at the same profile.
        var org = organizationPartnerId is ulong id
            ? $"partner:{id}"
            : $"text:{PersonIdentity.Normalize(organization)}";
        return $"{name}|{PersonIdentity.Normalize(jobTitle)}|{org}|{PersonIdentity.Normalize(nationality)}";
    }

    public static string Fingerprint(MemberIdentityInput m) =>
        Fingerprint(m.FullName, m.JobTitle, m.Organization, m.OrganizationPartnerId, m.Nationality);

    /// <summary>
    /// Every pair of rows in ONE campus's MERGED list that describe the same person.
    ///
    /// <para>Merged is the point: the pairs this finds within <c>visitors</c> and within
    /// <c>supportTeam</c> were already caught in places, but the pair that spans the two — the same
    /// person entered as a guest and again as an interpreter — was caught nowhere.</para>
    ///
    /// <para>Scope is the CAMPUS, never the request. Each campus of a multi-campus request keeps its
    /// own independent copy of the same delegation, by design, and comparing across them would refuse
    /// an ordinary "áp dụng cho các cơ sở còn lại".</para>
    ///
    /// <para>Only the FIRST partner is reported per fingerprint, so a person entered three times is
    /// one conflict to resolve rather than three.</para>
    /// </summary>
    public static IReadOnlyList<MemberDuplicate> FindDuplicates(IEnumerable<MemberIdentityInput> members)
    {
        var firstByFingerprint = new Dictionary<string, MemberIdentityInput>();
        var reported = new HashSet<string>();
        var duplicates = new List<MemberDuplicate>();

        foreach (var member in members)
        {
            var fingerprint = Fingerprint(member);
            if (fingerprint.Length == 0) continue;
            if (!firstByFingerprint.TryGetValue(fingerprint, out var first))
            {
                firstByFingerprint[fingerprint] = member;
                continue;
            }
            if (reported.Add(fingerprint)) duplicates.Add(new MemberDuplicate(first, member));
        }

        return duplicates;
    }

    /// <summary>
    /// The refusal message for a set of conflicts, naming the people rather than counting them.
    ///
    /// <para>Says what to DO, because both answers are legitimate and the system cannot choose: one
    /// of the two rows is redundant, or they are genuinely two people and the form is missing what
    /// tells them apart.</para>
    /// </summary>
    public static string DescribeConflicts(IReadOnlyList<MemberDuplicate> duplicates) =>
        "Cùng một người đang xuất hiện nhiều lần trong danh sách của cơ sở này: "
        + string.Join("; ", duplicates.Select(d => d.Describe()))
        + ". Vui lòng xóa dòng thừa, hoặc bổ sung thông tin phân biệt (chức vụ, đơn vị công tác, "
        + "quốc tịch) nếu đây thực sự là hai người khác nhau.";
}
