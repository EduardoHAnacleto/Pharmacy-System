using System.Linq.Expressions;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Mapping
{
    /// <summary>
    /// The single definition of how a promotion becomes a response.
    /// </summary>
    /// <remarks>
    /// This projection used to be copy-pasted into six controller actions, and
    /// they had already drifted: the one in the create action was the only one
    /// that did not prefix the image URL, so a POST returned a differently shaped
    /// ImageUrl than every GET.
    /// <para>
    /// It is an <see cref="Expression"/> rather than a method because EF Core has
    /// to translate it into SQL. <see cref="ToDto"/> exposes the same mapping,
    /// compiled, for entities already in memory — so both paths cannot diverge.
    /// </para>
    /// </remarks>
    public static class ItemPromotionMapping
    {
        public static readonly Expression<Func<ItemPromotion, ItemPromotionResponseDto>> ToResponseDto =
            p => new ItemPromotionResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                PriceBefore = p.PriceBefore,
                ImageUrl = p.ImagePath,

                DateStart = p.DateStart,
                DateEnd = p.DateEnd,

                IsActive = p.IsActive,
                CategoryId = p.CategoryId,
                ProductType = p.ProductType,
                CreatedByUserId = p.CreatedByUserId,
                CreatedByUserName = p.CreatedByUserName,
            };

        private static readonly Func<ItemPromotion, ItemPromotionResponseDto> Compiled =
            ToResponseDto.Compile();

        public static ItemPromotionResponseDto ToDto(this ItemPromotion promotion) =>
            Compiled(promotion);
    }
}
