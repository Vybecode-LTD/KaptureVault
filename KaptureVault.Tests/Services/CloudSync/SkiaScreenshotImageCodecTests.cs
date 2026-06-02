using FluentAssertions;
using Kapture.Services.CloudSync.Online;
using SkiaSharp;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// Phase 3 slice F: the BMP→PNG re-encode step. Screenshots are saved as BMP on disk and must be
/// re-encoded (losslessly) to PNG before encryption + upload. Verifies a real hand-crafted BMP
/// decodes and re-encodes to a valid PNG of the same dimensions, and that garbage input is rejected.
/// </summary>
public class SkiaScreenshotImageCodecTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void ReEncodeToPng_ProducesValidDecodablePng_WithSameDimensions()
    {
        var codec = new SkiaScreenshotImageCodec();
        var bmp = MakeBmp24(width: 4, height: 3);

        var png = codec.ReEncodeToPng(bmp);

        // 1. It carries the PNG magic signature.
        png.Take(8).Should().Equal(PngSignature);

        // 2. It is genuinely decodable as an image and preserves the dimensions (lossless re-encode).
        using var decoded = SKBitmap.Decode(png);
        decoded.Should().NotBeNull();
        decoded!.Width.Should().Be(4);
        decoded.Height.Should().Be(3);
    }

    [Fact]
    public void ReEncodeToPng_OnUndecodableBytes_Throws()
    {
        var codec = new SkiaScreenshotImageCodec();

        // Skia throws ArgumentNullException internally for undecodable bytes; the codec normalizes that
        // (and any other decode/encode failure) to a single InvalidOperationException.
        Action act = () => codec.ReEncodeToPng([1, 2, 3, 4]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReEncodeToPng_NullSource_Throws()
    {
        var codec = new SkiaScreenshotImageCodec();

        Action act = () => codec.ReEncodeToPng(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Builds a minimal valid 24-bit (BI_RGB, bottom-up) BMP of a solid colour.</summary>
    private static byte[] MakeBmp24(int width, int height)
    {
        int rowSize = ((width * 3 + 3) / 4) * 4; // each scanline padded to a 4-byte boundary
        int pixelArraySize = rowSize * height;
        const int headerSize = 54;               // 14-byte file header + 40-byte info header
        int fileSize = headerSize + pixelArraySize;
        var bmp = new byte[fileSize];

        // BITMAPFILEHEADER
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
        BitConverter.GetBytes(headerSize).CopyTo(bmp, 10); // pixel data offset

        // BITMAPINFOHEADER
        BitConverter.GetBytes(40).CopyTo(bmp, 14);          // biSize
        BitConverter.GetBytes(width).CopyTo(bmp, 18);       // biWidth
        BitConverter.GetBytes(height).CopyTo(bmp, 22);      // biHeight (+ = bottom-up)
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);    // biPlanes
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);   // biBitCount
        BitConverter.GetBytes(0).CopyTo(bmp, 30);           // biCompression = BI_RGB
        BitConverter.GetBytes(pixelArraySize).CopyTo(bmp, 34); // biSizeImage

        for (int y = 0; y < height; y++)
        {
            int rowStart = headerSize + y * rowSize;
            for (int x = 0; x < width; x++)
            {
                int p = rowStart + x * 3;
                bmp[p] = 0x20;     // Blue
                bmp[p + 1] = 0x40; // Green
                bmp[p + 2] = 0x80; // Red
            }
        }

        return bmp;
    }
}
