using System.Security.Cryptography;
using System.Text;

namespace TaskEngine.Infrastructure.Auth;

/// <summary>
/// A PKCE (RFC 7636) verifier/challenge pair for the Authorization Code + PKCE flow. Generic -
/// not specific to any single provider, so it can be reused by other providers' OAuth
/// authenticators alongside <see cref="LoopbackCallbackListener"/>.
/// </summary>
public sealed record PkceChallenge(string CodeVerifier, string CodeChallenge)
{
    /// <summary>
    /// 32 bytes of cryptographically secure randomness, base64url-encoded without padding, yields
    /// a 43-character verifier - the RFC 7636 minimum length (valid range is 43-128 chars) and the
    /// entropy RFC 7636 recommends.
    /// </summary>
    private const int VerifierEntropyBytes = 32;

    public static PkceChallenge Create()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(VerifierEntropyBytes);
        var codeVerifier = Base64UrlEncode(verifierBytes);

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);

        return new PkceChallenge(codeVerifier, codeChallenge);
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
