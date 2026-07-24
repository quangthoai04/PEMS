using PEMS.Application.Common.Options;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Pins the Pure V2 default for the per-campus form availability flags: ON.
///
/// The runtime is per-campus only — there is no v1 create/read flow to fall back to — so a deployment
/// that omits the config section (Development and Production both do) must still get a working visit
/// form. When the C# default was false, the whole feature was dead outside the test environment: the v2
/// endpoints 404'd and the capability endpoint reported disabled, with nothing behind it. This test is
/// the regression guard for that break.
/// </summary>
public sealed class PerCampusFormV2OptionsDefaultTests
{
    [Fact]
    public void Read_flag_defaults_on_so_an_unconfigured_deployment_serves_the_visit_form()
    {
        Assert.True(new PerCampusFormV2Options().Enabled);
    }

    [Fact]
    public void Write_flag_defaults_on_so_an_unconfigured_deployment_accepts_visit_requests()
    {
        Assert.True(new PerCampusFormV2WriteOptions().Enabled);
    }

    [Fact]
    public void Both_default_on_together_which_is_the_only_valid_configuration()
    {
        // Write-on + read-off is the one invalid pairing (create records nothing can read), so the two
        // defaults must agree. Defaulting both on keeps the unconfigured deployment in the valid state.
        var read = new PerCampusFormV2Options().Enabled;
        var write = new PerCampusFormV2WriteOptions().Enabled;
        Assert.True(read && write);
    }
}
