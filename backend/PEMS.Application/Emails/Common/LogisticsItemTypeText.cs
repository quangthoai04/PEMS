using System;
using System.Collections.Generic;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Turns a <c>visit_logistics_items.item_type</c> code into the wording a recipient reads.
///
/// <para>
/// The send point used to pass the raw code, so a department leader was emailed "Loại: MEAL" while the
/// screen the request was created on said "Suất ăn / Teabreak" — and the email preview, which builds its
/// own context in the browser, said the second one. Two spellings of one field, and the one that reached
/// a human was the machine's. The labels here are the ones the logistics screen already shows, so the
/// preview, the sent message and the form now agree by construction rather than by coincidence.
/// </para>
///
/// <para>
/// <b>TRANSPORT covers both "Xe điện" and "Người lái"</b> — they are one item_type told apart by title,
/// which the message prints directly above as "Hạng mục". So the label stays deliberately broad: naming
/// it "Xe điện" would mislabel every driver request, and no code-to-label map can recover a distinction
/// the code does not carry.
/// </para>
/// </summary>
public static class LogisticsItemTypeText
{
    private static readonly IReadOnlyDictionary<string, string> LabelsVi =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ROOM"] = "Phòng / Hội trường",
            ["TRANSPORT"] = "Xe / Di chuyển",
            ["MEAL"] = "Suất ăn / Teabreak",
            ["EQUIPMENT"] = "Thiết bị",
            ["BANNER"] = "Banner / Standee",
            ["LED"] = "Màn hình LED",
            ["OTHER"] = "Yêu cầu khác",
        };

    private static readonly IReadOnlyDictionary<string, string> LabelsEn =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ROOM"] = "Room / Hall",
            ["TRANSPORT"] = "Vehicle / Transport",
            ["MEAL"] = "Catering / Tea break",
            ["EQUIPMENT"] = "Equipment",
            ["BANNER"] = "Banner / Standee",
            ["LED"] = "LED screen",
            ["OTHER"] = "Other request",
        };

    /// <summary>
    /// The label for a code, or the code itself when it is not one this map knows.
    ///
    /// <para>
    /// Falling back to the code rather than to "Khác" is deliberate: a code added to the column without
    /// being added here should look obviously unfinished to whoever reads the mail, not quietly land in a
    /// bucket that reads as a real answer.
    /// </para>
    /// </summary>
    public static string Label(string? itemType, string? language = null)
    {
        var code = itemType?.Trim();
        if (string.IsNullOrEmpty(code)) return string.Empty;

        var labels = EmailLanguages.Normalize(language) == EmailLanguages.En ? LabelsEn : LabelsVi;
        return labels.TryGetValue(code, out var label) ? label : code;
    }
}
