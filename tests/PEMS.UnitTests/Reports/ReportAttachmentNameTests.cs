using System;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;
using PEMS.Application.Reports.Common;

namespace PEMS.UnitTests.Reports;

/// <summary>
/// The attachment file name reaches a MIME header, the email history and the recipient's disk. These
/// cover what it must refuse to carry there.
/// </summary>
public class ReportAttachmentNameTests
{
    private static readonly DateTime Stamp = new(2026, 7, 27, 14, 5, 0);

    [Fact]
    public void Build_follows_the_house_convention()
        => Assert.Equal("PEMS_Department_Invoice_20260727_1405.pdf",
            ReportAttachmentName.Build("Department_Invoice", Stamp));

    [Theory]
    [InlineData("BaoCao_VanHanh_Campus")]
    [InlineData("BaoCao_PhoiHop_PhongBan")]
    [InlineData("BaoCao_HieuSuat_CaNhan")]
    public void Every_report_name_is_a_pdf_with_a_timestamp(string topic)
    {
        var name = ReportAttachmentName.Build(topic, Stamp);

        Assert.StartsWith("PEMS_", name);
        Assert.EndsWith("_20260727_1405.pdf", name);
    }

    [Theory]
    [InlineData("report\r\nBcc: attacker@evil.test.pdf")]   // header injection
    [InlineData("report\nX-Injected: 1.pdf")]
    [InlineData("report\ttab.pdf")]
    public void A_name_carrying_a_control_character_is_refused(string name)
        => AssertRejected(name);

    [Theory]
    [InlineData("../../etc/passwd.pdf")]
    [InlineData("..\\..\\windows\\system32\\evil.pdf")]
    [InlineData("/var/data/report.pdf")]
    [InlineData("C:\\Users\\admin\\report.pdf")]
    public void A_name_that_walks_the_filesystem_is_refused(string name)
        => AssertRejected(name);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_name_is_refused(string? name) => AssertRejected(name);

    [Fact]
    public void A_name_that_is_not_a_pdf_is_refused()
        => AssertRejected("PEMS_BaoCao_20260727_1405.xlsx");

    [Fact]
    public void A_very_long_name_is_refused()
        => AssertRejected(new string('a', 200) + ".pdf");

    /// <summary>
    /// Vietnamese is not the problem — MIME encodes it. Refusing it would push callers towards stripping
    /// diacritics from names a person recognises.
    /// </summary>
    [Fact]
    public void A_unicode_name_is_allowed()
        => Assert.Equal("Báo cáo hiệu suất — Nguyễn Văn A.pdf",
            ReportAttachmentName.Validate("Báo cáo hiệu suất — Nguyễn Văn A.pdf"));

    private static void AssertRejected(string? name)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => ReportAttachmentName.Validate(name));
        Assert.Equal(EmailErrorCodes.ReportAttachmentNameInvalid, ex.ErrorCode);
    }
}
