using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows; // MessageBox를 사용하기 위해 추가 (UI 프로젝트)

namespace TabletLink_WindowsApp
{
    /// <summary>
    /// ADB의 자동 설치 및 포트 포워딩 기능을 관리하는 래퍼 클래스입니다.
    /// </summary>
    public class AdbManager
    {
        /// <summary>
        /// ADB가 설치될 기본 경로입니다. (예: 내 문서\MyWpfProject\platform-tools)
        /// </summary>
        private readonly string _installDir;

        /// <summary>
        /// adb.exe 파일의 전체 경로입니다.
        /// </summary>
        public string AdbPath { get; private set; }


        /// <summary>
        /// 현재 ADB 실행 파일이 경로에 존재하는지 여부를 반환합니다.
        /// </summary>
        public bool IsAdbAvailable => File.Exists(AdbPath);

        /// <summary>
        /// ADB Manager를 초기화합니다.
        /// </summary>
        /// <param name="projectName">ADB를 저장할 폴더 이름입니다.</param>
        public AdbManager(string projectName = "MyWpfProject")
        {
            // ADB를 저장할 경로 설정 (예: C:\Users\사용자\Documents\MyWpfProject)
            _installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), projectName);
            AdbPath = Path.Combine(_installDir, "platform-tools", "adb.exe");
        }

        /// <summary>
        /// ADB가 설치되어 있는지 확인하고, 없으면 자동으로 다운로드 및 설치를 진행합니다.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public async Task<bool> InitializeAsync()
        {
            if (File.Exists(AdbPath))
            {
                Debug.WriteLine($"ADB found at: {AdbPath}");
                return true;
            }

            var result = MessageBox.Show(
                "컴퓨터와 태블릿의 유선 연결을 위해 ADB(Android Debug Bridge)가 필요합니다.\n\n자동으로 다운로드하여 설정하시겠습니까?",
                "ADB 설치 필요",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                return await DownloadAndSetupAdbAsync();
            }

            return false;
        }

        /// <summary>
        /// 지정된 포트로 포트 포워딩을 시작합니다.
        /// </summary>
        /// <param name="port">포워딩할 포트 번호입니다.</param>
        /// <returns>성공 여부와 결과 메시지를 포함하는 튜플</returns>
        public async Task<(bool success, string message)> StartForwardingAsync(int port)
        {
            if (!File.Exists(AdbPath))
            {
                return (false, "ADB가 설치되어 있지 않습니다. 먼저 초기화를 진행해주세요.");
            }

            string arguments = $"forward tcp:{port} tcp:{port}";
            return await ExecuteAdbCommandAsync(arguments);
        }

        /// <summary>
        /// 모든 포트 포워딩을 중지합니다.
        /// </summary>
        /// <returns>성공 여부와 결과 메시지를 포함하는 튜플</returns>
        public async Task<(bool success, string message)> StopAllForwardingAsync()
        {
            if (!File.Exists(AdbPath))
            {
                return (false, "ADB가 설치되어 있지 않습니다.");
            }

            return await ExecuteAdbCommandAsync("forward --remove-all");
        }

        private async Task<bool> DownloadAndSetupAdbAsync()
        {
            // Google 공식 다운로드 URL (Windows 용)
            const string downloadUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";
            string zipPath = Path.Combine(Path.GetTempPath(), "platform-tools.zip");

            try
            {
                // 1. 다운로드
                MessageBox.Show("ADB 다운로드를 시작합니다...", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                // 2. 압축 해제
                if (Directory.Exists(Path.Combine(_installDir, "platform-tools")))
                {
                    Directory.Delete(Path.Combine(_installDir, "platform-tools"), true);
                }
                ZipFile.ExtractToDirectory(zipPath, _installDir);

                if (File.Exists(AdbPath))
                {
                    MessageBox.Show($"ADB 설치가 완료되었습니다.\n경로: {AdbPath}", "설치 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ADB 자동 설치에 실패했습니다.\n오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
            return false;
        }

        private async Task<(bool success, string message)> ExecuteAdbCommandAsync(string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = AdbPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas" // 관리자 권한으로 실행 (필요시)
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                process.Start();

                // 비동기적으로 출력과 에러를 읽어옵니다.
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await Task.Run(() => process.WaitForExit()); // 백그라운드 스레드에서 대기

                if (process.ExitCode == 0)
                {
                    return (true, string.IsNullOrWhiteSpace(output) ? "명령이 성공적으로 실행되었습니다." : output);
                }
                else
                {
                    return (false, $"ADB 오류: {error}\n(Output: {output})");
                }
            }
        }
    }
}
