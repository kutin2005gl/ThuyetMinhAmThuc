using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QrController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public QrController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            publicBaseUrl = GetPublicBaseUrl(),
            appDownloadUrl = _configuration["AppDownloadUrl"] ?? ""
        });
    }

    [HttpGet("{poiId:int}")]
    public async Task<IActionResult> GenerateQr(int poiId)
    {
        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound();

        var qrData = BuildLandingUrl(poiId);

        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrBytes = qrCode.GetGraphic(10);

        return File(qrBytes, "image/png");
    }

    private string BuildLandingUrl(int poiId)
        => $"{GetPublicBaseUrl()}/p/{poiId}";

    private string GetPublicBaseUrl()
    {
        var configuredUrl = _configuration["PublicBaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(configuredUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : configuredUrl.Trim();

        return baseUrl.TrimEnd('/');
    }
}
