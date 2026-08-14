using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// NP-01 — a keyword that matched must always be able to say WHERE.
///
/// <para>
/// The defect these pin: the SQL keyword predicate and the match-context field list were maintained
/// by hand in two places. Three registrant fields were added to the predicate and not to the context
/// list, so searching somebody's name returned their delegation with an EMPTY <c>matchedContexts</c>
/// — and the UI, correctly, rendered no "Khớp tại" line at all. A row appeared as a search result
/// that the screen could not explain.
/// </para>
/// <para>
/// The structural fix is <see cref="VisitSearchFields"/>: required named parameters, so adding a
/// searchable field breaks every call site until it is supplied. These tests guard the rest — that
/// every declared code is actually reachable, and that a match reports the right one.
/// </para>
/// </summary>
public class VisitSearchFieldsTests
{
    private const string Keyword = "nguyên";

    /// <summary>Every field filled with a value that contains nothing, so nothing matches by accident.</summary>
    private static List<VisitSearchMatchContextBuilder.Field> AllRequestFields(string value) =>
        VisitSearchFields.RequestScope(
            requestCode: value, registrantOrganization: value, registrantFullName: value,
            registrantNationality: value, registrantJobTitle: value, partnerName: value,
            operationalContactName: value);

    [Fact]
    public void Every_declared_field_code_is_produced_by_one_of_the_two_factories()
    {
        var produced = AllRequestFields("x")
            .Concat(VisitSearchFields.CampusScope(campusName: "x", hostName: "x", delegationName: "x"))
            .Select(f => f.Code)
            .ToHashSet();

        // A code declared but never emitted is a label the frontend can render and the backend can
        // never send — dead weight that reads like a feature.
        var missing = VisitSearchFieldCodes.All.Where(c => !produced.Contains(c)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void No_factory_emits_a_code_that_is_not_declared()
    {
        var declared = VisitSearchFieldCodes.All.ToHashSet();
        var produced = AllRequestFields("x")
            .Concat(VisitSearchFields.CampusScope("x", "x", "x"))
            .Select(f => f.Code);

        // The reverse leak: an emitted code with no entry in the allowlist has no VI/EN label either,
        // so the UI falls back to printing the raw code at the user.
        Assert.All(produced, code => Assert.Contains(code, declared));
    }

    [Theory]
    [InlineData(VisitSearchFieldCodes.RegistrantFullName)]
    [InlineData(VisitSearchFieldCodes.RegistrantNationality)]
    [InlineData(VisitSearchFieldCodes.RegistrantJobTitle)]
    [InlineData(VisitSearchFieldCodes.RequestCode)]
    [InlineData(VisitSearchFieldCodes.RegistrantOrganization)]
    [InlineData(VisitSearchFieldCodes.Partner)]
    [InlineData(VisitSearchFieldCodes.OperationalContact)]
    public void A_request_level_match_reports_the_field_that_matched_and_only_that_field(string code)
    {
        // Only the field under test carries the keyword; everything else is inert.
        var fields = VisitSearchFields.RequestScope(
            requestCode: Value(code, VisitSearchFieldCodes.RequestCode),
            registrantOrganization: Value(code, VisitSearchFieldCodes.RegistrantOrganization),
            registrantFullName: Value(code, VisitSearchFieldCodes.RegistrantFullName),
            registrantNationality: Value(code, VisitSearchFieldCodes.RegistrantNationality),
            registrantJobTitle: Value(code, VisitSearchFieldCodes.RegistrantJobTitle),
            partnerName: Value(code, VisitSearchFieldCodes.Partner),
            operationalContactName: Value(code, VisitSearchFieldCodes.OperationalContact));

        var contexts = VisitSearchMatchContextBuilder.Build(
            Keyword, fields, Array.Empty<VisitSearchMatchContextBuilder.CampusScope>());

        var ctx = Assert.Single(contexts!);
        Assert.Equal(SearchMatchScopes.Request, ctx.Scope);
        Assert.Equal(new[] { code }, ctx.MatchedFields);
    }

    [Theory]
    [InlineData(VisitSearchFieldCodes.Campus)]
    [InlineData(VisitSearchFieldCodes.Host)]
    [InlineData(VisitSearchFieldCodes.DelegationName)]
    public void A_campus_level_match_is_reported_against_its_own_instance(string code)
    {
        var campus = new VisitSearchMatchContextBuilder.CampusScope(
            VisitInstanceId: 77, CampusId: 3, CampusName: "FPTU HCM",
            Fields: VisitSearchFields.CampusScope(
                campusName: Value(code, VisitSearchFieldCodes.Campus),
                hostName: Value(code, VisitSearchFieldCodes.Host),
                delegationName: Value(code, VisitSearchFieldCodes.DelegationName)));

        var contexts = VisitSearchMatchContextBuilder.Build(
            Keyword, AllRequestFields("không khớp"), new[] { campus });

        var ctx = Assert.Single(contexts!);
        Assert.Equal(SearchMatchScopes.Campus, ctx.Scope);
        Assert.Equal(77UL, ctx.VisitInstanceId);
        Assert.Equal(new[] { code }, ctx.MatchedFields);
    }

    [Fact]
    public void A_field_this_query_does_not_search_is_passed_null_and_cannot_match()
    {
        // The request-level query has no single campus, so campus/host/contact are not searched there.
        // Passing null must mean "not searched", never "matches everything".
        var contexts = VisitSearchMatchContextBuilder.Build(
            Keyword,
            VisitSearchFields.RequestScope(
                requestCode: null, registrantOrganization: null, registrantFullName: null,
                registrantNationality: null, registrantJobTitle: null, partnerName: null,
                operationalContactName: null),
            Array.Empty<VisitSearchMatchContextBuilder.CampusScope>());

        Assert.Empty(contexts!);
    }

    [Fact]
    public void Matching_is_case_insensitive_and_substring_like_the_SQL_predicate()
    {
        // Mirrors LOWER(x).Contains(kw): the context builder must agree with what SQL matched on, or
        // a row comes back with no explanation for the hit that produced it.
        var contexts = VisitSearchMatchContextBuilder.Build(
            "NGUYÊN",
            VisitSearchFields.RequestScope(
                requestCode: null, registrantOrganization: null,
                registrantFullName: "Trần Thị Nguyên Anh",
                registrantNationality: null, registrantJobTitle: null,
                partnerName: null, operationalContactName: null),
            Array.Empty<VisitSearchMatchContextBuilder.CampusScope>());

        var ctx = Assert.Single(contexts!);
        Assert.Equal(new[] { VisitSearchFieldCodes.RegistrantFullName }, ctx.MatchedFields);
    }

    [Fact]
    public void No_keyword_means_no_contexts_at_all_rather_than_an_empty_list()
    {
        // Null, not empty: a browse with no search has nothing to explain, and an empty list would
        // read to the client as "matched on nothing", which is a different (and alarming) statement.
        Assert.Null(VisitSearchMatchContextBuilder.Build(
            null, AllRequestFields("x"), Array.Empty<VisitSearchMatchContextBuilder.CampusScope>()));
        Assert.Null(VisitSearchMatchContextBuilder.Build(
            "   ", AllRequestFields("x"), Array.Empty<VisitSearchMatchContextBuilder.CampusScope>()));
    }

    /// <summary>The keyword for the field under test; an inert value for every other field.</summary>
    private static string Value(string codeUnderTest, string thisCode) =>
        codeUnderTest == thisCode ? $"giá trị {Keyword} ở đây" : "không có gì";
}
