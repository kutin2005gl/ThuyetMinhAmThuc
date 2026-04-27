using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QrController : ControllerBase
{
    private const string DeepLinkFormat = "foodguide://poi/{0}";

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
            appDownloadUrl = _configuration["AppDownloadUrl"] ?? "",
            deepLinkFormat = DeepLinkFormat
        });
    }

    [HttpGet("{poiId:int}")]
    public async Task<IActionResult> GenerateQr(int poiId)
    {
        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound();

        var qrData = BuildLandingUrl(poiId);

        var qrBytes = GenerateQrPng(qrData);

        return File(qrBytes, "image/png");
    }

    [HttpGet("{poiId:int}/deep-link")]
    public async Task<IActionResult> GenerateDeepLinkQr(int poiId)
    {
        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound();

        // Công dụng: tạo QR chỉ chứa deep link để mở trực tiếp app FoodGuide nếu thiết bị đã cài app.
        var qrBytes = GenerateQrPng(BuildDeepLink(poiId));

        return File(qrBytes, "image/png");
    }

    [HttpGet("{poiId:int}/metadata")]
    public async Task<IActionResult> GetPoiQrMetadata(int poiId)
    {
        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound();

        // Công dụng: trả đủ link QR/landing/deep link cho các client mà không đổi endpoint QR cũ.
        return Ok(new
        {
            poiId,
            landingUrl = BuildLandingUrl(poiId),
            deepLink = BuildDeepLink(poiId),
            qrImageUrl = $"{GetPublicBaseUrl()}/api/Qr/{poiId}",
            deepLinkQrImageUrl = $"{GetPublicBaseUrl()}/api/Qr/{poiId}/deep-link"
        });
    }

    private string BuildLandingUrl(int poiId)
        => $"{GetPublicBaseUrl()}/p/{poiId}";

    private static string BuildDeepLink(int poiId)
        => string.Format(DeepLinkFormat, poiId);

    // Công dụng: sinh ảnh PNG QR bằng QRCoder nhẹ, không thêm dependency mới.
    private static byte[] GenerateQrPng(string qrData)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(10);
    }

    private string GetPublicBaseUrl()
    {
        var configuredUrl = _configuration["PublicBaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(configuredUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : configuredUrl.Trim();

        return baseUrl.TrimEnd('/');
    }
}
