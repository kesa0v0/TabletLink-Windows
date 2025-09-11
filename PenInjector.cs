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
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    /// <param name="pressure">필압 (0.0f ~ 1.0f)</param>
    public static void InjectPenDown(int x, int y, float pressure = 0.5f)
    {
        var pointerInfo = CreatePointerPenInfo(x, y, pressure, POINTER_FLAGS.DOWN);
        InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
        _isPenDown = true;
    }

    /// <summary>
    /// 지정된 좌표로 펜을 움직이는 입력을 주입합니다. InjectPenDown()이 호출된 후에 사용해야 합니다.
    /// </summary>
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    /// <param name="pressure">필압 (0.0f ~ 1.0f)</param>
    public static void InjectPenMove(int x, int y, float pressure = 0.5f)
    {
        if (!_isPenDown) return; // 펜이 눌려있지 않으면 무시

        var pointerInfo = CreatePointerPenInfo(x, y, pressure, POINTER_FLAGS.UPDATE);
        InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
    }

    /// <summary>
    /// 지정된 좌표에서 펜을 떼는 입력을 주입합니다.
    /// </summary>
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    public static void InjectPenUp(int x, int y)
    {
        var pointerInfo = CreatePointerPenInfo(x, y, 0, POINTER_FLAGS.UP);
        InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
        _isPenDown = false;
    }

    #endregion

    #region Private Helper

    private static POINTER_TYPE_INFO CreatePointerPenInfo(int x, int y, float pressure, POINTER_FLAGS flags)
    {
        var pointerInfo = new POINTER_TYPE_INFO { type = PointerInputType.PT_PEN };
        pointerInfo.penInfo.pointerInfo.pointerType = PointerInputType.PT_PEN;
        pointerInfo.penInfo.pointerInfo.ptPixelLocation.x = x;
        pointerInfo.penInfo.pointerInfo.ptPixelLocation.y = y;
        pointerInfo.penInfo.pointerInfo.pointerId = 0;

        // DOWN 또는 UPDATE 시에는 접촉(INCONTACT)과 범위 내(INRANGE) 플래그가 필요
        if (flags == POINTER_FLAGS.DOWN || flags == POINTER_FLAGS.UPDATE)
        {
            flags |= POINTER_FLAGS.INCONTACT | POINTER_FLAGS.INRANGE;
        }
        pointerInfo.penInfo.pointerInfo.pointerFlags = flags;

        // 필압 설정
        pointerInfo.penInfo.penMask = PEN_MASK.PRESSURE;
        pointerInfo.penInfo.pressure = (uint)(pressure * 1024);

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