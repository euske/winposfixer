//  WinPosFixer.cs
//
using System;
using System.IO;
using System.Drawing;
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

    private IContainer _components = null;
    private NotifyIcon _notifyIcon;
    private ToolStripMenuItem _enabled;

    public WinPosFixer() {
        _components = new System.ComponentModel.Container();
        _notifyIcon = new NotifyIcon(_components);

        ContextMenuStrip contextMenu = new ContextMenuStrip(_components);
        _enabled = new ToolStripMenuItem();
        _enabled.Text = "Active";
        _enabled.Click += check_Click;
        _enabled.Checked = true;
        contextMenu.Items.Add(_enabled);
        contextMenu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem quitItem = new ToolStripMenuItem();
        quitItem.Text = "Quit";
        quitItem.Click += quit_Click;
        contextMenu.Items.Add(quitItem);
        _notifyIcon.ContextMenuStrip = contextMenu;

        setupWinEventHook();
        updateStatus();
    }

    private void quit_Click(object sender, EventArgs args) {
        Application.Exit();
    }

    private void check_Click(object sender, EventArgs args) {
        if (sender is ToolStripMenuItem) {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            item.Checked = !item.Checked;
            updateStatus();
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing && _components != null) {
            _components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void updateStatus() {
        _notifyIcon.Text = _enabled.Checked? "Active" : "Inactive";
        _notifyIcon.Icon = this.Icon;
        _notifyIcon.Visible = true;
    }

    [DllImport("User32.dll", EntryPoint = "SetParent")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndParent);
    private static IntPtr HWND_MESSAGE = new IntPtr(-3);

    protected override void CreateHandle() {
        base.CreateHandle();
        SetParent(Handle, HWND_MESSAGE);
    }

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

    private IntPtr hWinEventHook;
    private void setupWinEventHook() {
        hWinEventHook = SetWinEventHook(
            EVENT_OBJECT_SHOW, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, new WinEventDelegate(eventProc),
            0, 0, (WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS));
        if (hWinEventHook == IntPtr.Zero) {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        Console.WriteLine("setupWinEventHook: success");
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

    private string getWindowClass(IntPtr hWnd) {
        StringBuilder sb = new StringBuilder(256);
        if (GetClassName(hWnd, sb, sb.Capacity) == 0) return null;
        return sb.ToString();
    }

    private string getWindowText(IntPtr hWnd) {
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return null;
        StringBuilder sb = new StringBuilder(len+1);
        if (GetWindowText(hWnd, sb, sb.Capacity) == 0) return null;
        return sb.ToString();
    }

    private const string CLASSNAME = "vncviewer::DesktopWindow";
    private static Point POS = new Point(70, 100);

    private void eventProc(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hWnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime) {
        if ((eventType == EVENT_OBJECT_SHOW ||
             eventType == EVENT_OBJECT_LOCATIONCHANGE) &&
            idObject == OBJID_WINDOW) {
            if (!IsZoomed(hWnd) && !IsIconic(hWnd) &&
                hWnd == GetAncestor(hWnd, GA_ROOT)) {
                Console.WriteLine(
                    string.Format(
                        "eventProc: eventType={0:x}, hWnd={1}",
                        eventType, hWnd));
                fixWindowPos(hWnd);
            }
        }
    }

    private void fixWindowPos(IntPtr hWnd) {
        if (!_enabled.Checked) return;
        string klass = getWindowClass(hWnd);
        if (klass != CLASSNAME) return;
        string text = getWindowText(hWnd);
        if (text == null) return;
        RECT rect;
        if (!GetWindowRect(hWnd, out rect)) return;
        if (rect.Left == POS.X && rect.Top == POS.Y) return;
        Console.WriteLine(
            string.Format(
                "fixWindowPos: hWnd={0}, text={1}, class={2}, rect={3}",
                hWnd, text, klass, rect));
        SetWindowPos(hWnd, IntPtr.Zero, POS.X, POS.Y, 0, 0,
                     (SWP_NOACTIVATE | SWP_NOSIZE |
                      SWP_NOZORDER | SWP_NOREDRAW |
                      SWP_ASYNCWINDOWPOS));
    }
}
