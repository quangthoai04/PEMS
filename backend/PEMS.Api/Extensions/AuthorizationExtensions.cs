namespace PEMS.Api.Extensions;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers authorization services. Fine-grained RBAC is enforced per endpoint
    /// via <see cref="Filters.RequirePermissionAttribute"/>; this just wires the core
    /// authorization middleware so <c>[Authorize]</c> works.
    /// </summary>
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        return services;
    }
}
