using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinformException
{
    public partial class FrmBugReport : Form
    {
        Exception _buginfo;
        public FrmBugReport(Exception buginfo)
        {
            InitializeComponent();
            _buginfo = buginfo;
            this.textBox1.Text = buginfo.Message;
            lblErrorCode.Text = Guid.NewGuid().ToString();
        }

        public FrmBugReport(Exception buginfo, string errorCode)
        {
            InitializeComponent();
            _buginfo = buginfo;
            this.textBox1.Text = buginfo.Message;
            lblErrorCode.Text = errorCode;
        }

        public static void ShowBug(Exception buginfo, string errorCode)
        {
            new FrmBugReport(buginfo, errorCode).ShowDialog();
        }

        public static void ShowBug(Exception buginfo)
        {
            new FrmBugReport(buginfo, Guid.NewGuid().ToString());
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("异常详细信息：" + _buginfo.Message + "\r\n跟踪：" + _buginfo.StackTrace);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //重启或者关闭
            Application.Restart();
        }
    }
}
