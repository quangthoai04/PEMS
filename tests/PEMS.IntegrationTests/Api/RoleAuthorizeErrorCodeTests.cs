using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Api.Filters;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Departments.Common;
using Xunit;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// What the role gate SAYS when it refuses — the two answers it must be able to give.
///
/// <para>
/// The gate was introduced in front of handlers that had been guarding themselves. That is the right
/// move: it keeps a wrong role out of the handler entirely. But a refusal that moves earlier must not
/// quietly change its wording, and this one did — Department management answered
/// <c>DEPARTMENT_MANAGEMENT_FORBIDDEN</c>, which the department screen maps to a specific sentence,
/// and the gate replaced it with the generic <c>FORBIDDEN</c>, which falls through to a vaguer one.
/// </para>
/// <para>
/// Both directions are pinned here. The generic default matters as much as the override: an escape
/// hatch that everything starts using gives clients more strings to branch on and no more information.
/// </para>
/// </summary>
public sealed class RoleAuthorizeErrorCodeTests
{
    private sealed class Actor : ICurrentUserService
    {
        public bool IsAuthenticated { get; init; } = true;
        public ulong? UserId => 7;
        public string? Email => "actor@fpt.edu.vn";
        public ulong? RoleId => null;
        public string? RoleCode { get; init; } = "HO";
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId => 1;
        public ulong? SessionId => 1;
        public ulong? DepartmentId => null;
        public string? LoginPortal => null;
    }

    /// <summary>Runs the filter against one actor and hands back whatever it decided.</summary>
    private static async Task<(int? Status, string? ErrorCode, string? Message)> RefuseAsync(
        RoleAuthorizeAttribute gate, ICurrentUserService actor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(actor);

        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            TraceIdentifier = "trace-for-this-test",
        };

        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());

        await gate.OnAuthorizationAsync(context);

        if (context.Result is not ObjectResult result) return (null, null, null);

        // The payload is an anonymous type by design — it is a response shape, not a contract class —
        // so it is read the way a client reads the JSON: by name.
        var value = result.Value!;
        string? Read(string name) =>
            value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(value) as string;

        return (result.StatusCode, Read("errorCode"), Read("message"));
    }

    /// <summary>An HO is authenticated and is not a Staff Leader — the ordinary wrong-role refusal.</summary>
    private static readonly Actor WrongRole = new() { RoleCode = "HO" };

    [Fact]
    public async Task A_gate_with_no_error_code_answers_the_generic_one()
    {
        var (status, errorCode, message) = await RefuseAsync(
            new RoleAuthorizeAttribute(EffectiveRole.StaffLeader), WrongRole);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("FORBIDDEN", errorCode);
        Assert.Equal("Bạn không có quyền thực hiện thao tác này.", message);
    }

    [Fact]
    public async Task A_gate_that_declares_a_module_code_answers_with_it()
    {
        var (status, errorCode, message) = await RefuseAsync(
            new RoleAuthorizeAttribute(EffectiveRole.StaffLeader)
            {
                ErrorCode = DepartmentErrorCodes.DepartmentManagementForbidden,
                Message = "Bạn không có quyền quản lý phòng ban.",
            },
            WrongRole);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, errorCode);
        Assert.Equal("Bạn không có quyền quản lý phòng ban.", message);
    }

    /// <summary>
    /// An account whose (role_code, sub_role) pair is not a valid combination is a data defect, and it
    /// must fail closed with the SAME code the endpoint publishes.
    ///
    /// <para>
    /// Pinned because this path is easy to miss when adding a code: it is a different branch of the
    /// filter, and a caller who hit it would get a refusal the screen could not translate.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_unresolvable_role_pair_also_answers_with_the_module_code()
    {
        var brokenAccount = new Actor { RoleCode = "STAFF", SubRole = null };

        var (status, errorCode, _) = await RefuseAsync(
            new RoleAuthorizeAttribute(EffectiveRole.StaffLeader)
            {
                ErrorCode = DepartmentErrorCodes.DepartmentManagementForbidden,
            },
            brokenAccount);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, errorCode);
    }

    /// <summary>
    /// A code with no message still reads correctly: the default sentence is used rather than an empty
    /// one. Half-configuring the override must not produce a blank message.
    /// </summary>
    [Fact]
    public async Task A_code_without_a_message_keeps_the_default_sentence()
    {
        var (_, errorCode, message) = await RefuseAsync(
            new RoleAuthorizeAttribute(EffectiveRole.StaffLeader)
            {
                ErrorCode = DepartmentErrorCodes.DepartmentManagementForbidden,
            },
            WrongRole);

        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, errorCode);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    /// <summary>
    /// An allowed role is not refused at all — the override changes what a refusal SAYS, never who is
    /// refused. Without this, a gate that rejected everybody would satisfy every assertion above.
    /// </summary>
    [Fact]
    public async Task An_allowed_role_passes_the_gate()
    {
        var staffLeader = new Actor { RoleCode = "STAFF", SubRole = "LEADER" };

        var (status, _, _) = await RefuseAsync(
            new RoleAuthorizeAttribute(EffectiveRole.StaffLeader)
            {
                ErrorCode = DepartmentErrorCodes.DepartmentManagementForbidden,
            },
            staffLeader);

        Assert.Null(status);
    }

    /// <summary>
    /// An unauthenticated caller gets 401, not the module's 403 — the module code describes a
    /// permission, and "you did not say who you are" is a different answer that a client must be able
    /// to act on differently. In the running API the fallback policy stops these even earlier.
    /// </summary>
    [Fact]
    public async Task An_unauthenticated_caller_is_challenged_not_refused()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new Actor { IsAuthenticated = false });

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());

        await new RoleAuthorizeAttribute(EffectiveRole.StaffLeader)
        {
            ErrorCode = DepartmentErrorCodes.DepartmentManagementForbidden,
        }.OnAuthorizationAsync(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }
}
