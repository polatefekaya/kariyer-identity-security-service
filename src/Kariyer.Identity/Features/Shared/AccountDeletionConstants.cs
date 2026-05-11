namespace Kariyer.Identity.Features.Shared;

internal static class AccountDeletionConstants
{
    public static readonly string[] ActiveDeletionStates =
        ["DeletionRequested", "GracePeriodActive", "Executing", "Cancelling"];
}
