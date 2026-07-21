using FluentValidation;
using PEMS.Application.Campuses.Common;

namespace PEMS.Application.Campuses.Commands.AddNewCampus;

/// <summary>
/// UC-81 create. Every rule comes from <see cref="CampusMasterRules"/> via
/// <see cref="CampusValidationExtensions"/>, so create and UC-85 update accept and reject exactly
/// the same master data (spec §12.1). Duplicate checks live in the handler.
/// </summary>
public sealed class AddNewCampusCommandValidator : AbstractValidator<AddNewCampusCommand>
{
    public AddNewCampusCommandValidator()
    {
        RuleFor(x => x.CampusCode).ApplyCampusCodeRules();
        RuleFor(x => x.Name).ApplyCampusNameRules();
        // A brand-new campus can never carry a legacy city, so the whitelist applies unconditionally.
        RuleFor(x => x.City).ApplyCampusCityRules();
        RuleFor(x => x.Address).ApplyCampusAddressRules();
        RuleFor(x => x.Phone).ApplyCampusPhoneRules();
        RuleFor(x => x.Email).ApplyCampusEmailRules();
    }
}
