using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Application.Authentication;

namespace Pems_WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // ĐƯỜNG DẪN API: POST https://localhost:xxxx/api/Auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);

                if (result == null)
                {
                    return Unauthorized(new { message = "Email hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa!" });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Trả về mã lỗi 400 kèm theo nội dung lỗi "Không trùng khớp cơ sở" bốc từ Service ra
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}