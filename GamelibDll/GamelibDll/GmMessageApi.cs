using System;
using System.Collections;
using System.Net;
using System.IO;
using System.Threading;

namespace GmMessageApi
{
    public class MessageApi
    {
        private static string mac = "";
        private static string host = "";
        private static string port = "";
        private static string head = "http://";
        private static int coin = 0;
        private static int integrate = 0;
        private static string controlchar = "";
        private static Stack myStack = new Stack();
        private static bool CoinSendStatus = true;
        private static bool IntegrateSendStatus = true;
        private static string Override = "";
        private static bool StopState = false;

        private static string StopResult = null;

        private static string path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        private static string logfile = path + @"\GameController\gamerun.txt";

        public delegate void KeyValueDelegate(string key, string value);
        private static KeyValueDelegate RankValue;
        public KeyValueDelegate SetRankMethod
        {
            set {
                RankValue = value;
                WriteDebug("--set--rank--method---success");
            }
        }

        private void WriteDebug(string message)
        {
            File.AppendAllText(logfile, message + "\n");
        }

        private void SetParam(string h, string p, string devid)
        {
            host = h;
            port = p;
            mac = devid;
        }
        private void SetHead(string s)
        {
            head = s;
        }

        private string GetHost()
        {
            return host;
        }
        private string GetPort()
        {
            return port;
        }
        private string GetHead()
        {
            return head;
        }

        public int GameCoinGet()
        {
            return coin;
        }

        private void GameCoinSet()
        {
            coin = 0;
        }

        public int GameIntegrateGet()
        {
            return integrate;
        }

        private void GameIntegrateSet()
        {
            integrate = 0;
        }

        public string GameOverrideGet()
        {
            return Override;
        }

        private void GameOverrideSet()
        {
            Override = "";
        }

        private string HttpSend1(string url)
        {
            string responseFromserver = "";
            try
            {
                WebRequest http = WebRequest.Create(url);
                http.Credentials = CredentialCache.DefaultCredentials;
                HttpWebResponse response = (HttpWebResponse)http.GetResponse();
                Stream datastream = response.GetResponseStream();
                StreamReader reader = new StreamReader(datastream);
                responseFromserver = reader.ReadToEnd();
                reader.Close();
                datastream.Close();
                response.Close();
            }
            catch (Exception e)
            {
                WriteDebug("HttpSend1 exception " + e.ToString());
                throw e;
            }
            return responseFromserver;
        }

        private void HttpSend2(string url)
        {
            WebRequest http = WebRequest.Create(url);
            http.Credentials = CredentialCache.DefaultCredentials;
        }

        //处理中控发送的控制博彩类游戏赔率
        private void WinningController()
        {
            while (true)
            {
                try
                {
                    string url = GetHead() + GetHost() + ":" + GetPort() + "/game/gm_winningctrl?devid=" + mac;
                    string response = HttpSend1(url);
                    if (!response.Contains("no"))
                    {
                        string key = "Odds";
                        controlchar = response;
                        if (controlchar != null && !controlchar.Equals(""))
                        {
                            WriteDebug("reset game multiplying power, ==> " + key + "=" + controlchar);
                            RankValue?.Invoke(key, controlchar);
                        }
                    }

                    Thread.Sleep(3000);
                }
                catch (Exception e)
                {
                    WriteDebug("WinningController exception : " + e.ToString());
                    throw e;
                }
            }
        }

        //初始化与中控通信的参数
        public void Init(string[] args)
        {
            try
            {
                if (!File.Exists(logfile))
                {
                    File.Create(logfile).Dispose();
                }
                if (args.Length >= 1)
                {
                    if(args[0] != null)
                        host = args[0];
                    if(args.Length >= 2 && args[1] != null)
                        port = args[1];
                    if(args.Length >= 3 && args[2] != null)
                        mac = args[2];
                    if (args.Length >= 4 && args[3] != null)
                    {
                        coin = int.Parse(args[3]);
                        integrate = int.Parse(args[3]);
                    }
                    if(args.Length >= 5 && args[4] != null)
                        Override = args[4];
                }
                WriteDebug("init game progress! start param: host=" + host + ",port=" + port + ",mac=" + mac + ",coin=" + coin + ",integrate=" + integrate + ",Override=" + Override);

                ThreadStart child = new ThreadStart(WinningController);
                Thread childref = new Thread(child);
                childref.IsBackground = true;
                childref.Start();

                Thread.Sleep(500);

                ThreadStart child1 = new ThreadStart(Commit);
                Thread childref1 = new Thread(child1);
                childref1.IsBackground = true;
                childref1.Start();

                WriteDebug("success Init!");
            }
            catch (Exception e)
            {
                WriteDebug("Init exception " + e.ToString());
                throw e;
            }
        }

        private void Start()
        {
            try
            {
                while (true)
                {
                    string url = GetHead() + GetHost() + ":" + GetPort() + "/game/gm_start?devid=" + mac;
                    Console.WriteLine("game start {0}", url);
                    string result = HttpSend1(url);
                    if (result.Contains("ok"))
                    {
                        WriteDebug("start game !");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public bool GameStart()
        {
            ThreadStart child = new ThreadStart(Start);
            Thread childref = new Thread(child);
            childref.IsBackground = true;
            childref.Start();

            return true;
        }

        public string GameStop()
        {
            try
            {
                if (!StopState)
                {
                    StopState = true;
                    string url = GetHead() + GetHost() + ":" + GetPort() + "/game/gm_stop?devid=" + mac;
                    WriteDebug("game stop " + url);
                    StopResult = HttpSend1(url);
                }
                if (StopResult.Contains("unidle") || StopResult.Contains("idle"))
                {
                    GameCoinSet();
                    GameIntegrateSet();
                    GameOverrideSet();
                    WriteDebug("stop game !");
                    StopState = false;
                    if (StopResult.Contains("unidle"))
                    {
                        StopResult = "unidle";
                    }
                    else
                    {
                        StopResult = "idle";
                    }
                }
                return StopResult;//返回值分unidle和idle两种，若返回值为idle，则游戏进入游戏的空闲状态
            }
            catch(Exception)
            {
                GameCoinSet();
                GameIntegrateSet();
                GameOverrideSet();
                WriteDebug("unusual stop game !");
                return "unidle";
            }
        }

        public void EventRcord(object m)
        {
            WriteDebug("push event to stack, " + m.ToString());
            myStack.Push(m);
        }

        private void Commit()
        {
            try
            {
                string str = "";
                while (true)
                {
                    if(myStack.Count != 0)
                    {
                        str = (string)myStack.Pop();
                        string url = GetHead() + GetHost() + ":" + GetPort() + "/game/gm_commit?devid=" + mac + "&data=" + str;
                        Console.WriteLine("game commit {0}", url);
                        HttpSend1(url);
                        WriteDebug("commit game EventRecord");
                    }

                    Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public void GameResultSend(string gamemark, string gamerank)
        {
            string url = GetHead() + GetHost() + ":" + GetPort() + "/game/gm_result?devid=" + mac + "&gamemark=" + gamemark + "&gamerank=" + gamerank;
            try
            {
                Console.WriteLine("game result {0}", url);
                HttpSend1(url);
                WriteDebug("send game result " + gamemark + "- -" + gamerank);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void IntegrateSend(int integrate)
        {
            string url = GetHead() + GetHost() + ":" + GetPort() + "/game/gm_integrate?devid=" + mac + "&integrate=" + integrate;
            try
            {
                WriteDebug("send game integrate start......" + url);
                while (true)
                {
                    string result = HttpSend1(url);
                    if (result.Contains("ok"))
                    {
                        WriteDebug("send game integrate success");
                        IntegrateSendStatus = true;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public bool GameIntegrateSend(int integrade)
        {
            if(IntegrateSendStatus)
            {
                WriteDebug("call GameIntegrateSend success");
                IntegrateSendStatus = false;
                Thread thread = new Thread(() => IntegrateSend(integrade));
                thread.Start();
            }

            return true;
        }

        private void GameCoinSettle(int coin1)
        {
            string url = GetHead() + GetHost() + ":" + GetPort() + "/game/gm_coinsettle?devid=" + mac + "&coin=" + coin1;
            try
            {
                WriteDebug("GameCoinSettle start ......" + url);
                while (true)
                {
                    string result = HttpSend1(url);
                    if (result.Contains("ok"))
                    {
                        WriteDebug("execute GameCoinSettle success");
                        CoinSendStatus = true;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public bool GameCoinSettleSend(int coin1)
        {
            if (CoinSendStatus)
            {
                WriteDebug("call GameCoinSettle success");
                CoinSendStatus = false;
                Thread thread = new Thread(() => GameCoinSettle(coin1));
                thread.Start();
            }

            return true;
        }
    }
}
