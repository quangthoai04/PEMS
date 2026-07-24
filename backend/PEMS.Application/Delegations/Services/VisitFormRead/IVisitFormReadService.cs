using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <summary>
/// The single, central resolver for visit form content. Every read screen that needs the
/// submitted/active form must go through this service, so campus scope is enforced once, before any
/// detail is projected, and no screen invents its own way of reaching the content.
///
/// Rules:
///  • Content is resolved ONLY from visit_instance_form_details + visit_instance_guest_members, per
///    campus instance. visit_requests carries no form content to fall back to.
///  • A visible instance with no detail row is a controlled consistency error
///    (VISIT_FORM_DETAIL_MISSING) — never borrowed content, never a silent blank.
///  • A request whose campuses differ never designates one of them as representative; a caller that
///    needs a single instance must name it.
///  • Only campus instances the caller may see are returned; hidden campuses never leak.
/// </summary>
public interface IVisitFormReadService
{
    /// <summary>
    /// Resolves the visit request into a fully per-campus <see cref="ResolvedVisitFormDto"/>, scoped to
    /// the current user. Throws NotFoundException if the request does not exist, ForbiddenException if
    /// the caller may see no campus of it, and a coded ConflictException when a visible campus is
    /// missing its form detail.
    /// </summary>
    Task<ResolvedVisitFormDto> ResolveAsync(ulong visitRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the form CONTENT for an already-authorized set of visible campus instances of
    /// <paramref name="request"/>, keyed by visit_instance_id. The caller keeps ownership of scope,
    /// decision, schedule and cancellation metadata (instance/request columns that are not form content)
    /// and uses this only for the content half, so the per-campus rule lives in one place:
    /// each instance is answered from its OWN visit_instance_form_details row and its OWN
    /// visit_instance_guest_members links, and an instance missing its detail raises a coded
    /// consistency error rather than being answered from a sibling or from the request row.
    /// Only the passed <paramref name="visibleInstanceIds"/> are read — hidden campuses are never queried.
    /// Batched into 2 queries regardless of campus/member count — no per-campus N+1.
    /// </summary>
    Task<IReadOnlyDictionary<ulong, VisitCampusFormContent>> ResolveCampusFormContentAsync(
        VisitRequest request,
        IReadOnlyList<ulong> visibleInstanceIds,
        CancellationToken cancellationToken);
}
