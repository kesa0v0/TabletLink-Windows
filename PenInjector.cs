using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace TabletLink_WindowsApp
{
    public static class PenInjector
    {
        // Windows API 함수 및 구조체 정의
        // SetLastError=true를 추가하여 Win32 오류 코드를 가져올 수 있도록 설정
        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool InjectTouchInput([In] POINTER_PEN_INFO[] penInfo, int count);

        #region WinAPI Enums and Structs
        // 필요한 상수, 열거형, 구조체들을 정의합니다.
        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-pointer_pen_info

        // 포인터 입력 타입
        public enum POINTER_INPUT_TYPE : int
        {
            PT_POINTER = 0x00000001,
            PT_TOUCH = 0x00000002,
            PT_PEN = 0x00000003,
            PT_MOUSE = 0x00000004,
        }

        // 포인터 플래그
        [Flags]
        public enum POINTER_FLAGS : uint
        {
            POINTER_FLAG_NONE = 0x00000000,
            POINTER_FLAG_NEW = 0x00000001,
            POINTER_FLAG_INRANGE = 0x00000002,
            POINTER_FLAG_INCONTACT = 0x00000004,
            POINTER_FLAG_FIRSTBUTTON = 0x00000010,
            POINTER_FLAG_SECONDBUTTON = 0x00000020,
            POINTER_FLAG_THIRDBUTTON = 0x00000040,
            POINTER_FLAG_FOURTHBUTTON = 0x00000080,
            POINTER_FLAG_FIFTHBUTTON = 0x00000100,
            POINTER_FLAG_PRIMARY = 0x00002000,
            POINTER_FLAG_CONFIDENCE = 0x00004000,
            POINTER_FLAG_CANCELED = 0x00008000,
            POINTER_FLAG_DOWN = 0x00010000,
            POINTER_FLAG_UPDATE = 0x00020000,
            POINTER_FLAG_UP = 0x00040000,
            POINTER_FLAG_WHEEL = 0x00080000,
            POINTER_FLAG_HWHEEL = 0x00100000,
            POINTER_FLAG_CAPTURECHANGED = 0x00200000,
            POINTER_FLAG_HASTRANSFORM = 0x00400000,
        }

        [Flags]
        public enum PEN_MASK : uint
        {
            PEN_MASK_NONE = 0x00000000,
            PEN_MASK_PRESSURE = 0x00000001,
            PEN_MASK_ROTATION = 0x00000002,
            PEN_MASK_TILT_X = 0x00000004,
            PEN_MASK_TILT_Y = 0x00000008,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTER_INFO
        {
            public POINTER_INPUT_TYPE pointerType;
            public uint pointerId;
            public uint frameId;
            public POINTER_FLAGS pointerFlags;
            public IntPtr sourceDevice;
            public IntPtr hwndTarget;
            public POINT ptPixelLocation;
            public POINT ptHimetricLocation;
            public POINT ptPixelLocationRaw;
            public POINT ptHimetricLocationRaw;
            public uint dwTime;
            public uint historyCount;
            public int inputData;
            public uint dwKeyStates;
            public ulong PerformanceCount;
            public int ButtonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTER_PEN_INFO
        {
            public POINTER_INFO pointerInfo;
            public uint penFlags;
            public PEN_MASK penMask;
            public uint pressure;
            public uint rotation;
            public int tiltX;
            public int tiltY;
        }
        #endregion

        // 펜 입력을 주입하는 메인 함수
        public static void InjectPenInput(float x, float y, float pressure, string action)
        {
            var penInfo = new POINTER_PEN_INFO();

            // 태블릿 좌표를 PC 화면 좌표로 변환
            // TODO: 태블릿의 해상도를 받아와서 더 정확하게 스케일링해야 합니다.
            penInfo.pointerInfo.ptPixelLocation.x = (int)x;
            penInfo.pointerInfo.ptPixelLocation.y = (int)y;

            penInfo.pointerInfo.pointerType = POINTER_INPUT_TYPE.PT_PEN;
            penInfo.pointerInfo.pointerId = 0;

            // 안드로이드에서 받은 필압(0.0 ~ 1.0)을 윈도우 API (0 ~ 1024)로 변환
            uint finalPressure = (uint)(pressure * 1024);
            penInfo.pressure = finalPressure;
            penInfo.penMask = PEN_MASK.PEN_MASK_PRESSURE;

            string logMessage = $"Inject -> Action: {action}, X: {(int)x}, Y: {(int)y}, Pressure: {finalPressure}";

            // 터치 액션에 따라 플래그 설정
            switch (action.ToUpper())
            {
                case "DOWN":
                    penInfo.pointerInfo.pointerFlags = POINTER_FLAGS.POINTER_FLAG_DOWN | POINTER_FLAGS.POINTER_FLAG_INRANGE | POINTER_FLAGS.POINTER_FLAG_INCONTACT;
                    break;
                case "MOVE":
                    penInfo.pointerInfo.pointerFlags = POINTER_FLAGS.POINTER_FLAG_UPDATE | POINTER_FLAGS.POINTER_FLAG_INRANGE | POINTER_FLAGS.POINTER_FLAG_INCONTACT;
                    break;
                case "UP":
                    penInfo.pointerInfo.pointerFlags = POINTER_FLAGS.POINTER_FLAG_UP | POINTER_FLAGS.POINTER_FLAG_INRANGE;
                    break;
                default:
                    Console.WriteLine($"[DEBUG] Unknown Action: {action}");
                    return;
            }

            Console.WriteLine($"[DEBUG] {logMessage}, Flags: {penInfo.pointerInfo.pointerFlags}");

            // OS에 펜 정보 주입
            bool result = InjectTouchInput(new[] { penInfo }, 1);

            if (!result)
            {
                // 주입 실패 시, Marshal.GetLastWin32Error()를 호출하여 구체적인 오류 코드를 가져옴
                int errorCode = Marshal.GetLastWin32Error();
                Console.WriteLine($"[ERROR] Pen Injection FAILED! Win32 Error Code: {errorCode}");
                // 자주 발생하는 오류 코드에 대한 설명
                // 5: Access is denied. (액세스 거부) -> UIPI 문제. 관리자 권한으로 실행해 보세요.
                // 87: The parameter is incorrect. (매개변수 오류) -> 구조체에 잘못된 값이 들어갔습니다.
            }
        }
    }
}
