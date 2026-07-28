using System;
using PEMS.Domain.Enums;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// Canonical <c>MALE / FEMALE / OTHER</c> wire values for personnel gender, and the mapping to the
/// <see cref="Gender"/> enum stored in <c>users.gender</c> (spec §5.4).
///
/// The API never accepts or emits the display labels ("Nam"/"Nữ"/"Khác") and never accepts the raw
/// enum ordinal: an ordinal on the wire is what previously let a Male/Female account silently become
/// Other when a modal round-tripped a value it did not understand. Anything unrecognised is REJECTED
/// here rather than defaulted, so a bad payload fails loudly instead of rewriting the record.
/// </summary>
public static class DepartmentPersonnelGenders
{
    public const string Male = "MALE";
    public const string Female = "FEMALE";
    public const string Other = "OTHER";

    /// <summary>Wire value for a stored enum, or null when the account has no gender recorded.</summary>
    public static string? ToWire(Gender? gender) => gender switch
    {
        Gender.Male => Male,
        Gender.Female => Female,
        Gender.Other => Other,
        _ => null,
    };

    /// <summary>
    /// Parses a wire value. Returns false for anything that is not exactly one of the three canonical
    /// values (case-insensitively) — including the Vietnamese labels and numeric ordinals.
    /// </summary>
    public static bool TryParse(string? value, out Gender gender)
    {
        gender = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        switch (value.Trim().ToUpperInvariant())
        {
            case Male: gender = Gender.Male; return true;
            case Female: gender = Gender.Female; return true;
            case Other: gender = Gender.Other; return true;
            default: return false;
        }
    }

    /// <summary>True when <paramref name="value"/> is one of the three canonical wire values.</summary>
    public static bool IsValid(string? value) => TryParse(value, out _);

    public const string InvalidMessage = "Giới tính chỉ nhận một trong các giá trị: MALE, FEMALE, OTHER.";

    /// <summary>Parses or throws the standard 400 message. Used by handlers after validator screening.</summary>
    public static Gender Parse(string? value)
        => TryParse(value, out var gender)
            ? gender
            : throw new PEMS.Application.Common.Exceptions.ValidationException(InvalidMessage);
}
