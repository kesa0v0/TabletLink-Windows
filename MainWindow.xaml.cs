using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        public bool isCapturing = false;

        public MainWindow()
        {
            InitializeComponent();

            frameCallbackInstance = new FrameCallback(frameCallback);
            UDPServer server = new UDPServer();

        }

        #region UI

        public void UpdateStatusText(string text)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.StatusText.Text = text;
            });
        }

        public void CloseWindow(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public void MinimizeWindow(object sender, RoutedEventArgs e)
        {

        }

        public void OpenSettings(object sender, RoutedEventArgs e)
        {

        }

        public void Window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            this.DragMove();
        }

        // 버튼 입력
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string buttonName = button.Name;
            string? buttonText = button.Content.ToString();
            UpdateStatusText($"Button {buttonName} clicked: {buttonText}");

            if (!isCapturing)
            {
                Task.Factory.StartNew(() =>
                {
                    server.StartServer();
                });
                //StartCapture();
                isCapturing = true;
            }
            else
            {
                //StopCapture();
                isCapturing = false;
            }
        }

        #endregion


        #region DLL

        [DllImport("ScreenCaptureLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void StartCapture(FrameCallback frameCallback);
        [DllImport("ScreenCaptureLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void StopCapture();

        [StructLayout(LayoutKind.Sequential)]
        public struct FrameData
        {
            public IntPtr data;
            public int size;
            public int width;
            public int height;
            public long timestamp;
        }

        // Callback 델리게이트 정의
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FrameCallback(FrameData frameData);
        private static FrameCallback? frameCallbackInstance;


        private void StartCapture()
        {
            UpdateStatusText("Capturing started...");

            frameCallbackInstance ??= new FrameCallback(frameCallback);
            StartCapture(frameCallbackInstance);
            Console.WriteLine("StartCapture called");
        }

        // Callback 함수 구현
        void frameCallback(FrameData frameData)
        {
            // C# 현재 시간 측정
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 네이티브 측 타임스탬프 읽기
            byte[] frameDataArray = new byte[frameData.width * frameData.height * 4];
            Marshal.Copy(frameData.data, frameDataArray, 0, frameDataArray.Length);

            long nativeTimestamp = frameData.timestamp; // 네이티브 측 타임스탬프
            long delay = currentTime - nativeTimestamp; // C++ → C# 전달 지연 시간

            // UI 쓰레드에서 이미지 업데이트
            this.Dispatcher.Invoke(() =>
            {
                // send data to Android with Wi-fi Direct and WebRTC
            });
        }
        #endregion


        #region Network

        UDPServer server = new UDPServer();

        class UDPServer
        {
            public void StartServer()
            {
                IPAddress ip = IPAddress.Any;
                UdpClient udpServer = new UdpClient(12345); // 포트 12345로 바인딩
                IPEndPoint remoteEP = new IPEndPoint(ip, 0);

                Console.WriteLine("UDP 서버 실행 중...");
                Console.WriteLine($"IP:{ip}");
                
                try
                {
                    while (true)
                    {
                        byte[] receivedData = udpServer.Receive(ref remoteEP);
                        string receivedMessage = Encoding.UTF8.GetString(receivedData);
                        Console.WriteLine($"클라이언트({remoteEP.Address}): {receivedMessage}");

                        // 응답 메시지 전송
                        byte[] responseData = Encoding.UTF8.GetBytes("Hello from WPF!");
                        udpServer.Send(responseData, responseData.Length, remoteEP);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                finally
                {
                    udpServer.Close();
                }

                Console.ReadLine();
            }
        }

        #endregion
    }
}