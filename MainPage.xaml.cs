using CommunityToolkit.Maui.Core;

namespace MauiCameraViewIssue
{
    public partial class MainPage : ContentPage
    {
        bool isInitialized;
        IReadOnlyList<CameraInfo> cameras = Array.Empty<CameraInfo>();
        int selectedCameraIndex = -1;


        public MainPage()
        {
            InitializeComponent();


        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            await InitializeCameraAsync();
        }

        async Task InitializeCameraAsync()
        {
            try
            {
                var hasPermission = await EnsureCameraPermissionAsync();
                if (!hasPermission)
                {
                    return;
                }

                cameras = await CameraPreview.GetAvailableCameras(CancellationToken.None);

                if (cameras.Count == 0)
                {
                    StatusLabel.Text = "Status: No cameras found.";
                    ReproResultLabel.Text = "Repro Result: Cannot verify (no camera).";
                    return;
                }

                var rearCamera = cameras.FirstOrDefault(c => c.Position == CameraPosition.Rear);
                selectedCameraIndex = 0;
                if (rearCamera is not null)
                {
                    for (var i = 0; i < cameras.Count; i++)
                    {
                        if (cameras[i].Equals(rearCamera))
                        {
                            selectedCameraIndex = i;
                            break;
                        }
                    }
                }
                CameraPreview.SelectedCamera = cameras[selectedCameraIndex];

                await CameraPreview.StartCameraPreview(CancellationToken.None);
                await Task.Delay(700);

                StatusLabel.Text = "Status: Camera preview started.";
                RefreshZoomDiagnostics();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Status: Camera init failed - {ex.Message}";
                ReproResultLabel.Text = "Repro Result: Cannot verify due to initialization failure.";
            }
        }

        async Task<bool> EnsureCameraPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status == PermissionStatus.Granted)
            {
                return true;
            }

            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status == PermissionStatus.Granted)
            {
                return true;
            }

            StatusLabel.Text = "Status: Camera permission denied.";
            ReproResultLabel.Text = "Repro Result: Cannot verify until camera permission is granted.";
            ReproResultLabel.TextColor = Colors.IndianRed;
            return false;
        }

        void RefreshZoomDiagnostics()
        {
            var selected = CameraPreview.SelectedCamera;

            if (selected is null)
            {
                SelectedCameraLabel.Text = "Selected Camera: (null)";
                MinZoomLabel.Text = "MinimumZoomFactor: (unavailable)";
                MaxZoomLabel.Text = "MaximumZoomFactor: (unavailable)";
                ZoomStateLabel.Text = $"ZoomFactor: {CameraPreview.ZoomFactor:F2}";
                ReproResultLabel.Text = "Repro Result: Min/Max ZoomFactor not detected (SelectedCamera is null).";
                ReproResultLabel.TextColor = Colors.IndianRed;
                return;
            }

            var minZoom = selected.MinimumZoomFactor;
            var maxZoom = selected.MaximumZoomFactor;

            SelectedCameraLabel.Text = $"Selected Camera: {selected.DeviceId} ({selected.Position})";
            MinZoomLabel.Text = $"MinimumZoomFactor: {minZoom:F4}";
            MaxZoomLabel.Text = $"MaximumZoomFactor: {maxZoom:F4}";
            ZoomStateLabel.Text = $"ZoomFactor: {CameraPreview.ZoomFactor:F4} (slider {ZoomSlider.Value:F4})";

            var minMaxMissing = minZoom <= 0 || maxZoom <= 0 || maxZoom <= minZoom;
            if (minMaxMissing)
            {
                ReproResultLabel.Text = "Repro Result: Min/Max ZoomFactor not detected (invalid/default values).";
                ReproResultLabel.TextColor = Colors.IndianRed;
                ZoomSlider.Minimum = 1;
                ZoomSlider.Maximum = 10;
            }
            else
            {
                ReproResultLabel.Text = "Repro Result: Min/Max ZoomFactor detected.";
                ReproResultLabel.TextColor = Colors.ForestGreen;
                ZoomSlider.Minimum = minZoom;
                ZoomSlider.Maximum = maxZoom;
            }

            if (ZoomSlider.Value < ZoomSlider.Minimum || ZoomSlider.Value > ZoomSlider.Maximum)
            {
                ZoomSlider.Value = ZoomSlider.Minimum;
            }
        }

        void OnRefreshClicked(object? sender, EventArgs e)
        {
            RefreshZoomDiagnostics();
        }

        void OnZoomSliderChanged(object? sender, ValueChangedEventArgs e)
        {
            CameraPreview.ZoomFactor = (float)e.NewValue;
            ZoomStateLabel.Text = $"ZoomFactor: {CameraPreview.ZoomFactor:F4} (slider {ZoomSlider.Value:F4})";
        }

        async void OnSwitchCameraClicked(object? sender, EventArgs e)
        {
            if (cameras.Count <= 1)
            {
                StatusLabel.Text = "Status: No alternate camera available.";
                return;
            }

            selectedCameraIndex = (selectedCameraIndex + 1) % cameras.Count;
            CameraPreview.SelectedCamera = cameras[selectedCameraIndex];

            try
            {
                await CameraPreview.StartCameraPreview(CancellationToken.None);
                await Task.Delay(300);
                StatusLabel.Text = $"Status: Switched to camera {selectedCameraIndex + 1} of {cameras.Count}.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Status: Switch camera failed - {ex.Message}";
            }

            RefreshZoomDiagnostics();
        }
    }
}
