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