namespace Kariyer.Identity.Features.Admins;

internal record AdminDto(string Uid, string Name, string Surname, string FullName, string Email, string Role, string Status, string FormattedLastLogin);