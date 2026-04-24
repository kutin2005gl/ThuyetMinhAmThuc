using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("p")]
public class PublicPoiController : ControllerBase
{
    private static readonly string[] SupportedLanguages = ["vi", "en", "zh", "ja", "ko", "fr"];

    private static readonly Dictionary<string, UiText> UiTexts = new()
    {
        ["vi"] = new(
            "Thuyết Minh Ẩm Thực",
            "Trang giới thiệu địa điểm dành cho khách tham quan sau khi quét mã QR.",
            "Nội dung thuyết minh",
            "Bán kính",
            "Mở trong ứng dụng",
            "Tải ứng dụng FoodGuideApp",
            "FoodGuideApp giúp bạn nghe thuyết minh món ăn và địa điểm ngay trên điện thoại.",
            "Không tìm thấy địa điểm này",
            "Chọn ngôn ngữ",
            "Mã QR có thể không còn hiệu lực hoặc địa điểm đã tạm ngừng hiển thị.",
            "Nội dung thuyết minh đang được cập nhật."),
        ["en"] = new(
            "Culinary Audio Guide",
            "A visitor landing page for this place after scanning the QR code.",
            "Audio guide content",
            "Radius",
            "Open in app",
            "Download FoodGuideApp",
            "FoodGuideApp helps you listen to food and place audio guides right on your phone.",
            "This place could not be found",
            "Choose language",
            "The QR code may no longer be valid, or this place is temporarily unavailable.",
            "The audio guide content is being updated."),
        ["zh"] = new(
            "美食语音导览",
            "游客扫描二维码后打开的地点介绍页面。",
            "导览内容",
            "半径",
            "在应用中打开",
            "下载 FoodGuideApp",
            "FoodGuideApp 可帮助你直接在手机上收听美食和地点导览。",
            "找不到此地点",
            "选择语言",
            "二维码可能已失效，或此地点已暂时停止显示。",
            "导览内容正在更新。"),
        ["ja"] = new(
            "食の音声ガイド",
            "QRコードを読み取った来訪者向けのスポット紹介ページです。",
            "ガイド内容",
            "半径",
            "アプリで開く",
            "FoodGuideApp をダウンロード",
            "FoodGuideApp では、料理やスポットの音声ガイドをスマートフォンで聞くことができます。",
            "この地点は見つかりません",
            "言語を選択",
            "QRコードが無効になっているか、この地点は一時的に表示されていません。",
            "ガイド内容は更新中です。"),
        ["ko"] = new(
            "음식 오디오 가이드",
            "QR 코드를 스캔한 방문객을 위한 장소 소개 페이지입니다.",
            "해설 내용",
            "반경",
            "앱에서 열기",
            "FoodGuideApp 다운로드",
            "FoodGuideApp으로 휴대폰에서 음식과 장소 해설을 바로 들을 수 있습니다.",
            "이 장소를 찾을 수 없습니다",
            "언어 선택",
            "QR 코드가 더 이상 유효하지 않거나 이 장소가 일시적으로 표시되지 않습니다.",
            "해설 내용이 업데이트 중입니다."),
        ["fr"] = new(
            "Guide Audio Culinaire",
            "Page de présentation du lieu pour les visiteurs après le scan du QR code.",
            "Contenu du guide audio",
            "Rayon",
            "Ouvrir dans l'application",
            "Télécharger FoodGuideApp",
            "FoodGuideApp vous aide à écouter les guides des plats et des lieux directement sur votre téléphone.",
            "Ce lieu est introuvable",
            "Choisir la langue",
            "Le QR code n'est peut-être plus valide ou ce lieu est temporairement indisponible.",
            "Le contenu du guide audio est en cours de mise à jour.")
    };

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public PublicPoiController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("{poiId:int}")]
    public async Task<IActionResult> LandingPage(int poiId, [FromQuery] string? lang)
    {
        var currentLanguage = NormalizeLanguage(lang);
        var ui = UiTexts[currentLanguage];

        var poi = await _db.Pois
            .Include(p => p.Translations)
            .FirstOrDefaultAsync(p => p.Id == poiId && p.IsActive);

        if (poi == null)
        {
            return Html(BuildNotFoundPage(poiId, currentLanguage, ui), StatusCodes.Status404NotFound);
        }

        var publicBaseUrl = (_configuration["PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}")
            .TrimEnd('/');

        string? imageUrl = null;

        if (!string.IsNullOrWhiteSpace(poi.ImagePath))
        {
            var imagePath = poi.ImagePath.Trim();

            if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                imageUrl = imagePath;
            }
            else
            {
                imageUrl = $"{publicBaseUrl}/{imagePath.TrimStart('/')}";
            }
        }

        var description = GetPoiText(
            currentLanguage,
            poi.Description,
            poi.Translations.Select(t => (t.Language, t.Text)),
            ui.EmptyDescription);

        return Html(BuildLandingPage(
            poi.Id,
            poi.Name,
            description,
            imageUrl,
            poi.Latitude,
            poi.Longitude,
            poi.RadiusMeters,
            currentLanguage,
            ui,
            $"foodguide://poi/{poi.Id}",
            _configuration["AppDownloadUrl"] ?? "#"));
    }

    private ContentResult Html(string html, int statusCode = StatusCodes.Status200OK)
        => new()
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = statusCode
        };

    private static string NormalizeLanguage(string? lang)
    {
        var normalized = string.IsNullOrWhiteSpace(lang)
            ? "vi"
            : lang.Trim().ToLowerInvariant();

        return SupportedLanguages.Contains(normalized) ? normalized : "vi";
    }

    private static string GetPoiText(
        string lang,
        string description,
        IEnumerable<(string Language, string Text)> translations,
        string emptyDescription)
    {
        var translationList = translations.ToList();
        var selectedText = translationList.FirstOrDefault(t =>
            t.Language.Equals(lang, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(t.Text)).Text;

        if (!string.IsNullOrWhiteSpace(selectedText))
        {
            return selectedText.Trim();
        }

        var viText = translationList.FirstOrDefault(t =>
            t.Language.Equals("vi", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(t.Text)).Text;

        if (!string.IsNullOrWhiteSpace(viText))
        {
            return viText.Trim();
        }

        return string.IsNullOrWhiteSpace(description) ? emptyDescription : description.Trim();
    }

    private static string BuildLandingPage(
        int poiId,
        string name,
        string description,
        string? imageUrl,
        double latitude,
        double longitude,
        double radiusMeters,
        string currentLanguage,
        UiText ui,
        string deepLink,
        string appDownloadUrl)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeDescription = WebUtility.HtmlEncode(description);
        var safeImageUrl = WebUtility.HtmlEncode(imageUrl);
        var safeDeepLink = WebUtility.HtmlEncode(deepLink);
        var safeDownloadUrl = WebUtility.HtmlEncode(appDownloadUrl);
        var lat = latitude.ToString("0.######", CultureInfo.InvariantCulture);
        var lng = longitude.ToString("0.######", CultureInfo.InvariantCulture);
        var radius = radiusMeters.ToString("0.#", CultureInfo.InvariantCulture);
        var languageOptions = BuildLanguageOptions(poiId, currentLanguage);
        var imageMarkup = string.IsNullOrWhiteSpace(imageUrl)
            ? $$"""
              <div class="poi-image placeholder"><span>{{WebUtility.HtmlEncode(ui.Brand)}}</span></div>
              """
            : $$"""
              <img class="poi-image" src="{{safeImageUrl}}" alt="{{safeName}}" />
              """;

        return $$"""
<!doctype html>
<html lang="{{currentLanguage}}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{safeName}} - {{WebUtility.HtmlEncode(ui.Brand)}}</title>
    <style>
        :root {
            color-scheme: light;
            --ink: #24160f;
            --muted: #765f51;
            --line: #ead9c8;
            --paper: #fffaf4;
            --accent: #b94325;
            --accent-dark: #813018;
            --leaf: #28745b;
        }
        * { box-sizing: border-box; }
        body {
            margin: 0;
            min-height: 100vh;
            font-family: Arial, "Helvetica Neue", sans-serif;
            color: var(--ink);
            background: linear-gradient(180deg, #fff7ec 0%, #f7eadb 100%);
        }
        .page {
            width: min(960px, 100%);
            margin: 0 auto;
            padding: 18px;
        }
        .shell {
            overflow: hidden;
            background: var(--paper);
            border: 1px solid var(--line);
            border-radius: 24px;
            box-shadow: 0 20px 60px rgba(93, 51, 24, .14);
        }
        .hero {
            padding: 24px 22px 18px;
            background: #fff2df;
            border-bottom: 1px solid var(--line);
        }
        .topbar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 12px;
            flex-wrap: wrap;
        }
        .brand {
            display: inline-flex;
            padding: 7px 11px;
            border-radius: 999px;
            background: #ffffffa8;
            color: var(--accent-dark);
            font-weight: 700;
            font-size: 13px;
        }
        .language-picker {
            display: flex;
            align-items: center;
            gap: 8px;
            color: var(--muted);
            font-size: 13px;
            font-weight: 700;
        }
        .language-picker select {
            min-height: 38px;
            padding: 0 34px 0 12px;
            border: 1px solid var(--line);
            border-radius: 999px;
            background: #fffaf4;
            color: var(--ink);
            font-weight: 700;
        }
        h1 {
            margin: 18px 0 10px;
            font-size: clamp(30px, 8vw, 52px);
            line-height: 1.03;
            letter-spacing: 0;
        }
        .subtitle {
            margin: 0;
            color: var(--muted);
            font-size: 16px;
            line-height: 1.6;
        }
        .content {
            display: grid;
            gap: 20px;
            padding: 20px;
        }
        .poi-image {
            display: block;
            width: 100%;
            aspect-ratio: 16 / 10;
            object-fit: cover;
            border-radius: 18px;
            border: 1px solid var(--line);
            background: #f3dfca;
        }
        .poi-image.placeholder {
            display: grid;
            place-items: center;
            min-height: 220px;
            color: var(--accent-dark);
            font-weight: 800;
            text-align: center;
        }
        .section-title {
            margin: 2px 0 10px;
            color: var(--accent-dark);
            font-size: 15px;
            text-transform: uppercase;
        }
        .description {
            margin: 0;
            color: #3d2b20;
            font-size: 16px;
            line-height: 1.72;
            white-space: pre-line;
        }
        .facts {
            display: grid;
            grid-template-columns: repeat(3, minmax(0, 1fr));
            gap: 10px;
            margin-top: 18px;
        }
        .fact {
            padding: 13px;
            border: 1px solid var(--line);
            border-radius: 14px;
            background: #fffdf9;
        }
        .fact span {
            display: block;
            color: var(--muted);
            font-size: 12px;
            margin-bottom: 5px;
        }
        .fact strong {
            display: block;
            overflow-wrap: anywhere;
            font-size: 15px;
        }
        .actions {
            display: grid;
            gap: 12px;
            margin-top: 20px;
        }
        .button {
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 52px;
            padding: 14px 18px;
            border-radius: 14px;
            text-decoration: none;
            font-weight: 800;
            text-align: center;
        }
        .button.primary {
            color: white;
            background: var(--accent);
        }
        .button.secondary {
            color: var(--leaf);
            background: #edf8f1;
            border: 1px solid #c7e3d1;
        }
        .footer {
            padding: 0 20px 22px;
            color: var(--muted);
            font-size: 13px;
            text-align: center;
        }
        @media (min-width: 760px) {
            .page { padding: 34px; }
            .content { grid-template-columns: .9fr 1.1fr; padding: 26px; }
            .actions { grid-template-columns: 1fr 1fr; }
        }
        @media (max-width: 520px) {
            .page { padding: 0; }
            .shell { min-height: 100vh; border-radius: 0; border-width: 0; }
            .hero { padding-top: 22px; }
            .facts { grid-template-columns: 1fr; }
            .language-picker { width: 100%; justify-content: space-between; }
            .language-picker select { flex: 1; }
        }
    </style>
</head>
<body>
    <main class="page">
        <article class="shell">
            <header class="hero">
                <div class="topbar">
                    <div class="brand">{{WebUtility.HtmlEncode(ui.Brand)}}</div>
                    <label class="language-picker">
                        <span>{{WebUtility.HtmlEncode(ui.ChooseLanguage)}}</span>
                        <select onchange="window.location.href=this.value">
                            {{languageOptions}}
                        </select>
                    </label>
                </div>
                <h1>{{safeName}}</h1>
                <p class="subtitle">{{WebUtility.HtmlEncode(ui.Subtitle)}}</p>
            </header>
            <section class="content">
                {{imageMarkup}}
                <div>
                    <h2 class="section-title">{{WebUtility.HtmlEncode(ui.ContentTitle)}}</h2>
                    <p class="description">{{safeDescription}}</p>
                    <div class="facts">
                        <div class="fact"><span>LAT</span><strong>{{lat}}</strong></div>
                        <div class="fact"><span>LNG</span><strong>{{lng}}</strong></div>
                        <div class="fact"><span>{{WebUtility.HtmlEncode(ui.Radius)}}</span><strong>{{radius}} m</strong></div>
                    </div>
                    <div class="actions">
                        <a class="button primary" href="{{safeDeepLink}}">{{WebUtility.HtmlEncode(ui.OpenInApp)}}</a>
                        <a class="button secondary" href="{{safeDownloadUrl}}" target="_blank" rel="noopener">{{WebUtility.HtmlEncode(ui.DownloadApp)}}</a>
                    </div>
                </div>
            </section>
            <footer class="footer">{{WebUtility.HtmlEncode(ui.Footer)}}</footer>
        </article>
    </main>
</body>
</html>
""";
    }

    private static string BuildLanguageOptions(int poiId, string currentLanguage)
    {
        return string.Join(Environment.NewLine, SupportedLanguages.Select(lang =>
        {
            var selected = lang == currentLanguage ? " selected" : "";
            return $"""<option value="/p/{poiId}?lang={lang}"{selected}>{WebUtility.HtmlEncode(GetLanguageLabel(lang))}</option>""";
        }));
    }

    private static string GetLanguageLabel(string lang)
        => lang switch
        {
            "en" => "English",
            "zh" => "中文",
            "ja" => "日本語",
            "ko" => "한국어",
            "fr" => "Français",
            _ => "Tiếng Việt"
        };

    private static string BuildNotFoundPage(int poiId, string currentLanguage, UiText ui)
    {
        var languageOptions = BuildLanguageOptions(poiId, currentLanguage);

        return $$"""
<!doctype html>
<html lang="{{currentLanguage}}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{WebUtility.HtmlEncode(ui.NotFoundTitle)}}</title>
    <style>
        body {
            margin: 0;
            min-height: 100vh;
            display: grid;
            place-items: center;
            padding: 24px;
            font-family: Arial, "Helvetica Neue", sans-serif;
            color: #24160f;
            background: linear-gradient(180deg, #fff7ec 0%, #f7eadb 100%);
        }
        .box {
            width: min(440px, 100%);
            padding: 30px 24px;
            border-radius: 22px;
            border: 1px solid #ead9c8;
            background: #fffaf4;
            box-shadow: 0 20px 60px rgba(93, 51, 24, .14);
            text-align: center;
        }
        .language-picker {
            display: flex;
            gap: 8px;
            align-items: center;
            justify-content: center;
            margin-bottom: 20px;
            color: #765f51;
            font-size: 13px;
            font-weight: 700;
        }
        select {
            min-height: 38px;
            padding: 0 12px;
            border: 1px solid #ead9c8;
            border-radius: 999px;
            background: #fffaf4;
            color: #24160f;
            font-weight: 700;
        }
        .mark {
            width: 62px;
            height: 62px;
            margin: 0 auto 18px;
            display: grid;
            place-items: center;
            border-radius: 18px;
            background: #fff2df;
            color: #b94325;
            font-size: 34px;
            font-weight: 800;
        }
        h1 {
            margin: 0 0 10px;
            font-size: 28px;
            letter-spacing: 0;
        }
        p {
            margin: 0;
            color: #765f51;
            line-height: 1.6;
        }
    </style>
</head>
<body>
    <main class="box">
        <label class="language-picker">
            <span>{{WebUtility.HtmlEncode(ui.ChooseLanguage)}}</span>
            <select onchange="window.location.href=this.value">
                {{languageOptions}}
            </select>
        </label>
        <div class="mark">?</div>
        <h1>{{WebUtility.HtmlEncode(ui.NotFoundTitle)}}</h1>
        <p>{{WebUtility.HtmlEncode(ui.NotFoundBody)}}</p>
    </main>
</body>
</html>
""";
    }

    private sealed record UiText(
        string Brand,
        string Subtitle,
        string ContentTitle,
        string Radius,
        string OpenInApp,
        string DownloadApp,
        string Footer,
        string NotFoundTitle,
        string ChooseLanguage,
        string NotFoundBody,
        string EmptyDescription);
}
