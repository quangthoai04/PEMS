using FluentValidation;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// FluentValidation glue over <see cref="AccountIdentityRules"/> so CreateAccount,
/// UpdateBasicAccountInfo and ReplaceStaffLeader share one regex and one message set.
/// Every rule validates the NORMALIZED value, matching what the handler eventually writes.
/// </summary>
public static class AccountIdentityValidationExtensions
{
    /// <summary>Required + 2–150 chars + allowed character set, applied to the normalized name.</summary>
    public static IRuleBuilderOptions<T, string?> ApplyAccountFullNameRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => AccountIdentityRules.ValidateFullName(v) is null)
            .WithMessage((_, v) => AccountIdentityRules.ValidateFullName(v) ?? string.Empty);

    /// <summary>
    /// Required + length + shape + local-part + no plus-addressing + exact domain whitelist, applied
    /// to the normalized email. Uniqueness stays in the handler (needs the database).
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ApplyAccountEmailRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => AccountIdentityRules.ValidateEmail(v) is null)
            .WithMessage((_, v) => AccountIdentityRules.ValidateEmail(v) ?? string.Empty);

    /// <summary>Required + 10–500 chars + meaningful content (spec §6.3.3).</summary>
    public static IRuleBuilderOptions<T, string?> ApplyReplacementReasonRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(v => AccountIdentityRules.ValidateReplacementReason(v) is null)
            .WithMessage((_, v) => AccountIdentityRules.ValidateReplacementReason(v) ?? string.Empty);
}
