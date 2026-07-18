using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Api.Middleware;
using PEMS.Application.Common.Exceptions;
using Xunit;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// H-3 observability regression: the exception middleware must make the v2 stable failure codes observable
/// in logs (ConflictException / BusinessRuleException) by CODE ONLY — never the message or any PII — while
/// keeping the HTTP status + errorCode payload unchanged. No DB (pure middleware).
/// </summary>
public sealed class ExceptionHandlingObservabilityTests
{
    private sealed class CapturingLogger : ILogger<ExceptionHandlingMiddleware>
    {
        public List<string> Lines { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
        private sealed class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }
    }

    private sealed class ProdEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "PEMS";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static async Task<(int status, string body, CapturingLogger log)> Run(Exception toThrow)
    {
        var logger = new CapturingLogger();
        var mw = new ExceptionHandlingMiddleware(_ => throw toThrow, logger, new ProdEnv());
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v2/visit-requests/verify";
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body, Encoding.UTF8).ReadToEndAsync();
        return (ctx.Response.StatusCode, body, logger);
    }

    [Fact]
    public async Task Conflict_is_409_with_code_and_is_logged_by_code_only()
    {
        var secretishMessage = "Không tìm thấy phiên đăng ký cho user@example.com"; // pretend the message had PII
        var (status, body, log) = await Run(
            new ConflictException(secretishMessage, "PER_CAMPUS_V2_PENDING_NOT_FOUND"));

        Assert.Equal(409, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("PER_CAMPUS_V2_PENDING_NOT_FOUND", doc.RootElement.GetProperty("errorCode").GetString());

        // Exactly one observability line, containing the stable code + path, but NOT the (PII-bearing) message.
        var line = Assert.Single(log.Lines);
        Assert.Contains("PER_CAMPUS_V2_PENDING_NOT_FOUND", line);
        Assert.Contains("/api/v2/visit-requests/verify", line);
        Assert.DoesNotContain("user@example.com", line);
        Assert.DoesNotContain(secretishMessage, line);
    }

    [Fact]
    public async Task BusinessRule_is_422_with_code_and_is_logged_by_code_only()
    {
        var (status, body, log) = await Run(
            new BusinessRuleException("Nội dung biểu mẫu đã thay đổi", "PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH"));

        Assert.Equal(422, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH", doc.RootElement.GetProperty("errorCode").GetString());

        var line = Assert.Single(log.Lines);
        Assert.Contains("PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH", line);
        Assert.DoesNotContain("Nội dung biểu mẫu đã thay đổi", line);
    }
}
