using Microsoft.AspNetCore.Authorization;

namespace PEMS.Api.Extensions;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers authorization services. Fine-grained RBAC is enforced per endpoint
    /// via <see cref="Filters.RoleAuthorizeAttribute"/>; this wires the core
    /// authorization middleware so <c>[Authorize]</c> works.
    ///
    /// The fallback policy makes the API fail-closed: an endpoint that carries no
    /// authorization metadata at all now requires an authenticated user instead of
    /// being silently public. Before this, forgetting <c>[Authorize]</c> on a
    /// controller published every one of its actions anonymously — which is exactly
    /// how AgendaTemplates, Calendars, Delegations, Documents, Galleries,
    /// VisitDocuments, VisitInvitations and VisitPhotos ended up reachable without a
    /// token. Genuinely public endpoints must now opt out explicitly with
    /// <c>[AllowAnonymous]</c>, which is greppable and testable.
    /// </summary>
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        return services;
    }
}
