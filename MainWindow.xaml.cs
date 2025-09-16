using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        private UdpPenReceiver _udpPenReceiver;

        private int _androidDeviceWidth = 0;
        private int _androidDeviceHeight = 0;

        private int _monitorWidth = 0;
        private int _monitorHeight = 0;

        // Variables for aspect-ratio correct scaling
        private float _scaleRatio = 1.0f;
        private float _offsetX = 0f;
        private float _offsetY = 0f;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
            this.Loaded += Window_Loaded;

            try
            {
                PenInputInjector.Initialize();
            }
            catch (Exception ex)
            {
                Log($"Critical Error: {ex.Message}");
                MessageBox.Show($"Could not initialize Pen Input Injector: {ex.Message}\nThe application will now close.", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            _udpPenReceiver = new UdpPenReceiver();
            _udpPenReceiver.OnDeviceInfoReceived += UdpPenReceiver_OnDeviceInfoReceived;
            _udpPenReceiver.OnPenDataReceived += UdpPenReceiver_OnPenDataReceived;
            Log($"Local IP Address: {GetLocalIPAddress()}");
            _udpPenReceiver.StartListening(9999);
        }

        private void UdpPenReceiver_OnDeviceInfoReceived(int width, int height)
        {
            Dispatcher.Invoke(() =>
            {
                _androidDeviceWidth = width;
                _androidDeviceHeight = height;
                Log($"[UDP] Device resolution received: {width} x {height}");
                CalculateMapping();
            });
        }

        private void UdpPenReceiver_OnPenDataReceived(UdpPenData data)
        {
            // Apply aspect-ratio correct scaling and offset
            int scaledX = (int)(data.X * _scaleRatio + _offsetX);
            int scaledY = (int)(data.Y * _scaleRatio + _offsetY);

            // Call PenInputInjector based on the action type
            // (action: 0=Down, 1=Move, 2=Up, 3=Hover)
            switch (data.Action)
            {
                case 0: // Down
                    PenInputInjector.InjectPenDown(scaledX, scaledY, data.Pressure, data.IsBarrelPressed, data.TiltX, data.TiltY);
                    break;
                case 1: // Move
                    PenInputInjector.InjectPenMove(scaledX, scaledY, data.Pressure, data.IsBarrelPressed, data.TiltX, data.TiltY);
                    break;
                case 2: // Up
                    PenInputInjector.InjectPenUp(scaledX, scaledY);
                    break;
                case 3: // Hover
                    PenInputInjector.InjectPenHover(scaledX, scaledY, data.IsBarrelPressed, data.TiltX, data.TiltY);
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GetMonitorInfo();
        }

        private void GetMonitorInfo()
        {
            // Get the DPI scaling from the current window
            PresentationSource source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            // Get the logical screen size
            double logicalScreenWidth = SystemParameters.PrimaryScreenWidth;
            double logicalScreenHeight = SystemParameters.PrimaryScreenHeight;

            // Calculate the actual physical pixel resolution
            _monitorWidth = (int)(logicalScreenWidth * dpiX);
            _monitorHeight = (int)(logicalScreenHeight * dpiY);

            Log(
                $"Logical Resolution: {logicalScreenWidth}x{logicalScreenHeight}\n" +
                $"DPI Scale: {dpiX * 100}%\n" +
                $"Actual Resolution: {_monitorWidth}x{_monitorHeight}"
            );

            CalculateMapping();
        }

        /// <summary>
        /// Calculates the scaling ratio and offset to map the Android screen to the monitor
        /// while preserving the aspect ratio (letterboxing).
        /// </summary>
        private void CalculateMapping()
        {
            if (_monitorWidth == 0 || _monitorHeight == 0 || _androidDeviceWidth == 0 || _androidDeviceHeight == 0)
            {
                return;
            }

            float monitorAspect = (float)_monitorWidth / _monitorHeight;
            float androidAspect = (float)_androidDeviceWidth / _androidDeviceHeight;

            if (monitorAspect > androidAspect) // Monitor is wider than the tablet
            {
                _scaleRatio = (float)_monitorHeight / _androidDeviceHeight;
                float scaledAndroidWidth = _androidDeviceWidth * _scaleRatio;
                _offsetX = (_monitorWidth - scaledAndroidWidth) / 2f;
                _offsetY = 0f;
            }
            else // Monitor is narrower or has the same aspect ratio
            {
                _scaleRatio = (float)_monitorWidth / _androidDeviceWidth;
                float scaledAndroidHeight = _androidDeviceHeight * _scaleRatio;
                _offsetX = 0f;
                _offsetY = (_monitorHeight - scaledAndroidHeight) / 2f;
            }
            Log($"Calculated new mapping: Scale={_scaleRatio:F2}, OffsetX={_offsetX:F0}, OffsetY={_offsetY:F0}");
        }

        private void Log(string message)
        {
            Dispatcher.InvokeAsync(() => {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                LogText.Text += $"[{timestamp}] {message}\n";
                Console.WriteLine($"[{timestamp}] {message}");
                LogScrollViewer.ScrollToEnd();
            });
        }

        private string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    // This doesn't actually send data, it just determines the best outbound IP.
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString() ?? "Not found";
                }
            }
            catch (SocketException)
            {
                return "Not found (No network?)";
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PenInputInjector.Uninitialize();
            _udpPenReceiver?.StopListening();
        }
    }
}
