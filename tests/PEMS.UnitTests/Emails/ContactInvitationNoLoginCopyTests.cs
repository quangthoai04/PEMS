using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The two operational-contact invitations must not tell their reader to sign in.
///
/// <para>
/// The invitation stopped requiring a Google account when the passwordless action-token flow landed:
/// each email carries TWO one-time, action-bound links, the confirmation page mutates nothing on GET,
/// and the invited person — normally an external guest with no PEMS account at all — answers without a
/// session. The copy did not follow. Both bodies went on saying the page "yêu cầu đăng nhập bằng đúng
/// tài khoản Google" / "asks you to sign in with the Google account", which describes a barrier that
/// no longer exists.
/// </para>
/// <para>
/// That is not a wording nit. This mail is the ONLY thing standing between a campus and its
/// confirmation gate: a reader who believes they must create a Google account to say yes closes the
/// email, the campus never gets a contact, the request never leaves PENDING_CONTACT_CONFIRMATION, and
/// no Staff Leader ever sees it. Copy drift here is an outage with a polite tone.
/// </para>
/// <para>
/// Pinned against the SHIPPED DEFAULTS rather than a database, so it runs without MySQL and guards the
/// file that a "restore to default" actually writes. The canonical seed and 02_sync_templates.sql are
/// held to the same wording by <c>EmailTemplateDefaultsParityTests</c>.
/// </para>
/// </summary>
public sealed class ContactInvitationNoLoginCopyTests
{
    public static IEnumerable<object[]> Invitations() => new[]
    {
        new object[] { SystemEmailTemplates.VisitContactClaim },
        new object[] { SystemEmailTemplates.VisitContactTransfer },
    };

    /// <summary>The exact sentences the F05 patch removes, in both languages.</summary>
    private static readonly string[] StaleLoginPhrases =
    {
        "đăng nhập bằng đúng tài khoản Google",
        "sign in with the Google account",
    };

    [Theory]
    [MemberData(nameof(Invitations))]
    public void No_body_asks_the_reader_to_sign_in_with_a_google_account(string code)
    {
        var t = EmailTemplateDefaults.For(code);
        Assert.NotNull(t);

        foreach (var (lang, body) in new[] { ("body_vi", t!.BodyVi), ("body_en", t.BodyEn) })
            foreach (var stale in StaleLoginPhrases)
                Assert.False(body.Contains(stale),
                    $"{code}.{lang} still tells the recipient to sign in: \"{stale}\".");
    }

    /// <summary>
    /// And says the true thing in its place. Removing the false sentence is only half the fix — a reader
    /// with no PEMS account needs to be told they do not need one, or they will assume they do.
    /// </summary>
    [Theory]
    [MemberData(nameof(Invitations))]
    public void Both_bodies_say_no_sign_in_is_needed(string code)
    {
        var t = EmailTemplateDefaults.For(code)!;
        Assert.Contains("không cần đăng nhập PEMS", t.BodyVi);
        Assert.Contains("do not need to sign in to PEMS", t.BodyEn);
    }

    /// <summary>
    /// There are two links now, so the security note must not promise one. A reader told "the link can
    /// be used once" who then presses the second button reasonably concludes something has gone wrong.
    /// </summary>
    [Theory]
    [MemberData(nameof(Invitations))]
    public void The_security_note_speaks_of_both_links(string code)
    {
        var t = EmailTemplateDefaults.For(code)!;
        Assert.Contains("Các liên kết có thời hạn và mỗi liên kết chỉ dùng được một lần.", t.BodyVi);
        Assert.Contains("The links expire and each can be used once.", t.BodyEn);
    }

    /// <summary>
    /// The action block stays a PLACEHOLDER. The accept/decline URLs are credentials: the backend mints
    /// the one-time tokens at send time and injects a trusted block, so a template that carried
    /// {{acceptUrl}} / {{declineUrl}} as ordinary variables would hand token composition to anyone who
    /// can edit copy — and the URLs would then be stored in template history in the clear.
    /// </summary>
    [Theory]
    [MemberData(nameof(Invitations))]
    public void The_links_stay_inside_the_trusted_action_block(string code)
    {
        var t = EmailTemplateDefaults.For(code)!;

        foreach (var (lang, body) in new[] { ("body_vi", t.BodyVi), ("body_en", t.BodyEn) })
        {
            Assert.True(body.Contains("{{actionBlock}}"), $"{code}.{lang} lost {{{{actionBlock}}}}.");
            foreach (var forbidden in new[] { "acceptUrl", "declineUrl", "confirmationUrl", "rawToken" })
                Assert.False(body.Contains(forbidden),
                    $"{code}.{lang} embeds a raw link variable ({forbidden}); links belong to the action block.");
        }
    }

    /// <summary>
    /// The rewrite was content-only: neither template may have gained a variable, because the renderer
    /// fails closed on a declared variable the sender does not supply — a new name here would stop every
    /// invitation from rendering at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Invitations))]
    public void The_declared_variable_set_is_unchanged(string code)
    {
        var declared = SystemEmailTemplates.Find(code)!.DeclaredVariables;

        var expected = code == SystemEmailTemplates.VisitContactClaim
            ? new[]
            {
                "contactFullName", "requestCode", "delegationName", "campusName", "plannedTime",
                "senderName", "senderRole", "senderEmail", "senderPhone", "senderDepartment", "senderCampus",
            }
            : new[]
            {
                "contactFullName", "currentContactName", "requestCode", "delegationName", "campusName",
                "plannedTime", "senderName", "senderRole", "senderEmail", "senderPhone", "senderDepartment",
                "senderCampus",
            };

        Assert.Equal(expected.OrderBy(v => v), declared.OrderBy(v => v));
    }
}
