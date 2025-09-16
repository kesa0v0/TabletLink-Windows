using OpenTabletDriver.Native.Posix;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;


namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        private UdpPenReceiver udpPenReceiver;

        private int androidDeviceWidth = 0;
        private int androidDeviceHeight = 0;
        private int androidDeviceFPS = 0;

        private int monitorWidth = 0;
        private int monitorHeight = 0;

        private float widthRatio = 1.0f;
        private float heightRatio = 1.0f;

        public MainWindow()
        {
            InitializeComponent();

            PenInputInjector.Initialize();

            udpPenReceiver = new UdpPenReceiver();
            udpPenReceiver.OnDeviceInfoReceived += UdpPenReceiver_OnDeviceInfoReceived;
            udpPenReceiver.OnPenDataReceived += UdpPenReceiver_OnPenDataReceived;
            Log($"로컬 IP 주소: {GetLocalIPAddress()}");
            udpPenReceiver.StartListening(9999);
        }

        private void UdpPenReceiver_OnDeviceInfoReceived(int width, int height)
        {
            Dispatcher.Invoke(() =>
            {
                androidDeviceWidth = width;
                androidDeviceHeight = height;
                widthRatio = (float)monitorWidth / androidDeviceWidth;
                heightRatio = (float)monitorHeight / androidDeviceHeight;
                Log($"[UDP] 기기 해상도 수신: {width} x {height}");
            });
        }


        private void UdpPenReceiver_OnPenDataReceived(UdpPenData data)
        {
            // 스케일링
            int scaledX = (int)(data.X * widthRatio);
            int scaledY = (int)(data.Y * heightRatio);

            // PenInputInjector 호출 (action: 0=Down, 1=Move, 2=Up, 3=Hover)
            switch (data.Action)
            {
                case 0:
                    PenInputInjector.InjectPenDown(scaledX, scaledY, data.Pressure, data.IsBarrelPressed, data.TiltX, data.TiltY);
                    break;
                case 1:
                    PenInputInjector.InjectPenMove(scaledX, scaledY, data.Pressure, data.IsBarrelPressed, data.TiltX, data.TiltY);
                    break;
                case 2:
                    PenInputInjector.InjectPenUp(scaledX, scaledY);
                    break;
                case 3:
                    PenInputInjector.InjectPenHover(scaledX, scaledY, data.TiltX, data.TiltY);
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 윈도우가 로드되면 비동기적으로 서버 시작
            GetMonitorInfo();
        }

        private void GetMonitorInfo()
        {
            // 1. WPF가 인식하는 논리적 화면 크기
            double logicalScreenWidth = SystemParameters.PrimaryScreenWidth;
            double logicalScreenHeight = SystemParameters.PrimaryScreenHeight;

            // 2. 현재 창의 DPI 스케일링 값 얻기
            PresentationSource source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // 3. 실제 물리적 픽셀 해상도 계산
            double actualScreenWidth = logicalScreenWidth * dpiX;
            double actualScreenHeight = logicalScreenHeight * dpiY;

            // 결과 출력
            Log(
                $"논리 해상도: {logicalScreenWidth} x {logicalScreenHeight}\n" +
                $"DPI 배율: {dpiX * 100}%\n" +
                $"실제 해상도: {actualScreenWidth} x {actualScreenHeight}"
            );
            // 아마도 "실제 해상도: 2880 x 1800" 라고 출력될 것입니다.

            monitorWidth = (int)actualScreenWidth;
            monitorHeight = (int)actualScreenHeight;

        }



        // 로그 기록을 위한 헬퍼 함수
        private void Log(string message)
        {
            // UI 스레드에서 안전하게 LogText 업데이트
            Dispatcher.InvokeAsync(() => {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                LogText.Text += $"[{timestamp}] {message}\n";
                Console.WriteLine($"[{timestamp}] {message}");
                LogScrollViewer.ScrollToEnd(); // 스크롤을 항상 맨 아래로 이동
            });
        }

        // 로컬 IP 주소를 가져오는 도우미 함수
        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "IP를 찾을 수 없음";
        }
        // 애플리케이션 종료 시 (예: MainWindow_Closing 이벤트)
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PenInputInjector.Uninitialize();
            udpPenReceiver?.StopListening();
        }
    }
}