using System;
using System.Text.Json;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;
using PEMS.Application.Common.DTOs;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string json = @"
{
  ""registrantFullName"": ""Nguy?n Van A"",
  ""registrantNationality"": ""VN"",
  ""registrantOrganization"": ""Ð?i h?c XYZ"",
  ""registrantPosition"": ""Giám d?c"",
  ""registrantPhone"": ""0987654321"",
  ""registrantEmail"": ""nguyenvana1@gmail.com"",
  ""partnerId"": null,
  ""delegationName"": ""Ðoàn Tham Quan XYZ"",
  ""visitScope"": ""SINGLE_CAMPUS"",
  ""campusVisits"": [
    {
      ""campusId"": ""HN"",
      ""startDatetime"": ""2026-06-27T10:15:00"",
      ""endDatetime"": ""2026-06-30T10:15:00""
    }
  ],
  ""purpose"": ""H?c h?i và trao d?i"",
  ""workingContent"": ""Th?o lu?n giáo trình"",
  ""expectedGuestCount"": 1,
  ""visitType"": ""CAMPUS_TOUR"",
  ""visitTypeOther"": null,
  ""workingLanguage"": ""VI"",
  ""interpreterNote"": null,
  ""transportationType"": ""SELF_ARRANGED"",
  ""transportationDetail"": null,
  ""mediaConsentStatus"": ""AGREED"",
  ""mediaConsentNote"": null,
  ""contactPerson"": {
    ""fullName"": ""Nguy?n Van A"",
    ""organization"": ""Ð?i h?c XYZ"",
    ""phone"": ""0987654321"",
    ""email"": ""nguyenvana1@gmail.com""
  },
  ""visitors"": [
    {
      ""fullName"": ""Khách 1"",
      ""email"": ""khach1@test.com"",
      ""nationality"": ""VN"",
      ""jobTitle"": null,
      ""organization"": null
    }
  ],
  ""supportMembers"": [],
  ""isContactSelf"": true,
  ""notes"": null
}";
        try {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var obj = JsonSerializer.Deserialize<InitiateVisitRequestCommand>(json, options);
            Console.WriteLine("Success!");
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
        }
    }
}
