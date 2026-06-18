namespace PEMS.Application.Common.Security;

/// <summary>
/// Centralised authentication policy bound from the <c>"AuthOptions"</c> config
/// section. Drives the SSO-first / dual-portal behaviour and the dev-vs-production
/// login mode (see docs/authentication/PROMPT_SUA_LOGIN_SSO_FIRST_DUAL_PORTAL_PEMS.md §3).
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "AuthOptions";

    /// <summary><c>DevMixed</c> (password + SSO) or <c>ProductionSsoOnly</c> (SSO/FEID only).</summary>
    public string LoginMode { get; set; } = AuthLoginModes.DevMixed;

    public bool AllowPasswordLogin { get; set; } = true;
    public bool AllowGoogleSso { get; set; } = true;
    public bool AllowFeid { get; set; } = false;

    /// <summary>Auto-create a VISITOR account on first external (SSO/FEID) login via the Visitor portal.</summary>
    public bool AutoCreateVisitorOnExternalLogin { get; set; } = true;

    /// <summary>
    /// Internal accounts are NEVER auto-provisioned on login regardless of this flag;
    /// it is kept only for config visibility / parity with the spec.
    /// </summary>
    public bool AutoCreateInternalOnExternalLogin { get; set; } = false;

    /// <summary>Minimum student cohort eligible for FEID login (e.g. <c>"K19"</c>).</summary>
    public string? StudentFeidMinCohort { get; set; }

    /// <summary>True when the system runs in SSO-only (production) mode.</summary>
    public bool IsProductionSsoOnly =>
        string.Equals(LoginMode, AuthLoginModes.ProductionSsoOnly, StringComparison.OrdinalIgnoreCase);

    /// <summary>Password (credentials) login is usable only in dev mode AND when explicitly allowed.</summary>
    public bool PasswordLoginEnabled => AllowPasswordLogin && !IsProductionSsoOnly;
}

public static class AuthLoginModes
{
    public const string DevMixed = "DevMixed";
    public const string ProductionSsoOnly = "ProductionSsoOnly";
}
