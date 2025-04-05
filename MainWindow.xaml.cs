using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;


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

        protected override void OnClosing(CancelEventArgs e)
        {
            StopCapture();
            server.CloseServer();
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
                //StartCapture(frameCallbackInstance, 1920, 1080, 1);
                isCapturing = true;
                sendTestData();
            }
            else
            {
                server.CloseServer();
                //StopCapture();
                isCapturing = false;
            }
        }

        public void sendTestData()
        {
            // 비동기적으로 1초마다 데이터 전송

            Task.Run(async () =>
            {
                while (isCapturing)
                {
                    FrameData testData = new FrameData();
                    testData.dataSize = 10 * 10 * 4;
                    testData.data = Marshal.AllocHGlobal(testData.dataSize);
                    testData.width = 10;
                    testData.height = 10;
                    testData.frameRate = 1;
                    testData.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    server.SendData(StructToBytes(testData));
                    await Task.Delay(1000);
                }
            });

        }
        public static int ReverseBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static long ReverseBytes(long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }
        // 엔디언 변환 함수
        public static int SwapEndian(int value) => BitConverter.IsLittleEndian ? ReverseBytes(value) : value;
        public static long SwapEndian(long value) => BitConverter.IsLittleEndian ? ReverseBytes(value) : value;

        public static byte[] StructToBytes(FrameData frameData)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(SwapEndian(frameData.width));
                writer.Write(SwapEndian(frameData.height));
                writer.Write(SwapEndian(frameData.frameRate));
                writer.Write(SwapEndian(frameData.dataSize));
                writer.Write(SwapEndian(frameData.timestamp));

                if (frameData.data != IntPtr.Zero && frameData.dataSize > 0)
                {
                    byte[] dataArray = new byte[frameData.dataSize];
                    Marshal.Copy(frameData.data, dataArray, 0, frameData.dataSize);
                    writer.Write(dataArray);
                }
                return stream.ToArray();
            }

        }

        // Callback 함수 구현
        void frameCallback(FrameData frameData)
        {
            if(isCapturing == false)
                return;
            

            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 네이티브 측 타임스탬프 읽기
            byte[] frameDataArray = new byte[frameData.dataSize];
            Marshal.Copy(frameData.data, frameDataArray, 0, frameDataArray.Length);

            long nativeTimestamp = frameData.timestamp; // 네이티브 측 타임스탬프
            long delay = currentTime - nativeTimestamp; // C++ → C# 전달 지연 시간
            int datasize = frameDataArray.Length;

            // FPS 카운트 증가
            frameCount++;
            if (stopwatch.ElapsedMilliseconds >= 1000)
            {
                UpdateStatusText($"FPS: {frameCount}/{frameData.frameRate}, delay: {delay}, datasize: {datasize}");
                frameCount = 0;
                stopwatch.Restart();
            }

            server.SendData(StructToBytes(frameData));
        }

    }

    #endregion
}