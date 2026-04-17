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

    public QrController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{poiId}")]
    public async Task<IActionResult> GenerateQr(int poiId)
    {
        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound();

        var qrData = $"foodguide://poi/{poiId}";

        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrBytes = qrCode.GetGraphic(10);

        return File(qrBytes, "image/png");
    }
}