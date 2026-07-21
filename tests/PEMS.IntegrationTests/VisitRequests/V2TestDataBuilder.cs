using System;
using System.Collections.Generic;
using PEMS.Application.Common.DTOs;

namespace PEMS.IntegrationTests.VisitRequests;

public static class V2TestDataBuilder
{
    public static Dictionary<string, object?> BuildCreatePayload(
        string delegationName = "Default Test Delegation",
        string registrantEmail = "registrant@integration.test",
        string contactEmail = "contact@integration.test",
        params (string CampusCode, string ProcessingMode, ulong? HostUserId)[] campuses)
    {
        var start = DateTime.Now.AddDays(10).Date.AddHours(9);
        var campusVisits = new List<Dictionary<string, object?>>();
        var offsetDays = 0;

        foreach (var (code, mode, hostId) in campuses)
        {
            var s = start.AddDays(offsetDays++);
            campusVisits.Add(new Dictionary<string, object?>
            {
                ["campusId"] = code,
                ["plannedStartAt"] = s.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                ["plannedEndAt"] = s.AddHours(4).ToString("yyyy-MM-dd'T'HH:mm:ss"),
                ["delegationName"] = delegationName,
                ["visitType"] = "CAMPUS_TOUR",
                ["visitTypeOther"] = null,
                ["purpose"] = "Tham quan v� trao d?i h?p t�c (integration test)",
                ["workingContent"] = "Default valid working content",
                ["visitors"] = Array.Empty<object>(),
                ["externalSupportMembers"] = Array.Empty<object>(),
                ["operationalContact"] = new Dictionary<string, object?>
                {
                    ["fullName"] = "IT Operational Contact",
                    ["organization"] = "Test Organization",
                    ["phone"] = "0987654321",
                    ["email"] = contactEmail,
                },
                ["workingLanguage"] = "VI",
                ["transportationNote"] = null,
                ["mediaConsentStatus"] = "DECLINED",
                ["mediaConsentNote"] = null,
                ["notes"] = null,
                ["processing"] = new Dictionary<string, object?>
                {
                    ["mode"] = mode,
                    ["hostUserId"] = hostId,
                    ["confirmedHostConflict"] = false,
                }
            });
        }

        return new Dictionary<string, object?>
        {
            ["submissionId"] = Guid.NewGuid().ToString(),
            ["registrant"] = new Dictionary<string, object?>
            {
                ["fullName"] = "IT Registrant",
                ["nationality"] = "VN",
                ["organization"] = "Test Organization",
                ["jobTitle"] = "Staff",
                ["phone"] = "0912345678",
                ["email"] = registrantEmail,
            },
            ["primaryContact"] = new Dictionary<string, object?>
            {
                ["fullName"] = "IT Primary Contact",
                ["organization"] = "Test Organization",
                ["phone"] = "0987654321",
                ["email"] = contactEmail,
            },
            ["partnerId"] = null,
            ["campusVisits"] = campusVisits,
        };
    }

    public static Dictionary<string, object?> BuildEditPayload(
        int expectedRowVersion,
        string registrantEmail = "registrant@integration.test",
        string contactEmail = "contact@integration.test",
        params (ulong? InstanceId, int? InstanceRowVersion, string CampusCode)[] campuses)
    {
        var start = DateTime.Now.AddDays(10).Date.AddHours(9);
        var campusVisits = new List<Dictionary<string, object?>>();
        var offsetDays = 0;

        foreach (var (instId, rowVer, code) in campuses)
        {
            var s = start.AddDays(offsetDays++);
            campusVisits.Add(new Dictionary<string, object?>
            {
                ["visitInstanceId"] = instId,
                ["expectedRowVersion"] = rowVer,
                ["campusId"] = code,
                ["plannedStartAt"] = s.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                ["plannedEndAt"] = s.AddHours(4).ToString("yyyy-MM-dd'T'HH:mm:ss"),
                ["delegationName"] = "Edited Delegation Name",
                ["visitType"] = "CAMPUS_TOUR",
                ["visitTypeOther"] = null,
                ["purpose"] = "Edited Purpose",
                ["workingContent"] = "Edited Working Content",
                ["visitors"] = Array.Empty<object>(),
                ["externalSupportMembers"] = Array.Empty<object>(),
                ["operationalContact"] = new Dictionary<string, object?>
                {
                    ["fullName"] = "Edit Operational Contact",
                    ["organization"] = "FPT",
                    ["phone"] = "0999999999",
                    ["email"] = contactEmail,
                },
                ["workingLanguage"] = "VI",
                ["transportationNote"] = null,
                ["mediaConsentStatus"] = "DECLINED",
                ["mediaConsentNote"] = null,
                ["notes"] = null
            });
        }

        return new Dictionary<string, object?>
        {
            ["expectedRequestRowVersion"] = expectedRowVersion,
            ["registrant"] = new Dictionary<string, object?>
            {
                ["fullName"] = "Integration Registrant",
                ["nationality"] = "VN",
                ["organization"] = "FPT",
                ["jobTitle"] = "Staff",
                ["phone"] = "0999999999",
                ["email"] = registrantEmail,
            },
            ["primaryContact"] = new Dictionary<string, object?>
            {
                ["fullName"] = "Integration Contact",
                ["organization"] = "FPT",
                ["phone"] = "0999999999",
                ["email"] = contactEmail,
            },
            ["partnerId"] = null,
            ["campusVisits"] = campusVisits,
        };
    }
}
