using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace logcurrent
{
    static class Program
    {
        private static object ExceptionLock = new object();
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();

                Application.EnableVisualStyles();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1());
                }
                else
                {
                    //创建启动对象
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                    //设置运行文件
                    startInfo.FileName = System.Windows.Forms.Application.ExecutablePath;

                    //设置启动动作,确保以管理员身份运行
                    startInfo.Verb = "runas";
                    //如果不是管理员，则启动UAC
                    Process.Start(startInfo);
                    //退出
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                lock (ExceptionLock)
                {
                    var strDateInfo = "出现应用程序未处理的异常：" + DateTime.Now + "\r\n";

                    var str = string.Format(strDateInfo + "异常类型：{0}\r\n异常消息：{1}\r\n异常信息：{2}\r\n",
                                               ex.GetType().Name, ex.Message, ex.StackTrace);

                    WriteLog(str);
                    if (ex.Message.Contains("网络故障"))
                    {
                        MessageBox.Show("发生网络故障，请检查网络，并重启程序", "网络故障", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //RestartApp();
                    }
                    else
                    {
                        MessageBox.Show("发生错误，请查看程序日志", "系统错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    Environment.Exit(0);
                }
            }
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            lock (ExceptionLock)
            {
                string str;
                var strDateInfo = "出现应用程序未处理的异常：" + DateTime.Now + "\r\n";
                var error = e.Exception;
                if (error != null)
                {
                    str = string.Format(strDateInfo + "异常类型：{0}\r\n异常消息：{1}\r\n异常内容", error.GetType().Name, error.Message, error.StackTrace);
                }
                else
                {
                    str = string.Format("应用程序线程错误：{0}", e);
                }

                WriteLog(str);
                if (error.Message.Contains("网络故障"))
                {
                    MessageBox.Show("发生网络故障，请检查网络，并重启程序", "网络故障", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //RestartApp();
                }
                else
                {
                    MessageBox.Show("发生错误，请查看程序日志", "系统错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                //Application.Exit();
                Environment.Exit(0);
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            lock (ExceptionLock)
            {
                var error = e.ExceptionObject as Exception;
                var strDateInfo = "出现应用程序未处理的异常：" + DateTime.Now + "\r\n";
                var str = error != null ? string.Format(strDateInfo + "异常消息:{0};\n\r堆栈信息:{1}", error.Message, error.StackTrace) : string.Format("Application UnhandledError:{0}", e);

                WriteLog(str);
                if (error.Message.Contains("网络故障"))
                {
                    MessageBox.Show("发生网络故障，请检查网络，并重启程序", "网络故障", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //RestartApp();
                }
                else
                {
                    MessageBox.Show("发生错误，请查看程序日志", "系统错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                //Application.Exit();
                Environment.Exit(0);
            }
        }

        static void RestartApp()
        {
            ProcessStartInfo p = new ProcessStartInfo();
            p.FileName = Application.ExecutablePath;
            p.UseShellExecute = true;
            Process.Start(p);
        }

        static void WriteLog(string str)
        {
            if (!Directory.Exists(Application.StartupPath + @"\ErrLog"))
            {
                Directory.CreateDirectory(Application.StartupPath + @"\ErrLog");
            }

            using (var sw = new StreamWriter(Application.StartupPath + @"\ErrLog\ErrLog.txt", true))
            {
                sw.WriteLine(str);
                sw.WriteLine("---------------------------------------------------------");
                sw.Close();
            }
        }
    }
}
