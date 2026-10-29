// TopBar v0.7 - auto-hiding top taskbar for the main screen. Windows 11 style: centered, icon-only, grouped by app.
// Right corner: hidden icons, Bluetooth, sound (volume + switch outputs/inputs), clock (calendar/notifications).
// Start button, pinned apps, open windows, live stats (CPU/GPU temp + watts, RAM), clock.
// Slides down when the mouse hits the top edge or the Windows key is pressed.
// Written for the .NET Framework 4 compiler that ships with Windows (C# 5 only).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Native
{
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")] public static extern IntPtr GetClassLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES p);
    [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PSIZE size);
    [StructLayout(LayoutKind.Sequential)] public struct PSIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] public struct DWM_THUMBNAIL_PROPERTIES { public int dwFlags; public RECT rcDestination; public RECT rcSource; public byte opacity; [MarshalAs(UnmanagedType.Bool)] public bool fVisible; [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly; }
    [DllImport("shell32.dll")] public static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA d);
    [DllImport("kernel32.dll")] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder s, ref int n);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMSTAT m);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] public struct MEMSTAT { public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys; public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual; public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual; }
}


// ---------------- Windows audio (same system the real volume flyout uses) ----------------
[StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public int pid; }
[StructLayout(LayoutKind.Sequential)] struct PROPVARIANT { public ushort vt, r1, r2, r3; public IntPtr p1, p2; }
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCo { }
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator {
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
}
[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceCollection { [PreserveSig] int GetCount(out uint n); [PreserveSig] int Item(uint i, out IMMDevice d); }
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice {
    [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
    [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore ps);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out int s);
}
[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore { [PreserveSig] int GetCount(out int c); [PreserveSig] int GetAt(int i, out PROPERTYKEY k); [PreserveSig] int GetValue(ref PROPERTYKEY k, out PROPVARIANT v); }
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume {
    [PreserveSig] int RegisterControlChangeNotify(IntPtr p); [PreserveSig] int UnregisterControlChangeNotify(IntPtr p);
    [PreserveSig] int GetChannelCount(out uint c);
    [PreserveSig] int SetMasterVolumeLevel(float l, ref Guid ctx); [PreserveSig] int SetMasterVolumeLevelScalar(float l, ref Guid ctx);
    [PreserveSig] int GetMasterVolumeLevel(out float l); [PreserveSig] int GetMasterVolumeLevelScalar(out float l);
    [PreserveSig] int SetChannelVolumeLevel(uint ch, float l, ref Guid ctx); [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float l, ref Guid ctx);
    [PreserveSig] int GetChannelVolumeLevel(uint ch, out float l); [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float l);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid ctx); [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
}
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPolicyConfig {
    [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr f);
    [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int b, IntPtr f);
    [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr a, IntPtr b);
    [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int b, IntPtr a, IntPtr c);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr p);
    [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr m);
    [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr m);
    [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr k, IntPtr v);
    [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr k, IntPtr v);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int v);
}
[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] class PolicyConfigClient { }

class AudioDev { public string Id, Name; public bool IsDefault; }
static class Audio
{
    static Guid IID_Vol = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
    static PROPERTYKEY PK_Name = new PROPERTYKEY { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
    static IMMDeviceEnumerator En() { return (IMMDeviceEnumerator)new MMDeviceEnumeratorCo(); }
    public static List<AudioDev> List(int flow)   // 0 = outputs, 1 = inputs
    {
        var r = new List<AudioDev>();
        try {
            var en = En(); string def = null; IMMDevice d0;
            if (en.GetDefaultAudioEndpoint(flow, 1, out d0) == 0 && d0 != null) d0.GetId(out def);
            IMMDeviceCollection col; if (en.EnumAudioEndpoints(flow, 1, out col) != 0) return r;
            uint n; col.GetCount(out n);
            for (uint i = 0; i < n; i++)
            {
                IMMDevice d; if (col.Item(i, out d) != 0) continue;
                var a = new AudioDev(); d.GetId(out a.Id); a.Name = NameOf(d); a.IsDefault = a.Id == def; r.Add(a);
            }
        } catch (Exception ex) { Bar.Log("audio list failed " + ex.Message); }
        return r;
    }
    static string NameOf(IMMDevice d)
    {
        try {
            IPropertyStore ps; if (d.OpenPropertyStore(0, out ps) != 0) return "Audio device";
            PROPVARIANT v; var k = PK_Name;
            if (ps.GetValue(ref k, out v) == 0 && v.vt == 31) return Marshal.PtrToStringUni(v.p1);
        } catch { }
        return "Audio device";
    }
    public static void SetDefault(string id)
    {
        try { var pc = (IPolicyConfig)new PolicyConfigClient(); for (int role = 0; role < 3; role++) pc.SetDefaultEndpoint(id, role); }
        catch (Exception ex) { Bar.Log("switch audio failed " + ex.Message); }
    }
    static IAudioEndpointVolume Vol()
    {
        IMMDevice d; if (En().GetDefaultAudioEndpoint(0, 1, out d) != 0 || d == null) return null;
        object o; var iid = IID_Vol; if (d.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) return null;
        return (IAudioEndpointVolume)o;
    }
    public static int GetVolume() { try { var v = Vol(); float f; if (v != null && v.GetMasterVolumeLevelScalar(out f) == 0) return (int)Math.Round(f * 100); } catch { } return -1; }
    public static void SetVolume(int pct) { try { var v = Vol(); var g = Guid.Empty; if (v != null) v.SetMasterVolumeLevelScalar(Math.Max(0, Math.Min(100, pct)) / 100f, ref g); } catch { } }
    public static bool GetMute() { try { var v = Vol(); bool m; if (v != null && v.GetMute(out m) == 0) return m; } catch { } return false; }
    public static void SetMute(bool m) { try { var v = Vol(); var g = Guid.Empty; if (v != null) v.SetMute(m, ref g); } catch { } }
}

class WinItem { public IntPtr H; public string Title; public Icon Icon; public string Exe; }
class PinItem { public string Path; public string Target; public string Name; public Icon Icon; }
class AppGroup { public string Key; public string Exe; public PinItem Pin; public List<WinItem> Wins = new List<WinItem>(); public Icon Icon; public RectangleF Rect; }

class Stats
{
    public string CpuC = "--", CpuW = "--", GpuC = "--", GpuW = "--", Ram = "--";
    public double CpuTemp = 0;
    PerformanceCounter[] zones; PerformanceCounter pkg;
    public void Start()
    {
        Thread t = new Thread(Loop); t.IsBackground = true; t.Start();
    }
    void Init()
    {
        try {
            var cat = new PerformanceCounterCategory("Thermal Zone Information");
            var list = new List<PerformanceCounter>();
            foreach (string n in cat.GetInstanceNames()) list.Add(new PerformanceCounter("Thermal Zone Information", "High Precision Temperature", n));
            zones = list.ToArray();
        } catch { zones = new PerformanceCounter[0]; }
        try {
            var cat = new PerformanceCounterCategory("Energy Meter");
            foreach (string n in cat.GetInstanceNames()) if (n.ToLower().Contains("pkg")) { pkg = new PerformanceCounter("Energy Meter", "Power", n); break; }
        } catch { pkg = null; }
    }
    void Loop()
    {
        Init();
        while (true)
        {
            try {
                double mx = 0;
                foreach (var z in zones) { try { double v = z.NextValue(); if (v > mx) mx = v; } catch { } }
                if (mx > 0) { CpuTemp = mx / 10.0 - 273.15; CpuC = Math.Round(CpuTemp).ToString() + "°"; }
                if (pkg != null) CpuW = Math.Round(pkg.NextValue() / 1000.0).ToString() + "W";
                var m = new Native.MEMSTAT(); m.dwLength = (uint)Marshal.SizeOf(typeof(Native.MEMSTAT));
                if (Native.GlobalMemoryStatusEx(ref m)) Ram = ((m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0).ToString("0.0") + "/" + Math.Round(m.ullTotalPhys / 1073741824.0) + "G";
                Gpu();
            } catch { }
            Thread.Sleep(2000);
        }
    }
    void Gpu()
    {
        try {
            var psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=temperature.gpu,power.draw --format=csv,noheader,nounits");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.CreateNoWindow = true;
            using (var p = Process.Start(psi)) {
                string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(3000);
                string[] parts = o.Trim().Split(',');
                if (parts.Length >= 2) { GpuC = parts[0].Trim() + "°"; double w; GpuW = double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out w) ? Math.Round(w) + "W" : "--"; }
                else { GpuC = "--"; GpuW = "--"; }
            }
        } catch { GpuC = "--"; GpuW = "--"; }
    }
}

// ---------------- live window previews (Windows' own live thumbnails) ----------------
class PreviewForm : Form
{
    class Card { public WinItem W; public IntPtr Thumb; public RectangleF Box, Pic, X; }
    List<Card> cards = new List<Card>(); float k; Point hover = new Point(-1, -1);
    public string Key; public Action<IntPtr> OnPick; public Action<IntPtr> OnCloseWin;
    Font fT; Color bg = Color.FromArgb(28, 30, 35), card = Color.FromArgb(40, 43, 50), cardHot = Color.FromArgb(58, 62, 72), txt = Color.FromArgb(225, 228, 232), dim = Color.FromArgb(140, 146, 156);
    public PreviewForm(float scale)
    {
        k = scale; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
        BackColor = bg; DoubleBuffered = true; fT = new Font("Segoe UI", 12f * k, FontStyle.Regular, GraphicsUnit.Pixel);
        CreateHandle();
    }
    protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; return cp; } }
    protected override bool ShowWithoutActivation { get { return true; } }
    void Clear() { foreach (var c in cards) if (c.Thumb != IntPtr.Zero) Native.DwmUnregisterThumbnail(c.Thumb); cards.Clear(); }
    public string Signature() { var sb = new StringBuilder(); foreach (var c in cards) sb.Append(c.W.H).Append(','); return sb.ToString(); }
    public void ShowFor(string key, List<WinItem> wins, RectangleF anchor, Rectangle screen)
    {
        Clear(); Key = key;
        float pad = 10 * k, titleH = 28 * k, thH = 150 * k, gap = 8 * k, x = pad;
        int max = Math.Max(1, (int)((screen.Width - 40 * k) / (200 * k)));
        foreach (var w in wins)
        {
            if (cards.Count >= Math.Min(max, 8)) break;
            var c = new Card(); c.W = w;
            IntPtr th; if (Native.DwmRegisterThumbnail(Handle, w.H, out th) == 0) c.Thumb = th;
            float tw = thH * 16f / 9f;
            if (c.Thumb != IntPtr.Zero) { Native.PSIZE sz; if (Native.DwmQueryThumbnailSourceSize(c.Thumb, out sz) == 0 && sz.cx > 50 && sz.cy > 50) tw = thH * sz.cx / (float)sz.cy; }
            tw = Math.Max(140 * k, Math.Min(330 * k, tw));
            c.Box = new RectangleF(x, pad, tw + 2 * gap, titleH + thH + gap * 2);
            c.Pic = new RectangleF(x + gap, pad + titleH + gap, tw, thH);
            c.X = new RectangleF(c.Box.Right - 26 * k, pad + 4 * k, 22 * k, 22 * k);
            cards.Add(c); x = c.Box.Right + gap;
        }
        int W = (int)(x - gap + pad), Hh = (int)(cards.Count > 0 ? cards[0].Box.Bottom + pad : 0);
        int left = (int)(screen.Left + anchor.X + anchor.Width / 2 - W / 2f);
        left = Math.Max(screen.Left + 8, Math.Min(screen.Right - W - 8, left));
        Bounds = new Rectangle(left, (int)(screen.Top + anchor.Bottom + 10 * k), W, Hh);
        foreach (var c in cards) Place(c);
        if (!Visible) Show(); Invalidate();
    }
    void Place(Card c)
    {
        if (c.Thumb == IntPtr.Zero) return;
        Native.PSIZE sz; Native.DwmQueryThumbnailSourceSize(c.Thumb, out sz);
        float bw = c.Pic.Width, bh = c.Pic.Height, w = bw, h = bh;
        if (sz.cx > 0 && sz.cy > 0) { float r = Math.Min(bw / sz.cx, bh / sz.cy); w = sz.cx * r; h = sz.cy * r; }
        var p = new Native.DWM_THUMBNAIL_PROPERTIES();
        p.dwFlags = 1 | 4 | 8 | 16; p.opacity = 255; p.fVisible = true; p.fSourceClientAreaOnly = false;
        p.rcDestination.Left = (int)(c.Pic.X + (bw - w) / 2); p.rcDestination.Top = (int)(c.Pic.Y + (bh - h) / 2);
        p.rcDestination.Right = (int)(p.rcDestination.Left + w); p.rcDestination.Bottom = (int)(p.rcDestination.Top + h);
        Native.DwmUpdateThumbnailProperties(c.Thumb, ref p);
    }
    public void HidePreview() { Clear(); Key = null; Hide(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; g.Clear(bg);
        foreach (var c in cards)
        {
            bool hot = c.Box.Contains(hover);
            using (var path = Bar.RoundPath(c.Box, 8 * k)) using (var b = new SolidBrush(hot ? cardHot : card)) g.FillPath(b, path);
            float ic = 18 * k;
            try { g.DrawIcon(c.W.Icon ?? SystemIcons.Application, Rectangle.Round(new RectangleF(c.Box.X + 8 * k, c.Box.Y + 5 * k, ic, ic))); } catch { }
            var sf = new StringFormat(); sf.LineAlignment = StringAlignment.Center; sf.Trimming = StringTrimming.EllipsisCharacter; sf.FormatFlags = StringFormatFlags.NoWrap;
            using (var b = new SolidBrush(txt)) g.DrawString(c.W.Title, fT, b, new RectangleF(c.Box.X + 14 * k + ic, c.Box.Y, c.Box.Width - ic - 46 * k, 28 * k), sf);
            if (Native.IsIconic(c.W.H)) using (var b = new SolidBrush(dim)) { var cf = new StringFormat(); cf.Alignment = StringAlignment.Center; cf.LineAlignment = StringAlignment.Center; g.DrawString("minimized", fT, b, c.Pic, cf); }
            if (hot) {
                bool xHot = c.X.Contains(hover);
                if (xHot) using (var path = Bar.RoundPath(c.X, 4 * k)) using (var b = new SolidBrush(Color.FromArgb(196, 43, 28))) g.FillPath(b, path);
                using (var pen = new Pen(txt, 1.6f * k)) { float m = 7 * k; g.DrawLine(pen, c.X.X + m, c.X.Y + m, c.X.Right - m, c.X.Bottom - m); g.DrawLine(pen, c.X.Right - m, c.X.Y + m, c.X.X + m, c.X.Bottom - m); }
            }
        }
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (e.Location != hover) { hover = e.Location; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = new Point(-1, -1); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        foreach (var c in cards)
        {
            if (c.X.Contains(e.Location) || (e.Button == MouseButtons.Middle && c.Box.Contains(e.Location))) { if (OnCloseWin != null) OnCloseWin(c.W.H); return; }
            if (c.Box.Contains(e.Location)) { if (OnPick != null) OnPick(c.W.H); HidePreview(); return; }
        }
    }
}

class Bar : Form
{
    static string Home = AppDomain.CurrentDomain.BaseDirectory;   // pins + settings live next to TopBar.exe, wherever it is
    static string PinDir = Path.Combine(Home, "Pinned");
    static string Ini = Path.Combine(Home, "TopBar settings.txt");
    static string LogF = Path.Combine(Home, "TopBar.log");

    Screen scr; float k = 1f, baseK = 1f; int H, Trig = 2, sizePct = 125;
    bool shown = false; int anim = 0; DateTime lastInside = DateTime.Now, winUntil = DateTime.MinValue;
    bool gameGuard = true, menuOpen = false, taskMgrPinned = false;
    RectangleF statsRect, trayRect, btRect, soundRect, clockRect;
    int volPct = -1; bool muted = false; Font fGlyph; string gUp = "\uE70E", gBt = "\uE702", gVol = "\uE767", gMute = "\uE74F";
    List<WinItem> wins = new List<WinItem>(); List<PinItem> pins = new List<PinItem>(); List<AppGroup> groups = new List<AppGroup>();
    PreviewForm prev; string hoverKey = null; DateTime hoverSince = DateTime.Now, prevLastInside = DateTime.MinValue;
    ToolTip tip = new ToolTip(); AppGroup tipFor = null; Dictionary<string, int> cycleAt = new Dictionary<string, int>();
    Dictionary<IntPtr, Icon> iconCache = new Dictionary<IntPtr, Icon>();
    RectangleF startRect; Point hover = new Point(-1, -1);
    Stats stats = new Stats();
    System.Windows.Forms.Timer fast = new System.Windows.Forms.Timer(), slow = new System.Windows.Forms.Timer();
    Font fTitle, fSmall, fClock, fDate;
    Color bg = Color.FromArgb(18, 20, 24), btn = Color.FromArgb(34, 37, 43), btnHot = Color.FromArgb(52, 56, 64), accent = Color.FromArgb(255, 170, 30), txt = Color.FromArgb(225, 228, 232), dim = Color.FromArgb(140, 146, 156);

    public static void Log(string m) { try { File.AppendAllText(LogF, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + m + "\r\n"); } catch { } }

    public Bar()
    {
        Directory.CreateDirectory(PinDir);
        LoadSettings();
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
        BackColor = bg; DoubleBuffered = true; Text = "TopBar";
        scr = Screen.PrimaryScreen;
        CreateHandle();
        uint dpi = 96; try { dpi = Native.GetDpiForWindow(Handle); } catch { }
        if (dpi < 96) dpi = 96;
        k = dpi / 96f; baseK = k; k *= sizePct / 100f; H = (int)(42 * k);   // bar size % on top of Windows scaling
        fTitle = new Font("Segoe UI", 13f * k, FontStyle.Regular, GraphicsUnit.Pixel);
        fSmall = new Font("Segoe UI Semibold", 13f * k, FontStyle.Regular, GraphicsUnit.Pixel);
        fClock = new Font("Segoe UI Semibold", 15f * k, FontStyle.Regular, GraphicsUnit.Pixel);
        fDate = new Font("Segoe UI", 11f * k, FontStyle.Regular, GraphicsUnit.Pixel);
        string gf = "Segoe MDL2 Assets"; foreach (var ff in FontFamily.Families) if (ff.Name == "Segoe Fluent Icons") gf = ff.Name;
        fGlyph = new Font(gf, 15f * k, FontStyle.Regular, GraphicsUnit.Pixel);
        Bounds = new Rectangle(scr.Bounds.Left, scr.Bounds.Top - H, scr.Bounds.Width, H);
        MakeDefaultPins(); PinTaskManager(); LoadPins(); RefreshWindows();
        tip.ShowAlways = true; tip.InitialDelay = 300;
        prev = new PreviewForm(baseK);
        prev.OnPick = delegate(IntPtr h) { Activate(h); Invalidate(); };
        prev.OnCloseWin = delegate(IntPtr h) { Native.PostMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero); var t = new System.Windows.Forms.Timer(); t.Interval = 400; t.Tick += delegate { t.Stop(); t.Dispose(); RefreshWindows(); SyncPreview(true); }; t.Start(); };
        stats.Start();
        fast.Interval = 15; fast.Tick += delegate { Fast(); }; fast.Start();
        slow.Interval = 1000; slow.Tick += delegate { RefreshWindows(); SyncPreview(false); if (shown || volPct < 0) { volPct = Audio.GetVolume(); muted = Audio.GetMute(); } Invalidate(); }; slow.Start();
        AutoHideRealTaskbar();
        Log("v0.7 started on screen " + scr.Bounds + " dpi " + dpi + " height " + H);
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; return cp; } // NOACTIVATE | TOOLWINDOW | TOPMOST
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    // ---------------- settings / pins ----------------
    void LoadSettings()
    {
        try { if (File.Exists(Ini)) foreach (string l in File.ReadAllLines(Ini)) { if (l.StartsWith("GameGuard=")) gameGuard = l.EndsWith("1"); if (l.StartsWith("TaskMgrPinned=")) taskMgrPinned = l.EndsWith("1"); int sz; if (l.StartsWith("Size=") && int.TryParse(l.Substring(5).Trim(), out sz) && sz >= 75 && sz <= 250) sizePct = sz; } } catch { }
    }
    void SaveSettings() { try { File.WriteAllText(Ini, "GameGuard=" + (gameGuard ? "1" : "0") + "\r\nTaskMgrPinned=" + (taskMgrPinned ? "1" : "0") + "\r\nSize=" + sizePct + "\r\n"); } catch { } }

    void MakeDefaultPins()
    {
        if (Directory.GetFiles(PinDir).Length > 0) return;
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[][] guess = {
            new[] { "1 File Explorer", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") },
            new[] { "2 Chrome", Path.Combine(pf, @"Google\Chrome\Application\chrome.exe") },
            new[] { "3 Edge", Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe") },
            new[] { "4 Tarkov Launcher", @"C:\Battlestate Games\BsgLauncher\BsgLauncher.exe" },
            new[] { "5 Steam", Path.Combine(pf86, @"Steam\steam.exe") },
            new[] { "6 Discord", Path.Combine(la, @"Discord\Update.exe") } };
        foreach (string[] g in guess)
        {
            if (!File.Exists(g[1])) continue;
            try {
                Type t = Type.GetTypeFromProgID("WScript.Shell"); object sh = Activator.CreateInstance(t);
                object l = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { Path.Combine(PinDir, g[0] + ".lnk") });
                Type lt = l.GetType();
                lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, l, new object[] { g[1] });
                if (g[0].Contains("Discord")) lt.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, l, new object[] { "--processStart Discord.exe" });
                lt.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, l, null);
            } catch { }
        }
        Log("made default pins");
    }
    static void MakeShortcut(string lnk, string target, string args)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell"); object sh = Activator.CreateInstance(t);
        object l = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
        Type lt = l.GetType();
        lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, l, new object[] { target });
        if (args != null) lt.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, l, new object[] { args });
        lt.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, l, null);
    }
    void PinTaskManager()
    {
        if (taskMgrPinned) return;
        try { MakeShortcut(Path.Combine(PinDir, "9 Task Manager.lnk"), TaskMgrPath(), null); taskMgrPinned = true; SaveSettings(); Log("pinned Task Manager"); } catch (Exception ex) { Log("task manager pin failed " + ex.Message); }
    }
    static string TaskMgrPath() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Taskmgr.exe"); }
    static string ResolveLnk(string lnk)
    {
        try {
            Type t = Type.GetTypeFromProgID("WScript.Shell"); object sh = Activator.CreateInstance(t);
            object l = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            return (string)l.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, l, null);
        } catch { return null; }
    }
    static string LnkArgs(string lnk)
    {
        try {
            Type t = Type.GetTypeFromProgID("WScript.Shell"); object sh = Activator.CreateInstance(t);
            object l = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            return (string)l.GetType().InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, l, null);
        } catch { return null; }
    }
    // Apps like Discord install an "Update.exe" launcher that starts the real app from a versioned folder (app-1.0.9xxx).
    // Find that real exe so the pin gets the right icon and lights up when the app is open.
    static string RealApp(string target, string args)
    {
        try {
            if (string.IsNullOrEmpty(target) || !Path.GetFileName(target).Equals("Update.exe", StringComparison.OrdinalIgnoreCase)) return target;
            string dir = Path.GetDirectoryName(target), name = null;
            if (args != null) { int i = args.IndexOf("--processStart", StringComparison.OrdinalIgnoreCase); if (i >= 0) { string rest = args.Substring(i + 14).Trim().Trim('"'); int sp = rest.IndexOfAny(new[] { ' ', '"' }); name = sp > 0 ? rest.Substring(0, sp) : rest; } }
            if (string.IsNullOrEmpty(name)) name = Path.GetFileName(dir) + ".exe";
            string best = null; Version bv = null;
            foreach (string d in Directory.GetDirectories(dir, "app-*"))
            {
                string exe = Path.Combine(d, name); if (!File.Exists(exe)) continue;
                Version v; if (!Version.TryParse(Path.GetFileName(d).Substring(4), out v)) v = new Version(0, 0);
                if (bv == null || v > bv) { bv = v; best = exe; }
            }
            if (best != null) { Log("pin " + Path.GetFileName(dir) + " -> " + best); return best; }
        } catch (Exception ex) { Log("RealApp failed " + ex.Message); }
        return target;
    }
    static void OpenTaskManager() { try { Process.Start(TaskMgrPath()); } catch (Exception ex) { Log("task manager failed " + ex.Message); } }
    void LoadPins()
    {
        pins.Clear();
        var files = new List<string>(Directory.GetFiles(PinDir)); files.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string f in files)
        {
            string ext = Path.GetExtension(f).ToLower(); if (ext != ".lnk" && ext != ".exe" && ext != ".url") continue;
            var p = new PinItem(); p.Path = f; p.Name = Path.GetFileNameWithoutExtension(f);
            string nm = p.Name; int sp = nm.IndexOf(' '); if (sp > 0 && sp <= 3) { int dummy; if (int.TryParse(nm.Substring(0, sp), out dummy)) nm = nm.Substring(sp + 1); } p.Name = nm;
            p.Target = ext == ".lnk" ? ResolveLnk(f) : (ext == ".exe" ? f : null);
            p.Target = RealApp(p.Target, ext == ".lnk" ? LnkArgs(f) : null);   // Discord/Slack/Teams-style updaters -> the real app
            p.Icon = null;
            try { if (!string.IsNullOrEmpty(p.Target) && File.Exists(p.Target)) p.Icon = Icon.ExtractAssociatedIcon(p.Target); } catch { }   // the app's own icon = no shortcut arrow
            if (p.Icon == null) { try { p.Icon = Icon.ExtractAssociatedIcon(f); } catch { p.Icon = SystemIcons.Application; } }
            pins.Add(p);
        }
    }

    // ---------------- windows list ----------------
    void RefreshWindows()
    {
        var list = new List<WinItem>(); var seen = new HashSet<IntPtr>();
        Native.EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (h == Handle || !Native.IsWindowVisible(h)) return true;
            int ex = Native.GetWindowLong(h, -20);
            if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return true;          // tool window
            if (Native.GetWindow(h, 4) != IntPtr.Zero && (ex & 0x40000) == 0) return true; // owned
            int cloaked; if (Native.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            int len = Native.GetWindowTextLength(h); if (len == 0) return true;
            var sb = new StringBuilder(len + 1); Native.GetWindowText(h, sb, sb.Capacity);
            var cn = new StringBuilder(256); Native.GetClassName(h, cn, 256); string c = cn.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return true;
            var w = new WinItem(); w.H = h; w.Title = sb.ToString(); w.Icon = GetIcon(h); w.Exe = ExeOf(h);
            list.Add(w); seen.Add(h); return true;
        }, IntPtr.Zero);
        // keep stable order: existing first, new ones at end
        var ordered = new List<WinItem>();
        foreach (var old in wins) foreach (var n in list) if (n.H == old.H) { ordered.Add(n); break; }
        foreach (var n in list) { bool found = false; foreach (var o in ordered) if (o.H == n.H) { found = true; break; } if (!found) ordered.Add(n); }
        wins = ordered;
        var dead = new List<IntPtr>(); foreach (var key in iconCache.Keys) if (!seen.Contains(key)) dead.Add(key); foreach (var d in dead) iconCache.Remove(d);
        BuildGroups(list);
    }
    // one button per app, like Windows: pinned apps first (lit up if open), then other open apps.
    // list is in z-order, so Wins[0] of each group is the one you used last.
    void BuildGroups(List<WinItem> zorder)
    {
        var g = new List<AppGroup>(); var byKey = new Dictionary<string, AppGroup>();
        foreach (var p in pins)
        {
            var a = new AppGroup(); a.Pin = p; a.Icon = p.Icon; a.Exe = p.Target;
            a.Key = string.IsNullOrEmpty(p.Target) ? "pin:" + p.Path : p.Target.ToLower();
            g.Add(a); if (!byKey.ContainsKey(a.Key)) byKey[a.Key] = a;
        }
        foreach (var w in wins)   // stable left-to-right order for unpinned apps
        {
            string key = w.Exe == null ? "hwnd:" + w.H : w.Exe.ToLower();
            if (key.EndsWith("applicationframehost.exe")) key = "hwnd:" + w.H;   // Store apps all share one host - keep them separate
            AppGroup a;
            if (!byKey.TryGetValue(key, out a)) { a = new AppGroup(); a.Key = key; a.Exe = w.Exe; a.Icon = w.Icon; g.Add(a); byKey[key] = a; }
        }
        foreach (var w in zorder)
        {
            string key = w.Exe == null ? "hwnd:" + w.H : w.Exe.ToLower();
            if (key.EndsWith("applicationframehost.exe")) key = "hwnd:" + w.H;
            AppGroup a; if (byKey.TryGetValue(key, out a)) a.Wins.Add(w);
        }
        foreach (var a in g) if (a.Pin == null && a.Wins.Count > 0) a.Icon = a.Wins[0].Icon;
        groups = g;
    }
    string ExeOf(IntPtr h)
    {
        try {
            uint pid; Native.GetWindowThreadProcessId(h, out pid);
            IntPtr ph = Native.OpenProcess(0x1000, false, pid);
            if (ph == IntPtr.Zero) return null;
            var sb = new StringBuilder(1024); int n = 1024; string r = null;
            if (Native.QueryFullProcessImageName(ph, 0, sb, ref n)) r = sb.ToString();
            Native.CloseHandle(ph); return r;
        } catch { return null; }
    }
    Icon GetIcon(IntPtr h)
    {
        Icon ic; if (iconCache.TryGetValue(h, out ic)) return ic;
        IntPtr hi = IntPtr.Zero, r;
        if (Native.SendMessageTimeout(h, 0x7F, (IntPtr)2, IntPtr.Zero, 2, 50, out r) != IntPtr.Zero) hi = r;           // WM_GETICON small2
        if (hi == IntPtr.Zero && Native.SendMessageTimeout(h, 0x7F, (IntPtr)0, IntPtr.Zero, 2, 50, out r) != IntPtr.Zero) hi = r;
        if (hi == IntPtr.Zero) hi = Native.GetClassLongPtr(h, -34);
        if (hi == IntPtr.Zero) hi = Native.GetClassLongPtr(h, -14);
        try { if (hi != IntPtr.Zero) ic = (Icon)Icon.FromHandle(hi).Clone(); } catch { ic = null; }
        if (ic == null) {
            try {
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                IntPtr ph = Native.OpenProcess(0x1000, false, pid);
                if (ph != IntPtr.Zero) { var sb = new StringBuilder(1024); int n = 1024; if (Native.QueryFullProcessImageName(ph, 0, sb, ref n)) ic = Icon.ExtractAssociatedIcon(sb.ToString()); Native.CloseHandle(ph); }
            } catch { }
        }
        if (ic == null) ic = SystemIcons.Application;
        iconCache[h] = ic; return ic;
    }

    // ---------------- show / hide ----------------
    bool ForegroundIsFullscreenGame()
    {
        IntPtr f = Native.GetForegroundWindow(); if (f == IntPtr.Zero || f == Handle) return false;
        var cn = new StringBuilder(256); Native.GetClassName(f, cn, 256); string c = cn.ToString();
        if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
        int style = Native.GetWindowLong(f, -16);
        if ((style & 0x00C00000) == 0x00C00000) return false;   // has a title bar = normal app (maximized Chrome etc), not a game
        Native.RECT r; if (!Native.GetWindowRect(f, out r)) return false;
        return r.Left <= scr.Bounds.Left && r.Top <= scr.Bounds.Top && r.Right >= scr.Bounds.Right && r.Bottom >= scr.Bounds.Bottom;
    }
    void Fast()
    {
        Point c = Cursor.Position;
        bool onScreen = c.X >= scr.Bounds.Left && c.X < scr.Bounds.Right;
        bool winKey = (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
        if (winKey) winUntil = DateTime.Now.AddSeconds(4);
        bool edge = onScreen && c.Y <= scr.Bounds.Top + Trig;
        if (edge && gameGuard && !shown && ForegroundIsFullscreenGame()) edge = false;   // game guard: no mouse-pop over a fullscreen game
        bool inPrev = prev != null && prev.Visible && prev.Bounds.Contains(c);
        bool inside = (onScreen && c.Y < scr.Bounds.Top + H + (int)(8 * k)) || inPrev;
        if (edge || (shown && inside) || menuOpen || DateTime.Now < winUntil) lastInside = DateTime.Now;
        bool want = (DateTime.Now - lastInside).TotalMilliseconds < 350;
        if (want != shown) { shown = want; if (shown) { if (!Visible) Show(); Native.ShowWindow(Handle, 4); TopMost = true; } }
        int target = shown ? scr.Bounds.Top : scr.Bounds.Top - H;
        if (Top != target) { int step = Math.Max(1, (int)(H / 5f)); Top = Top < target ? Math.Min(target, Top + step) : Math.Max(target, Top - step); }
        else if (!shown && Visible) Hide();
        if (shown) { Point lp = PointToClient(c); if (lp != hover) { hover = lp; Invalidate(); } }
        PreviewTick(c, inPrev);
        if (++anim % 60 == 0 && shown) Invalidate(); // clock/stats repaint ~1s
    }

    void PreviewTick(Point c, bool inPrev)
    {
        if (prev == null) return;
        if (!shown) { if (prev.Visible) prev.HidePreview(); hoverKey = null; return; }
        Point lp = PointToClient(c); AppGroup over = null;
        foreach (var a in groups) if (a.Wins.Count > 0 && a.Rect.Contains(lp)) over = a;
        string key = over == null ? null : over.Key;
        if (key != hoverKey) { hoverKey = key; hoverSince = DateTime.Now; }
        if (inPrev || (key != null && key == prev.Key)) prevLastInside = DateTime.Now;
        if (key != null && key != prev.Key && (DateTime.Now - hoverSince).TotalMilliseconds > (prev.Visible ? 80 : 380))
        { tip.Hide(this); prev.ShowFor(over.Key, over.Wins, over.Rect, scr.Bounds); prevLastInside = DateTime.Now; }
        else if (prev.Visible && (DateTime.Now - prevLastInside).TotalMilliseconds > 300) prev.HidePreview();
    }
    // after windows open/close, keep the preview in step
    void SyncPreview(bool force)
    {
        if (prev == null || !prev.Visible || prev.Key == null) return;
        foreach (var a in groups) if (a.Key == prev.Key)
        {
            if (a.Wins.Count == 0) { prev.HidePreview(); return; }
            var sb = new StringBuilder(); int n = 0; foreach (var w in a.Wins) { if (n++ >= 8) break; sb.Append(w.H).Append(','); }
            if (force || sb.ToString() != prev.Signature()) prev.ShowFor(a.Key, a.Wins, a.Rect, scr.Bounds);
            return;
        }
        prev.HidePreview();
    }
    void AutoHideRealTaskbar()
    {
        try {
            var d = new Native.APPBARDATA(); d.cbSize = Marshal.SizeOf(typeof(Native.APPBARDATA));
            int state = (int)Native.SHAppBarMessage(4, ref d);            // ABM_GETSTATE
            if ((state & 1) == 0) { d.lParam = (IntPtr)(state | 1); Native.SHAppBarMessage(10, ref d); Log("switched the Windows taskbar to auto-hide"); } // ABM_SETSTATE
        } catch { }
    }

    // ---------------- drawing ----------------
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(bg);
        using (var line = new Pen(Color.FromArgb(40, 255, 255, 255), 1)) g.DrawLine(line, 0, H - 1, Width, H - 1);
        float pad = 6 * k, x = 8 * k, bh = H - pad * 2, ic = 22 * k;
        IntPtr fg = Native.GetForegroundWindow();

        // ---- right side first: clock + stats (so we know how much room the middle has) ----
        string time = DateTime.Now.ToString("h:mm tt"), date = DateTime.Now.ToString("ddd MMM d");
        float tw = Math.Max(g.MeasureString(time, fClock).Width, g.MeasureString(date, fDate).Width);
        float right = Width - 12 * k - tw;
        clockRect = new RectangleF(right - 6 * k, pad, tw + 12 * k, bh); if (clockRect.Contains(hover)) Btn(g, clockRect, false);
        using (var b = new SolidBrush(txt)) g.DrawString(time, fClock, b, right, 1 * k);
        using (var b = new SolidBrush(dim)) g.DrawString(date, fDate, b, right + (tw - g.MeasureString(date, fDate).Width) / 2, H - fDate.Height - 3 * k);
        // sound: speaker + volume %
        string vt = volPct < 0 ? "" : (muted ? "mute" : volPct + "%");
        float vtw = g.MeasureString(vt, fSmall).Width, gw = 22 * k;
        right = clockRect.X - 6 * k;
        soundRect = new RectangleF(right - gw - vtw - 14 * k, pad, gw + vtw + 14 * k, bh); Btn(g, soundRect, false);
        Glyph(g, muted ? gMute : gVol, soundRect.X + 6 * k, pad, gw, bh, muted ? dim : txt);
        using (var b = new SolidBrush(txt)) g.DrawString(vt, fSmall, b, soundRect.X + 8 * k + gw, pad + (bh - fSmall.Height) / 2);
        btRect = new RectangleF(soundRect.X - 4 * k - bh, pad, bh, bh); Btn(g, btRect, false); Glyph(g, gBt, btRect.X, pad, bh, bh, txt);
        trayRect = new RectangleF(btRect.X - 4 * k - bh * 0.8f, pad, bh * 0.8f, bh); Btn(g, trayRect, false); Glyph(g, gUp, trayRect.X, pad, trayRect.Width, bh, txt);
        right = trayRect.X - 4 * k + 22 * k - 12 * k;
        string[][] st = { new[] { "CPU", stats.CpuC, stats.CpuW }, new[] { "GPU", stats.GpuC, stats.GpuW }, new[] { "RAM", stats.Ram, "" } };
        float sxr = right - 12 * k, statsRight = sxr;
        for (int i = st.Length - 1; i >= 0; i--)
        {
            string val = st[i][1] + (st[i][2] != "" ? "  " + st[i][2] : "");
            float vw = g.MeasureString(val, fSmall).Width, lw = g.MeasureString(st[i][0], fDate).Width;
            float bx = sxr - vw - lw - 18 * k;
            var r = new RectangleF(bx, pad, vw + lw + 18 * k, bh); Btn(g, r, false);
            Color vc = txt;
            if (i == 0 && stats.CpuTemp >= 95) vc = Color.FromArgb(255, 80, 70); else if (i == 0 && stats.CpuTemp >= 85) vc = Color.FromArgb(255, 170, 30);
            using (var b = new SolidBrush(dim)) g.DrawString(st[i][0], fDate, b, bx + 7 * k, pad + (bh - fDate.Height) / 2);
            using (var b = new SolidBrush(vc)) g.DrawString(val, fSmall, b, bx + 10 * k + lw, pad + (bh - fSmall.Height) / 2);
            sxr = bx - 6 * k;
        }
        statsRect = new RectangleF(sxr + 6 * k, pad, statsRight - sxr - 6 * k, bh);   // click = Task Manager

        // ---- middle group: Start + app icons, centered like Windows 11 ----
        float limit = sxr - 16 * k, cell = bh + 4 * k;
        var pinned = new List<AppGroup>(); var others = new List<AppGroup>();
        foreach (var a in groups) { if (a.Pin != null) pinned.Add(a); else if (a.Wins.Count > 0) others.Add(a); }
        float groupW = bh + 10 * k + pinned.Count * cell + (others.Count > 0 && pinned.Count > 0 ? 10 * k : 0) + others.Count * cell - 4 * k;
        x = (Width - groupW) / 2;
        if (x + groupW > limit) x = Math.Max(8 * k, limit - groupW);

        startRect = new RectangleF(x, pad, bh, bh); Btn(g, startRect, false);
        float q = ic / 2 - 1.5f * k, sx = startRect.X + (bh - ic) / 2, sy = startRect.Y + (bh - ic) / 2;
        using (var b = new SolidBrush(accent)) { g.FillRectangle(b, sx, sy, q, q); g.FillRectangle(b, sx + q + 3 * k, sy, q, q); g.FillRectangle(b, sx, sy + q + 3 * k, q, q); g.FillRectangle(b, sx + q + 3 * k, sy + q + 3 * k, q, q); }
        x += bh + 10 * k;
        foreach (var a in groups) a.Rect = RectangleF.Empty;
        foreach (var a in pinned) { DrawApp(g, a, x, pad, bh, ic, fg); x += cell; }
        if (others.Count > 0 && pinned.Count > 0) { using (var sep = new Pen(Color.FromArgb(50, 255, 255, 255), 1.5f * k)) g.DrawLine(sep, x + 3 * k, pad + 6 * k, x + 3 * k, H - pad - 6 * k); x += 10 * k; }
        foreach (var a in others) { if (x + bh > limit) break; DrawApp(g, a, x, pad, bh, ic, fg); x += cell; }
    }
    void DrawApp(Graphics g, AppGroup a, float x, float pad, float bh, float ic, IntPtr fg)
    {
        a.Rect = new RectangleF(x, pad, bh, bh);
        bool active = false; foreach (var w in a.Wins) if (w.H == fg) active = true;
        Btn(g, a.Rect, active);
        try { g.DrawIcon(a.Icon ?? SystemIcons.Application, Rectangle.Round(new RectangleF(x + (bh - ic) / 2, pad + (bh - ic) / 2 - 1 * k, ic, ic))); } catch { }
        if (a.Wins.Count > 0) {   // open-app dash: short grey, long orange if it's the one you're using
            float dw = active ? 14 * k : 6 * k;
            using (var b = new SolidBrush(active ? accent : dim)) using (var path = Round(new RectangleF(x + (bh - dw) / 2, pad + bh - 4 * k, dw, 3 * k), 1.5f * k)) g.FillPath(b, path);
        }
    }
    void Glyph(Graphics g, string ch, float x, float y, float w, float h, Color c)
    {
        var sf = new StringFormat(); sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
        using (var b = new SolidBrush(c)) g.DrawString(ch, fGlyph, b, new RectangleF(x, y, w, h), sf);
    }
    void Btn(Graphics g, RectangleF r, bool active)
    {
        bool hot = r.Contains(hover);
        using (var path = Round(r, 6 * k)) using (var b = new SolidBrush(active ? btnHot : (hot ? btnHot : btn))) g.FillPath(b, path);
    }
    public static GraphicsPath RoundPath(RectangleF r, float rad) { return Round(r, rad); }
    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }

    // ---------------- clicks ----------------
    protected override void OnMouseUp(MouseEventArgs e)
    {
        PointF p = e.Location;
        if (startRect.Contains(p)) {
            if (e.Button == MouseButtons.Right) { BarMenu(e.Location); return; }
            Native.keybd_event(0x11, 0, 0, UIntPtr.Zero); Native.keybd_event(0x1B, 0, 0, UIntPtr.Zero); Native.keybd_event(0x1B, 0, 2, UIntPtr.Zero); Native.keybd_event(0x11, 0, 2, UIntPtr.Zero); // Ctrl+Esc = Start
            winUntil = DateTime.Now.AddSeconds(1); return;
        }
        if (statsRect.Contains(p) && e.Button == MouseButtons.Left) { OpenTaskManager(); return; }
        if (soundRect.Contains(p)) { if (e.Button == MouseButtons.Middle) { Audio.SetMute(!Audio.GetMute()); muted = Audio.GetMute(); Invalidate(); } else SoundMenu(); return; }
        if (btRect.Contains(p)) { BtMenu(); return; }
        if (trayRect.Contains(p)) { WinCombo(0x42); var t = new System.Windows.Forms.Timer(); t.Interval = 300; t.Tick += delegate { t.Stop(); t.Dispose(); Native.keybd_event(0x0D, 0, 0, UIntPtr.Zero); Native.keybd_event(0x0D, 0, 2, UIntPtr.Zero); }; t.Start(); return; }   // Win+B then Enter = Windows' hidden icons popup
        if (clockRect.Contains(p)) { WinCombo(0x4E); return; }   // Win+N = calendar + notifications
        foreach (var ag in groups) if (ag.Rect.Contains(p)) { ClickApp(ag, e); return; }
        if (e.Button == MouseButtons.Right) BarMenu(e.Location);
    }
    void Activate(IntPtr h)
    {
        if (Native.IsIconic(h)) Native.ShowWindow(h, 9);
        Native.keybd_event(0x12, 0, 0, UIntPtr.Zero); Native.keybd_event(0x12, 0, 2, UIntPtr.Zero); // Alt tap lets us take focus
        Native.SetForegroundWindow(h);
    }
    void ClickApp(AppGroup a, MouseEventArgs e)
    {
        if (prev != null && prev.Visible) prev.HidePreview();
        hoverKey = a.Key; hoverSince = DateTime.Now.AddSeconds(3);   // don't pop the preview right back up after a click
        if (e.Button == MouseButtons.Right) { AppMenu(a, e.Location); return; }
        if (e.Button == MouseButtons.Middle || a.Wins.Count == 0) { Launch(a); return; }   // middle-click = open another one
        IntPtr fg = Native.GetForegroundWindow(); int at = -1;
        for (int i = 0; i < a.Wins.Count; i++) if (a.Wins[i].H == fg) at = i;
        if (at < 0) Activate(a.Wins[0].H);                                                  // not in front -> bring the last-used one up
        else if (a.Wins.Count == 1) Native.ShowWindow(a.Wins[0].H, 6);                     // only one and it's in front -> minimize
        else { int c; cycleAt.TryGetValue(a.Key, out c); c = (c + 1) % a.Wins.Count; cycleAt[a.Key] = c; Activate(a.Wins[c].H); }  // several -> cycle
        Invalidate();
    }
    static void WinCombo(byte key)
    {
        Native.keybd_event(0x5B, 0, 0, UIntPtr.Zero); Native.keybd_event(key, 0, 0, UIntPtr.Zero);
        Native.keybd_event(key, 0, 2, UIntPtr.Zero); Native.keybd_event(0x5B, 0, 2, UIntPtr.Zero);
    }
    static void OpenUri(string u) { try { Process.Start(u); } catch (Exception ex) { Log("open failed " + u + " " + ex.Message); } }
    void SoundMenu()
    {
        var m = new ContextMenuStrip(); m.ShowImageMargin = false; m.Font = new Font("Segoe UI", 10f);
        int v = Audio.GetVolume(); bool mu = Audio.GetMute();
        var lbl = new ToolStripLabel("Volume  " + (v < 0 ? "--" : v + "%")); m.Items.Add(lbl);
        var tb = new TrackBar(); tb.Minimum = 0; tb.Maximum = 100; tb.TickStyle = TickStyle.None; tb.Width = (int)(240 * k); tb.Value = Math.Max(0, v);
        tb.ValueChanged += delegate { Audio.SetVolume(tb.Value); if (Audio.GetMute()) Audio.SetMute(false); volPct = tb.Value; muted = false; lbl.Text = "Volume  " + tb.Value + "%"; Invalidate(); };
        var host = new ToolStripControlHost(tb); host.AutoSize = false; host.Width = tb.Width; host.Height = (int)(30 * k); m.Items.Add(host);
        var mt = new ToolStripMenuItem(mu ? "Unmute" : "Mute"); mt.Click += delegate { Audio.SetMute(!mu); muted = !mu; Invalidate(); }; m.Items.Add(mt);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(new ToolStripLabel("PLAY SOUND THROUGH"));
        foreach (var d in Audio.List(0)) { var dd = d; var it = new ToolStripMenuItem((dd.IsDefault ? "\u25CF  " : "      ") + dd.Name); it.Click += delegate { Audio.SetDefault(dd.Id); Log("output -> " + dd.Name); volPct = Audio.GetVolume(); Invalidate(); }; m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(new ToolStripLabel("MICROPHONE"));
        foreach (var d in Audio.List(1)) { var dd = d; var it = new ToolStripMenuItem((dd.IsDefault ? "\u25CF  " : "      ") + dd.Name); it.Click += delegate { Audio.SetDefault(dd.Id); Log("input -> " + dd.Name); }; m.Items.Add(it); }
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Sound settings", null, delegate { OpenUri("ms-settings:sound"); });
        m.Items.Add("Volume mixer", null, delegate { OpenUri("ms-settings:apps-volume"); });
        ShowMenu(m, new Point((int)(soundRect.Right - 260 * k), H));
    }
    void BtMenu()
    {
        var m = new ContextMenuStrip(); m.ShowImageMargin = false; m.Font = new Font("Segoe UI", 10f);
        m.Items.Add("Quick settings  (Bluetooth on/off, Wi-Fi, devices)", null, delegate { WinCombo(0x41); });
        m.Items.Add("Bluetooth && devices", null, delegate { OpenUri("ms-settings:bluetooth"); });
        m.Items.Add("Add a Bluetooth device", null, delegate { OpenUri("ms-settings-connectabledevices:devicediscovery"); });
        m.Items.Add("Cast to a screen", null, delegate { WinCombo(0x4B); });
        ShowMenu(m, new Point((int)(btRect.Right - 300 * k), H));
    }
    void Launch(AppGroup a)
    {
        string path = a.Pin != null ? a.Pin.Path : a.Exe;
        try { if (!string.IsNullOrEmpty(path)) Process.Start(path); } catch (Exception ex) { Log("launch failed " + path + " " + ex.Message); }
    }
    void AppMenu(AppGroup a, Point at)
    {
        var m = new ContextMenuStrip();
        foreach (var w in a.Wins) { var ww = w; string t = ww.Title.Length > 60 ? ww.Title.Substring(0, 57) + "..." : ww.Title; m.Items.Add(t, null, delegate { Activate(ww.H); }); }
        if (a.Wins.Count > 0) m.Items.Add(new ToolStripSeparator());
        m.Items.Add(a.Wins.Count > 0 ? "Open another one" : "Open", null, delegate { Launch(a); });
        if (a.Wins.Count > 0) m.Items.Add(a.Wins.Count > 1 ? "Close all " + a.Wins.Count + " windows" : "Close window", null, delegate { foreach (var w in a.Wins) Native.PostMessage(w.H, 0x10, IntPtr.Zero, IntPtr.Zero); });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("TopBar options...", null, delegate { BarMenu(at); });
        ShowMenu(m, at);
    }
    void BarMenu(Point at)
    {
        var m = new ContextMenuStrip();
        var gg = new ToolStripMenuItem("Game guard (don't pop over fullscreen games)"); gg.Checked = gameGuard; gg.Click += delegate { gameGuard = !gameGuard; SaveSettings(); };
        m.Items.Add(gg);
        m.Items.Add("Task Manager", null, delegate { OpenTaskManager(); });
        var size = new ToolStripMenuItem("Bar size");
        foreach (int pct in new[] { 100, 125, 150, 175 })
        {
            int pp = pct; var it = new ToolStripMenuItem(pct + "%"); it.Checked = pct == sizePct;
            it.Click += delegate { sizePct = pp; SaveSettings(); Log("size -> " + pp + "%, restarting"); Application.Restart(); };
            size.DropDownItems.Add(it);
        }
        m.Items.Add(size);
        m.Items.Add("Edit pinned apps (drop shortcuts in this folder)", null, delegate { Process.Start("explorer.exe", "\"" + PinDir + "\""); });
        m.Items.Add("Reload pinned apps", null, delegate { LoadPins(); Invalidate(); });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Close TopBar", null, delegate { Log("closed from menu"); Application.Exit(); });
        ShowMenu(m, at);
    }
    void ShowMenu(ContextMenuStrip m, Point at)
    {
        menuOpen = true; m.Closed += delegate { menuOpen = false; lastInside = DateTime.Now; };
        m.Show(this, at);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        AppGroup over = null; foreach (var a in groups) if (a.Rect.Contains(e.Location)) over = a;
        if (over == tipFor) return;
        tipFor = over; tip.Hide(this);
        if (over == null || over.Wins.Count > 0) return;
        string t = over.Wins.Count > 0 ? over.Wins[0].Title : (over.Pin != null ? over.Pin.Name : "");
        if (over.Wins.Count > 1) t += "   (+" + (over.Wins.Count - 1) + " more)";
        if (t.Length > 0) tip.Show(t, this, (int)over.Rect.X, H + 2, 3000);
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); tipFor = null; tip.Hide(this); }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        try { if (!Native.SetProcessDpiAwarenessContext((IntPtr)(-4))) Native.SetProcessDPIAware(); } catch { try { Native.SetProcessDPIAware(); } catch { } }
        bool fresh; var mx = new Mutex(true, "Claude.TopBar.SingleInstance", out fresh);
        if (!fresh) { try { if (!mx.WaitOne(4000)) return; } catch (AbandonedMutexException) { } }   // give a restarting TopBar a moment to close
        Application.EnableVisualStyles();
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Bar.Log("ERROR " + e.Exception); };
        try { Application.Run(new Bar()); } catch (Exception ex) { Bar.Log("CRASH " + ex); }
        GC.KeepAlive(mx);
    }
}
