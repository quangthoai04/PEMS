using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Renders a resolved contact as the trusted HTML block a body substitutes for
/// <c>{{contactInformationBlock}}</c>.
///
/// <para>
/// Trusted means the markup is injected verbatim, so the structure here is the ONLY markup in the block
/// and every value that came from the database goes through <see cref="Esc"/> on the way in. An operator
/// cannot contribute markup: the policy lets them choose which FIELDS appear and what the heading says,
/// never what the HTML is. That is the difference between configuring a block and authoring one, and it
/// is what stops a template screen from becoming an HTML injection surface.
/// </para>
/// <para>
/// Layout follows the same rules as the setup tables — a single-column table with inline styles, because
/// mail clients strip &lt;style&gt; and most do not lay out flex or grid.
/// </para>
/// </summary>
public static class EmailContactHtmlRenderer
{
    private const string Border = "#d1d5db";
    private const string Bg = "#f9fafb";
    private const string Muted = "#6b7280";
    private const string Navy = "#004c91";

    /// <summary>
    /// Builds the block. Returns an empty string when there is nothing worth showing — an OPTIONAL policy
    /// with no resolvable contact renders nothing rather than an empty bordered box.
    /// </summary>
    /// <param name="contact">The resolved contact, or null when none could be found.</param>
    /// <param name="policy">Which fields may be shown, and under what heading.</param>
    /// <param name="language">VI or EN.</param>
    /// <param name="senderName">Sender display name, used only when the policy shows a sender line.</param>
    public static string Render(
        EmailContactInformation? contact,
        EmailContactPolicyResolution policy,
        string language,
        string? senderName = null)
    {
        if (!policy.RendersBlock) return string.Empty;
        if (contact is null) return string.Empty;

        var en = EmailLanguages.Normalize(language) == EmailLanguages.En;

        var rows = new List<(string Label, string Value)>();

        if (!string.IsNullOrWhiteSpace(contact.RoleLabel))
            rows.Add((en ? "Role" : "Vai trò", contact.RoleLabel!));

        if (policy.ShowDepartment && !string.IsNullOrWhiteSpace(contact.DepartmentName))
            rows.Add((en ? "Department" : "Đơn vị", contact.DepartmentName!));

        if (policy.ShowCampus && !string.IsNullOrWhiteSpace(contact.CampusName))
            rows.Add((en ? "Campus" : "Cơ sở", contact.CampusName!));

        if (policy.ShowEmail && !string.IsNullOrWhiteSpace(contact.Email))
            rows.Add((en ? "Email" : "Email", contact.Email!));

        if (policy.ShowPhone && !string.IsNullOrWhiteSpace(contact.Phone))
            rows.Add((en ? "Phone" : "Điện thoại", contact.Phone!));

        // A heading and a name with no way to reach the person is the defect this block exists to remove,
        // so it is not rendered at all. An OPTIONAL policy then sends without it; a REQUIRED one has
        // already been refused upstream by the resolver.
        var hasChannel =
            (policy.ShowEmail && !string.IsNullOrWhiteSpace(contact.Email))
            || (policy.ShowPhone && !string.IsNullOrWhiteSpace(contact.Phone));

        if (!hasChannel) return string.Empty;

        var sb = new StringBuilder();

        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" ")
          .Append($"style=\"border-collapse:collapse;width:100%;table-layout:fixed;margin:18px 0;")
          .Append($"border:1px solid {Border};background:{Bg}\">");
        sb.Append("<colgroup><col style=\"width:34%\" width=\"34%\"/><col style=\"width:66%\" width=\"66%\"/></colgroup>");

        sb.Append("<tbody>");

        // The heading is a styled <td>, not <thead>/<th>. This block is injected into bodies that a Host
        // can reopen in the rich-text editor, whose document model has no table header: it drops the tag
        // and keeps the text, which lifts the heading out of the table and leaves it running into the
        // first row. Styled cells are also the more reliable choice across mail clients — Outlook's Word
        // engine ignores much of what is set on <th>. The table is role="presentation" either way, so no
        // semantics are lost. Same reasoning as VisitSetupEmailHtml.Head.
        sb.Append("<tr>")
          .Append("<td colspan=\"2\" align=\"left\" ")
          .Append($"style=\"border-bottom:1px solid {Border};padding:8px 10px;font-size:13px;")
          .Append($"font-weight:bold;color:{Navy};word-break:break-word;overflow-wrap:break-word\">")
          .Append(Esc(policy.Heading(language)))
          .Append("</td></tr>");

        // The name is the block's subject, so it leads and is not conditional on the policy.
        Row(sb, en ? "Contact" : "Người phụ trách", contact.DisplayName, strong: true);
        foreach (var (label, value) in rows) Row(sb, label, value);

        sb.Append("</tbody></table>");

        if (policy.ShowSender && !string.IsNullOrWhiteSpace(senderName))
        {
            sb.Append($"<p style=\"color:{Muted};font-size:12px;margin:6px 0 0\">")
              .Append(en ? "Sent by: " : "Được gửi bởi: ")
              .Append(Esc(senderName!))
              .Append("</p>");
        }

        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string label, string value, bool strong = false)
    {
        sb.Append("<tr>")
          .Append($"<td width=\"34%\" style=\"border-top:1px solid {Border};padding:6px 10px;font-size:13px;")
          .Append($"color:{Muted};vertical-align:top;word-break:break-word;overflow-wrap:break-word;width:34%\">")
          .Append(Esc(label))
          .Append("</td>")
          .Append($"<td width=\"66%\" style=\"border-top:1px solid {Border};padding:6px 10px;font-size:13px;")
          .Append($"vertical-align:top;word-break:break-word;overflow-wrap:break-word;width:66%")
          .Append(strong ? ";font-weight:bold" : "")
          .Append("\">")
          .Append(Esc(value))
          .Append("</td>")
          .Append("</tr>");
    }

    /// <summary>
    /// Stand-in for a preview, where there is no visit and therefore no Host to resolve.
    ///
    /// <para>
    /// It says where the block goes and where its content comes from, and stops there. Inventing a
    /// plausible name and address would show an operator a person who does not exist and invite them to
    /// edit contact details the template does not control.
    /// </para>
    /// </summary>
    public static string DisabledBlock(string language)
    {
        var en = EmailLanguages.Normalize(language) == EmailLanguages.En;
        var text = en
            ? "Contact block — the system fills in the reply contact (name, role, work email and phone) "
              + "from the visit, campus or department when the email is sent."
            : "Khối thông tin liên hệ — hệ thống điền đầu mối (họ tên, vai trò, email và điện thoại công việc) "
              + "từ chuyến thăm, cơ sở hoặc phòng ban khi gửi email thật.";

        return $@"<div style=""margin:18px 0"">
            <span style=""display:block;border:1px dashed #9aa6b2;color:{Muted};font-size:13px;padding:14px 18px;border-radius:10px"">{Esc(text)}</span>
        </div>";
    }

    /// <summary>
    /// The block as a given policy would render it, filled with obviously-fake data, for the template
    /// screen's live preview.
    ///
    /// <para>
    /// This goes through <see cref="Render"/> rather than building its own markup, which is the whole
    /// point: an operator toggling "Số điện thoại" has to see the row disappear from the SAME renderer
    /// the send uses, not from a mock-up that agrees with it today. It also means the policy rules come
    /// along for free — NONE renders nothing, and a policy with both channels hidden renders nothing
    /// rather than a heading over an unreachable name.
    /// </para>
    /// <para>
    /// The values are deliberately not plausible. Addresses use the reserved <c>.invalid</c> TLD
    /// (RFC 2606), which can never resolve, and every field is marked as sample data in the operator's
    /// language. Showing a realistic-looking person here would put an invented name and mailbox in front
    /// of an operator and invite them to "correct" details this screen does not control — and a preview
    /// that reads as real is one screenshot away from being taken for a real recipient's data.
    /// </para>
    /// </summary>
    public static string SampleBlock(EmailContactPolicyResolution policy, string language)
    {
        var en = EmailLanguages.Normalize(language) == EmailLanguages.En;

        var sample = new EmailContactInformation(
            Source: policy.ContactSource,
            DisplayName: en ? "Sample Contact (preview)" : "Nguyễn Văn A (dữ liệu mẫu)",
            RoleLabel: en ? "Host" : "Người phụ trách tiếp đón",
            DepartmentName: en ? "Administration Office" : "Phòng Hành chính",
            CampusName: en ? "FPTU Hanoi" : "FPTU Hà Nội",
            Email: "lien.he.mau@example.invalid",
            Phone: "0900 000 000");

        return Render(sample, policy, language,
            senderName: en ? "Sample sender (preview)" : "Người gửi mẫu");
    }

    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
