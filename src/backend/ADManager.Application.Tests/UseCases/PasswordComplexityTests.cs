using ADManager.Application.UseCases;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class PasswordComplexityTests
{
    [Theory]
    [InlineData("Aa1!", false)]              // too short
    [InlineData("alllowercase1!", true)]     // no uppercase, but 3 of 4 classes present
    [InlineData("ALLUPPERCASE1!", true)]     // no lowercase, but 3 of 4 classes present
    [InlineData("NoDigitsHere!", true)]      // no digit, but 3 of 4 classes present
    [InlineData("NoSpecial123", true)]       // no special char, but 3 of 4 classes present
    [InlineData("GoodPass1!", true)]
    public void Validate_ChecksLengthAndClassCoverage(string password, bool expectedValid)
    {
        var error = PasswordComplexity.Validate(password);

        Assert.Equal(expectedValid, error is null);
    }
}
