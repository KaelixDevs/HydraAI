using System.Security.Cryptography;

namespace Hydra.Core.Security;

public static class UpdateVerifier
{
    /// <summary>Public key must be pinned independently, never read from the downloaded manifest.</summary>
    public static bool VerifyManifest(byte[] manifest, byte[] signature, string trustedPublicKeyPem)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(trustedPublicKeyPem);
        return key.VerifyData(manifest, signature, HashAlgorithmName.SHA256);
    }
    public static async Task VerifyArtifactAsync(string path, long size, string sha256, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length != size || !Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).Equals(sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Artifact integrity verification failed.");
    }
}
