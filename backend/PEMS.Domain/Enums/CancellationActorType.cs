namespace PEMS.Shared;

// Maps visit_requests.cancellation_actor_type / visit_request_campuses.cancellation_actor_type ENUM (SQL v8.3).
public static class CancellationActorType
{
    public const string Visitor = "VISITOR";
    public const string Host = "HOST";
}
