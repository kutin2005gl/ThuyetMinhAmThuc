namespace FoodGuideApp.Models
{
    public class PoiRuntimeState
    {
        public bool WasInside { get; set; } = false;
        public bool WasNear { get; set; } = false;

        public DateTime LastTriggeredAt { get; set; } = DateTime.MinValue;
    }
}   