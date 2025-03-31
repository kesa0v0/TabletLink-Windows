using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.ComponentModel;



namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        [DllImport("ScreenCaptureLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void StartCapture(FrameCallback frameCallback, int frameWidth, int frameHeight, int frameRate);
        [DllImport("ScreenCaptureLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void StopCapture();
        [DllImport("ScreenCaptureLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr TestDLL();

        [StructLayout(LayoutKind.Sequential)]
        public struct FrameData
        {
            public IntPtr data;
            public int width;
            public int height;
            public int frameRate;

            public int dataSize;
            public long timestamp;
        }

        // Callback 델리게이트 정의
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FrameCallback(FrameData frameData);
        private static FrameCallback? frameCallbackInstance;
        UDPServer server = new UDPServer();

        private Stopwatch stopwatch = new Stopwatch(); // 시간 측정을 위한 스톱워치
        private int frameCount = 0; // 초당 프레임 수 카운트


        private WriteableBitmap? bitmap;
        public bool isCapturing = false;


        public MainWindow()
        {
            InitializeComponent();
            stopwatch.Start(); // 시간 측정 시작

            frameCallbackInstance = new FrameCallback(frameCallback);

        }

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

        protected override void OnClosing(CancelEventArgs e)
        {
            StopCapture();
            base.OnClosing(e);
        }

        // 버튼 입력
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string buttonName = button.Name;
            string? buttonText = button.Content.ToString();

            if (!isCapturing)
            {
                server.StartServer();
                StartCapture(frameCallbackInstance, 1920, 1080, 30);
                isCapturing = true;
            }
            else
            {
                server.CloseServer();
                StopCapture();
                isCapturing = false;
            }
        }

        // Callback 함수 구현
        void frameCallback(FrameData frameData)
        {
            if(isCapturing == false)
                return;
            

            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 네이티브 측 타임스탬프 읽기
            byte[] frameDataArray = new byte[frameData.width * frameData.height * 4];
            Marshal.Copy(frameData.data, frameDataArray, 0, frameDataArray.Length);

            long nativeTimestamp = frameData.timestamp; // 네이티브 측 타임스탬프
            long delay = currentTime - nativeTimestamp; // C++ → C# 전달 지연 시간

            // FPS 카운트 증가
            frameCount++;
            if (stopwatch.ElapsedMilliseconds >= 1000)
            {
                UpdateStatusText($"FPS: {frameCount}, delay: {delay}");
                frameCount = 0;
                stopwatch.Restart();
            }

            // UI 쓰레드에서 이미지 업데이트
            this.Dispatcher.Invoke(() =>
            {

            });
        }

    }
}