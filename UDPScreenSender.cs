using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TabletLink_WindowsApp
{
    /// <summary>
    /// 화면을 캡처하여 UDP로 전송하는 클래스입니다.
    /// </summary>
    public class UdpScreenSender
    {
        private UdpClient udpClient;
        private CancellationTokenSource cancellationTokenSource;

        // JPEG 압축 품질 설정 (0-100)
        private const long JpegQuality = 50L;
        private const int TargetFps = 30;

        /// <summary>
        /// 화면 전송을 시작합니다.
        /// </summary>
        /// <param name="targetEndpoint">전송할 대상(안드로이드 기기)의 IPEndPoint입니다.</param>
        public void StartStreaming(IPEndPoint targetEndpoint)
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                Debug.WriteLine("Screen streaming is already running.");
                return;
            }

            udpClient = new UdpClient();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            Task.Run(() => StreamingLoop(targetEndpoint, token), token);
            Debug.WriteLine($"Screen streaming started to {targetEndpoint}.");
        }

        public void StopStreaming()
        {
            cancellationTokenSource?.Cancel();
            udpClient?.Close();
            Debug.WriteLine("Screen streaming stopped.");
        }

        private async Task StreamingLoop(IPEndPoint targetEndpoint, CancellationToken token)
        {
            var frameInterval = 1000 / TargetFps;
            var stopwatch = new Stopwatch();

            // DPI 스케일링을 고려한 실제 화면 해상도를 가져옵니다.
            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            using (var bmp = new Bitmap(screenWidth, screenHeight))
            using (var gfx = Graphics.FromImage(bmp))
            {
                while (!token.IsCancellationRequested)
                {
                    stopwatch.Restart();
                    try
                    {
                        // 1. 화면 캡처
                        gfx.CopyFromScreen(0, 0, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

                        // 2. JPEG으로 압축하여 byte 배열로 변환
                        byte[] imageData = CompressImageToJpeg(bmp);

                        // 3. UDP로 전송 (65507 바이트 크기 제한 체크)
                        if (imageData.Length < 65507)
                        {
                            await udpClient.SendAsync(imageData, imageData.Length, targetEndpoint);
                        }
                        else
                        {
                            Debug.WriteLine($"Frame dropped, size too large: {imageData.Length} bytes");
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // StopStreaming 호출 시 정상적으로 발생할 수 있음
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Screen streaming error: {ex.Message}");
                    }

                    // 4. FPS 조절
                    var elapsed = stopwatch.ElapsedMilliseconds;
                    var delay = (int)(frameInterval - elapsed);
                    if (delay > 0)
                    {
                        await Task.Delay(delay, token);
                    }
                }
            }
        }

        private byte[] CompressImageToJpeg(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                var myEncoderParameters = new EncoderParameters(1);
                var myEncoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
                myEncoderParameters.Param[0] = myEncoderParameter;

                bmp.Save(ms, jpgEncoder, myEncoderParameters);
                return ms.ToArray();
            }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}
