using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

using System.Runtime.InteropServices;
using Gma.UserActivityMonitor;
using System.IO;
using System.Net;
using System.Collections.Specialized;
using System.Xml;
using System.Threading;

namespace WindowsFormsApplication4
{
    //TODO USE FORM FOR BOUNDING BOX INSTEAD SO CAN STRETCH ACROSS MONITORS!
    partial class uploader : Form
    {
        public static object Lock = new object();
        public static object QueueLock = new object();
        private void uploadWorker()
        {
            Bitmap toUp = null;
            lock (QueueLock)
            {
                toUp = uploads.Dequeue();
            }
            string link = "";

            string n = string.Format("img-{0:yyyy-MM-dd_hh-mm-ss-tt-fffffff}.bmp", DateTime.Now);
            toUp.Save(n);
            NameValueCollection nvc = new NameValueCollection();
            nvc.Add("key", "2036d03fad91b54fe094c625040892d2");
            string response = WebStuff.HttpUploadFile("http://api.imgur.com/3/upload.xml", n, "image", "image/bmp", nvc);

            try
            {
                using (XmlReader reader = XmlReader.Create(new StringReader(response)))
                {

                    reader.ReadToFollowing("original");
                    link = reader.ReadElementContentAsString();
                }
                this.Invoke(new testcallback(delegate(string s)
                {
                    Clipboard.SetText(s);
                    trayIcon.ShowBalloonTip(5000, "Upload done", s, ToolTipIcon.None);
                    trayMenu.MenuItems.Add(s, new EventHandler(delegate(object o, EventArgs ea) { Clipboard.SetText(s); }));
                }), link);
            }
            catch
            {
                //error
            }
        }
        public uploader()
            : base()
        {
            int w = 0;
            int h = 0;
            foreach (var i in Screen.AllScreens)
            {
                w += i.Bounds.Width;
                h += i.Bounds.Height;
            }

            //   this.Bounds = new Rectangle(0, 0, w, h);
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            //   this.WindowState = FormWindowState.Maximized;
            this.BackColor = System.Drawing.Color.DarkGray;
            this.Opacity = .3;
            //this.TransparencyKey = System.Drawing.Color.Purple;
            var values = Enum.GetValues(typeof(Keys));
            foreach (Keys i in values)
            {
                if (!currentState.ContainsKey(i))
                    currentState.Add(i, false);
            }
            HookManager.KeyDown += new KeyEventHandler(HookManager_KeyDown);
            HookManager.KeyUp += new KeyEventHandler(HookManager_KeyUp);
            //  HookManager.MouseDown += new MouseEventHandler(HookManager_MouseDown);
            //    HookManager.MouseUp += new MouseEventHandler(HookManager_MouseUp);
            //    HookManager.MouseClick += new MouseEventHandler(HookManager_MouseClickExt);
            //     int hookID = SetWindowsHookEx(WH_KEYBOARD_LL,,
            //        GetModuleHandle(curModule.ModuleName), 0);
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Exit", OnExit);
            // Create a tray icon. In this example we use a
            // standard system icon for simplicity, but you
            // can of course use your own custom icon too.
            trayIcon = new NotifyIcon();
            trayIcon.Text = "imgur uploader";
            trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

            // Add menu to tray icon and show it.
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            this.KeyPress += new KeyPressEventHandler(uploader_KeyPress);
            GlobalHotkey key2 = new GlobalHotkey(Constants.CTRL | Constants.SHIFT, Keys.D2, this);
            key2.Register();
            GlobalHotkey key = new GlobalHotkey(Constants.CTRL | Constants.SHIFT, Keys.D1, this);
            key.Register();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Constants.WM_HOTKEY_MSG_ID)
            {
                Keys vk = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                handleKey(vk);
            }
            base.WndProc(ref m);
        }
        bool run = false, canShow = false;
        public delegate void activate();
        private void activateUI()
        {
            this.Activate();
            start = Cursor.Position;
            SetForegroundWindow(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);
        }
        private void handleKey(Keys k)
        {
            if (k == Keys.D1)
            {
                if (!run)
                    new Thread(new ThreadStart(delegate()
                    {
                        run = true;
                        if (this.InvokeRequired && canShow)
                        {
                            this.Invoke(new activate(this.Show));
                            canShow = false;
                        }
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new activate(this.activateUI));
                        }

                        while (true)
                        {
                            this.Invalidate();
                            Thread.Sleep(15);
                            if (!(currentState[Keys.LControlKey] && currentState[Keys.LShiftKey] && currentState[Keys.D1]))
                            {
                                break;
                            }
                        }

                        run = false;
                        if (start != null && selected != null)
                        {
                            if (this.Width > 0 && this.Height > 0)
                            {
                                // trayIcon.ShowBalloonTip(100000, "Uploading", "Uploading Image", ToolTipIcon.Info);
                                //       ShowWindowAsync(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle, 2);
                                Bitmap bm = new Bitmap(this.Width, this.Height);
                                //        TryUnsubscribeFromGlobalMouseEvents();
                                using (Graphics g = Graphics.FromImage(bm))
                                {
                                    g.CopyFromScreen(this.Location.X, this.Location.Y, 0, 0, this.Size);
                                }
                                lock (QueueLock)
                                {
                                    uploads.Enqueue(bm);
                                }
                                new Thread(uploadWorker).Start();
                                if (this.InvokeRequired)
                                {
                                    this.Invoke(new activate(this.Hide));
                                    canShow = true;
                                }
                            }
                        }
                    })).Start();
            }
            else if (k == Keys.D2)
            {
                RECT rct;
                IntPtr active = GetForegroundWindow();
                GetWindowRect(active, out rct);
                if (rct.Top != rct.Bottom && rct.Left != rct.Right)
                {
                    Size sz = new Size(Math.Abs(rct.Right - rct.Left), Math.Abs(rct.Bottom - rct.Top));
                    Bitmap bm = new Bitmap(sz.Width, sz.Height);
                    using (Graphics g = Graphics.FromImage(bm))
                    {
                        g.CopyFromScreen(rct.Top, rct.Left, 0, 0, sz);
                    }
                    lock (QueueLock)
                    {
                        uploads.Enqueue(bm);
                    }
                    new Thread(uploadWorker).Start();
                }
            }
        }
        void uploader_KeyPress(object sender, KeyPressEventArgs e)
        {
            this.Location = new Point(this.Location.X, this.Location.Y + 1);
        }

        void HookManager_MouseClickExt(object sender, MouseEventArgs e)
        {
            this.Invalidate();
            //  Console.WriteLine(e.Button); 
        }
        void HookManager_MouseDown(object sender, MouseEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void OnExit(object sender, EventArgs e)
        {
            Application.Exit();
        }
        void HookManager_KeyUp(object sender, KeyEventArgs e)
        {
            currentState[e.KeyData] = false;
        }
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private Queue<Bitmap> uploads = new Queue<Bitmap>();
        private Dictionary<Keys, bool> currentState = new Dictionary<Keys, bool>();
        void HookManager_KeyDown(object sender, KeyEventArgs e)
        {

            if (!currentState.ContainsKey(e.KeyData))
            {
                currentState.Add(e.KeyData, true);
            }
            else
            {
                currentState[e.KeyData] = true;
            }

        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
        }
        bool initial;
        Rectangle selected;
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            int scanCode;
            public int flags;
            int time;
            int dwExtraInfo;
        }
        public delegate void testcallback(string s);
        private delegate IntPtr HookHandlerDelegate(
            int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);
        //  delegate void Hook();

        private bool isPressed(Keys k)
        {
            return currentState[k] && !pastState[k];
        }
        Dictionary<Keys, bool> pastState = new Dictionary<Keys, bool>();
        int counter = 0;
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // this.Location = Cursor.Position;
            if (start != null)
            {
                System.Drawing.Point current = (Cursor.Position);
                this.Size = new Size(Math.Abs(current.X - start.Value.X), Math.Abs(current.Y - start.Value.Y));
                Console.WriteLine(this.Size.Width);
                this.Location = new Point(current.X < start.Value.X ? current.X : start.Value.X, current.Y < start.Value.Y ? current.Y : start.Value.Y);
                //selected = new Rectangle(current.X < start.Value.X ? current.X : start.Value.X, current.Y < start.Value.Y ? current.Y : start.Value.Y, Math.Abs(current.X - start.Value.X), Math.Abs(current.Y - start.Value.Y));
                //e.Graphics.DrawRectangle(Pens.Black, selected);
            }
            //this.Invalidate()
            //Update();
            // base.OnPaint(e);
        }
        Point lastPoint;
        private static void TryUnsubscribeFromGlobalMouseEvents()
        {

            ForceUnsunscribeFromGlobalMouseEvents();

        }
        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Release the icon resource.
                trayIcon.Dispose();
            }

            base.Dispose(isDisposing);
        }

        static int s_MouseHookHandle;
        private const int WH_MOUSE_LL = 14;
        private delegate int HookProc(int nCode, int wParam, IntPtr lParam);
        private static HookProc s_MouseDelegate;
        /*   private static void EnsureSubscribedToGlobalMouseEvents()
           {
               // install Mouse hook only if it is not installed and must be installed
               if (s_MouseHookHandle == 0)
               {
                   //See comment of this field. To avoid GC to clean it up.
                   s_MouseDelegate = MouseHookProc;
                   //install hook
                   s_MouseHookHandle = SetWindowsHookEx(
                       WH_MOUSE_LL,
                       s_MouseDelegate,
                             System.Diagnostics.Process.GetCurrentProcess().MainModule.BaseAddress,
                       0);
                   //If SetWindowsHookEx fails.
                   if (s_MouseHookHandle == 0)
                   {
                       //Returns the error code returned by the last unmanaged function called using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                       int errorCode = Marshal.GetLastWin32Error();
                       //do cleanup

                       //Initializes and throws a new instance of the Win32Exception class with the specified error. 
                       // throw new Win32Exception(errorCode);
                   }
               }
           }
           */

        [STAThread]
        public static void Main(String[] args)
        {
            Application.Run(new uploader());
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // uploader
            // 
            this.BackColor = System.Drawing.Color.DimGray;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "uploader";
            this.Opacity = 0.3D;
            this.Load += new System.EventHandler(this.uploader_Load);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.test_MouseDown);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.test_MouseUp);
            this.ResumeLayout(false);

        }
        System.Drawing.Point? start;
        private void test_MouseDown(object sender, MouseEventArgs e)
        {
            start = new System.Drawing.Point(e.X, e.Y);
        }

        private void test_MouseUp(object sender, MouseEventArgs e)
        {
            start = null;
        }

        private void uploader_Load(object sender, EventArgs e)
        {

        }
    }
}
