using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using Xunit;

namespace PEMS.ArchitectureTests;

/// <summary>
/// Guards the SHAPE of the API surface: which controllers declare authorization, which actions
/// are anonymous, and that the debug endpoint stays deleted. Every rule here was violated in
/// production code at least once — an anonymous JWT-minting endpoint and eight controllers with
/// no authorization attribute at all.
///
/// Policy BEHAVIOUR (who may do what) is asserted in
/// PEMS.UnitTests/Policies/AuthorizationPolicyTests.cs, which runs on net8.0.
/// </summary>
public class AuthorizationTests
{
    private static readonly Assembly ApiAssembly = Assembly.Load("PEMS.Api");

    private static IEnumerable<Type> Controllers() =>
        ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static bool HasAuthMetadata(Type controller) =>
        controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
        || controller.GetCustomAttributes<RoleAuthorizeAttribute>(inherit: true).Any();

    private static bool HasAuthMetadata(MethodInfo action) =>
        action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
        || action.GetCustomAttributes<RoleAuthorizeAttribute>(inherit: true).Any();

    private static bool IsAnonymous(Type controller) =>
        controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    private static bool IsAnonymous(MethodInfo action) =>
        action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    /// <summary>
    /// Controllers whose anonymous surface is inherent to what they are, so listing their
    /// [AllowAnonymous] actions one by one would be noise rather than review. Being on this list
    /// is NOT a statement that the whole controller is anonymous — PublicContentController and
    /// AuthenticationController both mix authenticated actions in, and every action on every
    /// controller is still checked individually by
    /// <see cref="Every_action_declares_an_explicit_authorization_decision"/>.
    /// Keep this list explicit — no wildcards, no prefixes.
    /// </summary>
    private static readonly HashSet<string> PublicControllerWhitelist = new()
    {
        "PublicAccountConfirmationsController", // email confirmation links
        "PublicContentController",              // MIXED: public site content + authenticated notifications
        "PublicEmailActionsController",         // one-click actions from a sent email
        "PublicFeaturesController",             // feature flags read by the public site
        "PublicPartnersController",             // approved + public partner directory
        "PublicVisitFptuController",            // public "visit FPTU" landing data
        "HealthController",                     // liveness/readiness probes
        "GoogleDriveOAuthController",           // OAuth callback, verified by state token
        "AuthenticationController",             // MIXED: login/refresh (pre-auth) + logout/me
    };

    /// <summary>
    /// Controllers that legitimately carry no class-level attribute because their actions split
    /// between anonymous and authenticated, and a class-level attribute would be wrong for one
    /// half. Exempt from the class-level rule only — the per-action rule still applies in full,
    /// and that is what actually protects them.
    /// </summary>
    private static readonly HashSet<string> MixedSurfaceControllers = new()
    {
        "ProfilesController",      // every action carries its own [Authorize]
        "VisitRequestsController", // anonymous public registration + OTP, authenticated create/edit
    };

    [Fact]
    public void Every_business_controller_declares_authorization()
    {
        var offenders = Controllers()
            .Where(c => !PublicControllerWhitelist.Contains(c.Name))
            .Where(c => !MixedSurfaceControllers.Contains(c.Name))
            .Where(c => !HasAuthMetadata(c))
            .Select(c => c.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These controllers have no class-level [Authorize]/[RoleAuthorize], so every action on "
            + "them relies on the global fallback policy alone: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The class-level rule above has a blind spot: it says nothing about controllers on the
    /// whitelist, and nothing about individual actions anywhere else. Five actions on
    /// PublicContentController (homepage, search, contact, policy, gallery) sat in exactly that
    /// blind spot — no attribute of any kind — so the fallback policy quietly started demanding a
    /// token from anonymous visitors while this suite stayed green.
    ///
    /// Relying on the fallback is never acceptable as a DESIGN: it is the safety net for a
    /// forgotten attribute, not a way to express intent. Every action must say, in its own or its
    /// controller's metadata, either "authenticated/role-gated" or "deliberately anonymous".
    /// </summary>
    [Fact]
    public void Every_action_declares_an_explicit_authorization_decision()
    {
        var offenders = Controllers()
            .SelectMany(c => Actions(c).Select(a => new { Controller = c, Action = a }))
            .Where(x =>
                !HasAuthMetadata(x.Controller)
                && !HasAuthMetadata(x.Action)
                && !IsAnonymous(x.Controller)
                && !IsAnonymous(x.Action))
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These actions carry no [Authorize]/[RoleAuthorize] and no [AllowAnonymous], at either "
            + "the action or the controller, so they fall through to the global fallback policy. "
            + "Add [AllowAnonymous] if the endpoint is meant to be public, otherwise gate it: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// [AllowAnonymous] is not scoped like [Authorize]: the authorization middleware short-circuits
    /// on the FIRST one it finds anywhere in the endpoint's metadata, so a class-level
    /// [AllowAnonymous] silently voids every [Authorize] on the actions underneath it. The attribute
    /// stays visible in the source, which is what makes the mistake so easy to miss in review.
    ///
    /// A controller with a mixed surface must therefore declare [AllowAnonymous] per action.
    /// </summary>
    [Fact]
    public void A_class_level_AllowAnonymous_never_sits_above_an_authorized_action()
    {
        var offenders = Controllers()
            .Where(c => IsAnonymous(c))
            .SelectMany(c => Actions(c)
                .Where(a => HasAuthMetadata(a))
                .Select(a => $"{c.Name}.{a.Name}"))
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These actions declare [Authorize]/[RoleAuthorize] under a class-level [AllowAnonymous], "
            + "which overrides them and publishes the action anonymously. Move [AllowAnonymous] onto "
            + "the individual public actions instead: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_anonymous_debug_user_endpoint_is_gone()
    {
        // GET /api/dashboard/debug-user was [AllowAnonymous], resolved a user by email and returned
        // a signed access token. It must never come back under any name.
        var offenders = Controllers()
            .SelectMany(c => Actions(c).Select(a => new { Controller = c, Action = a }))
            .Where(x =>
                x.Action.GetCustomAttributes<HttpMethodAttribute>()
                    .Any(h => (h.Template ?? string.Empty).Contains("debug", StringComparison.OrdinalIgnoreCase))
                || x.Action.Name.Contains("DebugUser", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, "Debug endpoint(s) reintroduced: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AllowAnonymous_actions_stay_inside_the_whitelist()
    {
        // Actions on otherwise-protected controllers that deliberately opt out. Each entry is a
        // public surface with a business reason; adding one should be a conscious review step.
        var allowedAnonymousActions = new HashSet<string>
        {
            "CampusesController.GetActiveCampuses",        // login page campus dropdown
            "CampusesController.GetRegistrationCampuses",  // public visit-registration options

            // Public visit registration (/visit-registration/v2) — a guest with no account walks
            // the whole OTP flow, so every step of it is anonymous by design. The OTP endpoints
            // are rate-limited and Turnstile-gated rather than token-gated.
            "VisitRequestsController.InitiateFormV2",        // v2 step 1: validate + send OTP
            "VisitRequestsController.VerifyAndCreateFormV2", // v2 step 2: verify OTP + create
            "VisitRequestsController.GetSubmissionResultV2", // result page after submitting
            "VisitRequestsController.ResendOtp",             // new code for the same submission
            "VisitRequestsController.RecoverOtp",            // Turnstile-verified re-issue
            "VisitRequestsController.Initiate",              // v1 tombstone, answers 410 Gone
            "VisitRequestsController.Verify",                // v1 tombstone, answers 410 Gone

            // The masked landing summary behind an emailed confirmation token, for ONE campus.
            // Anonymous because the invited person may not have an account yet — that is the whole
            // point of the invitation — and because an unknown token has to answer exactly like an
            // expired one, or the endpoint would tell a stranger whether an address was invited.
            // It never mutates. Accepting or declining still requires a signed-in session whose
            // email matches the invited address; see the controller notes.
            //
            // It replaces the two request-level entries (GetContactClaimInfo /
            // GetContactTransferInfo): one link now resolves an invitation that already knows
            // whether it is an INITIAL_CONFIRMATION or a TRANSFER, so the kind decides the effect
            // rather than the URL the recipient happened to receive.
            "VisitRequestsController.GetOperationalContactConfirmationInfo",
        };

        var offenders = Controllers()
            .Where(c => !PublicControllerWhitelist.Contains(c.Name))
            .SelectMany(c => Actions(c)
                .Where(a => a.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
                .Select(a => $"{c.Name}.{a.Name}"))
            .Where(name => !allowedAnonymousActions.Contains(name))
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "[AllowAnonymous] outside the whitelist: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Campus_management_is_HO_only_at_the_controller()
    {
        var campus = Controllers().Single(c => c.Name == "CampusesController");
        var roleAttributes = campus.GetCustomAttributes<RoleAuthorizeAttribute>(inherit: true).ToList();

        Assert.True(roleAttributes.Count > 0, "CampusesController must carry a [RoleAuthorize].");

        var allowed = roleAttributes.SelectMany(GetAllowedRoles).Distinct().ToList();
        Assert.Equal(new[] { EffectiveRole.Ho }, allowed);
        Assert.DoesNotContain(EffectiveRole.Admin, allowed);
    }

    /// <summary>
    /// A module-specific refusal code stays the exception it was introduced as.
    ///
    /// <para>
    /// <c>RoleAuthorize.ErrorCode</c> exists for one reason: some modules were guarded inside their
    /// handlers, already published a code, and already had a client reading it — putting a gate in
    /// front of those handlers must not change what the refusal says. Department management is that
    /// case, and the department screen maps its code to a specific sentence.
    /// </para>
    /// <para>
    /// It is a hazard as much as a fix. If every endpoint eventually declares its own refusal code,
    /// clients gain a list of strings to branch on and learn nothing they did not already know from
    /// the 403 — and the generic answer, which is the right one almost everywhere, quietly becomes the
    /// unusual one. So the whitelist is the test: adding a code elsewhere is a decision that has to be
    /// made here, in the open, rather than in passing on one action.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_department_management_overrides_the_generic_forbidden_code()
    {
        var offenders = new List<string>();

        foreach (var controller in Controllers())
        {
            var gates = controller.GetCustomAttributes<RoleAuthorizeAttribute>(inherit: true)
                .Select(a => (Owner: controller.Name, Attribute: a))
                .Concat(Actions(controller).SelectMany(action =>
                    action.GetCustomAttributes<RoleAuthorizeAttribute>(inherit: true)
                        .Select(a => (Owner: $"{controller.Name}.{action.Name}", Attribute: a))));

            foreach (var (owner, gate) in gates)
            {
                if (string.IsNullOrWhiteSpace(gate.ErrorCode)) continue;

                if (controller.Name == "DepartmentsController"
                    && gate.ErrorCode == "DEPARTMENT_MANAGEMENT_FORBIDDEN")
                {
                    continue;
                }

                offenders.Add($"{owner} -> {gate.ErrorCode}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These gates declare a non-generic errorCode. Every one of them is a new string a client "
            + "has to know about, so each needs a deliberate decision and an entry here: "
            + string.Join(", ", offenders.OrderBy(o => o)));
    }

    /// <summary>
    /// …and the Department gates really do declare it, so the test above cannot pass by the override
    /// having been dropped altogether.
    /// </summary>
    [Fact]
    public void Department_management_gates_declare_their_own_forbidden_code()
    {
        var departments = Controllers().Single(c => c.Name == "DepartmentsController");

        var gated = Actions(departments)
            .SelectMany(a => a.GetCustomAttributes<RoleAuthorizeAttribute>(inherit: true))
            .ToList();

        Assert.NotEmpty(gated);
        Assert.All(gated, gate =>
            Assert.Equal("DEPARTMENT_MANAGEMENT_FORBIDDEN", gate.ErrorCode));
    }

    private static IEnumerable<string> GetAllowedRoles(RoleAuthorizeAttribute attribute)
    {
        // The attribute keeps its role list private; read it back for assertion purposes.
        var field = typeof(RoleAuthorizeAttribute)
            .GetField("_allowedRoles", BindingFlags.NonPublic | BindingFlags.Instance);
        return (string[])(field?.GetValue(attribute) ?? Array.Empty<string>());
    }
}
