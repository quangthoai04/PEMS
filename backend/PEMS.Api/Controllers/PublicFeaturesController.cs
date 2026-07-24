using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Common.Options;

namespace PEMS.Api.Controllers;

/// <summary>
/// DEPRECATED public capability surface. It exposes ONLY the boolean state a browser needs to decide
/// whether the per-campus form v2 flow is usable end-to-end — never any other config value, secret or
/// connection detail. Anonymous by design (the homepage CTA is public and unauthenticated) and never
/// mutates anything.
///
/// Retained for the existing browser contract, but there is exactly one runtime now and both flags
/// default ON, so in every real deployment this reports <c>enabled=true</c>. There is no v1 to fall back
/// to: when the flag IS off, the browser surfaces an error rather than an older flow. Slated for removal
/// once the frontend stops probing it.
/// </summary>
[ApiController]
[Route("api/public/features")]
public sealed class PublicFeaturesController : ControllerBase
{
    private readonly PerCampusFormV2Options _readOptions;
    private readonly PerCampusFormV2WriteOptions _writeOptions;

    public PublicFeaturesController(PerCampusFormV2Options readOptions, PerCampusFormV2WriteOptions writeOptions)
    {
        _readOptions = readOptions;
        _writeOptions = writeOptions;
    }

    /// <summary>
    /// Per-campus form v2 capability. <c>enabled</c> is the AND of the read and write flags: the only
    /// configuration in which a browser may safely submit a v2 request AND read it back. Read-only + write-off,
    /// or write-on + read-off, both surface as <c>enabled=false</c> so the browser keeps using v1.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("per-campus-form-v2")]
    public IActionResult GetPerCampusFormV2Capability()
        => Ok(PerCampusFormV2CapabilityResponse.From(_readOptions.Enabled, _writeOptions.Enabled));
}

/// <summary>
/// Minimal capability projection — the read flag, the write flag, and the derived <c>Enabled</c> only.
/// No internal configuration ever leaks through this shape.
/// </summary>
public sealed record PerCampusFormV2CapabilityResponse(bool ReadEnabled, bool WriteEnabled, bool Enabled)
{
    /// <summary><c>Enabled</c> is true only when BOTH flags are on.</summary>
    public static PerCampusFormV2CapabilityResponse From(bool readEnabled, bool writeEnabled)
        => new(readEnabled, writeEnabled, readEnabled && writeEnabled);
}
