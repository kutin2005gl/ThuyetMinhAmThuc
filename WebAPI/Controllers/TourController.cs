using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TourController : ControllerBase
{
    private readonly AppDbContext _db;

    public TourController(AppDbContext db)
    {
        _db = db;
    }

    // Công dụng: lấy danh sách tour đang hoạt động kèm thứ tự POI để WebAdmin hiển thị.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tours = await _db.Tours
            .Include(t => t.TourPois)
            .ThenInclude(tp => tp.Poi)
            .Where(t => t.IsActive)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                Pois = t.TourPois
                    .OrderBy(tp => tp.Order)
                    .Select(tp => new
                    {
                        tp.Order,
                        tp.PoiId,
                        tp.Poi!.Name
                    })
            })
            .ToListAsync();

        return Ok(tours);
    }

    // Công dụng: tạo tour mới và lưu danh sách POI theo thứ tự người quản trị chọn.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TourDto dto)
    {
        var tour = new Tour
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = new DateTime(2024, 1, 1)
        };

        _db.Tours.Add(tour);
        await _db.SaveChangesAsync();

        // Thêm các POI vào tour theo thứ tự
        for (int i = 0; i < dto.PoiIds.Count; i++)
        {
            _db.TourPois.Add(new TourPoi
            {
                TourId = tour.Id,
                PoiId = dto.PoiIds[i],
                Order = i + 1
            });
        }

        await _db.SaveChangesAsync();
        return Ok(tour);
    }

    // Công dụng: cập nhật thông tin tour và thay lại lộ trình POI theo thứ tự mới.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TourDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Tên tour không được rỗng.");
        }

        if (dto.PoiIds == null || !dto.PoiIds.Any())
        {
            return BadRequest("Tour cần ít nhất một POI.");
        }

        var tour = await _db.Tours
            .Include(t => t.TourPois)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

        if (tour == null) return NotFound();

        tour.Name = dto.Name.Trim();
        tour.Description = dto.Description?.Trim() ?? "";

        _db.TourPois.RemoveRange(tour.TourPois);

        var poiIds = dto.PoiIds.Distinct().ToList();
        for (int i = 0; i < poiIds.Count; i++)
        {
            _db.TourPois.Add(new TourPoi
            {
                TourId = tour.Id,
                PoiId = poiIds[i],
                Order = i + 1
            });
        }

        await _db.SaveChangesAsync();
        return Ok(tour);
    }

    // Công dụng: xóa mềm tour khỏi danh sách đang hoạt động.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tour = await _db.Tours.FindAsync(id);
        if (tour == null) return NotFound();

        tour.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record TourDto(string Name, string Description, List<int> PoiIds);
