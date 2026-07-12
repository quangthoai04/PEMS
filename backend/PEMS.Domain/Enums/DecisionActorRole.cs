namespace PEMS.Shared;

// Maps visit_request_campuses.decision_actor_role ENUM (SQL v10 + actor-relation patch).
// STAFF_LEADER — standard campus review / leader assign / leader self-host.
// STAFF        — regular IC Staff self-hosted their OWN campus inside the create
//                transaction of a request they registered (decision_source INTERNAL_SELF_HOST).
public static class DecisionActorRole
{
    public const string StaffLeader = "STAFF_LEADER";
    public const string Staff       = "STAFF";
}

// Maps visit_request_campuses.decision_source ENUM (actor-relation patch).
public static class DecisionSources
{
    public const string StandardCampusReview = "STANDARD_CAMPUS_REVIEW";
    public const string InternalSelfHost     = "INTERNAL_SELF_HOST";
    public const string InternalLeaderAssign = "INTERNAL_LEADER_ASSIGN";
}
