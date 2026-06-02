namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Re-encodes a captured screenshot to PNG before it is encrypted and uploaded to the Online Vault.
/// Screenshots are stored as BMP on disk (large, occasionally device-specific); PNG is lossless
/// (Phase 3 decision §6.1 — text/UI screenshots must stay crisp, so no JPEG) and much smaller, which
/// matters against the storage quota. Abstracted behind an interface so the sync pipeline can be
/// unit-tested without the SkiaSharp image stack.
/// </summary>
public interface IScreenshotImageCodec
{
    /// <summary>
    /// Decode <paramref name="source"/> (any format SkiaSharp can read — BMP in practice) and
    /// re-encode it as PNG bytes. Throws <see cref="InvalidOperationException"/> when the bytes
    /// cannot be decoded or re-encoded.
    /// </summary>
    byte[] ReEncodeToPng(byte[] source);
}
