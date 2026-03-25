using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui..Controls.Maps;

namespace FoodGuideApp
{
    public partial class MainPage : ContentPage
    {
        private CancellationTokenSource? trackingCts;
        private bool isTracking = false;

        public MainPage()
        {
            InitializeComponent();
        }

        private async Task<Location?> getLocation()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);
                return location;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", ex.Message, "OK");
                return null;
            }
        }

        private async Task startTracking()
        {
            if (isTracking) return;

            isTracking = true;
            trackingCts = new CancellationTokenSource();

            while (!trackingCts.Token.IsCancellationRequested)
            {
                var location = await getLocation();

                if (location != null)
                {
                    locationLabel.Text =
                        $"Lat: {location.Latitude:F6}\nLng: {location.Longitude:F6}";
                }

                await Task.Delay(5000, trackingCts.Token);
            }
        }

        private void stopTracking()
        {
            if (!isTracking) return;

            trackingCts?.Cancel();
            isTracking = false;
        }

        private async void OnStartTrackingClicked(object sender, EventArgs e)
        {
            await startTracking();
        }

        private void OnStopTrackingClicked(object sender, EventArgs e)
        {
            stopTracking();
        }
    }
}