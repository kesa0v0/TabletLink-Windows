using System;
using System.ComponentModel;
using System.Diagnostics;
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
    private static NativeMethods.HSYNTHETICPOINTERDEVICE _penDevice;
    private static bool _isPenDown = false;
    private static bool _previousBarrelButtonState = false;

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
        // When the pen is lifted, the barrel button is implicitly released.
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

        // Correctly handle barrel button state transitions vs. steady state.
        SetBarrelButtonState(ref penInfo.pointerInfo, ref penInfo.penFlags, isBarrelButtonPressed);

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

        NativeMethods.InjectSyntheticPointerInput(_penDevice, new[] { pointerInfo }, 1);

        // Hovering implies the pen is not in contact with the screen.
        _isPenDown = false;
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Creates a POINTER_TYPE_INFO structure for pen input.
    /// </summary>
    private static NativeMethods.POINTER_TYPE_INFO CreatePointerPenInfo(int x, int y, float pressure, NativeMethods.POINTER_FLAGS flags, bool isBarrelButtonPressed, int tiltX, int tiltY)
    {
        var pointerInfo = new NativeMethods.POINTER_TYPE_INFO { type = NativeMethods.PointerInputType.PT_PEN };
        ref var penInfo = ref pointerInfo.penInfo;

        penInfo.pointerInfo.pointerType = NativeMethods.PointerInputType.PT_PEN;
        penInfo.pointerInfo.ptPixelLocation.x = x;
        penInfo.pointerInfo.ptPixelLocation.y = y;
        penInfo.pointerInfo.pointerId = 0;

        if (flags.HasFlag(NativeMethods.POINTER_FLAGS.DOWN) || flags.HasFlag(NativeMethods.POINTER_FLAGS.UPDATE))
        {
            flags |= NativeMethods.POINTER_FLAGS.INCONTACT | NativeMethods.POINTER_FLAGS.INRANGE;
        }
        penInfo.pointerInfo.pointerFlags = flags;

        penInfo.penMask = NativeMethods.PEN_MASK.PRESSURE;
        penInfo.pressure = (uint)(Math.Max(0, Math.Min(1, pressure)) * 1024);

        // Correctly handle barrel button state transitions vs. steady state.
        SetBarrelButtonState(ref penInfo.pointerInfo, ref penInfo.penFlags, isBarrelButtonPressed);

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

    /// <summary>
    /// Sets the barrel button state according to Win32 API rules, distinguishing
    /// between a state transition (press/release) and a steady state (held).
    /// </summary>
    private static void SetBarrelButtonState(ref NativeMethods.POINTER_INFO pointerInfo, ref NativeMethods.PEN_FLAGS penFlags, bool isBarrelButtonPressed)
    {
        // Default to no change
        pointerInfo.ButtonChangeType = (int)NativeMethods.POINTER_BUTTON_CHANGE_TYPE.POINTER_CHANGE_NONE;

        string logMessage = $"[INJECT] Barrel: current={isBarrelButtonPressed}, prev={_previousBarrelButtonState}. ";

        // Check for a state transition
        if (isBarrelButtonPressed != _previousBarrelButtonState)
        {
            // If the state changed, report the transition via ButtonChangeType.
            if (isBarrelButtonPressed)
            {
                pointerInfo.ButtonChangeType = (int)NativeMethods.POINTER_BUTTON_CHANGE_TYPE.POINTER_CHANGE_SECONDBUTTON_DOWN;
                logMessage += "ChangeType=DOWN. ";
            }
            else
            {
                pointerInfo.ButtonChangeType = (int)NativeMethods.POINTER_BUTTON_CHANGE_TYPE.POINTER_CHANGE_SECONDBUTTON_UP;
                logMessage += "ChangeType=UP. ";
            }
        }
        else
        {
            // If the state is the same as before (steady state), report it via pointerFlags.
            if (isBarrelButtonPressed)
            {
                pointerInfo.pointerFlags |= NativeMethods.POINTER_FLAGS.SECONDBUTTON;
                logMessage += "State=SECONDBUTTON flag set. ";
            }
        }

        // The PEN_FLAGS.BARREL should always reflect the current physical state.
        if (isBarrelButtonPressed)
        {
            penFlags |= NativeMethods.PEN_FLAGS.BARREL;
        }

        Console.WriteLine(logMessage + $"Final PointerFlags={pointerInfo.pointerFlags}");

        // Update the state for the next event.
        _previousBarrelButtonState = isBarrelButtonPressed;
    }

    #endregion

    #region P/Invoke Declarations

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern HSYNTHETICPOINTERDEVICE CreateSyntheticPointerDevice(
            PointerInputType pointerType, uint maxCount, POINTER_FEEDBACK_MODE mode);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool InjectSyntheticPointerInput(
            HSYNTHETICPOINTERDEVICE device, [In] POINTER_TYPE_INFO[] pointerInfo, uint count);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern void DestroySyntheticPointerDevice(HSYNTHETICPOINTERDEVICE device);

        internal enum POINTER_FEEDBACK_MODE { DEFAULT = 1, INDIRECT = 2, NONE = 3 }

        internal enum PointerInputType { PT_POINTER = 1, PT_TOUCH = 2, PT_PEN = 3, PT_MOUSE = 4 }

        internal enum POINTER_BUTTON_CHANGE_TYPE
        {
            POINTER_CHANGE_NONE,
            POINTER_CHANGE_FIRSTBUTTON_DOWN,
            POINTER_CHANGE_FIRSTBUTTON_UP,
            POINTER_CHANGE_SECONDBUTTON_DOWN,
            POINTER_CHANGE_SECONDBUTTON_UP
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HSYNTHETICPOINTERDEVICE { public IntPtr handle; }

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
            public int ButtonChangeType; // This field is crucial for the fix.
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

