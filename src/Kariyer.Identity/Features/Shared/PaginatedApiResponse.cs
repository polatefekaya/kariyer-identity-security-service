namespace Kariyer.Identity.Features.Shared;

internal record PaginatedApiResponse<T>(bool Success, T Data, PaginationMeta Pagination);