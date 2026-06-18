namespace PEMS.Domain.Constants;

public static class VisitRequestStatuses
{
    public const string PendingHoApproval       = "PENDING_HO_APPROVAL";
    public const string PendingStaffLeadApproval = "PENDING_STAFF_LEAD_APPROVAL";
    public const string Approved                 = "APPROVED";
    public const string Rejected                 = "REJECTED";
    public const string Cancelled                = "CANCELLED";
    public const string Completed                = "COMPLETED";
    public const string InProgress               = "IN_PROGRESS";
}

public static class VisitScopes
{
    public const string SingleCampus = "SINGLE_CAMPUS";
    public const string MultiCampus  = "MULTI_CAMPUS";
}

public static class WorkingLanguages
{
    public const string English    = "EN";
    public const string Vietnamese = "VI";
}
