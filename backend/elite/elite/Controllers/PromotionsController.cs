using elite.DTOs;
using elite.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace elite.Controllers
{
    // Controllers/PromotionsController.cs
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class PromotionsController : ControllerBase
    {
        private readonly IPromotionService _promotionService;
        private readonly ILogger<PromotionsController> _logger;

        public PromotionsController(IPromotionService promotionService, ILogger<PromotionsController> logger)
        {
            _promotionService = promotionService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPromotions()
        {
            try
            {
                var promotions = await _promotionService.GetAllPromotionsAsync();
                return Ok(promotions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all promotions");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPromotionById(int id)
        {
            try
            {
                var promotion = await _promotionService.GetPromotionByIdAsync(id);
                return Ok(promotion);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting promotion by ID");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("code/{code}")]
        public async Task<IActionResult> GetPromotionByCode(string code)
        {
            try
            {
                var promotion = await _promotionService.GetPromotionByCodeAsync(code);
                return Ok(promotion);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting promotion by code");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePromotion([FromBody] PromotionCreateDto promotionCreateDto)
        {
            try
            {
                var result = await _promotionService.CreatePromotionAsync(promotionCreateDto);
                return CreatedAtAction(nameof(GetPromotionById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating promotion");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePromotion(int id, [FromBody] PromotionCreateDto promotionUpdateDto)
        {
            try
            {
                var result = await _promotionService.UpdatePromotionAsync(id, promotionUpdateDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating promotion");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            try
            {
                var result = await _promotionService.DeletePromotionAsync(id);
                return Ok(new { message = "Promotion deleted successfully" });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting promotion");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidatePromotion([FromBody] ApplyPromotionDto applyPromotionDto)
        {
            try
            {
                var isValid = await _promotionService.ValidatePromotionAsync(applyPromotionDto.Code, applyPromotionDto.OrderAmount);
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating promotion");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
