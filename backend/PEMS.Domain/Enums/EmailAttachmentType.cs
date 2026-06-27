namespace PEMS.Domain.Enums;

/// <summary>
/// Kind of file attached to an email / draft. Names map 1:1 to the SQL
/// ENUM('ATTACHMENT','INLINE_IMAGE') strings. INLINE_IMAGE is referenced from the HTML body
/// via a Content-ID (&lt;img src="cid:..."&gt;); ATTACHMENT is a plain downloadable file.
/// </summary>
public enum EmailAttachmentType
{
    ATTACHMENT,
    INLINE_IMAGE,
}
