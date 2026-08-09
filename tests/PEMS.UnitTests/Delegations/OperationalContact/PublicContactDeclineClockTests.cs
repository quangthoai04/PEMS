using System.IO;
using System.Text.RegularExpressions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.UnitTests.Delegations.OperationalContact;

/// <summary>
/// The public (no-login) decline must read the SAME clock as every other step of the workflow —
/// <c>IDateTimeService.VietnamNow</c> — and never the process wall clock.
///
/// <para>
/// The settlement it performs is the one accept/decline path that does not go through the canonical
/// command, so it was the one place a <c>DateTime.Now</c> could hide. On a developer machine in
/// Vietnam the two agree and nothing looks wrong; on the deployment target they are seven hours apart,
/// and every timestamp that action writes — <c>DeclinedAt</c>, <c>RetentionUntil</c>, the identity
/// event, the audit row — lands in a different day from the rest of the invitation's history. Worse,
/// the same wall clock decided EXPIRY: a link with hours left compared against a UTC "now" is refused
/// as expired, and the person is told to ask for a new invitation that will fail exactly the same way.
/// </para>
/// <para>
/// Two things are pinned here, because they need different kinds of proof. The expiry decision is
/// BEHAVIOUR and is exercised against a fake instant. The absence of a wall clock in that code path is
/// an ABSENCE — no test that runs the code can demonstrate it, since a wall clock and an injected
/// clock agree whenever the test machine happens to be in +07:00 — so it is asserted against the
/// source of the two files that path is made of.
/// </para>
/// </summary>
public class PublicContactDeclineClockTests
{
    /// <summary>A fixed Vietnam-local instant, deliberately not "around now" for any test machine.</summary>
    private static readonly DateTime VietnamNow = new(2026, 8, 9, 16, 0, 0);

    private static VisitRequestIdentityChange Pending(DateTime expiresAt) => new()
    {
        IdentityChangeId = 1,
        VisitRequestId = 10,
        VisitInstanceId = 100,
        ChangeKind = IdentityChangeKinds.InitialConfirmation,
        Status = IdentityChangeStatuses.Pending,
        ExpiresAt = expiresAt,
    };

    // ── Expiry is decided against the injected instant ───────────────────────

    [Fact]
    public void An_invitation_still_inside_its_window_is_accepted_at_the_injected_instant()
    {
        // One minute of life left. A UTC clock would place "now" seven hours later and refuse it.
        OperationalContactGuards.EnsurePending(Pending(VietnamNow.AddMinutes(1)), VietnamNow);
    }

    [Theory]
    [InlineData(0)]    // ExpiresAt == now: the window is closed AT the boundary, not after it
    [InlineData(-1)]
    public void An_invitation_at_or_past_its_deadline_is_refused_with_the_canonical_code(int minutes)
    {
        var ex = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsurePending(Pending(VietnamNow.AddMinutes(minutes)), VietnamNow));

        Assert.Equal(OperationalContactErrorCodes.ConfirmationExpired, ex.ErrorCode);
    }

    // ── The path itself owns no clock ────────────────────────────────────────

    /// <summary>
    /// The settlement takes its instant as an argument. If it could read a clock of its own, the
    /// handler's <c>IDateTimeService</c> would be advisory — one caller could pass the right instant
    /// while the method quietly used another.
    /// </summary>
    [Fact]
    public void The_no_account_settlement_is_given_its_instant_rather_than_reading_one()
    {
        // Asserted on the source rather than by reflection: PublicContactAnswer is internal to
        // PEMS.Application, and widening its visibility for one test would make a private helper part
        // of the assembly's surface.
        var source = ReadSource("PublicContactAnswer.cs");

        Assert.Contains("VisitRequestIdentityChange untracked, string rawToken, string? reason, DateTime now,", source);
        // The single instant is what every timestamp in the settlement is derived from.
        Assert.Contains("change.DeclinedAt = now;", source);
        Assert.Contains("change.RetentionUntil = now.AddDays(retentionDays);", source);
    }

    [Theory]
    [InlineData("PublicOperationalContactAnswerHandlers.cs")]
    [InlineData("PublicContactAnswer.cs")]
    public void The_public_answer_path_contains_no_direct_wall_clock_read(string fileName)
    {
        var source = ReadSource(fileName);

        // Comments explain WHY the wall clock is wrong, so they legitimately name it; only code counts.
        var code = string.Join('\n', source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///")));

        foreach (var wallClock in new[] { "DateTime.Now", "DateTime.UtcNow" })
            Assert.False(Regex.IsMatch(code, Regex.Escape(wallClock) + @"\b"),
                $"{fileName} reads {wallClock} in business logic; the public answer path must take its " +
                "instant from IDateTimeService.VietnamNow so every timestamp in one action agrees.");
    }

    /// <summary>And the handler really does hand the injected clock down.</summary>
    [Fact]
    public void The_public_decline_handler_passes_the_injected_vietnam_clock_to_the_settlement()
    {
        var source = ReadSource("PublicOperationalContactAnswerHandlers.cs");

        Assert.Contains("_clock.VietnamNow, cancellationToken", source);
        Assert.Contains("IDateTimeService _clock", source);
    }

    /// <summary>Walks up to the repository root and reads the file from the source tree.</summary>
    private static string ReadSource(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        var path = Path.Combine(
            dir!.FullName, "backend", "PEMS.Application", "Delegations", "Commands",
            "OperationalContact", fileName);

        Assert.True(File.Exists(path), $"Expected the public answer path at {path}.");
        return File.ReadAllText(path);
    }
}
