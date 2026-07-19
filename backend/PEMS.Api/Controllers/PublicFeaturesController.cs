using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Common.Options;

namespace PEMS.Api.Controllers;

/// <summary>
/// Public, read-only feature-capability surface. It exposes ONLY the boolean state a browser needs to
/// decide whether the per-campus form v2 flow is usable end-to-end — never any other config value, secret
/// or connection detail. Anonymous by design (the homepage CTA is public and unauthenticated) and never
/// mutates anything.
///
/// The frontend uses this as the single authority for the v2 cutover: it must NOT guess the flag itself,
/// and it must fail SAFE to v1 when this endpoint is unreachable.
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
