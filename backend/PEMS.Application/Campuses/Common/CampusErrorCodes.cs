namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Stable, machine-readable error codes for the HO Campus Management module
/// (UC-82 list, UC-83 search/filter, UC-86 manage status). Mirrored by the
/// frontend error map so the UI can localize messages off the code.
/// </summary>
public static class CampusErrorCodes
{
    /// <summary>Caller is not HO/ADMIN — may not access campus management. (403)</summary>
    public const string CampusManagementForbidden = "CAMPUS_MANAGEMENT_FORBIDDEN";

    /// <summary>Campus id does not exist. (404)</summary>
    public const string CampusNotFound = "CAMPUS_NOT_FOUND";

    /// <summary>Requested status value is not ACTIVE/INACTIVE. (400/422)</summary>
    public const string InvalidCampusStatus = "INVALID_CAMPUS_STATUS";

    /// <summary>Cannot re-activate: required master data is incomplete (UC-86 BR-86-06). (422)</summary>
    public const string CampusActivationMasterDataIncomplete = "CAMPUS_ACTIVATION_MASTER_DATA_INCOMPLETE";

    /// <summary>Cannot re-activate: campus has no ACTIVE IC department (UC-86 BR-86-06). (422)</summary>
    public const string CampusActivationMissingIcDepartment = "CAMPUS_ACTIVATION_MISSING_IC_DEPARTMENT";

    // ── Duplicate master data (UC-81 create / UC-85 update). Only city may duplicate. (409) ──
    public const string CampusCodeAlreadyExists = "CAMPUS_CODE_ALREADY_EXISTS";
    public const string CampusNameAlreadyExists = "CAMPUS_NAME_ALREADY_EXISTS";
    public const string CampusAddressAlreadyExists = "CAMPUS_ADDRESS_ALREADY_EXISTS";
    public const string CampusPhoneAlreadyExists = "CAMPUS_PHONE_ALREADY_EXISTS";
    public const string CampusEmailAlreadyExists = "CAMPUS_EMAIL_ALREADY_EXISTS";
}
