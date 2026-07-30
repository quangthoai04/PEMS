using PEMS.Application.Common.Exceptions;
using PEMS.Application.Reports.Common;

namespace PEMS.UnitTests.Reports;

/// <summary>
/// The bound on a unit price a Department Leader types into an invoice.
///
/// <para>
/// Declaring the price is their job, so the value comes from the client by design — the quantity never
/// does. What the handlers did not have was any ceiling: they refused a negative number and accepted
/// everything else, while the column that records money in this system is <c>DECIMAL(18,2)</c> with
/// <c>CHECK (unit_price &gt;= 0)</c>. A price near <see cref="decimal.MaxValue"/> made
/// <c>quantity * unitPrice</c> throw <see cref="System.OverflowException"/> — an unhandled 500 — and a
/// price with four decimals printed fractions of a đồng onto a document that gets emailed and signed.
/// </para>
/// </summary>
public class InvoiceMoneyTests
{
    private const string Item = "Thuê phòng họp";

    // ── The lower boundary ───────────────────────────────────────────────────

    [Fact]
    public void Zero_is_allowed_because_a_line_can_legitimately_cost_nothing()
        => InvoiceMoney.ValidateUnitPrice(0m, Item);

    [Fact]
    public void A_negative_price_is_refused()
    {
        var ex = Assert.Throws<ValidationException>(() => InvoiceMoney.ValidateUnitPrice(-0.01m, Item));
        Assert.Contains(Item, ex.Message);
    }

    [Fact]
    public void The_smallest_negative_step_below_zero_is_still_refused()
        => Assert.Throws<ValidationException>(() => InvoiceMoney.ValidateUnitPrice(-0.001m, Item));

    // ── The upper boundary, which is the database's ──────────────────────────

    [Fact]
    public void The_largest_value_the_column_holds_is_accepted()
        => InvoiceMoney.ValidateUnitPrice(InvoiceMoney.MaxAmount, Item);

    [Fact]
    public void One_hundredth_above_the_column_maximum_is_refused()
        => Assert.Throws<ValidationException>(
            () => InvoiceMoney.ValidateUnitPrice(InvoiceMoney.MaxAmount + 0.01m, Item));

    [Fact]
    public void A_price_near_the_decimal_maximum_is_refused_rather_than_overflowing()
    {
        var ex = Assert.Throws<ValidationException>(
            () => InvoiceMoney.ValidateUnitPrice(decimal.MaxValue, Item));
        Assert.Contains(Item, ex.Message);
    }

    // ── Scale ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1500")]
    [InlineData("1500.0")]
    [InlineData("1500.00")]
    [InlineData("1500.05")]
    public void Two_decimals_or_fewer_are_accepted(string value)
        => InvoiceMoney.ValidateUnitPrice(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), Item);

    [Theory]
    [InlineData("1500.001")]
    [InlineData("0.005")]
    [InlineData("1500.123456")]
    public void More_than_two_decimals_is_refused(string value)
    {
        var ex = Assert.Throws<ValidationException>(() => InvoiceMoney.ValidateUnitPrice(
            decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), Item));
        Assert.Contains("thập phân", ex.Message);
    }

    // ── Derived figures ──────────────────────────────────────────────────────

    [Fact]
    public void A_line_amount_is_the_quantity_times_the_price()
        => Assert.Equal(4_500_000m, InvoiceMoney.Multiply(3, 1_500_000m, "Thành tiền"));

    [Fact]
    public void A_line_amount_past_the_maximum_is_refused_not_overflowed()
    {
        // Each price is individually acceptable; the quantity is what takes the product over. This is the
        // 500 the handlers used to produce, now a 400 with a message naming the line.
        var ex = Assert.Throws<ValidationException>(
            () => InvoiceMoney.Multiply(1000, InvoiceMoney.MaxAmount, "Thành tiền của 'Thuê phòng họp'"));
        Assert.Contains("Thuê phòng họp", ex.Message);
    }

    [Fact]
    public void A_running_total_past_the_maximum_is_refused_not_overflowed()
    {
        var ex = Assert.Throws<ValidationException>(
            () => InvoiceMoney.Add(InvoiceMoney.MaxAmount, InvoiceMoney.MaxAmount, "Tổng tiền hóa đơn"));
        Assert.Contains("Tổng tiền hóa đơn", ex.Message);
    }

    [Fact]
    public void A_total_that_lands_exactly_on_the_maximum_is_kept()
        => Assert.Equal(InvoiceMoney.MaxAmount,
            InvoiceMoney.Add(InvoiceMoney.MaxAmount - 1m, 1m, "Tổng tiền hóa đơn"));

    [Fact]
    public void The_bound_is_the_one_the_database_declares()
        // DECIMAL(18,2): sixteen integer digits and two decimals. Written out so a schema change that
        // widens or narrows the column is caught here rather than at an INSERT.
        => Assert.Equal(9_999_999_999_999_999.99m, InvoiceMoney.MaxAmount);
}
