using System.Security.Cryptography;
using ADManager.Application.Abstractions;

namespace ADManager.Infrastructure.Passwords;

public sealed class PatternPasswordGenerator : IPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Special = "!@#$%^&*";
    private const string AllClasses = Upper + Lower + Digits + Special;
    private const int Length = 16;

    public string Generate()
    {
        string password;
        do
        {
            var chars = new char[Length];
            for (var i = 0; i < Length; i++)
            {
                chars[i] = AllClasses[RandomNumberGenerator.GetInt32(AllClasses.Length)];
            }
            password = new string(chars);
        }
        while (!HasAllClasses(password));

        return password;
    }

    private static bool HasAllClasses(string password) =>
        password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(c => Special.Contains(c));
}
