using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

/// <summary>
/// A wrapper class for injecting virtual pen input using Windows' Synthetic Pointer Injection API.
/// This class is static and must be initialized before use and uninitialized on application exit.
///
/// Usage:
/// 1. PenInputInjector.Initialize(); // Call once at application startup.
/// 2. PenInputInjector.InjectPenDown(x, y, pressure);
/// 3. PenInputInjector.InjectPenMove(x, y, pressure);
/// 4. PenInputInjector.InjectPenUp(x, y);
/// 5. PenInputInjector.InjectPenHover(x, y);
/// 6. PenInputInjector.Uninitialize(); // Call once at application exit.
/// </summary>
public static class PenInputInjector
{
    private static NativeMethods.HSYNTHETICPOUTERDEVICE _penDevice;
    private static bool _isPenDown = false;

    #region Public Methods

    /// <summary>
    /// Initializes the virtual pen device. Must be called once before any injection methods.
    /// </summary>
    /// <exception cref="Win32Exception">Thrown if the synthetic pointer device cannot be created.</exception>
    public static void Initialize()
    {
        if (_penDevice.handle != IntPtr.Zero)
        {
            return; // Already initialized
        }
        _penDevice = NativeMethods.CreateSyntheticPointerDevice(
            NativeMethods.PointerInputType.PT_PEN, 1, NativeMethods.POINTER_FEEDBACK_MODE.DEFAULT);

        if (_penDevice.handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create synthetic pen device.");
        }
    }

    /// <summary>
    /// Cleans up the virtual pen device. Must be called when the application is closing.
    /// </summary>
    public static void Uninitialize()
    {
        if (_penDevice.handle != IntPtr.Zero)
        {
            NativeMethods.DestroySyntheticPointerDevice(_penDevice);
            _penDevice.handle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Injects a pen down (contact) input at the specified coordinates.
    /// </summary>
    /// <param name="x">The x-coordinate in pixels.</param>
    /// <param name="y">The y-coordinate in pixels.</param>
    /// <param name="pressure">The pressure of the pen tip, from 0.0f to 1.0f.</param>
    /// <param name="isBarrelButtonPressed">True if the barrel button is pressed.</param>
    /// <param name="tiltX">The tilt of the pen in the x-axis, from -90 to +90.</param>
    /// <param name="tiltY">The tilt of the pen in the y-axis, from -90 to +90.</param>
    public static void InjectPenDown(int x, int y, float pressure = 0.5f, bool isBarrelButtonPressed = false, int tiltX = 0, int tiltY = 0)
    {
        var pointerInfo = CreatePointerPenInfo(x, y, pressure, NativeMethods.POINTER_FLAGS.DOWN, isBarrelButtonPressed, tiltX, tiltY);
        if (!NativeMethods.InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1))
        {
            Console.WriteLine($"Injection failed with error code: {Marshal.GetLastWin32Error()}");
        }
        _isPenDown = true;
    }

    /// <summary>
    /// Injects a pen move input at the specified coordinates. Only works if the pen is already down.
    /// </summary>
    public static void InjectPenMove(int x, int y, float pressure = 0.5f, bool isBarrelButtonPressed = false, int tiltX = 0, int tiltY = 0)
    {
        if (!_isPenDown) return;
        var pointerInfo = CreatePointerPenInfo(x, y, pressure, NativeMethods.POINTER_FLAGS.UPDATE, isBarrelButtonPressed, tiltX, tiltY);
        NativeMethods.InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
    }

    /// <summary>
    /// Injects a pen up (contact lifted) input at the specified coordinates.
    /// </summary>
    public static void InjectPenUp(int x, int y)
    {
        if (!_isPenDown) return;
        // Pressure, button, and tilt are irrelevant on pen up.
        var pointerInfo = CreatePointerPenInfo(x, y, 0, NativeMethods.POINTER_FLAGS.UP, false, 0, 0);
        NativeMethods.InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);
        _isPenDown = false;
    }

    /// <summary>
    /// Injects a pen hover (in-range, no contact) input at the specified coordinates.
    /// </summary>
    public static void InjectPenHover(int x, int y, bool isBarrelButtonPressed = false, int tiltX = 0, int tiltY = 0)
    {
        var pointerInfo = new NativeMethods.POINTER_TYPE_INFO { type = NativeMethods.PointerInputType.PT_PEN };
        ref var penInfo = ref pointerInfo.penInfo;

        penInfo.pointerInfo.pointerType = NativeMethods.PointerInputType.PT_PEN;
        penInfo.pointerInfo.ptPixelLocation.x = x;
        penInfo.pointerInfo.ptPixelLocation.y = y;
        penInfo.pointerInfo.pointerId = 0;

        // Core of hovering: INRANGE flag is set, but INCONTACT is not.
        penInfo.pointerInfo.pointerFlags = NativeMethods.POINTER_FLAGS.UPDATE | NativeMethods.POINTER_FLAGS.INRANGE;

        if (isBarrelButtonPressed)
        {
            penInfo.pointerInfo.pointerFlags |= NativeMethods.POINTER_FLAGS.FIRSTBUTTON; // Use standard button flags for hover clicks
        }

        penInfo.penMask = NativeMethods.PEN_MASK.NONE;
        if (tiltX != 0)
        {
            penInfo.penMask |= NativeMethods.PEN_MASK.TILT_X;
            penInfo.tiltX = tiltX;
        }
        if (tiltY != 0)
        {
            penInfo.penMask |= NativeMethods.PEN_MASK.TILT_Y;
            penInfo.tiltY = tiltY;
        }

        if (isBarrelButtonPressed)
        {
            penInfo.penFlags |= NativeMethods.PEN_FLAGS.BARREL;
        }

        NativeMethods.InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);

        // Hovering implies the pen is not in contact with the screen.
        _isPenDown = false;
    }

    #endregion

    #region Private Helper

    private static NativeMethods.POINTER_TYPE_INFO CreatePointerPenInfo(int x, int y, float pressure, NativeMethods.POINTER_FLAGS flags, bool isBarrelButtonPressed, int tiltX, int tiltY)
    {
        var pointerInfo = new NativeMethods.POINTER_TYPE_INFO { type = NativeMethods.PointerInputType.PT_PEN };
        ref var penInfo = ref pointerInfo.penInfo;

        penInfo.pointerInfo.pointerType = NativeMethods.PointerInputType.PT_PEN;
        penInfo.pointerInfo.ptPixelLocation.x = x;
        penInfo.pointerInfo.ptPixelLocation.y = y;
        penInfo.pointerInfo.pointerId = 0;

        // For Down and Update events, the pen must be in range and in contact.
        if (flags.HasFlag(NativeMethods.POINTER_FLAGS.DOWN) || flags.HasFlag(NativeMethods.POINTER_FLAGS.UPDATE))
        {
            flags |= NativeMethods.POINTER_FLAGS.INCONTACT | NativeMethods.POINTER_FLAGS.INRANGE;
        }
        penInfo.pointerInfo.pointerFlags = flags;

        // Always include pressure information.
        penInfo.penMask = NativeMethods.PEN_MASK.PRESSURE;
        penInfo.pressure = (uint)(Math.Max(0, Math.Min(1, pressure)) * 1024);

        if (isBarrelButtonPressed)
        {
            penInfo.penFlags |= NativeMethods.PEN_FLAGS.BARREL;
        }

        if (tiltX != 0)
        {
            penInfo.penMask |= NativeMethods.PEN_MASK.TILT_X;
            penInfo.tiltX = tiltX;
        }
        if (tiltY != 0)
        {
            penInfo.penMask |= NativeMethods.PEN_MASK.TILT_Y;
            penInfo.tiltY = tiltY;
        }

        return pointerInfo;
    }

    #endregion

    #region P/Invoke Declarations

    // Encapsulating P/Invoke definitions in a private static class is a common best practice.
    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern HSYNTHETICPOUTERDEVICE CreateSyntheticPointerDevice(
            PointerInputType pointerType, uint maxCount, POINTER_FEEDBACK_MODE mode);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool InjectSyntheticPointerInput(
            HSYNTHETICPOUTERDEVICE device, [In] POINTER_TYPE_INFO[] pointerInfo, uint count);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern void DestroySyntheticPointerDevice(HSYNTHETICPOUTERDEVICE device);

        internal enum POINTER_FEEDBACK_MODE { DEFAULT = 1, INDIRECT = 2, NONE = 3 }

        internal enum PointerInputType { PT_POINTER = 1, PT_TOUCH = 2, PT_PEN = 3, PT_MOUSE = 4 }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HSYNTHETICPOUTERDEVICE { public IntPtr handle; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT { public int x; public int y; }

        [Flags]
        internal enum POINTER_FLAGS : uint
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
        internal struct POINTER_INFO
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
        internal enum PEN_FLAGS : uint { NONE = 0, BARREL = 1, INVERTED = 2, ERASER = 4 }

        [Flags]
        internal enum PEN_MASK : uint { NONE = 0, PRESSURE = 1, ROTATION = 2, TILT_X = 4, TILT_Y = 8 }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINTER_PEN_INFO
        {
            public POINTER_INFO pointerInfo;
            public PEN_FLAGS penFlags;
            public PEN_MASK penMask;
            public uint pressure;
            public uint rotation;
            public int tiltX;
            public int tiltY;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct POINTER_TYPE_INFO
        {
            [FieldOffset(0)] public PointerInputType type;
            [FieldOffset(8)] public POINTER_PEN_INFO penInfo;
        }
    }
    #endregion
}
