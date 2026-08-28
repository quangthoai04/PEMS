using System;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;
using SixLabors.ImageSharp;

namespace PEMS.Application.Emails.Utils;

public interface IEmailImageLayoutNormalizer
{
    Task<string> NormalizeHtmlAsync(string html, CancellationToken cancellationToken = default);
}

public class EmailImageLayoutNormalizer : IEmailImageLayoutNormalizer
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public EmailImageLayoutNormalizer(IApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<string> NormalizeHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var images = doc.DocumentNode.SelectNodes("//img");
        if (images == null || images.Count == 0)
            return html;

        bool modified = false;

        // Pre-scan (pure in-memory, no DB access): every file id an <img> might need dimensions for,
        // so the read below is one batched query instead of one per <img> tag.
        var candidateFileIds = new HashSet<ulong>();
        foreach (var scanImg in images)
        {
            bool hasWidth = int.TryParse(scanImg.GetAttributeValue("width", null), out _);
            bool hasHeight = int.TryParse(scanImg.GetAttributeValue("height", null), out _);
            if ((!hasWidth || !hasHeight)
                && ulong.TryParse(scanImg.GetAttributeValue("data-file-id", null), out ulong candidateId))
                candidateFileIds.Add(candidateId);
        }
        var filesById = candidateFileIds.Count == 0
            ? new Dictionary<ulong, UploadedFile>()
            : await _context.Files
                .Where(f => candidateFileIds.Contains(f.FileId))
                .ToDictionaryAsync(f => f.FileId, cancellationToken);

        foreach (var img in images)
        {
            int? width = null;
            int? height = null;

            var wAttr = img.GetAttributeValue("width", null);
            var hAttr = img.GetAttributeValue("height", null);

            if (int.TryParse(wAttr, out int w)) width = w;
            if (int.TryParse(hAttr, out int h)) height = h;

            if ((width == null || height == null) && ulong.TryParse(img.GetAttributeValue("data-file-id", null), out ulong fileId))
            {
                if (filesById.TryGetValue(fileId, out var file))
                {
                    try
                    {
                        await using var stream = await _fileStorageService.OpenReadAsync(file, cancellationToken);
                        if (stream != null)
                        {
                            using var imageInfo = await Image.LoadAsync(stream, cancellationToken);
                            width = imageInfo.Width;
                            height = imageInfo.Height;
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            if (width.HasValue && height.HasValue)
            {
                double aspectRatio = (double)height.Value / width.Value;
                int maxDisplayWidth = 560;
                int maxDisplayHeight = 420;

                if (aspectRatio > 1.4)
                {
                    maxDisplayWidth = 320;
                    maxDisplayHeight = 420;
                }

                int finalWidth = width.Value;
                int finalHeight = height.Value;

                if (finalWidth > maxDisplayWidth)
                {
                    finalHeight = (int)Math.Round((double)finalHeight * maxDisplayWidth / finalWidth);
                    finalWidth = maxDisplayWidth;
                }

                if (finalHeight > maxDisplayHeight)
                {
                    finalWidth = (int)Math.Round((double)finalWidth * maxDisplayHeight / finalHeight);
                    finalHeight = maxDisplayHeight;
                }

                img.SetAttributeValue("width", finalWidth.ToString());
                img.SetAttributeValue("height", finalHeight.ToString());

                string newStyle = $"display:block; margin:0 auto; width:{finalWidth}px; max-width:100%; height:auto; border:0; outline:none; text-decoration:none;";
                img.SetAttributeValue("style", newStyle);

                var parent = img.ParentNode;
                if (parent != null && parent.Name != "div" && !parent.GetAttributeValue("style", "").Contains("text-align:center"))
                {
                    var wrapper = doc.CreateElement("div");
                    wrapper.SetAttributeValue("style", "text-align:center; margin:16px 0;");
                    parent.ReplaceChild(wrapper, img);
                    wrapper.AppendChild(img);
                }
                else if (parent != null && parent.Name == "div")
                {
                    string parentStyle = parent.GetAttributeValue("style", "");
                    if (!parentStyle.Contains("text-align:center"))
                    {
                        parent.SetAttributeValue("style", string.IsNullOrWhiteSpace(parentStyle) ? "text-align:center; margin:16px 0;" : $"text-align:center; margin:16px 0; {parentStyle}");
                    }
                }

                modified = true;
            }
        }

        return modified ? doc.DocumentNode.OuterHtml : html;
    }
}
