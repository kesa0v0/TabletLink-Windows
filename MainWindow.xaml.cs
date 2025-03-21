using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TabletLink_WindowsApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Callback 델리게이트 정의
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FrameCallback(IntPtr data, int width, int height);

        [DllImport("ScreenCaptureLib.dll")]
        public static extern void StartCapture(FrameCallback frameCallback);

        [DllImport("ScreenCaptureLib.dll")]
        public static extern void StopCapture();

        [DllImport("ScreenCaptureLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr TestDLL();



        private WriteableBitmap bitmap;
        public bool isCapturing = false;


        private FrameCallback frameCallbackDelegate;

        public MainWindow()
        {
            InitializeComponent();

            frameCallbackDelegate = new FrameCallback(frameCallback);
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

        // 버튼 입력
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string buttonName = button.Name;
            string? buttonText = button.Content.ToString();
            UpdateStatusText($"Button {buttonName} clicked: {buttonText}");

            if (isCapturing)
            {
                StopCapture();
                isCapturing = false;
            }
            else
            {
                StartCapture();
                isCapturing = true;
            }
        }

        // 캡처 데이터를 화면에 업데이트하는 함수
        private void UpdateCaptureImage(byte[] frameData, int width, int height)
        {
            if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
            {
                // WriteableBitmap 초기화
                bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                CaptureImage.Source = bitmap;
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
            StartCapture(frameCallbackDelegate);
            Console.WriteLine("StartCapture called");
        }


        // Callback 함수 구현
        void frameCallback(IntPtr data, int width, int height)
        {
            byte[] frameData = new byte[width * height * 4]; // RGBA 포맷 가정
            Marshal.Copy(data, frameData, 0, frameData.Length);

            Console.WriteLine($"Frame captured: {width}x{height}");

            // UI 쓰레드에서 이미지를 업데이트
            this.Dispatcher.Invoke(() =>
            {
                UpdateCaptureImage(frameData, width, height);
            });
        }
    }
}