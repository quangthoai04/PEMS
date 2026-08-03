using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Queries.GetEmailTemplateContract;

public sealed class GetEmailTemplateContractQueryHandler
    : IRequestHandler<GetEmailTemplateContractQuery, EmailTemplateContractDto>
{
    private readonly ICurrentUserService _currentUser;

    public GetEmailTemplateContractQueryHandler(ICurrentUserService currentUser) => _currentUser = currentUser;

    public Task<EmailTemplateContractDto> Handle(
        GetEmailTemplateContractQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
            throw new ValidationException("Thiếu mã template email.");

        var code = request.TemplateCode.Trim();
        var contract = EmailTemplateContracts.Describe(code, request.Language);

        if (contract is null)
        {
            // A historical row — some sent email or draft still references it — rather than a registered
            // system template. It is answered rather than 404'd so the editor can say "this template is
            // not part of the system catalog and cannot be edited" instead of showing a failed request.
            return Task.FromResult(new EmailTemplateContractDto
            {
                TemplateCode = code,
                Module = "",
                IsSystemTemplate = false,
                SecurityClassification = EmailTemplateContracts.ClassificationStandard,
                EditableFields = new List<string>(),
            });
        }

        return Task.FromResult(new EmailTemplateContractDto
        {
            TemplateCode = contract.TemplateCode,
            Module = contract.Module,
            IsSystemTemplate = true,
            Variables = contract.Variables
                .Select(v => new EmailTemplateContractVariableDto
                {
                    Name = v.Name,
                    Label = v.Label,
                    Sample = v.Sample,
                    Required = v.Required,
                    Sensitive = v.Sensitive,
                    ForbiddenInSubject = v.ForbiddenInSubject,
                })
                .ToList(),
            AllowedVariables = contract.AllowedVariables,
            RequiredVariables = contract.RequiredVariables,
            OptionalVariables = contract.OptionalVariables,
            RequiredSystemBlocks = contract.RequiredSystemBlocks,
            OptionalSystemBlocks = contract.OptionalSystemBlocks,
            SystemBlockPreviews = BuildBlockPreviews(contract, request.Language),
            SensitiveVariables = contract.SensitiveVariables,
            ForbiddenInSubject = contract.ForbiddenInSubject,
            ActionSupported = contract.ActionSupported,
            ActionRequired = contract.ActionRequired,
            SystemActionDescription = contract.SystemActionDescription,
            CarriesSecret = contract.CarriesSecret,
            AllowCc = contract.AllowCc,
            AllowBcc = contract.AllowBcc,
            SecurityClassification = contract.SecurityClassification,
            EditableFields = contract.EditableFields,
        });
    }

    /// <summary>
    /// The inert markup the editor substitutes for each allowed block.
    ///
    /// <para>
    /// Every branch here mirrors <c>PreviewEmailTemplateQueryHandler</c>, which is the same decision made
    /// for the preview modal: which disabled block a template gets depends on its action spec, and a
    /// detail-link template shows the label its own send uses rather than the Department flow's wording.
    /// The contact block is excluded — see the DTO remarks.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildBlockPreviews(
        EmailTemplateContract contract, string? language)
    {
        var lang = EmailLanguages.Normalize(language);
        var previews = new Dictionary<string, string>(System.StringComparer.Ordinal);

        foreach (var block in contract.AllowedSystemBlocks)
        {
            if (block == EmailTrustedBlocks.ContactInformationBlock) continue;

            previews[block] = block switch
            {
                EmailTrustedBlocks.SetupSummaryBlock =>
                    EmailComposition.DisabledSetupSummaryBlock(lang),
                EmailTrustedBlocks.ActionBlock =>
                    EmailActionTemplates.DisabledBlockFor(contract.TemplateCode, lang),
                _ => string.Empty,
            };
        }

        return previews;
    }
}
