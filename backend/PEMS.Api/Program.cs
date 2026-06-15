using Microsoft.EntityFrameworkCore;
using PEMS.Application;
using PEMS.Infrastructure;
using PEMS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Register application and infrastructure services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Thêm các dịch vụ mặc định của Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. KẾT NỐI DATABASE: Lấy chuỗi cấu hình từ appsettings.json và kích hoạt MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

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