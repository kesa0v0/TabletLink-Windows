using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;

namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        private const int PORT = 54321; // 태블릿과 통신할 포트 번호
        private TcpListener tcpListener;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 윈도우가 로드되면 비동기적으로 서버 시작
            Task.Run(() => StartServer());
        }

        private async Task StartServer()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, PORT);
                tcpListener.Start();

                // UI 스레드에서 상태 업데이트
                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = $"서버 시작됨. IP: {GetLocalIPAddress()}, Port: {PORT}\n연결 대기 중...";
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
                        LogText.Text = ""; // 로그 초기화
                    });

                    // 연결된 클라이언트와의 통신을 위한 새 태스크 시작
                    _ = Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서버 오류: {ex.Message}");
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                {
                    while (client.Connected)
                    {
                        // 클라이언트로부터 데이터를 비동기적으로 읽음
                        var data = await reader.ReadLineAsync();
                        if (data == null) break; // 클라이언트 연결 끊김

                        // UI 스레드에서 로그 업데이트
                        await Dispatcher.InvokeAsync(() =>
                        {
                            LogText.Text += data + "\n";
                        });

                        // TODO: 여기서 수신된 데이터를 파싱하여 펜 입력 처리 함수를 호출해야 합니다.
                        // 예: ParseAndInjectPenInput(data);
                    }
                }
            }
            catch (Exception)
            {
                // 예외 처리 (클라이언트 연결 끊김 등)
            }
            finally
            {
                client.Close();
                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = "클라이언트 연결 끊김. 다시 연결 대기 중...";
                });
            }
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
    }
}