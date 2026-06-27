using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Queries.GetEmailDraft;

/// <summary>
/// Loads a single email draft (header + recipients + attachments + file metadata) so the compose
/// modal can be re-hydrated after a reload. Only the draft owner may read it.
/// </summary>
public sealed record GetEmailDraftQuery(ulong EmailDraftId) : IRequest<EmailDraftDto>;
