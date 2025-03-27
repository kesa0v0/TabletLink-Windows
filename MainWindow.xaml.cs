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
        public bool isCapturing = false;
        UDPServer server = new UDPServer();

        public MainWindow()
        {
            InitializeComponent();

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

        protected override void OnClosing(CancelEventArgs e)
        {
            //server.CloseServer();
            //StopCapture();
            base.OnClosing(e);
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
                //server.StartServer();
                CStartCapture();
                isCapturing = true;
            }
            else
            {
                //server.CloseServer();
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


        private void CStartCapture()
        {
            Console.WriteLine("Capturing started...");

            frameCallbackInstance ??= new FrameCallback(frameCallback);
            StartCapture(frameCallbackInstance);
            Console.WriteLine("StartCapture called");
        }

        public static byte[] StructToBytes(FrameData frameData)
        {
            int size = Marshal.SizeOf(frameData);
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(frameData, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return bytes;
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
                //server.SendData(StructToBytes(frameData));
                // send data to Android with Wi-fi Direct and WebRTC
            });
        }
        #endregion

    }
}