using libzkfpcsharp;
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
        //路径信息
        string rootpath = AppDomain.CurrentDomain.BaseDirectory;
        //设备句柄
        IntPtr mDevHandle = IntPtr.Zero;
        //数据库句柄
        IntPtr mDBHandle = IntPtr.Zero;
        bool bIsTimeToDie = false;
        bool IsRegister = false;
        bool bIdentify = true;
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

        public MainWindow()
        {
            InitializeComponent();
            rootpath = rootpath.Substring(0, rootpath.LastIndexOf("\\"));
            rootpath = rootpath.Substring(0, rootpath.LastIndexOf("\\"));
            rootpath = rootpath.Substring(0, rootpath.LastIndexOf("\\"));
            if (init())
                writelog("程序初始化成功！");
            else
                writelog("程序初始化失败！");
            //writelog();
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
            
            StreamWriter sw = new StreamWriter(rootpath+"\\Finger.log",true);
            string cur_time = DateTime.Now.ToString();
            sw.WriteLine(cur_time + "     " + text);
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
                    writelog("捕获成功，调用委托显示该图片！");
                    //string b64 = "";
                    //b64 = zkfp2.BlobToBase64(FPBuffer, FPBuffer.Length);
                    //这里可以加上数据库
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
                    //textRes.Text = "This finger was already register by " + fid + "!";
                    return;
                }
                if (RegisterCount > 0 && zkfp2.DBMatch(mDBHandle, CapTmp, RegTmps[RegisterCount - 1]) <= 0)
                {
                    //textRes.Text = "Please press the same finger 3 times for the enrollment";
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
                        iFid++;
                        //textRes.Text = "enroll succ";
                    }
                    else
                    {
                        //textRes.Text = "enroll fail, error code=" + ret;
                    }
                    IsRegister = false;
                    return;
                }
                else
                {
                    //textRes.Text = "You need to press the " + (REGISTER_FINGER_COUNT - RegisterCount) + " times fingerprint";
                }
            }
            else
            {
                if (cbRegTmp <= 0)
                {
                    //textRes.Text = "Please register your finger first!";
                    return;
                }
                if (bIdentify)
                {
                    int ret = zkfp.ZKFP_ERR_OK;
                    int fid = 0, score = 0;
                    ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);
                    if (zkfp.ZKFP_ERR_OK == ret)
                    {
                        //textRes.Text = "Identify succ, fid= " + fid + ",score=" + score + "!";
                        return;
                    }
                    else
                    {
                        //textRes.Text = "Identify fail, ret= " + ret;
                        return;
                    }
                }
                else
                {
                    int ret = zkfp2.DBMatch(mDBHandle, CapTmp, RegTmp);
                    if (0 < ret)
                    {
                        //textRes.Text = "Match finger succ, score=" + ret + "!";
                        return;
                    }
                    else
                    {
                        //textRes.Text = "Match finger fail, ret= " + ret;
                        return;
                    }
                }
            }

        }



    }
}
