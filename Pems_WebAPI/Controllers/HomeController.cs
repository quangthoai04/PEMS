using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure._Persistence;

namespace Pems_WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Lấy 3 tin tức nổi bật hiển thị trang chủ</summary>
    [HttpGet("news")]
    public async Task<IActionResult> GetFeaturedNews()
    {
        try
        {
            var newsList = await _context.News
                .Where(n => n.Status == "Da Duyet" && n.DeletedAt == null)
                .OrderByDescending(n => n.CreatedAt)
                .Take(3)
                .Select(n => new
                {
                    id      = n.NewsId,
                    title   = n.Title,
                    excerpt = n.Summary ?? (n.Body != null && n.Body.Length > 150 ? n.Body.Substring(0, 150) + "..." : n.Body ?? ""),
                    image   = n.ImageUrl ?? "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80",
                    date    = n.CreatedAt.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            if (!newsList.Any())
            {
                _logger.LogInformation("Database news empty, returning mock data.");
                return Ok(new[]
                {
                    new { id = Guid.NewGuid().ToString(), title = "Đại học FPT mở rộng hợp tác với các trường tại Nhật Bản",    excerpt = "Đại học FPT ký kết thỏa thuận hợp tác với nhiều đối tác mới...", image = "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80", date = "10/05/2026" },
                    new { id = Guid.NewGuid().ToString(), title = "Hơn 200 sinh viên quốc tế tham gia trao đổi kỳ Fall 2026",  excerpt = "Chương trình Inbound Learning thu hút đông đảo sinh viên từ nhiều quốc gia...",                            image = "https://images.unsplash.com/photo-1523240795612-9a054b0db644?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80", date = "08/05/2026" },
                    new { id = Guid.NewGuid().ToString(), title = "Học bổng toàn phần du học chuyển tiếp tại Úc cho sinh viên IT", excerpt = "FPT và Đại học Swinburne cấp học bổng toàn phần ngành CNTT...",                                             image = "https://images.unsplash.com/photo-1606761568499-6d2451b23c66?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80", date = "05/05/2026" }
                });
            }

            return Ok(newsList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy tin tức trang chủ");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>Số liệu thống kê hiển thị trang chủ</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            int totalPartners   = await _context.Partners.CountAsync(p => p.Status == "Approved" && p.DeletedAt == null);
            int totalCountries  = await _context.Partners.Where(p => p.Status == "Approved" && p.DeletedAt == null).Select(p => p.Country).Distinct().CountAsync();
            int totalDelegations = await _context.VisitRequests.CountAsync(v => v.Status == "Da dong doan" || v.Status == "Da ket thuc");

            return Ok(new[]
            {
                new { id = 1, value = totalPartners  > 0 ? $"{totalPartners}+"        : "250+",  label = "Đối tác quốc tế",       desc = "Các trường Đại học và tổ chức toàn cầu",  color = "text-fpt-orange", borderColor = "border-fpt-orange" },
                new { id = 2, value = totalCountries > 0 ? $"{totalCountries}+"       : "40+",   label = "Quốc gia",               desc = "Mạng lưới kết nối trên thế giới",         color = "text-fpt-orange", borderColor = "border-fpt-orange" },
                new { id = 3, value = "3,000+",                                                   label = "Sinh viên Outbound",     desc = "Đã tham gia học kỳ nước ngoài",           color = "text-fpt-navy",   borderColor = "border-fpt-navy"   },
                new { id = 4, value = "1,500+",                                                   label = "Sinh viên Inbound",      desc = "Từ các nước đến trao đổi",                color = "text-fpt-navy",   borderColor = "border-fpt-navy"   },
                new { id = 5, value = totalDelegations > 0 ? $"{totalDelegations*10}+": "5,000+", label = "Khách quốc tế đã tiếp", desc = "Đã ghé thăm và làm việc tại trường",     color = "text-fpt-navy",   borderColor = "border-fpt-navy"   }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy số liệu thống kê");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>Danh sách logo đối tác trang chủ</summary>
    [HttpGet("partners")]
    public async Task<IActionResult> GetPartners()
    {
        try
        {
            var partners = await _context.Partners
                .Where(p => p.Status == "Approved" && p.DeletedAt == null)
                .Select(p => new { name = p.Name, logo = p.LogoUrl, website = p.Website })
                .ToListAsync();

            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách đối tác");
            return StatusCode(500, "Internal server error");
        }
    }
}
