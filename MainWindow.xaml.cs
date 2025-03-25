using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;



namespace TabletLink_WindowsApp
{
    public partial class MainWindow : Window
    {
        public bool isCapturing = false;

        public MainWindow()
        {
            InitializeComponent();
            stopwatch.Start(); // 시간 측정 시작

            frameCallbackInstance = new FrameCallback(frameCallback);

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
                StartCapture();
                isCapturing = true;
            }
            else
            {
                StopCapture();
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


            Console.WriteLine("StartCapture");
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
    }
}