using System;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Reports.Common;

/// <summary>
/// The money contract for the unit prices a Department Leader types into an invoice.
///
/// <para>
/// A leader declaring the price is the business rule, so the value legitimately comes from the client —
/// the quantity never does, it is re-read from the host's request. What was missing was any bound on it.
/// The handlers refused a negative price and accepted everything else, while the database that records
/// money in this system is <c>DECIMAL(18,2)</c> with <c>CHECK (unit_price &gt;= 0)</c>. Three things
/// followed from the gap:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>quantity * unitPrice</c> and the grand total are <see cref="decimal"/> arithmetic, so a price near
/// <see cref="decimal.MaxValue"/> raises <see cref="OverflowException"/> — an unhandled 500 that any
/// authenticated department leader could produce.
/// </item>
/// <item>
/// A price with more than two decimals prints fractions of a đồng on a document that is emailed and
/// signed, and that the system could never store if it were asked to.
/// </item>
/// <item>
/// A thirty-digit figure renders an invoice nobody can read and a total nobody can check.
/// </item>
/// </list>
/// <para>
/// So the bound is the database's, not an invented one: any price the invoice accepts is a price the
/// system could record.
/// </para>
/// </summary>
public static class InvoiceMoney
{
    /// <summary>Largest value <c>DECIMAL(18,2)</c> can hold: 16 integer digits and 2 decimals.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>Đồng are recorded to two decimals; the column stores no more.</summary>
    public const int Scale = 2;

    /// <summary>
    /// Validates one unit price. <paramref name="itemLabel"/> names the line in the message so a leader
    /// with twenty rows is told which one to fix.
    /// </summary>
    public static void ValidateUnitPrice(decimal unitPrice, string itemLabel)
    {
        if (unitPrice < 0)
            throw new ValidationException($"Đơn giá của '{itemLabel}' phải lớn hơn hoặc bằng 0.");

        if (unitPrice > MaxAmount)
            throw new ValidationException(
                $"Đơn giá của '{itemLabel}' vượt quá giá trị tối đa hệ thống ghi nhận được " +
                $"({MaxAmount:N2} ₫).");

        if (ScaleOf(unitPrice) > Scale)
            throw new ValidationException(
                $"Đơn giá của '{itemLabel}' chỉ được có tối đa {Scale} chữ số thập phân.");
    }

    /// <summary>
    /// Validates a computed line amount or an invoice total. Quantities come from the database, so this
    /// is not about a hostile input — it is about a legitimate price multiplied by a legitimate quantity
    /// producing a figure the invoice cannot honestly carry.
    /// </summary>
    public static decimal ValidateAmount(decimal amount, string context)
    {
        if (amount > MaxAmount)
            throw new ValidationException(
                $"{context} vượt quá giá trị tối đa hệ thống ghi nhận được ({MaxAmount:N2} ₫).");

        return amount;
    }

    /// <summary>
    /// Multiplies without ever throwing <see cref="OverflowException"/> at the caller: an overflow means
    /// the figure is far past <see cref="MaxAmount"/>, which is the same refusal by a different route.
    /// </summary>
    public static decimal Multiply(int quantity, decimal unitPrice, string context)
    {
        try
        {
            return ValidateAmount(quantity * unitPrice, context);
        }
        catch (OverflowException)
        {
            throw new ValidationException(
                $"{context} vượt quá giá trị tối đa hệ thống ghi nhận được ({MaxAmount:N2} ₫).");
        }
    }

    /// <summary>Adds without overflowing, for the same reason as <see cref="Multiply"/>.</summary>
    public static decimal Add(decimal running, decimal next, string context)
    {
        try
        {
            return ValidateAmount(running + next, context);
        }
        catch (OverflowException)
        {
            throw new ValidationException(
                $"{context} vượt quá giá trị tối đa hệ thống ghi nhận được ({MaxAmount:N2} ₫).");
        }
    }

    /// <summary>
    /// Number of digits after the decimal point, read from the value's own scale. `decimal` keeps its
    /// scale, so 1500.00m reports 2 and 1500m reports 0 — both acceptable, 1500.001m is not.
    /// </summary>
    private static int ScaleOf(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}
