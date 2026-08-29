using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CatTracker.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CatTracker.Endpoints;

public static class AuthEndpoints
{
    private record LoginRequest(string Password);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (HttpContext ctx, LoginRequest req, LoginThrottle throttle, PasswordProvider pw) =>
        {
            var clientKey = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (throttle.IsLockedOut(clientKey, out var retryAfter))
            {
                ctx.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                return Results.Json(
                    new { error = "Too many attempts. Try again later." },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var isMatch = FixedTimeEquals(pw.Password, req.Password ?? "");
            if (!isMatch)
            {
                throttle.RecordFailure(clientKey);
                return Results.Unauthorized();
            }

            throttle.Reset(clientKey);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, "single-user") },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });

            return Results.Ok(new { authenticated = true });
        });

        group.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { authenticated = false });
        });

        group.MapGet("/status", (HttpContext ctx) =>
            Results.Ok(new { authenticated = ctx.User.Identity?.IsAuthenticated ?? false }));
    }

    /// <summary>Constant-time string comparison to avoid leaking password length/content via timing.</summary>
    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        if (expectedBytes.Length != providedBytes.Length)
        {
            // Still touch the crypto API so mismatched lengths take a
            // comparable code path instead of returning instantly.
            CryptographicOperations.FixedTimeEquals(expectedBytes, expectedBytes);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
