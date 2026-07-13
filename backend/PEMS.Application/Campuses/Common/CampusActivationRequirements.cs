using System.Collections.Generic;
using PEMS.Domain.Entities.Campuses;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// UC-86 §16.1 enable (INACTIVE → ACTIVE) master-data requirements, shared by the manage-status
/// handler and the status-impact preview. A Staff Leader is deliberately NOT required here
/// (BR-86-15) — it only gates operational availability, never the ACTIVE status itself.
/// </summary>
public static class CampusActivationRequirements
{
    /// <summary>Returns the missing/invalid master-data field names (empty = OK).</summary>
    public static List<string> GetMissingMasterData(Campus campus)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(campus.CampusCode)) missing.Add("campusCode");
        if (string.IsNullOrWhiteSpace(campus.Name)) missing.Add("name");
        if (string.IsNullOrWhiteSpace(campus.City)) missing.Add("city");
        if (string.IsNullOrWhiteSpace(campus.Address)) missing.Add("address");
        if (string.IsNullOrWhiteSpace(campus.Phone)) missing.Add("phone");
        if (string.IsNullOrWhiteSpace(campus.Email) || !IsValidEmail(campus.Email)) missing.Add("email");
        return missing;
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var at = email.IndexOf('@');
        var dot = email.LastIndexOf('.');
        return at > 0 && dot > at + 1 && dot < email.Length - 1;
    }
}
