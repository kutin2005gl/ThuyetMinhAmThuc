namespace FoodGuideApp.Services;

public static class LocalizationHelper
{
    public static string GetCurrentLanguage()
    {
        return Preferences.Get("language", "vi").Trim().ToLower();
    }

    public static string GetText(string key)
    {
        string lang = GetCurrentLanguage();

        return key switch
        {
            "tour_page_title" => lang switch
            {
                "en" => "Tours",
                "zh" => "旅游列表",
                "ko" => "투어 목록",
                "ja" => "ツアー一覧",
                "fr" => "Liste des circuits",
                _ => "Danh sách tour"
            },

            "tour_points_count" => lang switch
            {
                "en" => "Number of places: {0}",
                "zh" => "景点数量: {0}",
                "ko" => "장소 수: {0}",
                "ja" => "スポット数: {0}",
                "fr" => "Nombre de lieux : {0}",
                _ => "Số điểm: {0}"
            },

            "error_title" => lang switch
            {
                "en" => "Error",
                "zh" => "错误",
                "ko" => "오류",
                "ja" => "エラー",
                "fr" => "Erreur",
                _ => "Lỗi"
            },

            "poi_not_found" => lang switch
            {
                "en" => "POI information not found.",
                "zh" => "未找到 POI 信息。",
                "ko" => "POI 정보를 찾을 수 없습니다.",
                "ja" => "POI 情報が見つかりません。",
                "fr" => "Informations du POI introuvables.",
                _ => "Không tìm thấy thông tin POI."
            },

            "invalid_poi_coordinates" => lang switch
            {
                "en" => "Invalid POI coordinates:",
                "zh" => "POI 坐标无效：",
                "ko" => "유효하지 않은 POI 좌표:",
                "ja" => "無効な POI 座標:",
                "fr" => "Coordonnées POI invalides :",
                _ => "Tọa độ POI không hợp lệ:"
            },

            "ok" => lang switch
            {
                "en" => "OK",
                "zh" => "确定",
                "ko" => "확인",
                "ja" => "OK",
                "fr" => "OK",
                _ => "OK"
            },
            "tour_detail_poi_section" => lang switch
            {
                "en" => "List of sightseeing / food places",
                "zh" => "景点 / 美食地点列表",
                "ko" => "관광지 / 음식 장소 목록",
                "ja" => "観光地 / グルメスポット一覧",
                "fr" => "Liste des lieux touristiques / gastronomiques",
                _ => "Danh sách điểm tham quan / ẩm thực"
            },

            _ => key
        };
    }
}