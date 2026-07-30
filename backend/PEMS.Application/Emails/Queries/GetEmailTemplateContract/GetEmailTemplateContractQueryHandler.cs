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
            SensitiveVariables = contract.SensitiveVariables,
            ForbiddenInSubject = contract.ForbiddenInSubject,
            RequiresActionBlock = contract.RequiresActionBlock,
            CarriesSecret = contract.CarriesSecret,
            AllowCc = contract.AllowCc,
            AllowBcc = contract.AllowBcc,
            SecurityClassification = contract.SecurityClassification,
            EditableFields = contract.EditableFields,
        });
    }
}
