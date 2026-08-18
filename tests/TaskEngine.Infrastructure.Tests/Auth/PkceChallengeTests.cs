using System.Security.Cryptography;
using System.Text;
using TaskEngine.Infrastructure.Auth;

namespace TaskEngine.Infrastructure.Tests.Auth;

public class PkceChallengeTests
{
    [Fact]
    public void Create_CodeChallengeIsSha256Base64UrlOfVerifier()
    {
        var challenge = PkceChallenge.Create();

        var expectedChallengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(challenge.CodeVerifier));
        var expectedChallenge = Convert.ToBase64String(expectedChallengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Assert.Equal(expectedChallenge, challenge.CodeChallenge);
    }

    [Fact]
    public void Create_CodeVerifierHasRfc7636ValidLength()
    {
        var challenge = PkceChallenge.Create();

        Assert.InRange(challenge.CodeVerifier.Length, 43, 128);
    }

    [Fact]
    public void Create_CodeVerifierOnlyContainsUnreservedCharacters()
    {
        var challenge = PkceChallenge.Create();

        Assert.Matches("^[A-Za-z0-9\\-._~]+$", challenge.CodeVerifier);
    }

    [Fact]
    public void Create_TwoCallsProduceDifferentValues()
    {
        var first = PkceChallenge.Create();
        var second = PkceChallenge.Create();

        Assert.NotEqual(first.CodeVerifier, second.CodeVerifier);
        Assert.NotEqual(first.CodeChallenge, second.CodeChallenge);
    }
}
