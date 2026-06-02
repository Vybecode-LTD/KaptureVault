using SkiaSharp;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// SkiaSharp-backed <see cref="IScreenshotImageCodec"/>. Uses the same decode/encode surface the
/// screenshot annotation editor already relies on (<c>SKBitmap.Decode</c> → <c>SKImage</c> →
/// <c>Encode(Png)</c>), so behaviour stays consistent across the app.
/// </summary>
public sealed class SkiaScreenshotImageCodec : IScreenshotImageCodec
{
    public byte[] ReEncodeToPng(byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            // For undecodable bytes Skia's internal SKCodec is null and SKBitmap.Decode throws
            // ArgumentNullException (not returns null); a 0×0 bitmap is also invalid. Normalize every
            // decode/encode failure below to one InvalidOperationException so the sync pipeline can
            // cleanly skip a corrupt screenshot.
            using var bitmap = SKBitmap.Decode(source);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
                throw new InvalidOperationException("Screenshot image could not be decoded for re-encoding.");
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("Screenshot image could not be re-encoded as PNG.");
            return encoded.ToArray();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Screenshot image could not be decoded for re-encoding.", ex);
        }
    }
}
