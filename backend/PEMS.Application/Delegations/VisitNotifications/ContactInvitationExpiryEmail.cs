using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.VisitNotifications;

/// <summary>
/// Tells the registrant that ONE campus's contact invitation lapsed with nobody answering it.
///
/// <para>
/// The registrant is the one who can act — resend it, or name a different contact — so they are the one
/// told. This is NOT a rejection and NOT the visit's own lead time: it says only that an address did
/// not reply in the window it was given. For a TRANSFER the campus's current contact keeps the role and
/// nothing about the request changes; for an INITIAL_CONFIRMATION there is still no contact and the
/// request stays behind the gate until the registrant acts. Both facts are theirs to know.
/// </para>
/// <para>
/// The invited address is printed MASKED, as the identity-change row already stores it — the registrant
/// needs to recognise WHICH invitation lapsed, not to be handed the address in writing.
/// </para>
/// </summary>
public sealed class ContactInvitationExpiryEmail : IRecoverableVisitEmail
{
    private readonly IApplicationDbContext _db;

    public ContactInvitationExpiryEmail(IApplicationDbContext db) => _db = db;

    public string TemplateCode => SystemEmailTemplates.VisitContactInvitationExpired;

    public string RelatedType => "VisitRequestIdentityChange";

    public async Task<SystemEmailRequest?> BuildAsync(ulong identityChangeId, CancellationToken ct)
    {
        var context = await (
            from ch in _db.VisitRequestIdentityChanges.AsNoTracking()
            join c in _db.VisitRequestCampuses.AsNoTracking() on ch.VisitInstanceId equals c.VisitInstanceId
            join site in _db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            join v in _db.VisitRequests.AsNoTracking() on c.VisitRequestId equals v.VisitRequestId
            where ch.IdentityChangeId == identityChangeId
            select new
            {
                v.RequestCode,
                v.RegistrantEmail,
                v.RegistrantFullName,
                CampusName = site.Name,
                DelegationName = c.FormDetail!.DelegationName,
                ch.NewEmailMasked,
            }).FirstOrDefaultAsync(ct);

        if (context is null || string.IsNullOrWhiteSpace(context.RegistrantEmail))
            return null;

        return new SystemEmailRequest(
            TemplateCode,
            new EmailRecipient(context.RegistrantEmail, context.RegistrantFullName),
            new Dictionary<string, string>
            {
                ["recipientName"] = context.RegistrantFullName ?? string.Empty,
                ["requestCode"] = context.RequestCode,
                ["delegationName"] = context.DelegationName,
                ["campusName"] = context.CampusName,
                // Masked, and read from the row rather than passed in — the retention sweep nulls the
                // full address after 90 days but deliberately keeps this one.
                ["pendingContactEmailMasked"] = context.NewEmailMasked ?? string.Empty,
            },
            RelatedType: RelatedType,
            RelatedId: identityChangeId);
    }
}
