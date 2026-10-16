
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using K_Sweet_Kreations_Co_Backend_API.Services;

namespace K_Sweet_Kreations_Co_Backend_API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unexpected error occurred while processing {Path}",
                    context.Request.Path);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                var status = ex is WebsiteException website ? website.Status : ex is DbUpdateException ? 409 : 500;
                var message = ex is WebsiteException ? ex.Message : status == 409
                    ? "The record changed or already exists. Refresh and try again."
                    : "An unexpected error occurred. Please try again later.";
                context.Response.StatusCode = status;

                var problem = new ProblemDetails
                {
                    Status = status,
                    Title = status == 500 ? "Internal Server Error" : "Request could not be completed",
                    Detail = message,
                    Instance = context.Request.Path
                };

                context.Response.ContentType =
                    "application/problem+json";

                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}
