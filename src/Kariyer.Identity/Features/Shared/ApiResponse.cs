namespace Kariyer.Identity.Features.Shared;

internal record ApiResponse<T>(bool Success, string Message, T? Data);