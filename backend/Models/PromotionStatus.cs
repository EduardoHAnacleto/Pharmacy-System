namespace PharmacyWorkerAPI.Models
{
    /// <summary>
    /// Lifecycle of a promotion.
    /// </summary>
    /// <remarks>
    /// Replaces the single <c>is_active</c> boolean, which could not tell a draft
    /// from a promotion that had expired, been switched off by hand, or been
    /// archived — all four collapsed to false.
    /// </remarks>
    public static class PromotionStatus
    {
        /// <summary>Not published. Invisible to the storefront.</summary>
        public const string Draft = "Draft";

        /// <summary>Published, but its window has not opened yet.</summary>
        public const string Scheduled = "Scheduled";

        /// <summary>Published and inside its window.</summary>
        public const string Active = "Active";

        /// <summary>Published, window closed.</summary>
        public const string Expired = "Expired";

        /// <summary>Retired by an operator. Kept, with its image, for reactivation.</summary>
        public const string Archived = "Archived";

        public static readonly string[] All =
            [Draft, Scheduled, Active, Expired, Archived];

        /// <summary>
        /// Statuses the storefront may show, subject to the date window.
        /// </summary>
        /// <remarks>
        /// Visibility is deliberately not "status == Active": that would make a
        /// promotion invisible until the maintenance job happened to run. The
        /// window is still evaluated per request, and status only decides whether
        /// the promotion is eligible at all.
        /// </remarks>
        public static readonly string[] Publishable = [Scheduled, Active, Expired];

        public static bool IsValid(string status) => All.Contains(status);
    }
}
