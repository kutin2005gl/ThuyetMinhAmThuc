using System;

namespace FoodGuideApp.Services
{
    public class AudioJob
    {
        public int PoiId { get; set; }
        public string Language { get; set; } = "vi";
        public string Text { get; set; } = "";
        public int Priority { get; set; } = 1;

        // thời điểm tạo job để bỏ các job quá cũ
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}