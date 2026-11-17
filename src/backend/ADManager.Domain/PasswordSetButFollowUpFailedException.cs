namespace ADManager.Domain;

/// <summary>
/// Thrown when AD's SetPassword call succeeded (the password is already changed on the
/// server) but a subsequent step - ExpirePasswordNow, RefreshExpiredPassword, or Save -
/// failed. Callers must surface <see cref="NewPassword"/> to the operator: the reset did
/// not fail, only a follow-up flag update did.
/// </summary>
public sealed class PasswordSetButFollowUpFailedException : Exception
{
    public string NewPassword { get; }

    public PasswordSetButFollowUpFailedException(string newPassword, Exception innerException)
        : base("Password was changed but a follow-up step failed.", innerException)
    {
        NewPassword = newPassword;
    }
}
