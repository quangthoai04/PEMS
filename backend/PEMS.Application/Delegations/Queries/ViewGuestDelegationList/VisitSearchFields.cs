using System.Collections.Generic;
using Field = PEMS.Application.Delegations.Queries.ViewGuestDelegationList.VisitSearchMatchContextBuilder.Field;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

/// <summary>
/// The ONE definition of what the delegation list searches, in the shape the match-context builder
/// needs (NP-01).
///
/// <para>
/// The bug this closes: the SQL keyword predicate and the match-context field list were written out
/// twice, by hand, in two different methods. Three registrant fields (name, nationality, job title)
/// were added to the predicate and not to the context list, so a keyword that matched somebody's name
/// returned the right row with an EMPTY <c>matchedContexts</c> — and the frontend, correctly, rendered
/// no "Khớp tại" line for it. The row looked like a search result nobody could explain.
/// </para>
/// <para>
/// Two things make that harder to repeat. The factories below take every searchable value as a NAMED,
/// REQUIRED parameter, so adding a field breaks every call site until it is supplied (the compiler does
/// the reminding, not a code reviewer). And the predicate lives beside them — <see cref="RequestScope"/>
/// and <see cref="CampusScope"/> document exactly which SQL term each code mirrors, so a new term has an
/// obvious home.
/// </para>
/// <para>
/// A value of <c>null</c> means "not searched in this query", not "no data": the request-level query does
/// not search campus name, host or contact (it has no single campus to search), so those arrive null and
/// simply cannot match. Only stable codes ever leave here — never the matched text.
/// </para>
/// </summary>
public static class VisitSearchFields
{
    /// <summary>
    /// Request-wide searchable fields — the terms in the keyword predicate that read off
    /// <c>visit_requests</c> (plus the campus-instance contact, which is request-scope for display
    /// because the instance-level row IS one campus).
    ///
    /// <para>Mirrors, term for term:</para>
    /// <code>
    /// vr.RequestCode | vr.RegistrantOrganization | vr.RegistrantFullName
    /// vr.RegistrantNationality | vr.RegistrantJobTitle | partner.Name
    /// [instance-level only] contact user FullName
    /// </code>
    /// </summary>
    public static List<Field> RequestScope(
        string? requestCode,
        string? registrantOrganization,
        string? registrantFullName,
        string? registrantNationality,
        string? registrantJobTitle,
        string? partnerName,
        string? operationalContactName) => new()
    {
        new(VisitSearchFieldCodes.RequestCode, requestCode),
        new(VisitSearchFieldCodes.RegistrantOrganization, registrantOrganization),
        new(VisitSearchFieldCodes.RegistrantFullName, registrantFullName),
        new(VisitSearchFieldCodes.RegistrantNationality, registrantNationality),
        new(VisitSearchFieldCodes.RegistrantJobTitle, registrantJobTitle),
        new(VisitSearchFieldCodes.Partner, partnerName),
        new(VisitSearchFieldCodes.OperationalContact, operationalContactName),
    };

    /// <summary>
    /// Campus-scoped searchable fields — the terms that read off ONE campus instance. Pure V2: the
    /// delegation name is per-campus, so it belongs here and never to the request scope.
    ///
    /// <para>Mirrors, term for term:</para>
    /// <code>
    /// campus.Name | host user FullName | c.FormDetail.DelegationName
    /// </code>
    /// </summary>
    public static List<Field> CampusScope(
        string? campusName,
        string? hostName,
        string? delegationName) => new()
    {
        new(VisitSearchFieldCodes.Campus, campusName),
        new(VisitSearchFieldCodes.Host, hostName),
        new(VisitSearchFieldCodes.DelegationName, delegationName),
    };
}
