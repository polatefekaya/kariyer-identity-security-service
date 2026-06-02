using System.Diagnostics;
using Serilog.Context;

namespace Kariyer.Identity.Infrastructure.Telemetry;

/// <summary>
/// Reads W3C baggage items injected by the frontend (user.id, user.type, session.id)
/// and stamps them onto the active span and Serilog log context for the request lifetime.
///
/// Reads from both Activity.Baggage (populated by the OTel propagator) AND directly
/// from the raw "baggage" HTTP header so baggage is captured even when the propagator
/// runs after this middleware.
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

        // Primary: read from Activity.Baggage (populated by OTel BaggagePropagator)
        string? userId = activity.GetBaggageItem("user.id");
        string? userType = activity.GetBaggageItem("user.type");
        string? sessionId = activity.GetBaggageItem("session.id");

        // Fallback: parse the raw "baggage" header if the propagator hasn't written into
        // Activity.Baggage yet (can happen depending on instrumentation ordering)
        if (userId is null && userType is null && sessionId is null)
        {
            string? baggageHeader = context.Request.Headers["baggage"].FirstOrDefault();
            if (!string.IsNullOrEmpty(baggageHeader))
            {
                Dictionary<string, string> items = baggageHeader
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => entry.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0].Trim(), parts => Uri.UnescapeDataString(parts[1].Trim()));

                items.TryGetValue("user.id", out userId);
                items.TryGetValue("user.type", out userType);
                items.TryGetValue("session.id", out sessionId);
            }
        }

        if (userId is not null) activity.SetTag("enduser.id", userId);
        if (userType is not null) activity.SetTag("enduser.type", userType);
        if (sessionId is not null) activity.SetTag("session.id", sessionId);

        // Push to Serilog context so every log line in this request carries the
        // frontend identifiers — correlates logs with the trace in SigNoz.
        using IDisposable? uidProp = userId is not null ? LogContext.PushProperty("FrontendUserId", userId) : null;
        using IDisposable? typeProp = userType is not null ? LogContext.PushProperty("FrontendUserType", userType) : null;
        using IDisposable? sidProp = sessionId is not null ? LogContext.PushProperty("FrontendSessionId", sessionId) : null;

        await next(context);
    }
}
