using System;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using SpaceMouseRepo.Core.Input;

namespace SpaceMouseRepo.Input;

/// <summary>
/// Reads SpaceMouse motion via the 3Dconnexion siappdll native SDK (siapp.h).
/// COM-based TDxInput is broken on Mono; this is the next-most-portable native path.
///
/// siapp's protocol is window-message-driven: the driver dispatches motion events as
/// WM_3DXSI* messages to a registered HWND. We create a hidden message-only window on
/// a dedicated background thread, run a GetMessage loop there, and forward each Si event
/// to atomic per-axis floats the main thread reads via the State property.
///
/// P/Invoke surface adapted from DMXControl/3Dconnexion-driver (DMXControl, 3Dconnexion SDK
/// license).
/// </summary>
public sealed class SpaceMouseSdk : IDisposable
{
    private const string SiAppDll = "siappdll";

    private enum SpwRetVal
    {
        SPW_NO_ERROR = 0,
        SPW_ERROR,
        SI_BAD_HANDLE,
        SI_BAD_ID,
        SI_BAD_VALUE,
        SI_IS_EVENT,
        SI_SKIP_EVENT,
        SI_NOT_EVENT,
        SI_NO_DRIVER,
        SI_NO_RESPONSE,
        SI_UNSUPPORTED,
        SI_UNINITIALIZED,
        SI_WRONG_DRIVER,
        SI_INTERNAL_ERROR,
        SI_BAD_PROTOCOL,
        SI_OUT_OF_MEMORY,
        SPW_DLL_LOAD_ERROR,
        SI_NOT_OPEN,
        SI_ITEM_NOT_FOUND,
        SI_UNSUPPORTED_DEVICE,
    }

    private enum SiEventType
    {
        SI_BUTTON_EVENT = 1,
        SI_MOTION_EVENT,
        SI_COMBO_EVENT,
        SI_ZERO_EVENT,
    }

    private const int SI_ANY_DEVICE = -1;
    private const int SI_EVENT = 0x0001;
    private const int MAX_PATH = 260;
    private const int SI_MAXBUF = 128;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct SiOpenData
    {
        public int hWnd;
        public IntPtr transCtl;
        public int processID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string exeFile;
        public int libFlag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SiGetEventData
    {
        public uint msg;
        public IntPtr wParam;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SiButtonData
    {
        public uint last, current, pressed, released;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SiSpwData
    {
        public SiButtonData bData;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public int[] mData;
        public int period;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SI_MAXBUF)]
        public byte[] exData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SiSpwEvent
    {
        public int type;          // SiEventType
        public SiSpwData spwData;
    }

    [DllImport(SiAppDll, EntryPoint = "SiInitialize")]
    private static extern SpwRetVal SiInitialize();
    [DllImport(SiAppDll, EntryPoint = "SiTerminate")]
    private static extern int SiTerminate();
    [DllImport(SiAppDll, EntryPoint = "SiClose")]
    private static extern SpwRetVal SiClose(IntPtr hdl);
    [DllImport(SiAppDll, EntryPoint = "SiOpenWinInit")]
    private static extern int SiOpenWinInit(ref SiOpenData o, IntPtr hwnd);
    [DllImport(SiAppDll, EntryPoint = "SiOpen", CharSet = CharSet.Ansi)]
    private static extern IntPtr SiOpen(string appName, int devID, IntPtr mask, int mode, ref SiOpenData data);
    [DllImport(SiAppDll, EntryPoint = "SiGetEvent")]
    private static extern SpwRetVal SiGetEvent(IntPtr hdl, int flags, ref SiGetEventData data, ref SiSpwEvent ev);

    // Win32 message-only window plumbing
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPTStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX cls);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string? windowName, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")]
    private static extern int GetCurrentProcessId();
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? module);

    private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
    private const uint WM_QUIT = 0x0012;
    private const uint WM_CLOSE = 0x0010;

    // Instance state
    private readonly ManualLogSource _log;
    private readonly float _tDead;
    private readonly float _rDead;
    private IntPtr _siHandle;
    private IntPtr _hwnd;
    private Thread? _msgThread;
    private uint _msgThreadId;
    private WndProc? _wndProcDelegate; // keep alive against GC
    private volatile bool _ready;
    private volatile float _tx, _ty, _tz, _rx, _ry, _rz;

    public bool IsActive => _ready;

    // Diagnostic counters for v0.3.8: surface whether siappdll is dispatching anything to our
    // hidden window's message pump, and what the latest motion data looks like. Read by Plugin.Update.
    public int WindowProcCalls => _windowProcCalls;
    public int SiMotionEvents => _siMotionEvents;
    public int SiAnyEvents => _siAnyEvents;
    public (float Tx, float Ty, float Tz, float Rx, float Ry, float Rz) RawAxes => (_tx, _ty, _tz, _rx, _ry, _rz);
    private int _windowProcCalls;
    private int _siMotionEvents;
    private int _siAnyEvents;

    public SpaceMouseSdk(ManualLogSource log, float translationDeadzone, float rotationDeadzone)
    {
        _log = log;
        _tDead = translationDeadzone;
        _rDead = rotationDeadzone;

        try
        {
            var startedSignal = new ManualResetEventSlim();
            _msgThread = new Thread(() => MessageLoop(startedSignal))
            {
                IsBackground = true,
                Name = "SpaceMouseSiAppMsgPump",
            };
            _msgThread.SetApartmentState(ApartmentState.STA);
            _msgThread.Start();
            // Wait up to 5s for siappdll to wake up. If it takes longer, the message-pump thread
            // continues running and IsActive will flip to true once setup finishes; State() reads
            // are safe at any time since they short-circuit on null sensor.
            startedSignal.Wait(5000);
            if (!_ready) _log.LogInfo("siappdll setup not yet complete; will continue initializing in background.");
        }
        catch (Exception e)
        {
            _log.LogError($"siappdll thread start failed: {e}");
        }
    }

    public SpaceMouseState State => new(
        Dz(_tx, _tDead), Dz(_ty, _tDead), Dz(_tz, _tDead),
        Dz(_rx, _rDead), Dz(_ry, _rDead), Dz(_rz, _rDead),
        button1: false, button2: false);

    private void MessageLoop(ManualResetEventSlim started)
    {
        try
        {
            _msgThreadId = GetCurrentThreadId();

            var initRc = SiInitialize();
            if (initRc != SpwRetVal.SPW_NO_ERROR)
            {
                _log.LogWarning($"SiInitialize returned {initRc}; SpaceMouse input inactive.");
                started.Set();
                return;
            }

            // Register a unique class name and create a hidden message-only window.
            const string className = "SpaceMouseRepo_HiddenWindow";
            _wndProcDelegate = WindowProc;
            var hInstance = GetModuleHandle(null);
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProcDelegate,
                hInstance = hInstance,
                lpszClassName = className,
            };
            var atom = RegisterClassEx(ref wc);
            if (atom == 0)
            {
                _log.LogError($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
                started.Set();
                return;
            }

            _hwnd = CreateWindowEx(0, className, null, 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                _log.LogError($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
                started.Set();
                return;
            }

            var openData = new SiOpenData { exeFile = "REPO.exe" };
            SiOpenWinInit(ref openData, _hwnd);
            _siHandle = SiOpen("SpaceMouseRepo", SI_ANY_DEVICE, IntPtr.Zero, SI_EVENT, ref openData);
            if (_siHandle == IntPtr.Zero)
            {
                _log.LogError("SiOpen returned null. SpaceMouse input inactive.");
                started.Set();
                return;
            }

            _ready = true;
            _log.LogInfo("3DxWare siappdll connected; SpaceMouse input active.");
            started.Set();

            // Pump messages until WM_QUIT.
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (DllNotFoundException)
        {
            _log.LogWarning("siappdll.dll not found. Install/repair 3Dconnexion 3DxWare to enable SpaceMouse input.");
            started.Set();
        }
        catch (Exception e)
        {
            _log.LogError($"siappdll message loop crashed: {e}");
            started.Set();
        }
    }

    // First-N message log: capture raw msg/wParam/lParam values for the first 20 unique-ish
    // window messages we receive, plus the SiGetEvent return code, so we can see whether the
    // messages siappdll appears to dispatch are actually 3DxWare WM_USER+offset events or just
    // generic Windows messages.
    private int _msgLogged;
    private const int MaxMsgsToLog = 20;

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Interlocked.Increment(ref _windowProcCalls);
        if (_siHandle != IntPtr.Zero)
        {
            var ed = new SiGetEventData { msg = msg, wParam = wParam, lParam = lParam };
            var ev = new SiSpwEvent { spwData = new SiSpwData { mData = new int[6], exData = new byte[SI_MAXBUF] } };
            var rc = SiGetEvent(_siHandle, 0, ref ed, ref ev);
            if (Interlocked.Increment(ref _msgLogged) <= MaxMsgsToLog)
            {
                _log.LogInfo($"[diag-msg] msg=0x{msg:X4} wParam=0x{(long)wParam:X} lParam=0x{(long)lParam:X} SiGetEvent={rc} type={ev.type}");
            }
            if (rc == SpwRetVal.SI_IS_EVENT) Interlocked.Increment(ref _siAnyEvents);
            if (rc == SpwRetVal.SI_IS_EVENT && ev.type == (int)SiEventType.SI_MOTION_EVENT)
            {
                Interlocked.Increment(ref _siMotionEvents);
                // mData[0..2] = translation X/Y/Z, [3..5] = rotation X/Y/Z, raw range ~[-1500, 1500].
                const float scale = 1.0f / 350f;
                _tx = ev.spwData.mData[0] * scale;
                _ty = ev.spwData.mData[1] * scale;
                _tz = ev.spwData.mData[2] * scale;
                _rx = ev.spwData.mData[3] * scale;
                _ry = ev.spwData.mData[4] * scale;
                _rz = ev.spwData.mData[5] * scale;
            }
            else if (rc == SpwRetVal.SI_IS_EVENT && ev.type == (int)SiEventType.SI_ZERO_EVENT)
            {
                _tx = _ty = _tz = _rx = _ry = _rz = 0f;
            }
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        try
        {
            if (_siHandle != IntPtr.Zero) { SiClose(_siHandle); _siHandle = IntPtr.Zero; }
            if (_hwnd != IntPtr.Zero) { DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
            if (_msgThreadId != 0) PostThreadMessage(_msgThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _msgThread?.Join(500);
            SiTerminate();
        }
        catch { /* best-effort shutdown */ }
    }

    private static float Dz(float v, float deadzone) => Math.Abs(v) <= deadzone ? 0f : v;
}
