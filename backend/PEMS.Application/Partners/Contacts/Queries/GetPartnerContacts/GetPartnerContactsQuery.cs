using System.Collections.Generic;
using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Contacts.Queries.GetPartnerContacts;

/// <summary>GET /api/partners/{partnerId}/contacts</summary>
public sealed record GetPartnerContactsQuery(ulong PartnerId, bool IncludeInactive = false)
    : IRequest<List<PartnerContactDto>>;
