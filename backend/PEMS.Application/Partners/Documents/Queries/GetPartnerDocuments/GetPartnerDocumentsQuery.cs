using System.Collections.Generic;
using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Documents.Queries.GetPartnerDocuments;

/// <summary>GET /api/partners/{partnerId}/documents — documents.owner_type = PARTNER.</summary>
public sealed record GetPartnerDocumentsQuery(ulong PartnerId) : IRequest<List<PartnerDocumentDto>>;
