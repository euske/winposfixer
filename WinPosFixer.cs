//  WinPosFixer.cs
//
using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

public class WinPosFixer : Form {

    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.Run(new WinPosFixer());
    }

    private const string CONFIG_PATH = "winpos.csv";
    private IContainer _components;
    private static Icon _iconActive;
    private static Icon _iconInactive;
    private static NotifyIcon _notifyIcon;
    private static ToolStripMenuItem _enabled;
    private static Entry[] _entries;

    public WinPosFixer() {
        _components = new System.ComponentModel.Container();
        _iconActive = getIcon("WinPosActive.ico");
        _iconInactive = getIcon("WinPosInactive.ico");
        _notifyIcon = new NotifyIcon(_components);
        _notifyIcon.DoubleClick += icon_DoubleClick;
        _enabled = new ToolStripMenuItem();
        _enabled.Text = "Active";
        _enabled.Click += check_Click;
        _enabled.Checked = true;
        Font font = _enabled.Font;
        _enabled.Font = new Font(font, font.Style | FontStyle.Bold);

        ContextMenuStrip contextMenu = new ContextMenuStrip(_components);
        contextMenu.Items.Add(_enabled);
        contextMenu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem listWindowsItem = new ToolStripMenuItem();
        listWindowsItem.Text = "List Windows";
        listWindowsItem.Click += listWindows_Click;
        contextMenu.Items.Add(listWindowsItem);
        ToolStripMenuItem openConfigItem = new ToolStripMenuItem();
        openConfigItem.Text = "Open Config";
        openConfigItem.Click += openConfig_Click;
        contextMenu.Items.Add(openConfigItem);
        ToolStripMenuItem reloadConfigItem = new ToolStripMenuItem();
        reloadConfigItem.Text = "Reload Config";
        reloadConfigItem.Click += reloadConfig_Click;
        contextMenu.Items.Add(reloadConfigItem);
        ToolStripMenuItem quitItem = new ToolStripMenuItem();
        quitItem.Text = "Quit";
        quitItem.Click += quit_Click;
        contextMenu.Items.Add(quitItem);
        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.Visible = true;

        loadEntries(CONFIG_PATH);
        updateStatus();

        setupWinEventHook();
    }

    protected override void Dispose(bool disposing) {
        if (disposing && _components != null) {
            _components.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void CreateHandle() {
        base.CreateHandle();
        SetParent(Handle, HWND_MESSAGE);
    }

    private Icon getIcon(string name) {
        Assembly asm = Assembly.GetAssembly(this.GetType());
        using (Stream strm = asm.GetManifestResourceStream(name)) {
            return new Icon(strm);
        }
    }

    private void icon_DoubleClick(object sender, EventArgs args) {
        _enabled.PerformClick();
    }
    private void quit_Click(object sender, EventArgs args) {
        Application.Exit();
    }
    private void listWindows_Click(object sender, EventArgs args) {
        StringBuilder b = new StringBuilder();
        foreach (IntPtr hWnd in enumToplevelWindows()) {
            if (!IsWindow(hWnd) || !IsWindowVisible(hWnd) ||
                IsZoomed(hWnd) || IsIconic(hWnd)) continue;
            if (hWnd != GetAncestor(hWnd, GA_ROOT)) continue;
            string klass = getWindowClass(hWnd);
            if (klass == null) continue;
            string text = getWindowText(hWnd);
            if (text == null) continue;
            RECT rect;
            if (!GetWindowRect(hWnd, out rect)) continue;
            b.Append(string.Format(
                         "{0},{1},{2},{3},{4},{5}"+Environment.NewLine,
                         klass, text, rect.Left, rect.Top,
                         rect.Width, rect.Height));
        }
        Form form = new Form();
        TextBox textBox = new TextBox();
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ReadOnly = true;
        textBox.ScrollBars = ScrollBars.Vertical;
        textBox.Text = b.ToString();
        form.Owner = this;
        form.Text = "Window List";
        form.Size = new Size(800, 600);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
        form.Controls.Add(textBox);
        form.Show();
    }
    private void openConfig_Click(object sender, EventArgs args) {
        try {
            Process.Start(CONFIG_PATH);
        } catch (Win32Exception ) {
        }
    }
    private void reloadConfig_Click(object sender, EventArgs args) {
        loadEntries(CONFIG_PATH);
        updateStatus();
    }
    private void check_Click(object sender, EventArgs args) {
        if (sender is ToolStripMenuItem) {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            item.Checked = !item.Checked;
            updateStatus();
        }
    }

    private static void updateStatus() {
        bool active = _enabled.Checked;
        _notifyIcon.Text = active? "Active" : "Inactive";
        _notifyIcon.Icon = active? _iconActive : _iconInactive;
        if (active) {
            foreach (IntPtr hWnd in enumToplevelWindows()) {
                fixWindowPos(true, hWnd);
            }
        }
    }

    private static bool enumFunc(
        IntPtr hWnd, IntPtr lParam) {
        GCHandle gch = GCHandle.FromIntPtr(lParam);
        List<IntPtr> a = (List<IntPtr>)gch.Target;
        a.Add(hWnd);
        return true;
    }
    private static EnumWindowsDelegate _enumFunc =
        new EnumWindowsDelegate(enumFunc);
    private static IntPtr[] enumToplevelWindows() {
        List<IntPtr> a = new List<IntPtr>();
        GCHandle gch = GCHandle.Alloc(a);
        EnumWindows(_enumFunc, GCHandle.ToIntPtr(gch));
        gch.Free();
        return a.ToArray();
    }

    private static void eventProc(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hWnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime) {
        if ((eventType == EVENT_OBJECT_SHOW ||
             eventType == EVENT_OBJECT_LOCATIONCHANGE) &&
            idObject == OBJID_WINDOW) {
            fixWindowPos(eventType == EVENT_OBJECT_SHOW, hWnd);
        }
    }
    private static WinEventDelegate _eventProc =
        new WinEventDelegate(eventProc);
    private static IntPtr _hWinEventHook = IntPtr.Zero;
    private static void setupWinEventHook() {
        if (_hWinEventHook != IntPtr.Zero) return;
        _hWinEventHook = SetWinEventHook(
            EVENT_OBJECT_SHOW, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _eventProc,
            0, 0, (WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS));
        if (_hWinEventHook == IntPtr.Zero) {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        Console.WriteLine("setupWinEventHook: success");
    }

    private static void fixWindowPos(bool show, IntPtr hWnd) {
        if (!_enabled.Checked) return;
        if (!IsWindow(hWnd) || IsZoomed(hWnd) || IsIconic(hWnd)) return;
        if (hWnd != GetAncestor(hWnd, GA_ROOT)) return;
        string klass = getWindowClass(hWnd);
        if (klass == null) return;
        string text = getWindowText(hWnd);
        if (text == null) return;
        if (show) {
            Console.WriteLine(
                "fixWindowPos: hWnd="+hWnd+", text="+text+", class="+klass);
        }
        Entry found = null;
        foreach (Entry entry in _entries) {
            if (entry.Match(klass, text)) {
                found = entry;
                break;
            }
        }
        if (found == null) return;
        RECT rect;
        if (!GetWindowRect(hWnd, out rect)) return;
        Console.WriteLine("fixWindowPos: found="+found+", rect="+rect);
        Point pos = Point.Empty;
        bool posSpec = false;
        if (found.Pos != null) {
            pos = found.Pos.Value;
            if (pos.X != rect.Left || pos.Y != rect.Top) {
                posSpec = true;
            }
        }
        Size size = Size.Empty;
        bool sizeSpec = false;
        if (found.Size != null) {
            size = found.Size.Value;
            if (size.Width != rect.Width || size.Height == rect.Height) {
                sizeSpec = true;
            }
        }
        Console.WriteLine("fixWindowPos: posSpec="+posSpec+
                          ", sizeSpec="+sizeSpec);
        if (!posSpec && !sizeSpec) return;
        uint flags = (SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOREDRAW |
                      SWP_ASYNCWINDOWPOS);
        if (!posSpec) {
            flags |= SWP_NOMOVE;
        }
        if (!sizeSpec) {
            flags |= SWP_NOSIZE;
        }
        SetWindowPos(hWnd, IntPtr.Zero, pos.X, pos.Y,
                     size.Width, size.Height, flags);
    }

    private static string getWindowClass(IntPtr hWnd) {
        StringBuilder sb = new StringBuilder(256);
        if (GetClassName(hWnd, sb, sb.Capacity) == 0) return null;
        return sb.ToString();
    }

    private static string getWindowText(IntPtr hWnd) {
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return null;
        StringBuilder sb = new StringBuilder(len+1);
        if (GetWindowText(hWnd, sb, sb.Capacity) == 0) return null;
        return sb.ToString();
    }

    private class Entry {
        public string Klass;
        public string Text;
        public Point? Pos;
        public Size? Size;

        public Entry(string klass, string text) {
            this.Klass = klass;
            this.Text = text;
        }

        public override string ToString() {
            return string.Format(
                "<Entry: klass={0}, text={1}, pos={2}, size={3}>",
                this.Klass, this.Text, this.Pos, this.Size);
        }

        private static bool PatMatch(string pat, string s) {
            int i = pat.IndexOf('*');
            if (0 <= i) {
                pat = pat.Substring(0, i);
                s = s.Substring(0, i);
            }
            return (pat == s);
        }

        public bool Match(string klass, string text) {
            if (!string.IsNullOrEmpty(this.Klass) &&
                !PatMatch(this.Klass, klass)) return false;
            if (!string.IsNullOrEmpty(this.Text) &&
                !PatMatch(this.Text, text)) return false;
            return true;
        }
    }

    private static void loadEntries(string path) {
        Console.WriteLine("loadEntries: opening: "+path);
        List<Entry> entries = new List<Entry>();
        try {
            using (TextReader reader = new StreamReader(path)) {
                while (true) {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    string[] cols = line.Trim().Split(',');
                    if (cols.Length < 2) continue;
                    Entry ent = new Entry(cols[0], cols[1]);
                    if (4 <= cols.Length) {
                        try {
                            ent.Pos = new Point(int.Parse(cols[2]),
                                                int.Parse(cols[3]));
                        } catch (FormatException) {
                        }
                    }
                    if (6 <= cols.Length) {
                        try {
                            ent.Size = new Size(int.Parse(cols[4]),
                                                int.Parse(cols[5]));
                        } catch (FormatException) {
                        }
                    }
                    Console.WriteLine("loadEntries: ent="+ent);
                    entries.Add(ent);
                }
            }
        } catch (IOException ) {
        }
        _entries = entries.ToArray();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
        public override string ToString() {
            return string.Format("<POINT {0},{1}>", X, Y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width {
            get { return Right-Left; }
        }
        public int Height {
            get { return Bottom-Top; }
        }
        public override string ToString() {
            return string.Format(
                "<RECT {0},{1}-{2},{3}>",
                Left, Top, Right, Bottom);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(
        IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(
        IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsZoomed(
        IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(
        IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        IntPtr hWnd, out RECT rect);

    private const uint GA_ROOT = 2;
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(
        IntPtr hWnd, uint gaFlags);
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    private static extern int GetWindowTextLength(
        IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    private static extern int GetWindowText(
        IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    private static extern int GetClassName(
        IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOREDRAW = 0x0008;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_ASYNCWINDOWPOS = 0x4000;
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static IntPtr HWND_MESSAGE = new IntPtr(-3);
    [DllImport("User32.dll", EntryPoint = "SetParent")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndParent);

    private delegate bool EnumWindowsDelegate(
        IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet=CharSet.Auto, SetLastError=true)]
    private static extern bool EnumWindows(
        EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hWnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime);
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800b;
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint WINEVENT_SKIPOWNPROCESS = 2;
    private const uint OBJID_WINDOW = 0;

    [DllImport("user32.dll", SetLastError=true)]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError=true)]
    private static extern bool UnhookWinEvent(
        IntPtr hWinEventHook);
}
