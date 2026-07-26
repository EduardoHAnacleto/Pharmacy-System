namespace Storefront.Api.Utility
{
    /// <summary>
    /// Identifies image formats from their leading bytes.
    /// </summary>
    /// <remarks>
    /// The uploaded file's Content-Type header and file name are both supplied by
    /// the client and are therefore not evidence of anything. Sniffing the actual
    /// bytes is what stops an arbitrary payload from being written to the web root
    /// under a trusted-looking extension.
    /// </remarks>
    public static class ImageValidator
    {
        /// <summary>Number of leading bytes needed to identify every supported format.</summary>
        public const int HeaderLength = 12;

        private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] RiffMagic = { 0x52, 0x49, 0x46, 0x46 };   // "RIFF"
        private static readonly byte[] WebpMagic = { 0x57, 0x45, 0x42, 0x50 };   // "WEBP"

        /// <summary>
        /// Returns the canonical file extension for the detected format, including the
        /// leading dot, or <c>null</c> when the header matches no supported format.
        /// </summary>
        public static string? DetectExtension(ReadOnlySpan<byte> header)
        {
            if (StartsWith(header, JpegMagic))
                return ".jpg";

            if (StartsWith(header, PngMagic))
                return ".png";

            // WebP is a RIFF container: "RIFF" <4-byte length> "WEBP".
            if (StartsWith(header, RiffMagic)
                && header.Length >= 12
                && header.Slice(8, 4).SequenceEqual(WebpMagic))
            {
                return ".webp";
            }

            return null;
        }

        private static bool StartsWith(ReadOnlySpan<byte> header, ReadOnlySpan<byte> magic) =>
            header.Length >= magic.Length && header[..magic.Length].SequenceEqual(magic);
    }
}
