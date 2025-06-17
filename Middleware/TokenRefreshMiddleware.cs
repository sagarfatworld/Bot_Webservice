using Botatwork_in_Livechat.Services;

namespace Botatwork_in_Livechat.Middleware
{
    public class TokenRefreshMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenRefreshMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                await tokenService.RefreshTokenIfNeeded();
            }
            await _next(context);
        }
    }

    // Extension method to make middleware registration cleaner
    public static class TokenRefreshMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenRefresh(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenRefreshMiddleware>();
        }
    }
}