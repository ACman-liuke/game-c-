using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using DaemonLib;
using System.Diagnostics;
using System.Drawing;
using Microsoft.Win32;

namespace logcurrent
{
    public partial class Form1 : Form
    {
        private static string path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);//C:\Users\xxx\Document
        private static string dir0 = @"\GameController";
        private static string file0 = @"\logout.txt";
        private static string file1 = @"\logout.txt.bak";
        private static string pathfile = @"\path.txt";
        private static string StartPath = Application.StartupPath;

        //private delegate void ShowStateDelegate(string value);
        //private ShowStateDelegate showStateCallback;
        //SynchronizationContext sc;

        NotifyIcon notifuicon = new NotifyIcon();
        Icon icon = new Icon(Application.StartupPath + @"\LOGO.ico");

        static ReaderWriterLockSlim LogWriteLock = new ReaderWriterLockSlim();

        public Form1()
        {
            string LogFile = path + dir0 + file0;
            string BackUp = path + dir0 + file1;

            CreateFileSystem();

            if (File.Exists(LogFile))
            {
                File.Copy(LogFile, BackUp, true);
                File.Delete(LogFile);
            }

            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;

            SaveOwnPath();
            /*showStateCallback = new ShowStateDelegate(ShowState);

            ThreadStart child = new ThreadStart(ReadLogToTextBox);
            Thread childThread = new Thread(child);
            childThread.IsBackground = true;
            childThread.Start();
            */

            ThreadStart childref = new ThreadStart(StartGameCtrl);
            Thread childThread1 = new Thread(childref);
            childThread1.IsBackground = true;
            childThread1.Start();

            IsBackground();

            SetAutoRun(Application.ExecutablePath, true);

            string str = "日志文件保存在：\r\n\t1、" + LogFile + "\r\n\t2、" + Application.StartupPath + @"\ErrLog\ErrLog.txt";
            textBox1.Text = str;

            // 捕捉调用线程的同步上下文派生对象
            //sc = SynchronizationContext.Current;
        }

        private void SetAutoRun(string fileName, bool isAutoRun)
        {
            RegistryKey reg = null;
            try
            {
                if (!File.Exists(fileName))
                    throw new Exception("文件不存在");

                string name = fileName.Substring(fileName.LastIndexOf(@"\") + 1);
                reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (reg == null)
                    reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                if (isAutoRun)
                    reg.SetValue(name, fileName);
                else
                    reg.SetValue(name, false);
            }
            catch (Exception e)
            {
                throw e;
            }
            finally {
                if (reg != null)
                    reg.Close();
            }
        }

        //程序启动后最小化到系统托盘中
        private void IsBackground()
        {
            try
            {
                this.WindowState = FormWindowState.Minimized;
                notifyIcon1.Icon = icon;
                ShowInTaskbar = false;
                notifyIcon1.Visible = true;
            }
            catch (Exception e)
            {
                WriteLog("progress start error, " + e.ToString());
                throw e;
            }
        }

        //保存exe文件路径
        private void SaveOwnPath()
        {
            string mypath = Application.StartupPath;
            try
            {
                File.WriteAllText(path + dir0 + pathfile, mypath);
            }
            catch (Exception e)
            {
                WriteLog("save my path fail, " + e.ToString());
                throw e;
            }
        }

        private void CreateFileSystem()
        {
            string path0 = path + dir0;

            if (!Directory.Exists(path0))
            {
                Directory.CreateDirectory(path0);
            }

            if (!File.Exists(path0 + file0))
            {
                File.Create(path0 + file0).Dispose();
            }

            if (!File.Exists(path0 + file1))
            {
                File.Create(path0 + file1).Dispose();
            }
        }

        /*
        private void ShowState(object value)
        {
        }
        */

        public void WriteLog(string log)
        {
            try
            {
                LogWriteLock.EnterWriteLock();
                string LogFile = path + dir0 + file0;
                File.AppendAllText(LogFile, log + "\n");
            }
            catch (Exception e)
            {
                throw e;
            }
            finally {
                LogWriteLock.ExitWriteLock();
            }
        }

        //读取日志文件，显示到文本框
        /*private void ReadLogToTextBox()
        {
            string line = "";
            string logfile = path + dir0 + file0;
            if (!File.Exists(logfile))
            {
                File.Create(logfile).Dispose();
            }
            FileStream stream = new FileStream(logfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader file = new StreamReader(stream, Encoding.UTF8);
            while (true)
            {
                if ((line = file.ReadLine()) != null)
                {
                    sc.Post(ShowState, line.ToString());
                    Thread.Sleep(10);
                }

                Thread.Sleep(25);
            }
        }
        */
        public void StartGameCtrl()
        {
            try
            {
                WriteLog("Welcome to Game Controller");
                DaemonDll api = new DaemonDll();
                string addr = api.GetLocalMac();
                api.SetAppStartPath(StartPath);//set startpath to dll
                api.Init(addr);

                //向控制器上报游戏信息；
                api.GetGameListInfo();
                api.Report();

                //另起线程轮询控制器cgi，根据返回值做出相应处理
                ThreadStart childref = new ThreadStart(api.Probe);
                Thread childThread = new Thread(childref);
                childThread.IsBackground = true;
                childThread.Start();

                //另起线程轮询控制器cgi，根据返回值判断是否重启游戏机
                ThreadStart childref0 = new ThreadStart(api.GameMachineRestart);
                Thread childThread0 = new Thread(childref0);
                childThread0.IsBackground = true;
                childThread0.Start();

                //另起线程轮询控制器cgi，根据返回值判断是否关闭游戏机
                ThreadStart childref3 = new ThreadStart(api.GameMachineBoot);
                Thread childThread3 = new Thread(childref3);
                childThread3.IsBackground = true;
                childThread3.Start();

                /*
                //另起线程告知控制器，游戏机是否被占用
                ThreadStart childref1 = new ThreadStart(api.GamePcState);
                Thread childref1Thread = new Thread(childref1);
                childref1Thread.IsBackground = true;
                childref1Thread.Start();
                */
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                string progress = "GameControllerUpdate";
                Process[] proarr = Process.GetProcessesByName(progress);
                foreach (Process closeprocess in proarr)
                {
                    closeprocess.Kill();
                    closeprocess.WaitForExit();
                }
                notifyIcon1.Dispose();
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        //双击系统托盘图标还原窗口
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            try
            {
                WindowState = FormWindowState.Normal;
                this.Activate();
                this.ShowInTaskbar = true;
                notifyIcon1.Visible = false;
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            try
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    notifyIcon1.Icon = icon;
                    ShowInTaskbar = false;
                    notifyIcon1.Visible = true;
                }
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }
    }
}
