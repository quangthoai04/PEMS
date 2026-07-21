using FluentValidation;
using PEMS.Application.Campuses.Common;

namespace PEMS.Application.Campuses.Commands.UpdateCampus;

/// <summary>
/// UC-85 update. Shares one rule set with UC-81 create via <see cref="CampusValidationExtensions"/>
/// (spec §12.1). The city whitelist is the single exception: it is enforced in the handler, which
/// can compare against the stored value and let an unmigrated legacy city stay as long as the HO
/// is not changing it (spec §6.3). Duplicate checks live in the handler too.
/// </summary>
public sealed class UpdateCampusCommandValidator : AbstractValidator<UpdateCampusCommand>
{
    public UpdateCampusCommandValidator()
    {
        RuleFor(x => x.CampusId)
            .GreaterThan(0ul).WithMessage("Campus không hợp lệ.");

        RuleFor(x => x.CampusCode).ApplyCampusCodeRules();
        RuleFor(x => x.Name).ApplyCampusNameRules();
        RuleFor(x => x.City).ApplyCampusCityPresenceRule();
        RuleFor(x => x.Address).ApplyCampusAddressRules();
        RuleFor(x => x.Phone).ApplyCampusPhoneRules();
        RuleFor(x => x.Email).ApplyCampusEmailRules();
    }
}
