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

        sb.Append("<thead><tr>")
          .Append("<th colspan=\"2\" align=\"left\" ")
          .Append($"style=\"border-bottom:1px solid {Border};padding:8px 10px;font-size:13px;")
          .Append($"font-weight:bold;color:{Navy};word-break:break-word;overflow-wrap:break-word\">")
          .Append(Esc(policy.Heading(language)))
          .Append("</th></tr></thead>");

        sb.Append("<tbody>");

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

    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
