using libzkfpcsharp;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Finger
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        string filepath = "";
        //路径信息
        string rootpath = AppDomain.CurrentDomain.BaseDirectory;
        //设备句柄
        IntPtr mDevHandle = IntPtr.Zero;
        //数据库句柄
        IntPtr mDBHandle = IntPtr.Zero;
        bool bIsTimeToDie = false;
        bool IsRegister = false;
        bool bIdentify = false;
        //指纹数组
        byte[] FPBuffer;
        //注册采集时需要用到
        int RegisterCount = 0;
        const int REGISTER_FINGER_COUNT = 3;
        //三次预登记指纹模板
        byte[][] RegTmps = new byte[3][];
        //指纹登记模板
        byte[] RegTmp = new byte[2048];
        //指纹模板
        byte[] CapTmp = new byte[2048];
        int cbCapTmp = 2048;
        int cbRegTmp = 0;
        int iFid = 1;

        private int mfpWidth = 0;
        private int mfpHeight = 0;

        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;
        List<FingerInfo> infos = new List<FingerInfo>();
        public MainWindow()
        {
            InitializeComponent();
            rootpath = rootpath.Substring(0, rootpath.LastIndexOf("\\"));
            MYAES.AesDecrypt(rootpath + "\\Finger.log", User.UserPassword);
            //rootpath = rootpath.Substring(0, rootpath.LastIndexOf("\\"));
            //rootpath = rootpath.Substring(0, rootpath.LastIndexOf("\\"));
        }

        //初始化
        private bool init()
        {
            cmbIdx.Items.Clear();
            int ret = zkfperrdef.ZKFP_ERR_OK;
            if ((ret = zkfp2.Init()) == zkfperrdef.ZKFP_ERR_OK)
            {
                int nCount = zkfp2.GetDeviceCount();
                if (nCount > 0)
                {
                    for (int i = 0; i < nCount; i++)
                    {
                        cmbIdx.Items.Add(i.ToString());
                    }
                    cmbIdx.SelectedIndex = 0;
                    writelog("已获取全部可连接设备！");
                }
                else
                {
                    zkfp2.Terminate();
                    MessageBox.Show("当前无设备可连接！");
                    writelog("init()函数返回值：false;详情：当前无设备可连接!");
                    return false;
                }
            }
            else
            {
                MessageBox.Show("初始化失败, ret=" + ret + " !"+"请检查设备连接情况！");
                writelog("init()函数返回值：false;详情：初始化失败, ret=" + ret + " !" + "请检查设备连接情况！");
                return false;
            }


            ret = zkfp.ZKFP_ERR_OK;
            //打开设备
            if (IntPtr.Zero == (mDevHandle = zkfp2.OpenDevice(cmbIdx.SelectedIndex)))
            {
                MessageBox.Show("设备打开失败！");
                writelog("init()函数返回值：false;详情：设备打开失败！");
                return false;
            }
            writelog("成功打开设备！");
            //初始化数据库
            if (IntPtr.Zero == (mDBHandle = zkfp2.DBInit()))
            {
                MessageBox.Show("初始化数据库失败！");
                writelog("init()函数返回值：false;详情：初始化数据库失败！");
                zkfp2.CloseDevice(mDevHandle);
                mDevHandle = IntPtr.Zero;
                return false;
            }
            writelog("初始化数据库成功！");
            //初始化数据
            RegisterCount = 0;
            cbRegTmp = 0;
            iFid = 1;

            for (int i = 0; i < 3; i++)
            {
                RegTmps[i] = new byte[2048];
            }

            //获取参数，宽高
            byte[] paramValue = new byte[4];
            int size = 4;
            zkfp2.GetParameters(mDevHandle, 1, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

            size = 4;
            zkfp2.GetParameters(mDevHandle, 2, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

            //根据宽高开数组
            FPBuffer = new byte[mfpWidth * mfpHeight];

            
            bIsTimeToDie = false;
            //任务并行，循环捕获后台指纹
            Parallel.Invoke(() => Task.Run(() => DoCapture()));
            writelog("任务并行，循环捕获后台指纹");
            //textRes.Text = "Open succ";
            return true;
        }

        private void writelog(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { info.Text = text; });
            StreamWriter sw = new StreamWriter(rootpath+"\\Finger.log",true);
            string cur_time = DateTime.Now.ToString();
            sw.WriteLine(cur_time + "  "+ User.UserName + "   :" + text);
            sw.Close();
        }

        private void DoCapture()
        {
            while (!bIsTimeToDie)
            {
                cbCapTmp = 2048;
                int ret = zkfp2.AcquireFingerprint(mDevHandle, FPBuffer, CapTmp, ref cbCapTmp);
                if (ret == zkfp.ZKFP_ERR_OK)
                {
                    //捕获成功，调用委托显示该图片！
                    writelog("成功捕获本次指纹图片，调用委托显示该图片！");
                    Application.Current.Dispatcher.Invoke(() =>kernelfunc());
                }
                Thread.Sleep(200);
            }
        }

        public void kernelfunc()
        {
            //显示图片
            MemoryStream ms = new MemoryStream();
            BitmapFormat.GetBitmap(FPBuffer, mfpWidth, mfpHeight, ref ms);
            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            img.Source = bmp;
            ms.Close();
            //注册
            if (IsRegister)
            {
                int ret = zkfp.ZKFP_ERR_OK;
                int fid = 0, score = 0;
                //首先辨别是否已经注册
                ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);
                if (zkfp.ZKFP_ERR_OK == ret)
                {
                    writelog("该指纹已经被注册!");
                    return;
                }
                if (RegisterCount > 0 && zkfp2.DBMatch(mDBHandle, CapTmp, RegTmps[RegisterCount - 1]) <= 0)
                {
                    writelog("由于指纹注册时匹配错误,请用相同的指纹按压三次！");
                    return;
                }
                Array.Copy(CapTmp, RegTmps[RegisterCount], cbCapTmp);
                String strBase64 = zkfp2.BlobToBase64(CapTmp, cbCapTmp);
                byte[] blob = zkfp2.Base64ToBlob(strBase64);
                RegisterCount++;
                if (RegisterCount >= REGISTER_FINGER_COUNT)
                {
                    RegisterCount = 0;
                    if (zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBMerge(mDBHandle, RegTmps[0], RegTmps[1], RegTmps[2], RegTmp, ref cbRegTmp)) &&
                           zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBAdd(mDBHandle, iFid, RegTmp)))
                    {
                        FingerInfo f = new FingerInfo();
                        f.id = iFid;
                        infos.Add(f);
                        iFid++;
                        writelog("采集已完成!请补充登记信息！");
                    }
                    else
                    {
                        writelog("注册失败，错误代码：" + ret);
                    }
                    IsRegister = false;
                    return;
                }
                else
                {
                    writelog("请继续按压" + (REGISTER_FINGER_COUNT - RegisterCount) + "次");
                }
            }
            else
            {
                if (cbRegTmp <= 0)
                {
                    //writelog("请先注册你的指纹！");
                    return;
                }
                if (bIdentify)
                {
                    int ret = zkfp.ZKFP_ERR_OK;
                    int fid = 0, score = 0;
                    ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);
                    if (zkfp.ZKFP_ERR_OK == ret)
                    {
                        foreach (var item in infos)
                        {
                            if(fid == item.id)
                            {
                                string s = "识别结果：" + item.name + "的"+item.finger+"，" + item.sex+"，"+item.age + "岁，联系方式为：" + item.phone + ",识别匹配分数为:" + score + "!";
                                writelog(s);
                            }
                        }
                        bIdentify = false;
                        //textRes.Text = "Identify succ, fid= " + fid + ",score=" + score + "!";
                        return;
                    }
                    else
                    {
                        writelog("指纹识别失败，错误代码为:" + ret );
                        bIdentify = false;
                        return;
                    }
                }
                //else
                //{
                //    int ret = zkfp2.DBMatch(mDBHandle, CapTmp, RegTmp);
                //    if (0 < ret)
                //    {
                //        writelog("匹配指纹成功，匹配分数为："+ret+"!");
                //        return;
                //    }
                //    else
                //    {
                //        writelog("匹配指纹失败，错误代码为：" + ret + "!");
                //        return;
                //    }
                //}
            }

        }

        //所有btn点击事件
        private void btn_click(object sender, RoutedEventArgs e)
        {
            Button btn = e.Source as Button;
            switch(btn.Content)
            {
                case "退出":
                    bIsTimeToDie = true;
                    writelog("成功退出！");
                    zkfp2.Terminate();
                    System.Environment.Exit(0);
                    break;
                case "查看日志":
                    System.Diagnostics.Process.Start("notepad.exe", rootpath+"\\Finger.log");
                    break;
                case "帮助":
                    MessageBox.Show("请联系管理员：jovy-rtt");
                    break;
                case "采集":
                    IsRegister = true;
                    info.Text = "请用相同的指纹按压3次！";
                    break;
                case "识别":
                    bIdentify = true;
                    info.Text = "请按压指纹识别器！";
                    break;
                case "登记/更新":
                    foreach (var item in infos)
                    {
                        if(iFid-1 == item.id)
                        {
                            item.name = name.Text;
                            item.phone = phone.Text;
                            item.age = Age.Text;
                            if (Sex.IsChecked == null || Sex.IsChecked == false)
                                item.sex = "女";
                            else
                                item.sex = "男";
                            item.finger = Cbox.Text;
                        }
                    }
                    writelog("id为："+(iFid-1).ToString()+" 的指纹信息已登记/更新！");
                    break;
                case "打开":
                    
                    OpenFileDialog obj = new OpenFileDialog();
                    if (obj.ShowDialog() == true)
                        filepath = obj.FileName;
                    BitmapImage bimg = new BitmapImage(new Uri(filepath));
                    img.Source = bimg;
                    break;
                case "保存":
                    SaveFileDialog save = new SaveFileDialog();
                    save.Filter = "Image Files (*.bmp, *.png, *.jpg)|*.bmp;*.png;*.jpg | All Files | *.*";
                    save.RestoreDirectory = true;
                    if(save.ShowDialog()==true)
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create((BitmapSource)this.img.Source));
                        using (FileStream stream = new FileStream(save.FileName, FileMode.Create))
                            encoder.Save(stream);
                    }
                    break;
                default:
                    //MessageBox.Show("该控件并未注册点击事件，请先注册！");
                    break;
                
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bIsTimeToDie = true;
            writelog("成功退出！");
            zkfp2.Terminate();
            MYAES.AesEncrypt(rootpath + "\\Finger.log", User.UserPassword);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (init())
                writelog("程序初始化成功！");
            else
                writelog("程序初始化失败！");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            zkfp2.Terminate();
            cmbIdx.Items.Clear();
            writelog("已成功关闭设备");
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            User.infos = infos;
            ShowInfo dbinfo = new ShowInfo();
            writelog("用户查看指纹库信息！");
            dbinfo.ShowDialog();
        }

        private void username_btn_Click(object sender, RoutedEventArgs e)
        {
            Login login = new Login();
            writelog("用户登录！");
            login.ShowDialog();
            checkuser();
        }

        public void checkuser()
        {
            if (User.UserAccount == "admin")
            {
                username_btn.Content = User.UserName;
                username_btn.IsEnabled = false;
            }
        }

        public bool IsRoot()
        {
            if (User.UserAccount == "admin")
            {
                return true;
            }
            return false;
        }
    }
}
