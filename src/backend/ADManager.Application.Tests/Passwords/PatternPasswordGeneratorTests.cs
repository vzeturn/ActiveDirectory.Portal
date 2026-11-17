using ADManager.Infrastructure.Passwords;
using Xunit;

namespace ADManager.Application.Tests.Passwords;

public sealed class PatternPasswordGeneratorTests
{
    [Fact]
    public void Generate_ReturnsSixteenCharacters()
    {
        var generator = new PatternPasswordGenerator();

        var password = generator.Generate();

        Assert.Equal(16, password.Length);
    }

    [Fact]
    public void Generate_ContainsAllFourCharacterClasses()
    {
        var generator = new PatternPasswordGenerator();

        var password = generator.Generate();

        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, c => "!@#$%^&*".Contains(c));
    }

    [Fact]
    public void Generate_ProducesDifferentValuesAcrossCalls()
    {
        var generator = new PatternPasswordGenerator();

        var passwords = Enumerable.Range(0, 50).Select(_ => generator.Generate()).ToHashSet();

        Assert.Equal(50, passwords.Count);
    }
}
