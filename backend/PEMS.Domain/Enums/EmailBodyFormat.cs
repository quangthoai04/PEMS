namespace PEMS.Domain.Enums;

/// <summary>
/// Format of an email body (template, sent snapshot, or draft). Names map 1:1 to the SQL
/// ENUM('PLAIN_TEXT','HTML') strings; HTML is used for the rich-text editor content.
/// </summary>
public enum EmailBodyFormat
{
    PLAIN_TEXT,
    HTML,
}
