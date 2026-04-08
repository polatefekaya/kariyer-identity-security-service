namespace Kariyer.Identity.Features.Admins.CreateAdmin;

internal record CreateAdminRequest(string Name, string Surname, string Email, string Password, string Role);