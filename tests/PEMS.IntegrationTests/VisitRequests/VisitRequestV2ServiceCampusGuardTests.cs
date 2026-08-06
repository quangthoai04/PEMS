using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The service-layer half of the "a request has at least one campus" invariant. The validator half lives
/// in PEMS.UnitTests (VisitRequestV2CampusInvariantTests); this one is here because the guard is in
/// Infrastructure, which the unit-test project deliberately does not reference.
///
/// Why it exists: the create and resubmit services each used to compute a dead "compatibility projection"
/// via <c>OrderBy(...).First()</c>. Those expressions would have thrown on an empty campus collection, so
/// removing them raised a fair question — was one of them the real guard? It was not, and this test is the
/// executable proof: the service rejects an empty payload on its own, with no validator in the picture.
///
/// No database is touched. The guard is the first statement in the method, so it fires before the
/// DbContext is ever used — passing a null context is what makes that visible rather than assumed.
/// </summary>
public sealed class VisitRequestV2ServiceCampusGuardTests
{
    private static readonly DateTime Now = DateTime.Now;

    private static VisitRequestFormDataV2 FormWith(List<CampusVisitFormDto>? campuses)
        => new(
            "SUB-1",
            new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", "+84912345678", "reg@example.com"),
            null,
            campuses!);

    [Theory]
    [InlineData(true)]   // null collection
    [InlineData(false)]  // empty collection
    public async Task Create_service_rejects_a_request_with_no_campus_without_any_validator(bool useNull)
    {
        var service = new VisitRequestV2CreateService(null!);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateV2Async(
                FormWith(useNull ? null : new List<CampusVisitFormDto>()),
                registrantUserId: 1,
                createdSource: "VISITOR_SUBMITTED",
                vietnamNow: Now,
                cancellationToken: CancellationToken.None));

        Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
    }
}
