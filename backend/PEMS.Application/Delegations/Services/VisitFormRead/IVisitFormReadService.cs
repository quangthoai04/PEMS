using PEMS.Domain.Entities.Delegations;

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

    /// <summary>
    /// Resolves the version-specific form CONTENT for an already-authorized set of visible campus
    /// instances of <paramref name="request"/>, keyed by visit_instance_id. The caller keeps ownership
    /// of scope, decision, schedule and cancellation metadata (version-agnostic instance/request
    /// columns) and uses this only for the form-content half, so the dual-read rule lives in one place:
    ///  • v1 → the global compatibility projection (the same content object for every visible instance);
    ///    members come from the request-level list.
    ///  • v2 → ONLY visit_instance_form_details + visit_instance_guest_members; NEVER the global fields;
    ///    a visible instance missing its detail is a coded consistency error (no silent fallback).
    /// Only the passed <paramref name="visibleInstanceIds"/> are read — hidden campuses are never queried.
    /// Batched (v1: 1 query; v2: 2 queries) regardless of campus/member count — no per-campus N+1.
    /// </summary>
    Task<IReadOnlyDictionary<ulong, VisitCampusFormContent>> ResolveCampusFormContentAsync(
        VisitRequest request,
        IReadOnlyList<ulong> visibleInstanceIds,
        CancellationToken cancellationToken);
}
