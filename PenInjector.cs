using System;
using System.Runtime.InteropServices;

/// <summary>
/// Windows의 Synthetic Pointer Injection API를 사용하여 가상 펜 입력을 주입하는 래퍼 클래스입니다.
/// 사용법:
/// 1. PenInputInjector.Initialize(); // 애플리케이션 시작 시 한 번 호출
/// 2. PenInputInjector.InjectPenDown(x, y, pressure); // 펜 누르기
/// 3. PenInputInjector.InjectPenMove(x, y, pressure); // 펜 움직이기
/// 4. PenInputInjector.InjectPenUp(x, y); // 펜 떼기
/// 5. PenInputInjector.Uninitialize(); // 애플리케이션 종료 시 반드시 호출
/// </summary>
public static class PenInputInjector
{
    private static HSYNTHETICPOINTERDEVICE _penDevice;
    private static bool _isPenDown = false;

    #region Public Methods

    /// <summary>
    /// 가상 펜 디바이스를 초기화합니다. 애플리케이션 시작 시 한 번만 호출해야 합니다.
    /// </summary>
    public static void Initialize()
    {
        if (_penDevice.handle != IntPtr.Zero)
        {
            return; // 이미 초기화됨
        }
        _penDevice = CreateSyntheticPointerDevice(PointerInputType.PT_PEN, 1, POINTER_FEEDBACK_MODE.DEFAULT);
    }

    /// <summary>
    /// 가상 펜 디바이스를 정리합니다. 애플리케이션 종료 시 반드시 호출해야 합니다.
    /// </summary>
    public static void Uninitialize()
    {
        if (_penDevice.handle != IntPtr.Zero)
        {
            DestroySyntheticPointerDevice(_penDevice);
            _penDevice.handle = IntPtr.Zero;
        }
    }


    /// <summary>
    /// 지정된 좌표에 펜을 누르는 입력을 주입합니다.
    /// </summary>
    public static void InjectPenDown(int x, int y, float pressure = 0.5f, bool isBarrelButtonPressed = false, int tiltX = 0, int tiltY = 0)
    {
        var pointerInfo = CreatePointerPenInfo(x, y, pressure, POINTER_FLAGS.DOWN, isBarrelButtonPressed, tiltX, tiltY);
        InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
        _isPenDown = true;
    }

    /// <summary>
    /// 지정된 좌표로 펜을 움직이는 입력을 주입합니다.
    /// </summary>
    public static void InjectPenMove(int x, int y, float pressure = 0.5f, bool isBarrelButtonPressed = false, int tiltX = 0, int tiltY = 0)
    {
        if (!_isPenDown) return;
        var pointerInfo = CreatePointerPenInfo(x, y, pressure, POINTER_FLAGS.UPDATE, isBarrelButtonPressed, tiltX, tiltY);
        InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
    }

    /// <summary>
    /// 지정된 좌표에서 펜을 떼는 입력을 주입합니다.
    /// </summary>
    public static void InjectPenUp(int x, int y)
    {
        // 펜을 뗄 때는 압력, 버튼, 기울기 정보가 의미 없음
        var pointerInfo = CreatePointerPenInfo(x, y, 0, POINTER_FLAGS.UP, false, 0, 0);
        InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
        _isPenDown = false;
    }

    /// <summary>
    /// 지정된 좌표로 펜을 호버링(Hovering)하는 입력을 주입합니다.
    /// 펜이 화면에 닿지 않은 채 커서만 움직이는 효과를 냅니다.
    /// </summary>
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    /// <param name="tiltX">X축 기울기 (-90 ~ +90)</param>
    /// <param name="tiltY">Y축 기울기 (-90 ~ +90)</param>
    public static void InjectPenHover(int x, int y, int tiltX = 0, int tiltY = 0)
    {
        var pointerInfo = new POINTER_TYPE_INFO { type = PointerInputType.PT_PEN };
        ref var penInfo = ref pointerInfo.penInfo;

        penInfo.pointerInfo.pointerType = PointerInputType.PT_PEN;
        penInfo.pointerInfo.ptPixelLocation.x = x;
        penInfo.pointerInfo.ptPixelLocation.y = y;
        penInfo.pointerInfo.pointerId = 0;

        // <<< 핵심: INRANGE 플래그만 설정하고 INCONTACT는 제외합니다.
        penInfo.pointerInfo.pointerFlags = POINTER_FLAGS.UPDATE | POINTER_FLAGS.INRANGE;

        // 호버링 중에도 기울기는 인식 가능합니다.
        penInfo.penMask = PEN_MASK.NONE;
        if (tiltX != 0)
        {
            penInfo.penMask |= PEN_MASK.TILT_X;
            penInfo.tiltX = tiltX;
        }
        if (tiltY != 0)
        {
            penInfo.penMask |= PEN_MASK.TILT_Y;
            penInfo.tiltY = tiltY;
        }

        InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);

        // 호버링은 펜을 뗀 상태이므로 _isPenDown을 false로 유지합니다.
        _isPenDown = false;
    }

    #endregion

    #region Private Helper

    private static POINTER_TYPE_INFO CreatePointerPenInfo(int x, int y, float pressure, POINTER_FLAGS flags, bool isBarrelButtonPressed, int tiltX, int tiltY)
    {
        var pointerInfo = new POINTER_TYPE_INFO { type = PointerInputType.PT_PEN };
        ref var penInfo = ref pointerInfo.penInfo; // ref 키워드로 더 간결하게 사용

        penInfo.pointerInfo.pointerType = PointerInputType.PT_PEN;
        penInfo.pointerInfo.ptPixelLocation.x = x;
        penInfo.pointerInfo.ptPixelLocation.y = y;
        penInfo.pointerInfo.pointerId = 0;

        if (flags == POINTER_FLAGS.DOWN || flags == POINTER_FLAGS.UPDATE)
        {
            flags |= POINTER_FLAGS.INCONTACT | POINTER_FLAGS.INRANGE;
        }
        penInfo.pointerInfo.pointerFlags = flags;

        // 필압은 항상 사용
        penInfo.penMask = PEN_MASK.PRESSURE;
        penInfo.pressure = (uint)(pressure * 1024);

        // <<< 추가된 부분: 배럴 버튼 처리
        if (isBarrelButtonPressed)
        {
            penInfo.penFlags |= PEN_FLAGS.BARREL;
        }

        // <<< 추가된 부분: 기울기 처리
        if (tiltX != 0)
        {
            penInfo.penMask |= PEN_MASK.TILT_X;
            penInfo.tiltX = tiltX;
        }
        if (tiltY != 0)
        {
            penInfo.penMask |= PEN_MASK.TILT_Y;
            penInfo.tiltY = tiltY;
        }

        return pointerInfo;
    }

    #endregion

    #region P/Invoke Declarations

    // Win32 API 함수 선언
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern HSYNTHETICPOINTERDEVICE CreateSyntheticPointerDevice(
        PointerInputType pointerType, uint maxCount, POINTER_FEEDBACK_MODE mode);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool InjectSyntheticPointerInput(
        HSYNTHETICPOINTERDEVICE device, [In] POINTER_TYPE_INFO[] pointerInfo, uint count);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern void DestroySyntheticPointerDevice(HSYNTHETICPOINTERDEVICE device);

    // 필요한 구조체 및 열거형 선언
    public enum POINTER_FEEDBACK_MODE
    {
        DEFAULT = 1,
        INDIRECT = 2,
        NONE = 3
    }

    public enum PointerInputType
    {
        PT_POINTER = 0x00000001,
        PT_TOUCH = 0x00000002,
        PT_PEN = 0x00000003,
        PT_MOUSE = 0x00000004
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HSYNTHETICPOINTERDEVICE
    {
        public IntPtr handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [Flags]
    public enum POINTER_FLAGS : uint
    {
        NONE = 0x00000000,
        NEW = 0x00000001,
        INRANGE = 0x00000002,
        INCONTACT = 0x00000004,
        FIRSTBUTTON = 0x00000010,
        SECONDBUTTON = 0x00000020,
        PRIMARY = 0x00002000,
        DOWN = 0x00010000,
        UPDATE = 0x00020000,
        UP = 0x00040000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTER_INFO
    {
        public PointerInputType pointerType;
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

    [Flags]
    public enum PEN_FLAGS : uint
    {
        NONE = 0x00000000,
        BARREL = 0x00000001,
        INVERTED = 0x00000002,
        ERASER = 0x00000004
    }

    [Flags]
    public enum PEN_MASK : uint
    {
        NONE = 0x00000000,
        PRESSURE = 0x00000001,
        ROTATION = 0x00000002,
        TILT_X = 0x00000004,
        TILT_Y = 0x00000008
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTER_PEN_INFO
    {
        public POINTER_INFO pointerInfo;
        public PEN_FLAGS penFlags;
        public PEN_MASK penMask;
        public uint pressure;
        public uint rotation;
        public int tiltX;
        public int tiltY;
    }

    // `POINTER_TYPE_INFO`는 공용체(union)이므로 `LayoutKind.Explicit`을 사용
    [StructLayout(LayoutKind.Explicit)]
    public struct POINTER_TYPE_INFO
    {
        [FieldOffset(0)]
        public PointerInputType type;

        [FieldOffset(8)] // 64비트 환경에서는 포인터/핸들 크기가 8바이트
        public POINTER_PEN_INFO penInfo;

        // 필요하다면 터치 정보도 추가할 수 있습니다.
        // [FieldOffset(8)]
        // public POINTER_TOUCH_INFO touchInfo;
    }
    #endregion
}