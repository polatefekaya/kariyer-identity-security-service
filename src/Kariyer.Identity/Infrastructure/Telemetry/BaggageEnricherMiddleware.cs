using System.Diagnostics;
using Serilog.Context;

namespace Kariyer.Identity.Infrastructure.Telemetry;

/// <summary>
/// Reads W3C baggage items injected by the frontend (user.id, user.type, session.id)
/// and stamps them onto the active span and Serilog log context for the request lifetime.
/// </summary>
public sealed class BaggageEnricherMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        Activity? activity = Activity.Current;

        if (activity is null)
        {
            await next(context);
            return;
        }

        string? userId = activity.GetBaggageItem("user.id");
        string? userType = activity.GetBaggageItem("user.type");
        string? sessionId = activity.GetBaggageItem("session.id");

        if (userId is not null) activity.SetTag("enduser.id", userId);
        if (userType is not null) activity.SetTag("enduser.type", userType);
        if (sessionId is not null) activity.SetTag("session.id", sessionId);

        // Push to Serilog context so every log line in this request carries the frontend identifiers.
        using IDisposable? uidProp = userId is not null ? LogContext.PushProperty("FrontendUserId", userId) : null;
        using IDisposable? typeProp = userType is not null ? LogContext.PushProperty("FrontendUserType", userType) : null;
        using IDisposable? sidProp = sessionId is not null ? LogContext.PushProperty("FrontendSessionId", sessionId) : null;

        await next(context);
    }
}
