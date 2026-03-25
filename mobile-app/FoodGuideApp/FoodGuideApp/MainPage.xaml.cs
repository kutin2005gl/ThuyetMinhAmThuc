using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel;
using System.Linq.Expressions;
using System.Threading;
namespace FoodGuideApp
{
    public partial class MainPage : ContentPage
    {
        bool isTracking = false;

        CancellationTokenSource trackingCts = new CancellationTokenSource();
        string selectedLanguage = "vi";
        Location sguCS1 = new Location(10.7797, 106.6893);
        Location fakeLocation = new Location(10.7797, 106.6893);
        double radius = 0.050;
        string lastStall = "";


        bool hasSpoken = false;

        public MainPage()
        {
            InitializeComponent();
            startCheckingLocation();
        }
        private string GetDescription(FoodStall stall)
        {
            switch (selectedLanguage)
            {
                case "en":
                    return stall.DescriptionEn;

                case "ja":
                    return stall.DescriptionJa;

                case "zh":
                    return stall.DescriptionZh;

                default:
                    return stall.DescriptionVi;
            }
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

            try
            {
                while (!trackingCts.Token.IsCancellationRequested)
                {
                    var location = await getLocation(GeolocationAccuracy.Medium);

                    if (location != null)
                    {
                        locationLabel.Text =
                            $"Lat: {location.Latitude:F6}\nLng: {location.Longitude:F6}";

                        var pos = new Location(location.Latitude, location.Longitude);

                        map.MoveToRegion(
                            MapSpan.FromCenterAndRadius(pos, Distance.FromMeters(200))
                        );
                    }

                    await Task.Delay(5000, trackingCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                isTracking = false;
            }
        }

        private void stopTracking()
        {
            if (!isTracking) return;

            trackingCts?.Cancel();
        }

        private async void OnStartTrackingClicked(object sender, EventArgs e)
        {
            await startTracking();
        }

        private void OnStopTrackingClicked(object sender, EventArgs e)
        {
            stopTracking();
        }
        private void startCheckingLocation()
        {
            Device.StartTimer(TimeSpan.FromSeconds(8), () =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await checkDistance();
                });

                return true;
            });
        }

        private async Task<Location?> getLocation(GeolocationAccuracy accuracy)
        {
            // ===== TEST MODE (giả lập bằng nút) =====
             return fakeLocation;

            // ===== REAL MODE (GPS thật) =====
            //try
            //{
            //    var request = new GeolocationRequest(accuracy, TimeSpan.FromSeconds(10));
            //    var location = await Geolocation.GetLocationAsync(request);
            //    return location;
            //}
            //catch (Exception)
            //{
            //    return null;
            //}
        }

        private async Task checkDistance()
        {
            GeolocationAccuracy mucDo = GeolocationAccuracy.Medium;

            var currentLocation = await getLocation(mucDo);

            if (currentLocation == null)
                return;

            double distance = Location.CalculateDistance(
                currentLocation,
                sguCS1,
                DistanceUnits.Kilometers);

            if (distance <= radius && !hasSpoken)
            {
                hasSpoken = true;
                await phatThuyetMinh();
            }

            if (distance > radius)
            {
                hasSpoken = false;
            }
        }

        private async Task phatThuyetMinh()
        {
            string noiDung = selectedLanguage == "vi"
                ? "Bạn đã đến quầy Bún Bò. Đây là món ăn nổi tiếng với nước dùng đậm đà và hương vị đặc trưng."
                : "You have arrived at the Bun Bo stall. This is a famous dish with rich broth and a unique flavor.";

            await DisplayAlert("Thông báo", noiDung, "OK");
            await TextToSpeech.SpeakAsync(noiDung);
        }
        public class FoodStall
        {
            public string Name { get; set; }
            public Location Position { get; set; }
            public string DescriptionVi { get; set; }
            public string DescriptionEn { get; set; }
            public string DescriptionJa { get; set; }
            public string DescriptionZh { get; set; }
            public FoodStall(string name, Location position, string vi, string en, string ja, string zh)
            {
                Name = name;
                Position = position;
                DescriptionVi = vi;
                DescriptionEn = en;
                DescriptionJa = ja;
                DescriptionZh = zh;
                    
            }
        }
        List<FoodStall> foodStalls = new List<FoodStall>()
{
    new FoodStall(
        "Quầy Bún Bò",
        new Location(10.7797, 106.6893),
        "Bạn đã đến quầy Bún Bò. Đây là món ăn nổi tiếng Việt Nam.",
        "You have arrived at the Bun Bo stall. This is a famous Vietnamese dish.",
        "ブンボーの屋台に到着しました。これはベトナムの有名な料理です。",
        "你已经到达牛肉粉摊。这是越南著名的美食。"
    ),

    new FoodStall(
        "Quầy Phở",
        new Location(10.7800, 106.6895),
        "Bạn đã đến quầy Phở. Món ăn truyền thống Việt Nam.",
        "You have arrived at the Pho stall. A traditional Vietnamese dish.",
        "フォーの屋台に到着しました。これは伝統的なベトナム料理です。",
        "你已经到达河粉摊。这是越南的传统美食。"
    ),
        new FoodStall(
        "Quầy Bánh Mì",
        new Location(10.7795, 106.6890),
        "Bạn đã đến quầy Bánh Mì. Đây là món ăn nhanh rất phổ biến tại Việt Nam.",
        "You have arrived at the Banh Mi stall. This is a very popular fast food in Vietnam.",
        "ここはバインミーの屋台です。バインミーはベトナムで非常に人気のあるファストフードです。",
        "您已来到越南法棍面包摊。这是越南非常受欢迎的快餐食品。"

    )
}; 
        private void moveFakeLocation(double latChange, double lngChange)
        {
            fakeLocation = new Location(
                fakeLocation.Latitude + latChange,
                fakeLocation.Longitude + lngChange
            );

            locationLabel.Text =
                $"Lat: {fakeLocation.Latitude:F6}\nLng: {fakeLocation.Longitude:F6}";

            map.MoveToRegion(
                MapSpan.FromCenterAndRadius(fakeLocation, Distance.FromMeters(200))
            );
            checkNearbyStall(fakeLocation);

        }
       

        private async void checkNearbyStall(Location currentLocation)
        {
            foreach (var stall in foodStalls)
            {
                double distance = Location.CalculateDistance(
                    currentLocation,
                    stall.Position,
                    DistanceUnits.Kilometers);

                if (distance <= radius)
                {
                    stallLabel.Text = $"Đang gần: {stall.Name} ({distance:F3} km)";

                    if (lastStall != stall.Name)
                    {
                        lastStall = stall.Name;
                        await TextToSpeech.SpeakAsync(GetDescription(stall));
                    }
                    return;
                }
            }

            stallLabel.Text = "Chưa ở gần quầy nào";
            lastStall = "";
        }

        private void MoveUpClicked(object sender, EventArgs e)
        {
            moveFakeLocation(0.0001, 0);
        }
        private void MoveDownClicked(object sender, EventArgs e)
        {
            moveFakeLocation(-0.0001, 0);
        }
        private void MoveLeftClicked(object sender, EventArgs e)
        {
            moveFakeLocation(0, 0.0001);
        }
        private void MoveRightClicked(object sender, EventArgs e)
        {
            moveFakeLocation(0, -0.0001);
        }
    }
}