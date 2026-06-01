using Microsoft.EntityFrameworkCore;
using Infrastructure._Persistence; // Gọi DbContext từ Infrastructure
using Application.Campuses;       // Gọi Service từ Application

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<Application.Authentication.IAuthService, Application.Authentication.AuthService>();

// Thêm các dịch vụ mặc định của Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. KẾT NỐI DATABASE: Lấy chuỗi cấu hình từ appsettings.json và kích hoạt MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 2. ĐĂNG KÝ DỊCH VỤ: Ép hệ thống nhận diện lớp CampusService của tầng Application
builder.Services.AddScoped<ICampusService, CampusService>();

// 3. CẤU HÌNH CORS: Cho phép ứng dụng React Frontend truy cập API từ local
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Sử dụng CORS Middleware trước Authorization
app.UseCors("AllowAll");

// Cấu hình đường ống HTTP Request Pipeline (Middleware)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();