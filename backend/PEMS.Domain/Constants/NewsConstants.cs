namespace PEMS.Domain.Constants;

public static class NewsConstants
{
    public static class Status
    {
        public const string PendingReview = "PENDING_REVIEW";
        public const string Rejected = "REJECTED";
        public const string Published = "PUBLISHED";
        public const string Hidden = "HIDDEN";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            PendingReview,
            Rejected,
            Published,
            Hidden
        };
    }

    public static class Languages
    {
        public const string Default = "vi";

        /// <summary>Languages a news post may be translated into (matches Google Translate codes).</summary>
        public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "vi", "en", "ja", "ko", "zh-CN"
        };

        public static string ToDisplayName(string code) => code switch
        {
            "vi"    => "Tiếng Việt",
            "en"    => "English",
            "ja"    => "日本語",
            "ko"    => "한국어",
            "zh-CN" => "中文",
            _        => code
        };
    }

    public static class ViewerMode
    {
        public const string Author = "AUTHOR";
        public const string Reviewer = "REVIEWER";
        public const string HoReadonly = "HO_READONLY";
    }

    public static class Limits
    {
        // Summary is mirrored verbatim into news_translations.seo_description, which is
        // VARCHAR(500) — this is the actual hard ceiling; exceeding it throws a raw MySQL
        // "Data too long for column 'seo_description'" error, so it must be validated here (not
        // 150, which was Title's limit, mistakenly reused for Summary).
        public const int SummaryMaxLength = 500;
    }

    public static string ToVietnameseStatusLabel(string status) => status switch
    {
        Status.PendingReview => "Chờ Duyệt",
        Status.Rejected => "Từ Chối",
        Status.Published => "Đã Duyệt",
        Status.Hidden => "Ẩn",
        _ => status
    };
}
