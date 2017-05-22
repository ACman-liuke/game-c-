using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Net;
using System.IO.Compression;
using System.Diagnostics;
using System.Net.Sockets;

namespace GameControllerUpdate
{
    public partial class Form1 : Form
    {
        private static string mac = "";
        private static string host = "127.0.0.1";
        private static string port = "8888";
        private static string head = "http://";
        private static int state = 0;
        private static string newver = "0.0";

        private static string path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        private static string dir = @"\GameControllerDown";
        private static string dir0 = @"\GameController";
        private static string file0 = @"\logout.txt";

        private static string downdir = path + dir;
        private static string versionfile = downdir + @"\version.json";
        private static string oldversionfile = path + @"\GameController\version.json";
        private static string pathfile = path + @"\GameController\path.txt";
        private static string updatedir = downdir + @"\UpdateFloder";
        static ReaderWriterLockSlim LogWriteLock = new ReaderWriterLockSlim();
        public Form1()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            CreateDownFileSystem();
            //ReciveUdpData();
            mac = GetLocalMac();

            InitializeComponent();

            Init();
        }

        //初始化函数，从文件获取中控的host和port配置
        private void Init()
        {
            ThreadStart child = new ThreadStart(AutoUpgrade);
            Thread chidlref = new Thread(child);
            chidlref.IsBackground = true;
            chidlref.Start();
            WriteLog("Upgrade process Init success");
        }

        public  void SetHost(string value)
        {
            host = value;
            WriteLog("Upgrade process host is " + host);
        }

        private void CreateDownFileSystem()
        {
            if (!Directory.Exists(downdir))
            {
                Directory.CreateDirectory(downdir);
            }
        }
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
            finally
            {
                LogWriteLock.ExitWriteLock();
            }
        }

        private void AutoUpgrade()
        {
            while (true)
            {
                try
                {
                    if (ProbeGameControllerState())
                    {
                        bool r = DownloadVer();
                        if (r)
                        {
                            if(ProbeGameControllerState())
                                DownloadUpdateFile();
                        }
                    }
                }
                catch (Exception) { Thread.Sleep(3600000); }

                Thread.Sleep(3600000);
            }
        }

        //接收中控广播包，以此获取中控的IP
        private void ReciveUdpData()
        {
            UdpClient myudp = new UdpClient(11000);
            try
            {
                while (true)
                {
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] recivebytes = myudp.Receive(ref RemoteIpEndPoint);
                    string returndata = Encoding.ASCII.GetString(recivebytes);
                    if (returndata == "AC" && RemoteIpEndPoint.Port.ToString() == "11000")
                    {
                        host = RemoteIpEndPoint.Address.ToString();
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        //获取本机Mac地址
        private string GetLocalMac()
        {
            try
            {
                StringBuilder localMac = new StringBuilder();
                NetworkInterface[] n = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in n)
                {
                    PhysicalAddress mac = adapter.GetPhysicalAddress();
                    byte[] bytes = mac.GetAddressBytes();//返回当前实例的地址

                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        sb.Append(bytes[i].ToString("X2"));//以十六进制格式化
                        /*if (i != bytes.Length - 1)
                        {
                            Console.WriteLine("content is {0}", sb);
                            sb.Append("-");
                        }*/
                    }

                    if (sb.Length - 1 == 11)
                    {
                        localMac = sb;
                    }
                }

                return localMac.ToString();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        //http请求发送函数
        private string HttpSend(string url)
        {
            string result;
            using (HttpClient http = new HttpClient())
            {
                try
                {
                    var content = http.GetAsync(url);
                    var rep = content.Result;
                    var ret = rep.Content.ReadAsStringAsync();
                    result = ret.Result;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            return result;
        }

        public void DownloadFile(string url, string filename)
        {
            try
            {
                string myStringWebResource = null;
                string fileName = url.Substring(url.LastIndexOf("/") + 1);

                WebClient myWebClient = new WebClient();
                myWebClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFileCallback2);
                myWebClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
                myStringWebResource = url;
                if (!myStringWebResource.Contains("http://"))
                {
                    myStringWebResource = "http://" + myStringWebResource;
                }

                myWebClient.DownloadFileAsync(new Uri(myStringWebResource), filename);
                WriteLog("start Download file : " + filename);
            }
            catch (Exception e)
            {
                state = 0;
                throw e;
            }
        }

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
        }

        private void DownloadFileCallback2(object sender, AsyncCompletedEventArgs e)
        {
            //下载完成
            WriteLog("Download File success!");
            state = 1;
        }

        //下载版本文件，判断是否下载升级包
        private bool DownloadVer()
        {
            string url = head + host + ":" + port + "/upgrade/version.json";
            DownloadFile(url, versionfile);
            while (state == 0)
            {
                Thread.Sleep(1000);
            }
            state = 0;
            if (File.Exists(versionfile))
            {
                string nowversion = File.ReadAllText(versionfile);
                string oldversion = "";
                if (File.Exists(oldversionfile))
                {
                    oldversion = File.ReadAllText(oldversionfile);
                }
                else
                {
                    oldversion = "0.0";
                }

                if (string.Compare(nowversion, oldversion) > 0)
                {
                    WriteLog("need upgrade!, beacause version = " + nowversion + " oldversion = " + oldversion);
                    newver = nowversion;
                    return true;
                }
            }
            return false;
        }

        //更新版本文件里的版本号
        private void UpdateVersion(string oldverfile, string newversion)
        {
            try
            {
                if (!File.Exists(oldverfile))
                {
                    File.Create(oldverfile).Dispose();
                }
                File.WriteAllText(oldverfile, newversion);
            }
            catch (Exception) { }
        }

        //下载升级包
        private void DownloadUpdateFile()
        {
            try
            {
                string url = head + host + ":" + port + "/upgrade/SDK.zip";
                DownloadFile(url, downdir + @"\SDK.zip");
                while (state == 0)
                {
                    Thread.Sleep(1000);
                }
                state = 0;

                if (File.Exists(downdir + @"\SDK.zip"))
                {
                    bool r = ExtractSdk(downdir + @"\SDK.zip");
                    if (r)
                    {
                        if (File.Exists(pathfile))
                        {
                            string exepath = File.ReadAllText(pathfile);
                            bool a = UpgradeSdk(exepath);
                            //TODO;通知中控升级成功；
                            if (a)
                            {
                                //更新版本文件里面的版本号
                                UpdateVersion(oldversionfile, newver);
                                StartGameController();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            { }
        }

        private bool ProbeGameControllerState()
        {
            //get game machine state from Controller
            string url = head + host + ":" + port + "/game/machinestate?devid=" + mac;
            string result = HttpSend(url);
            if (result.Contains("ok"))
            {
                return true;
            }
            return false;
        }

        //解压函数
        private bool ExtractSdk(string filename)
        {
            try
            {
                try
                {
                    WriteLog("start extract upgrade file");
                    ZipFile.ExtractToDirectory(filename, updatedir);
                }
                catch (InvalidDataException)
                {
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }

                Thread.Sleep(500);
                WriteLog("sucess extract upgrade file");
                File.Delete(filename);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //升级（替换原有文件）
        private bool UpgradeSdk(string filepath)
        {
            try
            {
                DirectoryInfo folder1 = new DirectoryInfo(filepath);
                foreach (FileInfo f in folder1.GetFiles())
                {
                    if (f.Extension == ".exe" && f.Name != "GameControllerUpdate.exe")
                    {
                        WriteLog("ready go to kill GameController.exe");
                        string ProcessName = f.ToString().Substring(0, f.ToString().LastIndexOf("."));
                        Process[] p = Process.GetProcessesByName(ProcessName);
                        foreach (Process pro in p)
                        {
                            pro.Kill();
                        }
                        Thread.Sleep(1000);
                       
                        DirectoryInfo folder = new DirectoryInfo(updatedir);
                        foreach (FileInfo file in folder.GetFiles())
                        {
                            string path = filepath + @"\" + file.ToString();
                            File.Copy(file.FullName, path, true);
                        }
                        WriteLog("sucess upgrade GameCtroller");
                        Directory.Delete(updatedir, true);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception e) { throw e; }
        }

        private void StartGameController()
        {
            try
            {
                string filepath = File.ReadAllText(pathfile);
                DirectoryInfo dirinfo = new DirectoryInfo(filepath);
                foreach (FileInfo file in dirinfo.GetFiles())
                {
                    if (file.Extension == ".exe" && file.Name == "GameController.exe")
                    {
                        WriteLog("restart GameController.exe when success upgrade");
                        Process[] allProgresse = Process.GetProcessesByName(file.Name.Substring(0, file.Name.LastIndexOf(".")));
                        foreach (Process closeProgress in allProgresse)
                        {
                            closeProgress.Kill();
                            closeProgress.WaitForExit();
                        }
                        ProcessStartInfo pr = new ProcessStartInfo(file.FullName);
                        pr.WindowStyle = ProcessWindowStyle.Minimized;
                        Process.Start(pr);
                    }
                }
            }
            catch (Exception e)
            {
                WriteLog("StartGameController error " + e.ToString());
                throw e;
            }
        }
    }
}
