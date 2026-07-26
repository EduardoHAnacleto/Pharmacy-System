using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Models;
using Storefront.Api.Services;

namespace Storefront.Api.Controllers
{
    /// <summary>
    /// CSV export of the measurable data, for analysis outside the system.
    /// </summary>
    [ApiController]
    [Route("api/v1/exports")]
    [Authorize(Roles = Roles.Admin)]
    public class ExportsController : ControllerBase
    {
        private readonly IExportService _exports;

        public ExportsController(IExportService exports) => _exports = exports;

        [HttpGet]
        public IActionResult ListDatasets() => Ok(_exports.Datasets);

        [HttpGet("{dataset}")]
        public async Task<IActionResult> Download(
            string dataset,
            CancellationToken ct,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null)
        {
            if (!_exports.IsKnown(dataset))
                return BadRequest("Dataset inválido.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var start = from ?? today.AddDays(-90);
            var end = to ?? today;

            if (start > end)
                (start, end) = (end, start);

            var fileName = $"{dataset}_{start:yyyyMMdd}_{end:yyyyMMdd}.csv";

            // Written straight to the response body: a year of order lines should
            // not have to fit in memory to be downloaded.
            Response.ContentType = "text/csv; charset=utf-8";
            Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

            await _exports.WriteCsvAsync(dataset, start, end, Response.Body, ct);

            return new EmptyResult();
        }
    }
}
