using elite.DTOs;

namespace elite.Interfaces
{
    public interface IPromotionService
    {
        Task<IEnumerable<PromotionDto>> GetAllPromotionsAsync();
        Task<PromotionDto> GetPromotionByIdAsync(int id);
        Task<PromotionDto> GetPromotionByCodeAsync(string code);
        Task<PromotionDto> CreatePromotionAsync(PromotionCreateDto promotionCreateDto);
        Task<PromotionDto> UpdatePromotionAsync(int id, PromotionCreateDto promotionUpdateDto);
        Task<bool> DeletePromotionAsync(int id);
        Task<bool> ValidatePromotionAsync(string code, decimal orderAmount);
        Task<decimal> CalculateDiscountAsync(string code, decimal orderAmount);
    }
}
