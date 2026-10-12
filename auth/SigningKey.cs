using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// RSA key pair used to sign tokens (RS256), plus the code that builds a JWT.
///
/// A JWT is just three base64url pieces joined by dots:
///     header.payload.signature
/// The signature is RSA-SHA256 over "header.payload" using the PRIVATE key.
/// Anyone with the PUBLIC key (published at /.well-known/jwks.json) can check it,
/// but only this service can create one.
///
/// The private key is saved as a PEM file on a Docker volume so tokens stay
/// valid across restarts.
/// </summary>
sealed class SigningKey
{
    private readonly RSA _rsa;
    public string KeyId { get; }
    public object PublicJwk { get; }

    private SigningKey(RSA rsa)
    {
        _rsa = rsa;
        var pub = rsa.ExportParameters(includePrivateParameters: false);

        // Key id ("kid") lets verifiers pick the right key once you rotate keys.
        KeyId = Base64Url(SHA256.HashData(pub.Modulus!))[..16];

        PublicJwk = new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = KeyId,
            n = Base64Url(pub.Modulus!),
            e = Base64Url(pub.Exponent!)
        };
    }

    public string CreateJwt(Dictionary<string, object> claims)
    {
        var header = new { alg = "RS256", typ = "JWT", kid = KeyId };

        string h = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        string p = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));

        byte[] signature = _rsa.SignData(
            Encoding.ASCII.GetBytes($"{h}.{p}"),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{h}.{p}.{Base64Url(signature)}";
    }

    public static SigningKey LoadOrCreate(string path)
    {
        RSA rsa;
        if (File.Exists(path))
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(path));
        }
        else
        {
            rsa = RSA.Create(2048);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); // chmod 600
        }
        return new SigningKey(rsa);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
