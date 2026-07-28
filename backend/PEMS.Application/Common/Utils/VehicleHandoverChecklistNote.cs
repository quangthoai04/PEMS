using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PEMS.Application.Common.Utils;

/// <summary>
/// Biên bản bàn giao (mọi loại hạng mục — xe điện lẫn tài sản thường) lưu cả checklist (mảng
/// {name,qty,giao,nhan}) lẫn ghi chú tự do (Bên giao/Bên nhận, gộp kiểu "+ nhãn: nội dung") trên
/// CÙNG 1 cột condition_note của dòng BORROW — tránh thêm cột DB mới. Envelope JSON
/// {"rows": [...], "note": "..."} tách 2 loại dữ liệu để không ghi đè lẫn nhau. Tương thích ngược với
/// dữ liệu ký trước khi có envelope: mảng JSON trần (xe điện) hoặc chuỗi note thường (item khác).
/// </summary>
public static class VehicleHandoverChecklistNote
{
    /// <summary>Ghi đè phần checklist (rows), giữ nguyên note đã có (nếu có).</summary>
    public static string MergeChecklist(string? existingConditionNote, string checklistRowsJson)
    {
        var note = ExtractNote(existingConditionNote);
        return Serialize(checklistRowsJson, note);
    }

    /// <summary>Gộp thêm 1 dòng ghi chú (nhãn: nội dung) vào phần note, giữ nguyên checklist đã có.</summary>
    public static string MergeNote(string? existingConditionNote, string label, string note)
    {
        var rowsJson = ExtractRowsJson(existingConditionNote) ?? "[]";
        var existingNote = ExtractNote(existingConditionNote);
        var line = $"+ {label}: {note}";
        var mergedNote = string.IsNullOrWhiteSpace(existingNote) ? line : $"{existingNote}\n{line}";
        return Serialize(rowsJson, mergedNote);
    }

    /// <summary>Trích phần checklist (mảng JSON trần) để trả cho frontend qua ChecklistJson.</summary>
    public static string? ExtractRowsJson(string? conditionNote)
    {
        if (string.IsNullOrWhiteSpace(conditionNote)) return null;
        var trimmed = conditionNote.TrimStart();
        if (trimmed.StartsWith('['))
            return conditionNote; // dữ liệu cũ trước khi có envelope: mảng trần
        if (!trimmed.StartsWith('{'))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(conditionNote);
            return doc.RootElement.TryGetProperty("rows", out var rows) ? rows.GetRawText() : null;
        }
        catch (JsonException)
        {
            return null; // dữ liệu hỏng — rơi về mặc định ở frontend
        }
    }

    /// <summary>Trích phần ghi chú tự do (đã gộp Bên giao/Bên nhận) để trả cho frontend qua BorrowNote.
    /// Tương thích ngược với dữ liệu ký TRƯỚC khi có envelope: mảng checklist trần (xe điện) → chưa có
    /// note; chuỗi note thường (item không phải xe điện) → coi cả chuỗi là note.</summary>
    public static string? ExtractNote(string? conditionNote)
    {
        if (string.IsNullOrWhiteSpace(conditionNote)) return null;
        var trimmed = conditionNote.TrimStart();
        if (trimmed.StartsWith('['))
            return null; // dữ liệu cũ: mảng checklist trần — chưa có note
        if (!trimmed.StartsWith('{'))
            return conditionNote; // dữ liệu cũ: note tự do thường, lưu trước khi có envelope
        try
        {
            using var doc = JsonDocument.Parse(conditionNote);
            return doc.RootElement.TryGetProperty("note", out var note) && note.ValueKind == JsonValueKind.String
                ? note.GetString()
                : null;
        }
        catch (JsonException)
        {
            return conditionNote; // JSON hỏng — coi như note thường thay vì mất trắng
        }
    }

    private static string Serialize(string rowsJson, string? note)
    {
        string safeRowsJson;
        try
        {
            using var probe = JsonDocument.Parse(string.IsNullOrWhiteSpace(rowsJson) ? "[]" : rowsJson);
            safeRowsJson = probe.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            safeRowsJson = "[]";
        }

        using var doc = JsonDocument.Parse(safeRowsJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("rows");
            doc.RootElement.WriteTo(writer);
            if (note != null) writer.WriteString("note", note); else writer.WriteNull("note");
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
