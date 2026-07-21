using FluentValidation;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// FluentValidation glue over <see cref="CampusMasterRules"/> so AddNewCampus and UpdateCampus
/// share one rule set and one message set (spec §14) — create can never accept what edit rejects,
/// or vice versa. Every rule validates the NORMALIZED value, matching what the handler writes.
/// Uniqueness stays in the handler via <see cref="CampusDuplicateGuard"/> (needs the database).
/// </summary>
public static class CampusValidationExtensions
{
    /// <summary>Required + 2–20 chars + charset + separator placement, on the normalized code.</summary>
    public static IRuleBuilderOptions<T, string?> ApplyCampusCodeRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => CampusMasterRules.ValidateCampusCode(v) is null)
            .WithMessage((_, v) => CampusMasterRules.ValidateCampusCode(v) ?? string.Empty);

    /// <summary>Required + 3–150 chars + meaningful content + charset, on the normalized name.</summary>
    public static IRuleBuilderOptions<T, string?> ApplyCampusNameRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => CampusMasterRules.ValidateCampusName(v) is null)
            .WithMessage((_, v) => CampusMasterRules.ValidateCampusName(v) ?? string.Empty);

    /// <summary>
    /// Required + province whitelist. Only used where a legacy value cannot occur (create);
    /// UpdateCampus enforces the whitelist in its handler so an unmigrated legacy city can still
    /// be saved as long as it is not being changed (spec §6.3).
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ApplyCampusCityRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => CampusMasterRules.ValidateCampusCity(v) is null)
            .WithMessage((_, v) => CampusMasterRules.ValidateCampusCity(v) ?? string.Empty);

    /// <summary>Required only — the whitelist check needs the stored value (see above).</summary>
    public static IRuleBuilderOptions<T, string?> ApplyCampusCityPresenceRule<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => CampusNormalization.City(v).Length > 0)
            .WithMessage(CampusMasterRules.CityRequiredMessage);

    /// <summary>Required + 5–255 chars + meaningful content + charset, on the normalized address.</summary>
    public static IRuleBuilderOptions<T, string?> ApplyCampusAddressRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => CampusMasterRules.ValidateCampusAddress(v) is null)
            .WithMessage((_, v) => CampusMasterRules.ValidateCampusAddress(v) ?? string.Empty);

    /// <summary>Required + ≤30 chars + charset + '+' placement + 8–15 digits + Vietnamese prefix.</summary>
    public static IRuleBuilderOptions<T, string?> ApplyCampusPhoneRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => CampusMasterRules.ValidateCampusPhone(v) is null)
            .WithMessage((_, v) => CampusMasterRules.ValidateCampusPhone(v) ?? string.Empty);

    /// <summary>Required + length + shape + no plus-addressing + exact @fpt.edu.vn/@fe.edu.vn.</summary>
    public static IRuleBuilderOptions<T, string?> ApplyCampusEmailRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => CampusMasterRules.ValidateCampusEmail(v) is null)
            .WithMessage((_, v) => CampusMasterRules.ValidateCampusEmail(v) ?? string.Empty);
}
