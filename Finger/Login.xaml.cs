using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Finger
{
    /// <summary>
    /// Login.xaml 的交互逻辑
    /// </summary>
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source == sign_in)//登录事件
            {
                string UserType = Combox.Text;
                if(account.Text=="admin" && GetMd5(passward.Password)==User.UserPassword)
                {
                    User.UserAccount = "admin";
                    this.Close();
                }
                else
                {
                    MessageBox.Show("登录失败！");
                }
            }
            else if (e.Source == forgetPw)//忘记密码事件
            {
                MessageBox.Show("请联系系统管理员：jovy-rtt");
            }
            else if (e.Source == sign_for)//注册事件
            {
                MessageBox.Show("请联系系统管理员：jovy-rtt");
            }
        }

        static string GetMd5(string str)
        {
            //创建MD5哈稀算法的默认实现的实例
            MD5 md5 = MD5.Create();
            //将指定字符串的所有字符编码为一个字节序列
            byte[] buffer = Encoding.Default.GetBytes(str);
            //计算指定字节数组的哈稀值
            byte[] bufferMd5 = md5.ComputeHash(buffer);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bufferMd5.Length; i++)
            {
                //x:表示将十进制转换成十六进制
                sb.Append(bufferMd5[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
