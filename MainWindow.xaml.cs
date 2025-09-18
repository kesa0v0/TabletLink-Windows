using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;

namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        private UdpPenReceiver udpPenReceiver;
        private UdpScreenSender udpScreenSender;

        private const int PenPort = 9999;      // �� �Է� ��Ʈ (A->PC)
        private const int ScreenPort = 9998;   // ȭ�� ���� ��Ʈ (PC->A)

        private int androidDeviceWidth = 0;
        private int androidDeviceHeight = 0;
        private int monitorWidth = 0;
        private int monitorHeight = 0;
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
                Logger.Log($"ERROR: Failed to initialize PenInputInjector: {ex.Message}");
                MessageBox.Show($"Fatal Error: Could not initialize Pen Input.\n{ex.Message}\nThe application will now close.", "Initialization Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }


            udpPenReceiver = new UdpPenReceiver();
            udpPenReceiver.OnDeviceInfoReceived += UdpPenReceiver_OnDeviceInfoReceived;
            udpPenReceiver.OnPenDataReceived += UdpPenReceiver_OnPenDataReceived;
            udpPenReceiver.OnConnectionLost += UdpPenReceiver_OnConnectionLost;

            udpScreenSender = new UdpScreenSender(); // ȭ�� ���۱� �ʱ�ȭ

            Logger.Log($"Local IP Address: {GetLocalIPAddress()}");
            udpPenReceiver.StartListening(PenPort);

            StatusText.Text = "����: �ʱ�ȭ �Ϸ�";
            Logger.Log("Application started.");
            Logger.Log($"Listening for Pen UDP packets on port {PenPort}.");
        }

        private void UdpPenReceiver_OnDeviceInfoReceived(int width, int height, IPEndPoint androidEndpoint)
        {
            Dispatcher.Invoke(() =>
            {
                androidDeviceWidth = width;
                androidDeviceHeight = height;
                Logger.Log($"[UDP] Device info received: {width} x {height}");
                CalculateMapping();
                // ȭ�� ���� ����
                // �ȵ���̵� ����� IP �ּҿ� ������ ȭ�� ��Ʈ�� EndPoint�� �����մϴ�.
                var screenTargetEndpoint = new IPEndPoint(androidEndpoint.Address, ScreenPort);
                udpScreenSender.StartStreaming(screenTargetEndpoint);

                StatusText.Text = $"����: ����� ({androidEndpoint.Address})";
            });
        }

        // ������ Ÿ�Ӿƿ� ������ ������ �� ȣ��˴ϴ�.
        private void UdpPenReceiver_OnConnectionLost()
        {
            Dispatcher.Invoke(() =>
            {
                Logger.Log("Connection to Android device lost.");
                udpScreenSender.StopStreaming();
                StatusText.Text = $"����: ���� ����. �翬�� ��� ��...\nPC IP: {GetLocalIPAddress()}";
                androidDeviceWidth = 0;
                androidDeviceHeight = 0;
            });
        }


        private void UdpPenReceiver_OnPenDataReceived(UdpPenData data)
        {
            if (androidDeviceWidth == 0) return; // ���� ������� ����

            Logger.Log($"[UDP RECV] Action:{data.Action}, X:{data.X:F2}, Y:{data.Y:F2}, P:{data.Pressure:F2}, Barrel:{data.IsBarrelPressed}, TiltX:{data.TiltX}, TiltY:{data.TiltY}");

            int scaledX = (int)(data.X * scale + offsetX);
            int scaledY = (int)(data.Y * scale + offsetY);

            switch (data.Action)
            {
                case 0: // Down
                    PenInputInjector.InjectPenDown(scaledX, scaledY, data.Pressure, data.IsBarrelPressed,
                        (int)data.TiltX, (int)data.TiltY); // <--- (int)�� ����� �� ��ȯ
                    break;
                case 1: // Move
                    PenInputInjector.InjectPenMove(scaledX, scaledY, data.Pressure, data.IsBarrelPressed,
                        (int)data.TiltX, (int)data.TiltY); // <--- (int)�� ����� �� ��ȯ
                    break;
                case 2: // Up
                    PenInputInjector.InjectPenUp(scaledX, scaledY);
                    break;
                case 3: // Hover
                    PenInputInjector.InjectPenHover(scaledX, scaledY, data.IsBarrelPressed,
                        (int)data.TiltX, (int)data.TiltY); // <--- (int)�� ����� �� ��ȯ
                    break;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
            Logger.Log($"Monitor Resolution: {monitorWidth} x {monitorHeight}");
        }

        private void CalculateMapping()
        {
            if (androidDeviceWidth == 0 || androidDeviceHeight == 0 || monitorWidth == 0 || monitorHeight == 0) return;

            float monitorAspectRatio = (float)monitorWidth / monitorHeight;
            float tabletAspectRatio = (float)androidDeviceWidth / androidDeviceHeight;

            if (monitorAspectRatio > tabletAspectRatio)
            {
                scale = (float)monitorHeight / androidDeviceHeight;
                offsetX = (monitorWidth - (androidDeviceWidth * scale)) / 2f;
                offsetY = 0f;
            }
            else
            {
                scale = (float)monitorWidth / androidDeviceWidth;
                offsetY = (monitorHeight - (androidDeviceHeight * scale)) / 2f;
                offsetX = 0f;
            }
            Logger.Log($"Calculated Mapping: Scale={scale:F4}, OffsetX={offsetX:F2}, OffsetY={offsetY:F2}");

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
                return "127.0.0.1";
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PenInputInjector.Uninitialize();
            udpPenReceiver?.StopListening();
            udpScreenSender?.StopStreaming();
            Logger.Log("Application closing.");
        }
    }
}

public static class Logger
{
    public static void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string formattedMessage = $"[{timestamp}] {message}\n";
        // �ʿ信 ���� �α� ��� ��ġ�� �����ϼ���.
        Console.Write(formattedMessage);
    }
}