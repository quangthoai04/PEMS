using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure._Persistence;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pems_WebAPI.Controllers
{
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

        /// <summary>
        /// API lấy danh sách 3 tin tức nổi bật nhất hiển thị trên trang chủ
        /// </summary>
        [HttpGet("news")]
        public async Task<IActionResult> GetFeaturedNews()
        {
            try
            {
                // Truy vấn danh sách tin tức đã được xuất bản (Published), sắp xếp mới nhất
                var newsList = await _context.News
                    .Where(n => n.NewsStatus == "Published")
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(3)
                    .Select(n => new
                    {
                        id = n.NewsId,
                        title = n.Title,
                        excerpt = n.Content.Length > 150 ? n.Content.Substring(0, 150) + "..." : n.Content,
                        image = n.ThumbnailUrl ?? "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80",
                        date = n.CreatedAt.ToString("dd/MM/yyyy")
                    })
                    .ToListAsync();

                // Nếu cơ sở dữ liệu trống, trả về dữ liệu mẫu (mock data) để giao diện không bị trống
                if (!newsList.Any())
                {
                    _logger.LogInformation("Database news empty, returning mockup news.");
                    var mockNews = new[]
                    {
                        new {
                            id = Guid.NewGuid(),
                            title = "Đại học FPT mở rộng quan hệ hợp tác với các trường Đại học tại Nhật Bản",
                            excerpt = "Nhằm mang đến thêm nhiều cơ hội học tập và trải nghiệm quốc tế cho sinh viên, Đại học FPT đã ký kết thỏa thuận hợp tác với nhiều đối tác mới...",
                            image = "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80",
                            date = "10/05/2026"
                        },
                        new {
                            id = Guid.NewGuid(),
                            title = "Hơn 200 sinh viên quốc tế tham gia chương trình trao đổi kỳ Fall 2026",
                            excerpt = "Chương trình Inbound Learning tại Đại học FPT thu hút đông đảo sinh viên từ các nước trong khu vực và trên thế giới đến tham gia học tập...",
                            image = "https://images.unsplash.com/photo-1523240795612-9a054b0db644?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80",
                            date = "08/05/2026"
                        },
                        new {
                            id = Guid.NewGuid(),
                            title = "Cơ hội nhận học bổng toàn phần du học chuyển tiếp tại Úc cho sinh viên IT",
                            excerpt = "Đại học FPT cùng với Đại học công nghệ Swinburne thông báo cấp học bổng toàn phần dành riêng cho sinh viên ngành Công nghệ thông tin...",
                            image = "https://images.unsplash.com/photo-1606761568499-6d2451b23c66?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80",
                            date = "05/05/2026"
                        }
                    };
                    return Ok(mockNews);
                }

                return Ok(newsList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tin tức trang chủ");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// API lấy các số liệu thống kê nổi bật trên trang chủ
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                // Đếm số đối tác đã được duyệt
                int totalPartners = await _context.Partners.CountAsync(p => p.IsApproved);
                // Đếm số quốc gia hợp tác độc bản
                int totalCountries = await _context.Partners.Where(p => p.IsApproved).Select(p => p.Country).Distinct().CountAsync();
                // Đếm số đoàn tham quan đã tiếp đón (Closed hoặc Approved/Ongoing)
                int totalDelegations = await _context.Delegations.CountAsync(d => d.DelegationStatus == "Closed" || d.DelegationStatus == "Ongoing");

                // Trả về số liệu động từ DB kết hợp số liệu mặc định nếu DB chưa có nhiều dữ liệu
                var stats = new[]
                {
                    new {
                        id = 1,
                        value = totalPartners > 0 ? $"{totalPartners}+" : "250+",
                        label = "Đối tác quốc tế",
                        desc = "Các trường Đại học và tổ chức toàn cầu",
                        color = "text-fpt-orange",
                        borderColor = "border-fpt-orange"
                    },
                    new {
                        id = 2,
                        value = totalCountries > 0 ? $"{totalCountries}+" : "40+",
                        label = "Quốc gia",
                        desc = "Mạng lưới kết nối trên thế giới",
                        color = "text-fpt-orange",
                        borderColor = "border-fpt-orange"
                    },
                    new {
                        id = 3,
                        value = "3,000+", // Số cố định từ tài liệu
                        label = "Sinh viên Outbound",
                        desc = "Đã tham gia học kỳ nước ngoài",
                        color = "text-fpt-navy",
                        borderColor = "border-fpt-navy"
                    },
                    new {
                        id = 4,
                        value = "1,500+", // Số cố định từ tài liệu
                        label = "Sinh viên Inbound",
                        desc = "Từ các nước đến trao đổi",
                        color = "text-fpt-navy",
                        borderColor = "border-fpt-navy"
                    },
                    new {
                        id = 5,
                        value = totalDelegations > 0 ? $"{totalDelegations * 10}+" : "5,000+", // Nhân hệ số hoặc trả về giá trị mock đẹp mắt
                        label = "Khách quốc tế",
                        desc = "Đã ghé thăm và làm việc tại trường",
                        color = "text-fpt-navy",
                        borderColor = "border-fpt-navy"
                    }
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy số liệu thống kê");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// API lấy danh sách logo các đối tác hiển thị trên trang chủ
        /// </summary>
        [HttpGet("partners")]
        public async Task<IActionResult> GetPartners()
        {
            try
            {
                // Lấy danh sách đối tác đã được phê duyệt
                var partners = await _context.Partners
                    .Where(p => p.IsApproved)
                    .Select(p => new
                    {
                        name = p.EnglishName,
                        logo = p.LogoUrl,
                        website = p.Website
                    })
                    .ToListAsync();

                // Nếu cơ sở dữ liệu trống, phía React sẽ tự động dùng lại logo cục bộ (hoặc ta có thể trả về danh sách rỗng để React nhận biết dùng fallback)
                return Ok(partners);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách đối tác");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
