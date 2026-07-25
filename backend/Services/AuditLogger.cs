using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    public interface IAuditLogger
    {
        Task RecordAsync(
            int? userId,
            string? userName,
            string action,
            string entityType,
            int? entityId,
            string? changes = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Appends administrative actions to the audit log.
    /// </summary>
    /// <remarks>
    /// Never throws at the caller: losing an audit row is bad, but failing the
    /// write the operator just performed because the log could not be appended is
    /// worse. Failures are logged loudly instead.
    /// </remarks>
    public class AuditLogger : IAuditLogger
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(AppDbContext context, ILogger<AuditLogger> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RecordAsync(
            int? userId,
            string? userName,
            string action,
            string entityType,
            int? entityId,
            string? changes = null,
            CancellationToken ct = default)
        {
            try
            {
                _context.AuditLog.Add(new AuditLogEntry
                {
                    UserId = userId,
                    UserName = userName,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Changes = changes,
                    OccurredAt = DateTime.UtcNow,
                });

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to write audit entry {Action} on {EntityType} {EntityId}.",
                    action, entityType, entityId);
            }
        }
    }
}
