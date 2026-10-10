using Nimbus.API;
namespace Nimbus.Api.Tests;

public class JwtSecretValidatorTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Missing_Secret_Fails(string? secret)
    {
        Assert.Throws<InvalidOperationException>(() => JwtSecretValidator.Require(secret));
    }

    [TestCase("REPLACE-WITH-32+-CHAR-RANDOM-STRING-IN-PROD")]
    [TestCase("please-change-me-please-change-me-123456")]
    [TestCase("your-secret-your-secret-your-secret-1234")]
    public void Placeholder_Secret_Fails(string secret)
    {
        Assert.Throws<InvalidOperationException>(() => JwtSecretValidator.Require(secret));
    }

    [Test]
    public void Short_Secret_Fails_AndTheMessageDoesNotLeakIt()
    {
        var secret = "short-0123456789";
        var ex = Assert.Throws<InvalidOperationException>(() => JwtSecretValidator.Require(secret));
        Assert.That(ex!.Message, Does.Not.Contain(secret));
    }

    [Test]
    public void Strong_Secret_Is_Accepted()
    {
        var secret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        Assert.That(JwtSecretValidator.Require(secret), Is.EqualTo(secret));
    }
}
