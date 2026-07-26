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
    /// The accepted shapes, in the user's words. Appended to EVERY phone rejection because "không
    /// hợp lệ" on its own is a guessing game: the caller has no way to know whether the problem is
    /// the country code, a space, or an extension, and tries spellings until one sticks. Kept
    /// verbatim in sync with the frontend's <c>validation:phoneInvalidField</c>, so the two sides
    /// never describe the same rule differently.
    /// </summary>
    public const string FormatHint =
        "Nhập số Việt Nam dạng 0912345678 hoặc số quốc tế dạng +84912345678. Không nhập số máy lẻ.";

    /// <summary>
    /// Passes only for a number that parses AND exists per the phone metadata — national Vietnamese
    /// form or full international E.164. Blank is NOT handled here so callers stay explicit about
    /// whether a field is required: chain <c>.NotEmpty()</c> before this for a required field, and
    /// use <c>.When(...)</c> for an optional one.
    /// </summary>
    /// <param name="message">
    /// Names WHICH phone is wrong (a visit request carries three). <see cref="FormatHint"/> is
    /// appended automatically, so callers pass only the field-specific sentence.
    /// </param>
    public static IRuleBuilderOptions<T, string?> MustBeAPhoneNumber<T>(
        this IRuleBuilder<T, string?> rule, string message)
        => rule
            .Must(value => string.IsNullOrWhiteSpace(value) || PhoneNumber.IsValid(value))
            .WithMessage($"{message} {FormatHint}");
}
