using System;
using System.IO;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using DownloadFile;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Media;
using System.Net.Sockets;
using System.Net;

namespace DaemonLib
{
    public class DaemonDll
    {
        private static string port = "8888";
        private static string head = "http://";
        private static string host = "127.0.0.1";
        private static string coin = "0";
        private static Process pro;
        private static string progress = "";
        private static string gametype = "";
        private static int gametime = 0;
        private static string Override = "";
        private static bool idleState = false;
        private static string SoundFile = "CountDown.wav";
        private static SoundPlayer s;
        System.Timers.Timer myTimer = new System.Timers.Timer();
        System.Timers.Timer myTimer0 = new System.Timers.Timer();

        private static string path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);//C:\Users\xxx\Document
        private static string dir0 = @"\GameController";
        private static string file0 = @"\logout.txt";
        private static string LogPath = path + dir0 + @"\RunLog.txt";

        private static string mac = "";
        private static string gameinfo = path + dir0 + @"\gameinfo.json";
        private static string localpath = path + dir0;
        private static string path1 = path + dir0 + file0;

        private static string AppPath = "";

        static ReaderWriterLockSlim LogWriteLock = new ReaderWriterLockSlim();

        private void LogDebug(string info)
        {
            try
            {
                LogWriteLock.EnterWriteLock();
                if (!File.Exists(LogPath))
                {
                    File.Create(LogPath).Dispose();
                }
                File.AppendAllText(LogPath, info + @"\n");
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

        private void WriteLog(string log)
        {
            try
            {
                LogWriteLock.EnterWriteLock();
                if (!Directory.Exists(localpath))
                {
                    Directory.CreateDirectory(localpath);
                }
                File.AppendAllText(path1, log + "\n");
            }
            catch (Exception e)
            {
                throw e;
            }
            finally {
                LogWriteLock.ExitWriteLock();
            }
        }

        public void SetAppStartPath(string path)
        {
            AppPath = path;
            SoundFile = AppPath + @"\" + SoundFile;
            s = new SoundPlayer(SoundFile);
        }

        private string GetAppStartPath()
        {
            if (AppPath.Length != 0)
                return AppPath;

            return null;
        }

        public string GetDeviceName()
        {
            string content = "-1";
            try
            {
                string CurrentDirectory = GetAppStartPath();
                if (CurrentDirectory != null)
                {
                    string path = CurrentDirectory + @"\DeviceName.txt";
                    if (File.Exists(path))
                        content = File.ReadAllText(path);
                }
            }
            catch (Exception e)
            {
                WriteLog(e.ToString());
                throw e;
            }
            return content;
        }

        public void Init(string mac)
        {
            LogDebug("Init start......");
            if (!Directory.Exists(localpath))
            {
                Directory.CreateDirectory(localpath);
            }

            string CurrentDirectory = GetAppStartPath();
            if (CurrentDirectory == null)
            {
                CurrentDirectory = Environment.CurrentDirectory;
                WriteLog("CurrentDirectory is null, use default value path");
            }

            if (File.Exists(CurrentDirectory + @"\ServerCfgmgr.json"))
            {
                string content = File.ReadAllText(CurrentDirectory + @"\ServerCfgmgr.json");
                JObject p = new JObject();
                p = JsonConvert.DeserializeObject<JObject>(content);
                if(p.GetValue("host") != null)
                    host = (string)p.GetValue("host");
                if (p.GetValue("port") != null)
                    port = (string)p.GetValue("port");
            }
            else
                ReciveUdpData();
            string url = head + host + ":" + port + "/game/gm_register?devid=" + mac + "&machinename=" + GetDeviceName();
            using (HttpClient http = new HttpClient())
            {
                while (true)
                {
                    string result = "";
                    try
                    {
                        WriteLog("connect controller start ...");
                        var response = http.GetAsync(url);
                        var rep = response.Result;
                        var ret = rep.Content.ReadAsStringAsync();
                        result = ret.Result;
                        if (result.Contains("ok"))
                        {
                            WriteLog("connect controller success");
                            break;
                        }
                    }
                    catch (AggregateException e)
                    {
                        WriteLog("connect controller fail, Destination unreachable " + e.ToString() + ", Please check network environment and restart program");
                        throw new AggregateException("网络故障");
                    }
                    catch (Exception e)
                    {
                        WriteLog("connect controller fail" + e.ToString());
                        throw new Exception("网络故障");
                    }

                    if (result.Contains("invalid"))
                    {
                        WriteLog("game machine register fail, " + result);
                        throw new Exception("游戏机名配置错误！\r\n" + result);
                    }

                    Thread.Sleep(500);
                }
            }

            GameTimeCounter();//Init timer
            GameTime0Counter();//Init timer0
        }

        public void Report()
        {
            string info = File.ReadAllText(gameinfo, Encoding.Default);
            if (info == null)
            {
                info = "game content is null from file";
            }
            string url = head + host + ":" + port + "/game/gm_report";
            HttpContent content;
            try
            {
                content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    {"devid", mac },
                    {"data", info}
                });
            }
            catch (Exception e)
            {
                WriteLog("game comfig is error, " + e.ToString());
                throw e;
            }
            using (HttpClient http = new HttpClient())
            {
                while (true)
                {
                    string result = "";
                    try
                    {
                        var response = http.PostAsync(url, content);
                        var rep = response.Result;
                        var ret = rep.Content.ReadAsStringAsync();
                        result = ret.Result;
                    }
                    catch (Exception e)
                    {
                        WriteLog("report error " + e.ToString());
                        throw new Exception("网络故障");
                    }
                    if (result.Contains("ok"))
                    {
                        WriteLog("report success");
                        //拉起升级进程；
                        StartGameControllerUpdate();
                        break;
                    }
                    Thread.Sleep(500);
                }
            }
        }

        private void StartGameControllerUpdate()
        {
            bool UpdateState = false;
            try
            {
                while (!UpdateState)
                {
                    string CurrentDirectory = GetAppStartPath();
                    if (CurrentDirectory == null)
                    {
                        CurrentDirectory = Environment.CurrentDirectory;
                        WriteLog("CurrentDirectory is null, use default value path");
                    }

                    string updatepath = CurrentDirectory + @"\GameControllerUpdate";
                    DirectoryInfo updatedir = new DirectoryInfo(updatepath);
                    foreach (FileInfo file in updatedir.GetFiles())
                    {
                        if (file.Extension == ".exe")
                        {
                            Process[] allProgresse = Process.GetProcessesByName(file.Name.Substring(0, file.Name.LastIndexOf(".")));
                            foreach (Process closeProgress in allProgresse)
                            {
                                closeProgress.Kill();
                                closeProgress.WaitForExit();
                            }
                            ProcessStartInfo pr = new ProcessStartInfo(file.FullName, host);
                            pr.WindowStyle = ProcessWindowStyle.Minimized;

                            Process p = Process.Start(pr);
                            if (p == null)
                            {
                                WriteLog("start GameControllerUpdate process fail");
                            }
                            else
                            {
                                UpdateState = true;
                                WriteLog("start GameControllerUpdate process sucess!");
                            }
                        }
                    }
                }
             }
            catch (Exception e)
            {
                WriteLog("start GameControllerUpdate process fail, " + e.ToString());
                throw e;
            }
        }

        public void GetGameListInfo()
        {
            try
            {
                string CurrentDirectory = GetAppStartPath();
                if (CurrentDirectory == null)
                {
                    CurrentDirectory = Environment.CurrentDirectory;
                    WriteLog("CurrentDirectory is null, use default value path");
                }

                List<string> listArr = new List<string>();
                DirectoryInfo dir = new DirectoryInfo(CurrentDirectory);
                WriteLog("current directory is " + CurrentDirectory);
                foreach (DirectoryInfo files in dir.GetDirectories())
                {
                    foreach (FileInfo f in files.GetFiles("gameinfo.json"))
                    {
                        string content = File.ReadAllText(f.FullName, Encoding.Default);
                        if (content.Contains("gameid") && content.Contains("gamename") && content.Contains("filename") && content.Contains("progress"))
                        {
                            listArr.Add(content);
                        }
                        else
                        {
                            //Console.WriteLine("获取游戏信息失败，因为没有游戏，或者游戏信息不完整......");
                            WriteLog("read game config information fail, maybe information inperfect......");
                        }
                    }
                }

                File.Delete(gameinfo);
                File.AppendAllText(gameinfo, "[", Encoding.Default);
                foreach (string info in listArr)
                {
                    int index = listArr.IndexOf(info); //当listArr里存在两个完全相同的值，就不能正确获取值在listArr中的索引
                    File.AppendAllText(gameinfo, info, Encoding.Default);
                    if (index != listArr.ToArray().Length - 1)
                    {
                        File.AppendAllText(gameinfo, ",", Encoding.Default);
                    }
                }
                File.AppendAllText(gameinfo, "]", Encoding.Default);
                WriteLog("read game config information success，altogether ===》" + listArr.ToArray().Length);
                Thread.Sleep(50);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public void Probe()
        {
            string url = head + host + ":" + port + "/game/gm_probe?devid=" + mac;
            using (HttpClient http = new HttpClient())
            {
                while (true)
                {
                    string result = "";
                    JObject p = new JObject();
                    string r = "";
                    try
                    {
                        var response = http.GetAsync(url);
                        var rep = response.Result;
                        var ret = rep.Content.ReadAsStringAsync();
                        result = ret.Result;
                        if (result.Contains("gameid") && result.Contains("cmd") && result.Contains("progress") && result.Contains("filename") && result.Contains("idle"))
                        {
                            WriteLog("prepare execute " + result);
                        }
                        else
                        {
                            continue;
                        }

                        p = JsonConvert.DeserializeObject<JObject>(result);
                        r = (string)p.GetValue("cmd");
                        if (result.Contains("coin"))
                        {
                            coin = (string)p.GetValue("coin");
                        }
                        if (result.Contains("override"))
                        {
                            Override = (string)p.GetValue("override");
                        }
                    }
                    catch (Exception e)
                    {
                        WriteLog("probe controller fail, " + e.ToString());
                        throw new Exception("网络故障");
                    }

                    try
                    {
                        if (r == "start")
                        {
                            Process[] allProgresse = Process.GetProcessesByName(progress);
                            foreach (Process closeProgress in allProgresse)
                            {
                                if (closeProgress.ProcessName.Equals(progress))
                                {
                                    closeProgress.Kill();
                                    closeProgress.WaitForExit();
                                    LogDebug("杀死进程 == >>" + progress);
                                }
                            }
                            LogDebug("控制器希望开启游戏 ====》》" + p.GetValue("gameid").ToString());
                            progress = (string)p.GetValue("progress");

                            if (result.Contains("gametime"))
                            {
                                gametype = (string)p.GetValue("gametype");
                                gametime = (int)p.GetValue("gametime");
                            }

                            Start((string)p.GetValue("filename"));
                        }
                        else if (r == "stop")
                        {
                            LogDebug("控制器希望关闭游戏====>>" + p.GetValue("gameid").ToString());
                            int idle = (int)p.GetValue("idle");
                            if (idle.Equals(0))
                            {
                                idleState = false;
                            }
                            else if (idle.Equals(1))
                            {
                                idleState = true;
                            }
                            Stop((string)p.GetValue("progress"));
                        }
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                    Thread.Sleep(1000);
                }
            }
        }

        private void GameTimeCounter()
        {
            //定时器
            myTimer.Elapsed += new System.Timers.ElapsedEventHandler(ReMindToMp3);
            myTimer.AutoReset = false;
        }

        private void SetTimerInterval(int time)
        {
            myTimer.Interval = time * 1000;
            myTimer.Start();
        }
        private void GameTime0Counter()
        {
            //定时器
            myTimer0.Elapsed += new System.Timers.ElapsedEventHandler(KillGame);
            myTimer0.AutoReset = false;
        }

        private void SetTimer0Interval(int time)
        {
            myTimer0.Interval = time * 1000;
            myTimer0.Start();
        }

        private void ReMindToMp3(object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                //打开语音提示文件
                if (File.Exists(SoundFile))
                {
                    LogDebug("准备播放声音文件");
                    s.Stop();
                    s.Play();//另起线程播放WAV文件
                    LogDebug("播放声音文件成功");
                }
                //定时器
                LogDebug("倒计时30秒，准备关闭游戏");
                SetTimer0Interval(30);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void KillGame(object source, System.Timers.ElapsedEventArgs e)
        {
            LogDebug("关闭游戏" + progress);
            s.Stop();
            Stop(progress);
        }

        public void Start(string filename)
        {
            try
            {
                string CurrentDirectory = GetAppStartPath();
                if (CurrentDirectory == null)
                {
                    CurrentDirectory = Environment.CurrentDirectory;
                    WriteLog("CurrentDirectory is null, use default value path");
                }
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = CurrentDirectory + filename;
                info.Arguments = host + " " + port + " " + mac + " " + coin + " " + Override;
                info.WindowStyle = ProcessWindowStyle.Maximized;
                LogDebug("开启游戏成功！" + filename + "<>" + gametype);
                pro = Process.Start(info);
                if (gametype == "experience")
                {
                    gametype = "";
                    LogDebug("启动定时器！");
                    SetTimerInterval(gametime);
                }

                Override = "";

                string result = "";
                string url = head + host + ":" + port + "/game/gm_start?devid=" + mac;
                using (HttpClient http = new HttpClient())
                {
                    while (true)
                    {
                        var response = http.GetAsync(url);
                        var rep = response.Result;
                        var ret = rep.Content.ReadAsStringAsync();
                        result = ret.Result;
                        if (result.Contains("ok"))
                        {
                            LogDebug("开启游戏成功！!!" + result);
                            break;
                        }
                    }
                    
                }
            }
            catch (Exception e)
            {
                WriteLog("Start game progress fail," + e.ToString());
                throw new Exception("网络故障");
            }
        }

        public void Stop(string progress)
        {
            try
            {
                Process[] allProgresse = Process.GetProcessesByName(progress);
                string result = "";
                string url = head + host + ":" + port + "/game/gm_stop?devid=" + mac;
                using (HttpClient http = new HttpClient())
                {
                    while (true)
                    {
                        var response = http.GetAsync(url);
                        var rep = response.Result;
                        var ret = rep.Content.ReadAsStringAsync();
                        result = ret.Result;
                        if (result.Contains("unidle") || result.Contains("idle"))
                        {
                            s.Stop();//退出语音播放
                            myTimer0.Stop();
                            myTimer.Stop();

                            foreach (Process closeProgress in allProgresse)
                            {
                                LogDebug("获取到的进程名 = >" + closeProgress.ProcessName);
                                if (closeProgress.ProcessName.Equals(progress))
                                {
                                    closeProgress.Kill();
                                    closeProgress.WaitForExit();
                                    LogDebug("退出游戏成功！" + closeProgress.ProcessName);
                                }
                            }
                            break;
                        }

                        Thread.Sleep(2000);
                    }
                }
            }
            catch (Exception e)
            {
                WriteLog("stop game progress fail...... " + e.ToString());
                throw new Exception("网络故障");
            }
        }

        public string GetLocalMac()
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
                mac = localMac.ToString();
                return localMac.ToString();
            }
            catch (Exception e)
            {
                WriteLog("get games console macaddress fail...... " + e.ToString());
                throw e;
            }
        }

        public void GameMachineRestart()
        {
            string url = head + host + ":" + port + "/game/machine_restart?devid=" + mac;
            string result = "";

            using (HttpClient http = new HttpClient())
            {
                while (true)
                {
                    try
                    {
                        var response = http.GetAsync(url);
                        var rep = response.Result;
                        var ret = rep.Content.ReadAsStringAsync();
                        result = ret.Result;
                        if (result.Contains("ok"))
                        {
                            Process.Start("shutdown.exe", "-r -t 5");
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        WriteLog("games console restart fail " + e.ToString());
                        throw new Exception("网络故障");
                    }
                    Thread.Sleep(1000);
                }
            }
        }

        public void GameMachineBoot()
        {
            string url = head + host + ":" + port + "/game/machine_boot?devid=" + mac;
            string result = "";

            using (HttpClient http = new HttpClient())
            {
                while (true)
                {
                    try
                    {
                        var response = http.GetAsync(url);
                        var rep = response.Result;
                        var ret = rep.Content.ReadAsStringAsync();
                        result = ret.Result;
                        if (result.Contains("ok"))
                        {
                            Process.Start("shutdown.exe", "-s -t 5");
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        WriteLog("games console boot fail " + e.ToString());
                        throw new Exception("网络故障");
                    }
                    Thread.Sleep(1000);
                }
            }
        }

        public async void GamePcState()
        {
            while (true)
            {
                if (!idleState)
                {
                    try
                    {
                        Process[] allProgresse = Process.GetProcessesByName(progress);
                        if (allProgresse.Length == 0)
                        {
                            using (HttpClient http = new HttpClient())
                            {
                                string url = head + host + ":" + port + "/game/gm_state?devid=" + mac + "&state=0";
                                await http.GetAsync(url);
                            }
                        }
                        else
                        {
                            foreach (Process closeProgress in allProgresse)
                            {
                                using (HttpClient http = new HttpClient())
                                {
                                    if (closeProgress.ProcessName.Equals(progress))
                                    {
                                        string url = head + host + ":" + port + "/game/gm_state?devid=" + mac + "&state=1";
                                        await http.GetAsync(url);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        WriteLog("send game console state fail...... " + e.ToString());
                        throw new Exception("网络故障");
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private void ReciveUdpData()
        {
            UdpClient myudp = new UdpClient(11000);
            try
            {
                while(true)
                {
                    WriteLog("Get Controller ip start");
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] recivebytes = myudp.Receive(ref RemoteIpEndPoint);
                    string returndata = Encoding.ASCII.GetString(recivebytes);
                    if (returndata == "AC" && RemoteIpEndPoint.Port.ToString() == "11000")
                    {
                        host = RemoteIpEndPoint.Address.ToString();
                        WriteLog("Get Controller ip success, ip = " + host);
                        break;
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                WriteLog("Get Controller ip and port fail,{0}" + e.ToString());
                throw e;
            }
        }

        public async void GameUpgradeProbe()
        {
            string result = "";
            JObject p = new JObject();
            while (true)
            {
                string url = head + host + ":" + port + "/game/gm_upgrade?devid=" + mac;
                try
                {
                    using (HttpClient http = new HttpClient())
                    {
                        var response = await http.GetAsync(url);
                        result = await response.Content.ReadAsStringAsync();
                    }
                    
                    if (result.Contains("cmd") && result.Contains("progress") && result.Contains("filename") && result.Contains("url"))
                    {
                        while (true)
                        {
                            Process[] allProgresse = Process.GetProcessesByName(progress);
                            if (allProgresse.Length == 0)
                            {
                                p = JsonConvert.DeserializeObject<JObject>(result);
                                Downfile d = new Downfile();
                                d.Execute_Download((string)p.GetValue("url"), (string)p.GetValue("filename")); //阻塞执行
                                break;
                            }
                            else
                            {
                                Thread.Sleep(1000);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    WriteLog("game upgrade fail ......" + e.ToString());
                    throw e;
                }

                Thread.Sleep(5000);
            }
        }
    }
}
