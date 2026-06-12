namespace LogMind.API.Middleware;

public class ApiKeyMiddleware(RequestDelegate next)
{
    private const string Header = "X-Api-Key";

    public async Task InvokeAsync(HttpContext ctx, IConfiguration cfg)
    {
        var path = ctx.Request.Path.Value ?? "";

        // Swagger + security/status are always open
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/security/status", StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx);
            return;
        }

        var configured = cfg["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            await next(ctx);   // no key configured = open
            return;
        }

        if (!ctx.Request.Headers.TryGetValue(Header, out var incoming) || incoming != configured)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"Invalid or missing API key. Set X-Api-Key header.\"}");
            return;
        }

        await next(ctx);
    }
}
