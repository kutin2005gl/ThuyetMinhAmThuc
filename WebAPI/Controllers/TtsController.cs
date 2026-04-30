using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TtsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TtsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] TtsRequest request)
        {
            // 1. Kiểm tra dữ liệu đầu vào cơ bản
            if (request.PoiId <= 0)
            {
                return BadRequest(new { message = "PoiId không hợp lệ.", audioUrl = (string?)null });
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { message = "Nội dung thuyết minh không được rỗng.", audioUrl = (string?)null });
            }

            if (string.IsNullOrWhiteSpace(request.Language))
            {
                return BadRequest(new { message = "Language không được rỗng.", audioUrl = (string?)null });
            }

            // 2. Kiểm tra POI có tồn tại trong DB không
            var poiExists = await _db.Pois.AnyAsync(p => p.Id == request.PoiId);
            if (!poiExists)
            {
                return NotFound(new { message = $"Không tìm thấy địa điểm (POI) có ID #{request.PoiId}.", audioUrl = (string?)null });
            }

            // 3. Chuẩn hóa ngôn ngữ và kiểm tra cấu hình
            var language = request.Language.Trim().ToLowerInvariant();
            var languageConfigured = await _db.SupportedLanguages.AnyAsync(l => l.Code == language);
            if (!languageConfigured)
            {
                return BadRequest(new { message = $"Ngôn ngữ '{request.Language}' chưa được hệ thống hỗ trợ.", audioUrl = (string?)null });
            }

            try
            {
                // 4. LOGIC QUAN TRỌNG: Lưu hoặc Cập nhật vào bảng Translations
                var translation = await _db.Translations
                    .FirstOrDefaultAsync(t => t.PoiId == request.PoiId && t.Language == language);

                if (translation == null)
                {
                    // Nếu chưa có ngôn ngữ này cho POI này -> Tạo mới
                    translation = new Translation
                    {
                        PoiId = request.PoiId,
                        Language = language,
                        Text = request.Text
                    };
                    _db.Translations.Add(translation);
                }
                else
                {
                    // Nếu đã có rồi -> Cập nhật nội dung mới
                    translation.Text = request.Text;
                    _db.Entry(translation).State = EntityState.Modified;
                }

                // 5. Lưu xuống file Database (SQLite)
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Đã cập nhật thành công nội dung tiếng {language.ToUpper()}. App mobile sẽ nhận được bản dịch mới này.",
                    audioUrl = (string?)null
                });
            }
            catch (Exception ex)
            {
                // Xử lý lỗi nếu database có vấn đề (khóa ngoại, mất kết nối...)
                return StatusCode(500, new { message = $"Lỗi hệ thống khi lưu database: {ex.Message}" });
            }
        }
    }

    // Class đại diện cho dữ liệu gửi lên từ Blazor
    public record TtsRequest(int PoiId, string Text, string Language);
}