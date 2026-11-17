namespace ADManager.Api.Tools;

public static class SecretEncryptionTool
{
    public static string Encrypt(string plaintext)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
            bytes, optionalEntropy: null, System.Security.Cryptography.DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(protectedBytes);
    }
}
