using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == dto.Username);

        if (user == null)
            return Unauthorized(new { message = "Tài khoản không tồn tại" });

        if (!user.IsActive)
            return Unauthorized(new { message = "Tài khoản đã bị khóa. Liên hệ Admin để mở khóa." });

        var valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!valid)
            return Unauthorized(new { message = "Mật khẩu không đúng" });

        return Ok(new
        {
            message = "Đăng nhập thành công",
            user.Username,
            user.FullName,
            user.Role,
            user.PoiId
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var exists = await _db.AdminUsers
            .AnyAsync(u => u.Username == dto.Username);

        if (exists)
            return BadRequest(new { message = "Tài khoản đã tồn tại" });

        var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        _db.AdminUsers.Add(new AdminUser
        {
            Username = dto.Username,
            PasswordHash = hash,
            FullName = dto.FullName,
            Role = dto.Role,
            PoiId = dto.PoiId,
            CreatedAt = new DateTime(2024, 1, 1)
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Tạo tài khoản thành công" });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.AdminUsers
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FullName,
                u.Role,
                u.PoiId,
                u.IsActive
            })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPut("users/{id}/toggle")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _db.AdminUsers.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { user.IsActive });
    }

    public record LoginDto(string Username, string Password);
    public record RegisterDto(string Username, string Password, string FullName, string Role, int? PoiId);
}