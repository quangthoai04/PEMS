namespace PEMS.Application.Authentication.Models;

/// <summary>
/// A verified identity returned by an external identity provider (FEID, and in
/// future any non-Google provider that does not have its own dedicated validator).
/// This is the trusted output of provider verification — the caller must never
/// trust an email/subject that did not come back from a verifier.
/// </summary>
public sealed class ExternalIdentityResult
{
    /// <summary>Provider type constant (see <c>ProviderTypes</c>), e.g. <c>FEID</c>.</summary>
    public string Provider { get; init; } = default!;

    /// <summary>Stable, provider-issued subject id used to link the local account.</summary>
    public string ProviderSubject { get; init; } = default!;

    public string Email { get; init; } = default!;

    public bool EmailVerified { get; init; }

    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    /// <summary>Student code, when the provider exposes one (FEID students).</summary>
    public string? StudentCode { get; init; }

    /// <summary>Cohort label (e.g. <c>"K19"</c>), used for FEID eligibility checks.</summary>
    public string? Cohort { get; init; }
}
