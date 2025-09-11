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
        private const int PORT = 54321; // 태블릿과 통신할 포트 번호
        private TcpListener tcpListener;

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
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 윈도우가 로드되면 비동기적으로 서버 시작
            GetMonitorInfo();
            Task.Run(() => StartServer());
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

        private async Task StartServer()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, PORT);
                tcpListener.Start();

                // UI 스레드에서 상태 및 로그 업데이트
                await Dispatcher.InvokeAsync(() =>
                {
                    string ipAddress = GetLocalIPAddress();
                    StatusText.Text = $"서버 시작됨. IP: {ipAddress}, Port: {PORT}\n연결 대기 중...";
                    Log($"서버가 IP {ipAddress} 포트 {PORT}에서 시작되었습니다.");
                });

                while (true)
                {
                    // 클라이언트의 연결을 비동기적으로 기다림
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();

                    // 클라이언트가 연결되면 UI 업데이트
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                        StatusText.Text = $"클라이언트 연결됨: {clientEndPoint.Address}";
                        LogText.Text = ""; // 새 클라이언트 연결 시 로그 초기화
                        Log($"클라이언트 연결 성공: {clientEndPoint.Address}");
                    });

                    // 연결된 클라이언트와의 통신을 위한 새 태스크 시작
                    _ = Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => Log($"서버 시작 오류: {ex.Message}"));
                MessageBox.Show($"서버 오류: {ex.Message}");
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            var clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                {
                    // 최초 연결 시 디바이스 정보 1회 수신
                    var deviceInfo = await reader.ReadLineAsync();
                    if (deviceInfo != null && deviceInfo.StartsWith("DEVICEINFO:"))
                    {
                        // 예시: DEVICEINFO:1920,1200,60
                        var infoParts = deviceInfo.Substring("DEVICEINFO:".Length).Split(',');
                        if (infoParts.Length == 3 &&
                            int.TryParse(infoParts[0], out androidDeviceWidth) &&
                            int.TryParse(infoParts[1], out androidDeviceHeight) &&
                            int.TryParse(infoParts[2], out androidDeviceFPS))
                        {
                            // 해상도 비율 계산
                            widthRatio = (float)monitorWidth / androidDeviceWidth;
                            heightRatio = (float)monitorHeight / androidDeviceHeight;

                            await Dispatcher.InvokeAsync(() =>
                                Log($"클라이언트 디바이스 정보 수신: Width={androidDeviceWidth}, Height={androidDeviceHeight}, FPS={androidDeviceFPS}")
                            );
                        }
                        else
                        {
                            await Dispatcher.InvokeAsync(() =>
                                Log($"디바이스 정보 파싱 실패: {deviceInfo}")
                            );
                            client.Close();
                            return;
                        }
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() =>
                            Log("디바이스 정보가 올바르지 않음. 연결 종료.")
                        );
                        client.Close();
                        return;
                    }
                    while (client.Connected)
                    {
                        // 클라이언트로부터 데이터를 비동기적으로 읽음
                        var data = await reader.ReadLineAsync();
                        if (data == null) break; // 클라이언트 연결 끊김

                        // 수신된 데이터를 파싱하여 펜 입력 처리 함수를 호출합니다.
                        ParseAndInjectPenInput(data);
                    }
                }
            }
            catch (Exception ex)
            {
                // 예외 처리 (클라이언트 연결 끊김 등)
                await Dispatcher.InvokeAsync(() => Log($"클라이언트({clientEndPoint.Address}) 처리 중 오류: {ex.Message}"));
            }
            finally
            {
                client.Close();
                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = "클라이언트 연결 끊김. 다시 연결 대기 중...";
                    Log($"클라이언트({clientEndPoint.Address}) 연결이 종료되었습니다.");
                });
            }
        }

        private void ParseAndInjectPenInput(string data)
        {
            try
            {
                // 데이터 형식: "ACTION:X,Y,Pressure" (예: "MOVE:123.45,678.90,0.543")
                var parts = data.Split(':');
                if (parts.Length != 2)
                {
                    Log($"데이터 형식 오류 (Invalid format): {data}");
                    return;
                }

                var action = parts[0].ToUpper();
                var coords = parts[1].Split(',');
                if (coords.Length != 3)
                {
                    Log($"데이터 형식 오류 (Invalid coords): {data}");
                    return;
                }

                // CultureInfo.InvariantCulture를 사용하여 소수점 파싱 오류 방지
                if (float.TryParse(coords[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(coords[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(coords[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float pressure))
                {
                    // DOWN, UP 이벤트만 로그를 남겨서 로그 창이 너무 빠르게 스크롤되는 것을 방지
                    if (action == "DOWN" || action == "UP")
                    {
                        Log($"입력 파싱: Action={action}, X={x:F2}, Y={y:F2}, Pressure={pressure:F3}");
                    }

                    // !!! 중요: 수신된 좌표를 윈도우 화면 해상도에 맞게 스케일링하는 로직이 여기에 필요합니다. !!!
                    int scaledX = (int)(x * widthRatio);
                    int scaledY = (int)(y * heightRatio);

                    if (action == "DOWN" || action == "UP")
                    {
                        Log($"스케일링된 좌표: X={scaledX}, Y={scaledY}");
                    }

                    // 펜 입력 주입 클래스 호출
                    switch (action)
                    {
                        case "DOWN": // ACTION_DOWN
                            PenInputInjector.InjectPenDown(scaledX, scaledY, pressure);
                            break;
                        case "MOVE": // ACTION_MOVE
                            PenInputInjector.InjectPenMove(scaledX, scaledY, pressure);
                            break;
                        case "UP": // ACTION_UP
                            PenInputInjector.InjectPenUp(scaledX, scaledY);
                            break;
                    }
                }
                else
                {
                    Log($"숫자 변환 오류: {data}");
                }
            }
            catch (Exception ex)
            {
                // 파싱 오류 발생 시 로그 기록
                Log($"파싱 및 주입 오류: {ex.Message}");
            }
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
        }
    }
}