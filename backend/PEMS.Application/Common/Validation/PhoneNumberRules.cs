using FluentValidation;
using PEMS.Shared;

namespace PEMS.Application.Common.Validation;

/// <summary>
/// The shared FluentValidation phone rule. Every validator that accepts a phone number must use
/// this instead of <c>MaximumLength(50)</c>: the frontend has always validated with libphonenumber,
/// so a length-only backend rule meant a direct API call could store values the UI would reject.
/// </summary>
public static class PhoneNumberRules
{
    /// <summary>
    /// Passes only for a number that parses AND exists per the phone metadata — national Vietnamese
    /// form or full international E.164. Blank is NOT handled here so callers stay explicit about
    /// whether a field is required: chain <c>.NotEmpty()</c> before this for a required field, and
    /// use <c>.When(...)</c> for an optional one.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeAPhoneNumber<T>(
        this IRuleBuilder<T, string?> rule, string message)
        => rule
            .Must(value => string.IsNullOrWhiteSpace(value) || PhoneNumber.IsValid(value))
            .WithMessage(message);
}
