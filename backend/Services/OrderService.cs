using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.DTOs.Analytics;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    public record CreateOrderResult(bool Succeeded, OrderCreatedDto? Order = null, string? Error = null)
    {
        public static CreateOrderResult Rejected(string error) => new(false, Error: error);
    }

    public interface IOrderService
    {
        Task<CreateOrderResult> CreateAsync(CreateOrderDto dto, CancellationToken ct = default);
    }

    /// <summary>
    /// Records orders without recording customers.
    /// </summary>
    /// <remarks>
    /// The endpoint behind this is public — anonymous visitors place orders — so
    /// the input is treated as a claim, not as fact: totals are recomputed from the
    /// lines rather than accepted, and referenced promotions are verified to exist.
    /// It is still best-effort data, which is the honest trade for not requiring
    /// accounts.
    /// </remarks>
    public class OrderService : IOrderService
    {
        private const int MaxItems = 100;
        private static readonly string[] FulfillmentTypes = ["pickup", "delivery"];

        // No ambiguous characters, so an operator can read a number off a screen
        // and match it to a WhatsApp conversation without guessing.
        private const string NumberAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(AppDbContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CreateOrderResult> CreateAsync(
            CreateOrderDto dto, CancellationToken ct = default)
        {
            if (!FulfillmentTypes.Contains(dto.FulfillmentType))
                return CreateOrderResult.Rejected("Tipo de entrega inválido.");

            if (dto.Items.Count is 0 or > MaxItems)
                return CreateOrderResult.Rejected("Pedido sem itens ou com itens em excesso.");

            var referencedIds = dto.Items
                .Where(i => i.PromotionId.HasValue)
                .Select(i => i.PromotionId!.Value)
                .Distinct()
                .ToList();

            if (referencedIds.Count > 0)
            {
                var known = await _context.ItemPromotions
                    .Where(p => referencedIds.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                if (known.Count != referencedIds.Count)
                    return CreateOrderResult.Rejected("Pedido referencia promoção inexistente.");
            }

            var items = dto.Items.ConvertAll(i => new OrderItem
            {
                PromotionId = i.PromotionId,
                // Frozen as they were: renaming or repricing later must not rewrite
                // this line, or last month's revenue report changes.
                NameSnapshot = i.Name,
                UnitPriceSnapshot = i.UnitPrice,
                Quantity = i.Quantity,
                LineTotal = decimal.Round(i.UnitPrice * i.Quantity, 2),
            });

            // Derived, never taken from the request.
            var subtotal = items.Sum(i => i.LineTotal);
            var deliveryFee = dto.FulfillmentType == "delivery" ? dto.DeliveryFee : 0m;

            var order = new Order
            {
                OrderNumber = await GenerateNumberAsync(ct),
                FulfillmentType = dto.FulfillmentType,
                PaymentMethod = dto.PaymentMethod,
                DeliveryCity = dto.FulfillmentType == "delivery" ? dto.DeliveryCity : null,
                ItemsSubtotal = subtotal,
                DeliveryFee = deliveryFee,
                Total = subtotal + deliveryFee,
                ItemCount = items.Sum(i => i.Quantity),
                CreatedAt = DateTime.UtcNow,
                Items = items,
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Recorded order {OrderNumber} with {ItemCount} items.",
                order.OrderNumber, order.ItemCount);

            return new CreateOrderResult(
                true,
                new OrderCreatedDto { OrderNumber = order.OrderNumber, Total = order.Total });
        }

        private async Task<string> GenerateNumberAsync(CancellationToken ct)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = RandomNumber();

                if (!await _context.Orders.AnyAsync(o => o.OrderNumber == candidate, ct))
                    return candidate;
            }

            // Astronomically unlikely; a timestamp suffix is still unique and
            // readable rather than failing the customer's order.
            return $"{RandomNumber()}{DateTime.UtcNow:ssfff}";
        }

        private static string RandomNumber()
        {
            var chars = new char[6];

            for (var i = 0; i < chars.Length; i++)
                chars[i] = NumberAlphabet[RandomNumberGenerator.GetInt32(NumberAlphabet.Length)];

            return new string(chars);
        }
    }
}
