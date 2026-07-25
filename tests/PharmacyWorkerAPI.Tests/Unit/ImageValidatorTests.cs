using System.Text;
using PharmacyWorkerAPI.Utility;
using Xunit;

namespace PharmacyWorkerAPI.Tests.Unit;

public class ImageValidatorTests
{
    private static byte[] Header(params byte[] leading)
    {
        var header = new byte[ImageValidator.HeaderLength];
        leading.CopyTo(header, 0);
        return header;
    }

    [Fact]
    public void DetectExtension_IdentifiesJpeg() =>
        Assert.Equal(".jpg", ImageValidator.DetectExtension(Header(0xFF, 0xD8, 0xFF, 0xE0)));

    [Fact]
    public void DetectExtension_IdentifiesPng() =>
        Assert.Equal(
            ".png",
            ImageValidator.DetectExtension(Header(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)));

    [Fact]
    public void DetectExtension_IdentifiesWebp()
    {
        // "RIFF" <4-byte size> "WEBP"
        var header = new byte[ImageValidator.HeaderLength];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(header, 8);

        Assert.Equal(".webp", ImageValidator.DetectExtension(header));
    }

    [Fact]
    public void DetectExtension_RejectsRiffThatIsNotWebp()
    {
        // A WAV file is also a RIFF container. Matching on "RIFF" alone would let
        // it through and store it as .webp.
        var header = new byte[ImageValidator.HeaderLength];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);

        Assert.Null(ImageValidator.DetectExtension(header));
    }

    [Fact]
    public void DetectExtension_RejectsAnExecutable()
    {
        // "MZ" — a Windows executable renamed to .png is exactly the upload the
        // content-type check used to accept.
        Assert.Null(ImageValidator.DetectExtension(Header(0x4D, 0x5A, 0x90, 0x00)));
    }

    [Fact]
    public void DetectExtension_RejectsAnSvg()
    {
        // SVG is XML and can carry script, so it is not in the allowed set.
        var header = Encoding.ASCII.GetBytes("<svg xmlns=");

        Assert.Null(ImageValidator.DetectExtension(header));
    }

    [Fact]
    public void DetectExtension_RejectsEmptyInput() =>
        Assert.Null(ImageValidator.DetectExtension([]));

    [Fact]
    public void DetectExtension_RejectsATruncatedMagicNumber()
    {
        // Two of the three JPEG bytes: must not be treated as a match.
        Assert.Null(ImageValidator.DetectExtension(new byte[] { 0xFF, 0xD8 }));
    }

    [Fact]
    public void DetectExtension_RejectsRiffTooShortToCarryTheWebpTag() =>
        Assert.Null(ImageValidator.DetectExtension(Encoding.ASCII.GetBytes("RIFF1234")));
}
