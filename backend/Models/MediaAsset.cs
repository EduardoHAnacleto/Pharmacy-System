using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWorkerAPI.Models
{
    /// <summary>
    /// A stored image, reusable across promotions.
    /// </summary>
    /// <remarks>
    /// Exists so that archiving a promotion no longer means losing its artwork,
    /// and so reactivating one does not require re-uploading it. The content hash
    /// is unique: uploading the same file twice reuses the existing row instead of
    /// writing a second copy to disk.
    /// </remarks>
    public class MediaAsset
    {
        [Column("id")]
        public int Id { get; set; }

        /// <summary>Public, relative URL — e.g. <c>/images/promotions/abc.webp</c>.</summary>
        [Column("file_path")]
        public string FilePath { get; set; } = null!;

        /// <summary>Lowercase hex SHA-256 of the file contents.</summary>
        [Column("content_hash")]
        public string ContentHash { get; set; } = null!;

        [Column("mime_type")]
        public string MimeType { get; set; } = null!;

        [Column("byte_size")]
        public int ByteSize { get; set; }

        [Column("original_filename")]
        public string? OriginalFileName { get; set; }

        [Column("created_by_user_id")]
        public int CreatedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// True when the referenced file is known to be missing from disk — the
        /// state left behind by promotions created before uploads had a volume.
        /// </summary>
        [Column("is_missing")]
        public bool IsMissing { get; set; }
    }
}
