using Microsoft.AspNetCore.Mvc;
using Application.CampusManagement.Services; // Gọi sang tầng Application để nhờ xử lý nghiệp vụ

namespace PEMS.Api.Controllers.Campuses
{
    [Route("api/[controller]")]
    [ApiController]
    public class CampusesController : ControllerBase
    {
        private readonly ICampusService _campusService;

        // Tiêm (Inject) Interface của tầng Application vào Controller
        public CampusesController(ICampusService campusService)
        {
            _campusService = campusService;
        }

        // ĐỊNH NGHĨA API: https://localhost:xxxx/api/Campuses
        [HttpGet]
        public async Task<IActionResult> GetCampuses()
        {
            try
            {
                // Controller chỉ ra lệnh cho Application chạy, không trực tiếp sờ vào Database
                var data = await _campusService.GetAllCampusesAsync();

                // Trả về dữ liệu dạng JSON cho giao diện web
                return Ok(data);
            }
            catch (Exception ex)
            {
                // Trả về lỗi nếu kết nối MySQL hoặc logic có vấn đề
                return BadRequest(new { message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }
    }
}