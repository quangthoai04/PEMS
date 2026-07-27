using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Reports.Common;

/// <summary>
/// One report or invoice message: the template that supplies the words, the person it goes to, and the
/// PDF the template promises is attached.
/// </summary>
/// <param name="TemplateCode">One of the four REPORT templates.</param>
/// <param name="To">The single addressee. Personalised reports are sent one message per person.</param>
/// <param name="Variables">Exactly the variables the template declares.</param>
/// <param name="FileName">Delivery name of the PDF, built by <see cref="ReportAttachmentName"/>.</param>
/// <param name="Pdf">The generated document. Never empty — these templates all promise an attachment.</param>
/// <param name="SentBy">The user who pressed "gửi".</param>
/// <param name="RelatedType">What the report is about, for the email history (campus / department / user).</param>
/// <param name="RelatedId">Id of that object.</param>
public sealed record ReportEmailMessage(
    string TemplateCode,
    EmailRecipient To,
    IReadOnlyDictionary<string, string> Variables,
    string FileName,
    byte[] Pdf,
    ulong? SentBy,
    string RelatedType,
    ulong RelatedId)
{
    public string Language { get; init; } = EmailLanguages.Vi;
}

/// <summary>What a report message is about — written to <c>sent_emails.related_type</c>.</summary>
public static class ReportEmailRelatedTypes
{
    public const string Campus = "CAMPUS";
    public const string Department = "DEPARTMENT";
    public const string User = "USER";
}

/// <summary>
/// Sends a report/invoice email with its PDF: stores the document, records the message and its attachment
/// linkage, delivers it, and fails the command when delivery did not happen.
///
/// <para>
/// It exists because the six report callers each need the same five steps in the same order, and getting
/// any one of them wrong is invisible from the outside: an attachment that never reached
/// <c>sent_email_attachments</c> still arrives in the recipient's inbox, and a Skipped delivery still
/// looks like success to a caller that only checks for exceptions. Doing it once here is what makes
/// "the history matches what was sent" and "Mandatory means Mandatory" true for all six rather than for
/// the ones somebody remembered.
/// </para>
/// </summary>
public interface IReportEmailSender
{
    /// <summary>
    /// Sends one report message. Throws when the template is broken, the PDF is unusable, storage fails,
    /// or the provider did not accept the message — these commands are Mandatory, so there is no outcome
    /// in which this returns normally and nothing was sent.
    /// </summary>
    Task<ulong> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken = default);
}
