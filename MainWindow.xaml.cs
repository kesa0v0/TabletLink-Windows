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
        [DllImport("ScreenCaptureLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void StartCapture(FrameCallback frameCallback);
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
            public long timestamp;
        }

        // Callback 델리게이트 정의
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FrameCallback(FrameData frameData);
        private static FrameCallback? frameCallbackInstance;

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

        // 캡처 데이터를 화면에 업데이트하는 함수
        private void UpdateCaptureImage(byte[] frameData, int width, int height)
        {
            //Console.WriteLine($"Frame captured: {width}x{height}");
            if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
            {
                // WriteableBitmap 초기화
                Console.WriteLine("Creating new WriteableBitmap");
                bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                CaptureImage.Source = bitmap;
            }
            if (frameData.Length != width * height * 4)
            {
                Console.WriteLine($"Unexpected frame data size: {frameData.Length}. Expected: {width * height * 4}");
                return;
            }

            bitmap.Lock();
            Marshal.Copy(frameData, 0, bitmap.BackBuffer, frameData.Length);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            bitmap.Unlock();
        }


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
                UpdateCaptureImage(frameDataArray, frameData.width, frameData.height);
            });
        }
    }
}