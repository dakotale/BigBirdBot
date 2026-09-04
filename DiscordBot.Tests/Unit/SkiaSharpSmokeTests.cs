using SkiaSharp;

namespace DiscordBot.Tests.Unit;

/// <summary>
/// Exercises the exact SkiaSharp API sequence Program.cs's TryCompressImageUnder8Mb uses
/// (decode → resize → encode) end to end against the native library, not just against the
/// managed API surface. A C# compile pass proves the API signatures still match after a
/// SkiaSharp version bump; it says nothing about whether the native libSkiaSharp binary that
/// version ships still loads and actually decodes/encodes correctly on this platform - that's
/// what these tests catch.
/// </summary>
public class SkiaSharpSmokeTests
{
    private static SKData EncodeSolidColorPng(int width, int height, SKColor color)
    {
        var info = new SKImageInfo(width, height);
        using var bitmap = new SKBitmap(info);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100);
    }

    [Fact]
    public void Decode_RoundTripsAnEncodedBitmap()
    {
        using var encoded = EncodeSolidColorPng(64, 48, SKColors.CornflowerBlue);

        using var decoded = SKBitmap.Decode(encoded);

        Assert.NotNull(decoded);
        Assert.Equal(64, decoded.Width);
        Assert.Equal(48, decoded.Height);
    }

    [Fact]
    public void Resize_ProducesABitmapAtTheRequestedDimensions()
    {
        using var encoded = EncodeSolidColorPng(200, 100, SKColors.Crimson);
        using var original = SKBitmap.Decode(encoded);

        using var resized = original.Resize(new SKImageInfo(100, 50), SKSamplingOptions.Default);

        Assert.NotNull(resized);
        Assert.Equal(100, resized!.Width);
        Assert.Equal(50, resized.Height);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    public void Encode_ProducesNonEmptyData(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 32));
        bitmap.Erase(SKColors.ForestGreen);
        using var image = SKImage.FromBitmap(bitmap);

        using var encoded = image.Encode(format, 85);

        Assert.NotNull(encoded);
        Assert.True(encoded.Size > 0);
    }

    [Fact]
    public void DecodeResizeEncode_FullPipeline_MatchesTryCompressImageUnder8MbUsage()
    {
        // Same call sequence as Program.cs's TryCompressImageUnder8Mb: Decode, Resize with
        // SKSamplingOptions.Default, FromBitmap, Encode, then read the encoded byte count and
        // save it to a stream - if the upgraded native library regressed any of these, this
        // throws or returns null/empty rather than silently misbehaving.
        using var sourceData = EncodeSolidColorPng(400, 300, SKColors.Goldenrod);
        using var original = SKBitmap.Decode(sourceData);
        Assert.NotNull(original);

        using var resized = original.Resize(new SKImageInfo(300, 225), SKSamplingOptions.Default);
        Assert.NotNull(resized);

        using var image = SKImage.FromBitmap(resized);
        using var reEncoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        Assert.True(reEncoded.Size > 0);

        using var ms = new MemoryStream();
        reEncoded.SaveTo(ms);
        Assert.True(ms.Length > 0);
    }
}
