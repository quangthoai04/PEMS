namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <summary>
/// The single, central dual-read resolver for the visit form. Every read screen that needs the
/// submitted/active form content must go through this service instead of re-mapping global fields —
/// so a v2 (per-campus) request is never accidentally read as a shared global snapshot, and campus
/// scope is enforced once, before any detail is projected.
///
/// Rules (per PEMS_MULTI_CAMPUS_PER_CAMPUS_FORM_AND_IDENTITY_EDIT_PLAN.md §6, PR-3):
///  • form_schema_version = 1 → resolve from the global compatibility fields on visit_requests;
///    every visible campus gets the same snapshot; members come from the request-level list.
///  • form_schema_version = 2 → resolve ONLY from visit_instance_form_details + visit_instance_guest_members;
///    never fall back to the global projection; a missing detail is a controlled consistency error.
///  • Only campus instances the caller may see are returned; hidden campuses never leak.
/// </summary>
public interface IVisitFormReadService
{
    /// <summary>
    /// Resolves the visit request into a fully per-campus <see cref="ResolvedVisitFormDto"/>, scoped to
    /// the current user. Throws NotFoundException if the request does not exist, ForbiddenException if
    /// the caller may see no campus of it, and a coded ConflictException on a v2 detail inconsistency.
    /// </summary>
    Task<ResolvedVisitFormDto> ResolveAsync(ulong visitRequestId, CancellationToken cancellationToken);
}
