using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;

namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        private readonly AdbManager adbManager = new AdbManager("TabletLink_WindowsApp");
        private UdpPenReceiver udpPenReceiver;

        private int androidDeviceWidth = 0;
        private int androidDeviceHeight = 0;

        private int monitorWidth = 0;
        private int monitorHeight = 0;

        // For aspect ratio correction
        private float scale = 1.0f;
        private float offsetX = 0f;
        private float offsetY = 0f;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                PenInputInjector.Initialize();
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to initialize PenInputInjector: {ex.Message}");
                MessageBox.Show($"Fatal Error: Could not initialize Pen Input.\n{ex.Message}\nThe application will now close.", "Initialization Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            IsWiredConnection.Checked += ActivateWiredConnection_Checked;
            IsWiredConnection.Unchecked += ActivateWiredConnection_Unchecked;

            udpPenReceiver = new UdpPenReceiver();
            udpPenReceiver.OnDeviceInfoReceived += UdpPenReceiver_OnDeviceInfoReceived;
            udpPenReceiver.OnPenDataReceived += UdpPenReceiver_OnPenDataReceived;
            Log($"Local IP Address: {GetLocalIPAddress()}");
            udpPenReceiver.StartListening(9999);

            StatusText.Text = "상태: 초기화 완료";
            Log("Application started.");
            Log("Listening for UDP packets on port 9999.");
            Log("Waiting for device info...");
        }

        private async void ActivateWiredConnection_Checked(object sender, RoutedEventArgs e)
        {
            if (!adbManager.IsAdbAvailable)
            {
                MessageBox.Show("ADB가 설치되어 있지 않습니다. 유선 연결을 사용할 수 없습니다.", "ADB Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                IsWiredConnection.IsChecked = false;
                return;
            }
            bool success;
            string msg;
            (success, msg) = await adbManager.StartForwardingAsync(9999);
            if (success)
            {
                StatusText.Text = "상태: 유선 연결 활성화됨";
                Log($"ADB port forwarding started on port 9999. Message: {msg}");
            }
            else
            {
                StatusText.Text = "상태: 유선 연결 활성화 실패";
                Log($"ERROR: {msg}");
                IsWiredConnection.IsChecked = false;
            }
        }

        private async void ActivateWiredConnection_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!adbManager.IsAdbAvailable)
            {
                return;
            }
            bool success;
            string msg;

            (success, msg) = await adbManager.StopAllForwardingAsync();
            if (success)
            {
                StatusText.Text = "상태: 유선 연결 비활성화됨";
                Log($"ADB port forwarding stopped. Message: {msg}");
            }
            else
            {
                StatusText.Text = "상태: 유선 연결 비활성화 실패";
                Log($"ERROR: {msg}");
                IsWiredConnection.IsChecked = true;
            }
        }



        private void UdpPenReceiver_OnDeviceInfoReceived(int width, int height)
        {
            Dispatcher.Invoke(() =>
            {
                androidDeviceWidth = width;
                androidDeviceHeight = height;
                Log($"[UDP] Device info received: {width} x {height}");
                CalculateMapping();
            });
        }

        private void UdpPenReceiver_OnPenDataReceived(UdpPenData data)
        {
            Log($"[UDP RECV] Action:{data.Action}, X:{data.X:F2}, Y:{data.Y:F2}, P:{data.Pressure:F2}, Barrel:{data.IsBarrelPressed}, TiltX:{data.TiltX}, TiltY:{data.TiltY}");

            int scaledX = (int)(data.X * scale + offsetX);
            int scaledY = (int)(data.Y * scale + offsetY);

            switch (data.Action)
            {
                case 0: // Down
                    PenInputInjector.InjectPenDown(scaledX, scaledY, data.Pressure, data.IsBarrelPressed,
                        (int)data.TiltX, (int)data.TiltY); // <--- (int)로 명시적 형 변환
                    break;
                case 1: // Move
                    PenInputInjector.InjectPenMove(scaledX, scaledY, data.Pressure, data.IsBarrelPressed,
                        (int)data.TiltX, (int)data.TiltY); // <--- (int)로 명시적 형 변환
                    break;
                case 2: // Up
                    PenInputInjector.InjectPenUp(scaledX, scaledY);
                    break;
                case 3: // Hover
                    PenInputInjector.InjectPenHover(scaledX, scaledY, data.IsBarrelPressed,
                        (int)data.TiltX, (int)data.TiltY); // <--- (int)로 명시적 형 변환
                    break;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {

            bool isReady = await adbManager.InitializeAsync();
            if (isReady)
            {
                StatusText.Text = "상태: ADB 준비 완료";
            }
            else
            {
                StatusText.Text = "상태: ADB 초기화 실패. 수동 설치가 필요합니다.";
            }

            GetMonitorInfo();
            CalculateMapping();
        }

        private void GetMonitorInfo()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                double dpiX = m.M11;
                double dpiY = m.M22;
                monitorWidth = (int)(SystemParameters.PrimaryScreenWidth * dpiX);
                monitorHeight = (int)(SystemParameters.PrimaryScreenHeight * dpiY);
            }
            else // Fallback
            {
                monitorWidth = (int)SystemParameters.PrimaryScreenWidth;
                monitorHeight = (int)SystemParameters.PrimaryScreenHeight;
            }
            Log($"Monitor Resolution: {monitorWidth} x {monitorHeight}");
        }

        private void CalculateMapping()
        {
            if (androidDeviceWidth == 0 || androidDeviceHeight == 0 || monitorWidth == 0 || monitorHeight == 0)
            {
                return;
            }

            float monitorAspectRatio = (float)monitorWidth / monitorHeight;
            float tabletAspectRatio = (float)androidDeviceWidth / androidDeviceHeight;

            if (monitorAspectRatio > tabletAspectRatio)
            {
                // Monitor is wider (letterbox)
                scale = (float)monitorHeight / androidDeviceHeight;
                float scaledTabletWidth = androidDeviceWidth * scale;
                offsetX = (monitorWidth - scaledTabletWidth) / 2f;
                offsetY = 0f;
            }
            else
            {
                // Tablet is wider or same aspect ratio (pillarbox)
                scale = (float)monitorWidth / androidDeviceWidth;
                float scaledTabletHeight = androidDeviceHeight * scale;
                offsetX = 0f;
                offsetY = (monitorHeight - scaledTabletHeight) / 2f;
            }

            Log($"Calculated Mapping: Scale={scale:F4}, OffsetX={offsetX:F2}, OffsetY={offsetY:F2}");
        }

        private void Log(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => Log(message));
                return;
            }
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formattedMessage = $"[{timestamp}] {message}\n";
            //LogText.AppendText(formattedMessage);
            Console.Write(formattedMessage); // Also write to console
        }

        private string GetLocalIPAddress()
        {
            try
            {
                using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "Not found";
            }
            catch (SocketException)
            {
                return "Not found (no network)";
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PenInputInjector.Uninitialize();
            udpPenReceiver?.StopListening();
        }
    }
}

