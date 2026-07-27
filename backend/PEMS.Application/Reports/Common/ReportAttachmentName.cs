using System;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Reports.Common;

/// <summary>
/// Builds and checks the file name a report PDF is delivered under.
///
/// <para>
/// A file name is not decoration: it goes into a MIME <c>Content-Disposition</c> header, into
/// <c>sent_email_attachments.display_name</c>, and into whatever the recipient's client writes to disk.
/// A newline in it would let the value inject headers of its own, and a path separator would let it
/// escape the folder a client saves into. Both are refused rather than quietly rewritten — a report
/// delivered under a name nobody chose is a worse outcome than a send that stops and says why.
/// </para>
/// </summary>
public static class ReportAttachmentName
{
    private const int MaxLength = 180;

    /// <summary>
    /// The house convention the download exports already use: <c>PEMS_{Topic}_{yyyyMMdd_HHmm}.pdf</c>.
    /// <paramref name="topic"/> is a constant chosen by the caller, never a value typed by a user.
    /// </summary>
    public static string Build(string topic, DateTime stampVn)
        => Validate($"PEMS_{topic}_{stampVn:yyyyMMdd_HHmm}.pdf");

    /// <summary>
    /// Returns the name unchanged when it is safe to put in a header, and throws otherwise. Unicode is
    /// allowed — Vietnamese names encode fine in MIME — so this rejects structure, not alphabet.
    /// </summary>
    public static string Validate(string? fileName)
    {
        var name = fileName?.Trim();

        if (string.IsNullOrEmpty(name))
            throw Reject("Tên tệp đính kèm báo cáo trống.");

        if (name.Length > MaxLength)
            throw Reject("Tên tệp đính kèm báo cáo quá dài.");

        // CR/LF and any other control character: header injection.
        if (name.Any(char.IsControl))
            throw Reject("Tên tệp đính kèm báo cáo chứa ký tự xuống dòng hoặc điều khiển.");

        // Directory structure of any flavour, plus the Windows drive form ("C:\…").
        if (name.Contains('/') || name.Contains('\\') || name.Contains(':'))
            throw Reject("Tên tệp đính kèm báo cáo chứa đường dẫn.");

        if (name.Contains("..", StringComparison.Ordinal))
            throw Reject("Tên tệp đính kèm báo cáo chứa đường dẫn cấp trên.");

        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw Reject("Tệp đính kèm báo cáo phải là PDF.");

        return name;
    }

    private static BusinessRuleException Reject(string message)
        => new(message, EmailErrorCodes.ReportAttachmentNameInvalid);
}
