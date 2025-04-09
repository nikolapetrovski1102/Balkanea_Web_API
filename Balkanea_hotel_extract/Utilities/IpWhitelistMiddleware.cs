namespace Balkanea_hotel_extract.Utility
{
    public class IpWhitelistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _allowedIp;

        public IpWhitelistMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _allowedIp = config["AllowedSettings:AllowedIp"];
        }

        public async Task Invoke(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();

            if (remoteIp != _allowedIp)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden - IP not allowed");
                return;
            }

            await _next(context);
        }
    }
}
