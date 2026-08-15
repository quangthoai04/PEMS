using PEMS.Application.Delegations.Minutes;
using PEMS.Shared;

namespace PEMS.UnitTests.Delegations.Minutes;

/// <summary>
/// MIN-02 — the biên bản has to say which list a person came from.
///
/// <para>
/// Every guest-side row used to be labelled "Khách", because <c>guest_member_id != null</c> was the
/// only thing the client was told. An interpreter or a travelling coordinator is written down as
/// <c>EXTERNAL_SUPPORT</c>, is eligible to hold the campus's contact role, and is not a member of
/// the delegation — recording them as one is simply wrong, and no amount of UI could have fixed it
/// without the field.
/// </para>
/// </summary>
public class MinuteParticipantSourceKindTests
{
    [Fact]
    public void An_account_holder_who_is_not_the_contact_is_internal()
    {
        Assert.Equal("INTERNAL", MinuteParticipantDto.KindOf(7, null, null));
        Assert.Equal("INTERNAL", MinuteParticipantDto.KindOf(7, 31, GuestMemberType.ExternalSupport));
    }

    // ── The đầu mối is guest-side, and an account of theirs proves only that they confirmed ──────

    [Fact]
    public void The_contact_is_NOT_internal_merely_for_holding_an_account()
    {
        // What the screen showed: the person leading the visiting delegation, in a row badged "Đầu
        // mối đoàn khách", labelled "Nội bộ". The user_id came from their SSO confirmation — the
        // invitation provisions or reuses an account so they can answer it — and it was being read as
        // "works here". OperationalContactEligibility refuses the role to an internal account twice
        // over, at the invited address and again at the account that answers, so a row wearing this
        // badge can never be internal.
        Assert.Equal(GuestMemberType.Guest,
            MinuteParticipantDto.KindOf(229, null, null, isOperationalContact: true));
    }

    [Fact]
    public void A_contact_who_travels_as_SUPPORT_keeps_that_kind()
    {
        // An interpreter or a travelling coordinator is exactly who a campus rings, and is eligible
        // for the role. "Khách" would be as wrong for them here as it is anywhere else.
        Assert.Equal(GuestMemberType.ExternalSupport,
            MinuteParticipantDto.KindOf(229, 31, GuestMemberType.ExternalSupport, isOperationalContact: true));
        Assert.Equal(GuestMemberType.Guest,
            MinuteParticipantDto.KindOf(229, 31, GuestMemberType.Guest, isOperationalContact: true));
    }

    [Fact]
    public void A_contact_outside_the_delegation_reads_as_a_guest()
    {
        // No member row, so there is no recorded member_type and none can be invented. Guest-side is
        // still everything that IS known, and it is what the badge sits on top of: "Khách · Đầu mối".
        Assert.Equal(GuestMemberType.Guest,
            MinuteParticipantDto.KindOf(null, null, null, isOperationalContact: true));
    }

    [Fact]
    public void A_delegation_member_keeps_the_kind_that_was_recorded()
    {
        Assert.Equal(GuestMemberType.Guest,
            MinuteParticipantDto.KindOf(null, 31, GuestMemberType.Guest));
        Assert.Equal(GuestMemberType.ExternalSupport,
            MinuteParticipantDto.KindOf(null, 31, GuestMemberType.ExternalSupport));
    }

    [Fact]
    public void A_row_with_neither_id_is_a_person_who_exists_only_here()
    {
        // MANUAL is its own kind, not a flavour of "Khách": it is the one row whose name / role /
        // organisation the biên bản owns and may edit (MIN-04).
        Assert.Equal("MANUAL", MinuteParticipantDto.KindOf(null, null, null));
    }

    [Fact]
    public void A_legacy_row_with_no_recorded_kind_reads_as_a_guest()
    {
        // Biên bản created before the column existed. GUEST is the old behaviour and much the
        // commoner of the two — a display default for rows that predate the question, never a
        // decision written back to the database.
        Assert.Equal(GuestMemberType.Guest, MinuteParticipantDto.KindOf(null, 31, null));
        Assert.Equal(GuestMemberType.Guest, MinuteParticipantDto.KindOf(null, 31, ""));
    }
}
