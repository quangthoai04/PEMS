namespace PEMS.Domain.Constants;

/// <summary>PEMS v10: logistics borrow/return handover enums.</summary>
public static class LogisticsHandoverTypes
{
    public const string Borrow = "BORROW";
    public const string Return = "RETURN";

    public static readonly string[] All = { Borrow, Return };
}

public static class HandoverItemConditions
{
    public const string Good = "GOOD";
    public const string Damaged = "DAMAGED";
    public const string Missing = "MISSING";
    public const string Other = "OTHER";

    public static readonly string[] All = { Good, Damaged, Missing, Other };

    /// <summary>Conditions that require a condition_note to be filled in.</summary>
    public static readonly string[] RequireNote = { Damaged, Missing, Other };
}

/// <summary>Which side of a handover is signing.</summary>
public static class HandoverSignerSides
{
    public const string Borrower = "BORROWER";
    public const string Provider = "PROVIDER";

    public static readonly string[] All = { Borrower, Provider };
}
