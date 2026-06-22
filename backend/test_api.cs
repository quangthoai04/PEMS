using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string json = @"
{
  ""registrantFullName"": ""Nguy?n Van A"",
  ""registrantNationality"": ""VN"",
  ""registrantOrganization"": ""Ð?i h?c XYZ"",
  ""registrantPosition"": ""Giám d?c"",
  ""registrantPhone"": ""0987654321"",
  ""registrantEmail"": ""nguyenvana1@gmail.com"",
  ""delegationName"": ""Ðoàn Tham Quan XYZ"",
  ""visitScope"": ""SINGLE_CAMPUS"",
  ""visitType"": ""CAMPUS_TOUR"",
  ""visitTypeOther"": null,
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
  ""contactPerson"": {
    ""fullName"": ""Nguy?n Van A"",
    ""organization"": ""Ð?i h?c XYZ"",
    ""phone"": ""0987654321"",
    ""email"": ""nguyenvana1@gmail.com""
  },
  ""isContactSelf"": true,
  ""workingLanguage"": ""VI"",
  ""interpreterNote"": null,
  ""transportationType"": ""SELF_ARRANGED"",
  ""transportationDetail"": null,
  ""mediaConsentStatus"": ""AGREED"",
  ""mediaConsentNote"": null,
  ""partnerId"": null,
  ""notes"": null
}";
        using var client = new HttpClient();
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("http://localhost:5265/api/visit-requests/initiate", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Initiate Response ({(int)response.StatusCode}): {responseBody}");
    }
}
