using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows.Threading;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using Microsoft.WindowsAPICodePack.Net;  

namespace PokerJoker
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class Window1 : Window
    {
        Random random;
        RotateTransform rotateTransform;

        Thread server_Thread;
        Socket server_Socket;

        Thread client_Thread;
        Socket client_Socket;

        Dictionary<String, String> dic = new Dictionary<string, string>();

        //WPF的定时器使用DispatcherTimer类对象
        private System.Windows.Threading.DispatcherTimer dTimer = new DispatcherTimer();

        public Window1()
        {
            InitializeComponent();

            //隐藏鼠标
            Mouse.OverrideCursor = Cursors.None;
            //导入字典
            putData();

            //设置窗体出现位置
            random = new Random();
            double sw= SystemParameters.PrimaryScreenWidth;//得到屏幕整体宽度
            double sh = SystemParameters.PrimaryScreenHeight;//得到屏幕整体高度
            WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = sw/5*3 + random.Next(-60,100);
            this.Top = sh / 5 + random.Next(-50,80);
            //设置卡牌随机旋转角度
            rotateTransform = new RotateTransform(random.Next(0, 180));//随机度
            rotateTransform.CenterX = card.Source.Width / 2;
            rotateTransform.CenterY = card.Source.Height / 2;
            
            card.RenderTransform = rotateTransform;//图片控件旋转

            //隐藏窗体
            this.Visibility = Visibility.Hidden;

            if (IsNetworkConnected())
            {
                //开启通信
                server_Thread = new Thread(new ThreadStart(ServerStart));
                server_Thread.Start();
            }
            else 
            {
                server_Thread = null;
                //MessageBox.Show("net error");
            }

            //定时器使用委托（代理）对象调用相关函数（方法）dTimer_Tick;
            //注：此处 Tick 为 dTimer 对象的事件（ 超过计时器间隔时发生）
            dTimer.Tick += new EventHandler(dTimer_Tick);
            //设置时间：TimeSpan（时, 分,秒）
            dTimer.Interval = new TimeSpan(0, 1, 0);
            //启动 DispatcherTimer对象dTime。
            dTimer.Start();

            //开机启动
            //SetSelfStarting(true,"高级魔术师");
        }

        private void dTimer_Tick(object sender, EventArgs e)
        {
            if (!IsNetworkConnected())
            {
                //网络断开
                server_Socket = null;
                //MessageBox.Show("net connecting");
            }
            else
            {
                if (!IsSocketConnected(server_Socket))
                {
                    //连接已断开
                    server_Socket = null;
                    server_Thread = new Thread(new ThreadStart(ServerStart));
                    server_Thread.Start();
                    client_Socket = null;
                    //MessageBox.Show("线程已断开，正在连接");
                }
                else
                {
                    //MessageBox.Show("线程连接中");
                }
            }
        }

        /// <summary>
        /// 导入字典
        /// </summary>
        private void putData() {
            //dic.Add();
        }

        /// <summary>
        /// 通信服务
        /// </summary>
        private void ServerStart()
        {
            String strIp = "192.168.1.100";
            try
            {
                StreamReader sr = new StreamReader("ip.txt", Encoding.Default);
                strIp = sr.ReadToEnd();
                sr.Close();
            }
            catch (Exception ex)
            {
                //MessageBox.Show("file error:"+ex.Message);
            }
            
            //创建IPEndPoint
            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(strIp), 8888);
            //创建Socket实例
            server_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //绑定Socket与IPEndPoint
            try
            {
                server_Socket.Bind(ipep);
                //设置Socket收听模式
                server_Socket.Listen(10);

                //MessageBox.Show("连接成功");
            }
            catch (Exception ex) 
            {
                server_Socket = null;
            }
  
            while (true && null != server_Socket)
            {
                try
                {
                    //接受Andorid信息
                    client_Socket = server_Socket.Accept();
                    client_Thread = new Thread(new ThreadStart(ReceiveAndroidData));
                    client_Thread.Start();
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("start error: " + ex.Message);
                    
                }
            }
        }

        /// <summary>
        /// 接收方法
        /// </summary>
        private void ReceiveAndroidData()
        {
            bool keepalive = true;
            Socket socketclient = client_Socket;
            Byte[] buffer = new Byte[1024];

            //根据收听到的客户端套接字向客户端发送信息
            IPEndPoint clientep = (IPEndPoint)socketclient.RemoteEndPoint;
            string str = "connect server----- ";
            byte[] data = new byte[1024];
            data = Encoding.ASCII.GetBytes(str);
            socketclient.Send(data, data.Length, SocketFlags.None);

            while (keepalive)
            {
                //在套接字上接收客户端发送的信息
                int buffer_lenght = 0;
                try
                {
                    buffer_lenght = socketclient.Available;

                    socketclient.Receive(buffer, 0, buffer_lenght, SocketFlags.None);
                    if (buffer_lenght == 0)
                        continue;
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("receive error:" + ex.Message);
                    return;
                }
                clientep = (IPEndPoint)socketclient.RemoteEndPoint;
                string strAndroid_CMD = System.Text.Encoding.ASCII.GetString(buffer).Substring(0, buffer_lenght);

                //对根据strAndroid_CMD到数据库中查询值
                //MessageBox.Show("receive data:" + strAndroid_CMD);
                Console.WriteLine("receive data:" + strAndroid_CMD);
                if ("DISCONNECT".Equals(strAndroid_CMD))
                {
                    keepalive = false;
                }
                else
                {
                    //使用Invoke方法执行DMSGD代理(其类型是DispMSGDelegate)
                    DispMsg(0,strAndroid_CMD);
                }
            }
        }

        /// <summary>
        ///  定义一个代理
        /// </summary>
        /// <param name="index"></param>
         /// <param name="MSG"></param>
        private delegate void DispMSGDelegate(int index,string MSG);

        /// <summary>
        /// 定义一个函数，用于向窗体上的ListView控件添加内容
        /// </summary>
         /// <param name="iIndex"></param>
        /// <param name="strMsg"></param>
        private void DispMsg(int iIndex,string strMsg)
        {

            if (this.Dispatcher.CheckAccess())
            {
                if (strMsg != null && strMsg.Contains("HIDDEN"))
                {//隐藏
                    this.Visibility = Visibility.Hidden;
                }
                else
                {
                    if (strMsg != null && (strMsg.Contains("CARD")))
                    {
                        try
                        {
                            strMsg = strMsg.Trim();
                            System.Windows.Media.Imaging.BitmapImage bi = new System.Windows.Media.Imaging.BitmapImage();
                            bi.BeginInit();
                            bi.UriSource = new Uri("cards/"+strMsg+".png", UriKind.Relative);
                            bi.EndInit();
                            card.Source = bi;

                            //设置卡牌随机旋转角度
                            rotateTransform = new RotateTransform(random.Next(0, 180));//随机度
                            rotateTransform.CenterX = card.Source.Width / 2;
                            rotateTransform.CenterY = card.Source.Height / 2;
                            
                            card.RenderTransform = rotateTransform;//图片控件旋转

                            this.Visibility = Visibility.Visible;
                        }
                        catch (Exception ex)
                        {
                            //MessageBox.Show("receive error:" + ex.Message);
                        }
                    }
                    else
                    {
                        //bad request
                    }
                }
            }
            else 
            {   //代理，用户处理界面
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,new DispMSGDelegate(DispMsg), 0, strMsg);
            }
        }
        
        // 检查网络是否连接
        private bool IsNetworkConnected()
        {
            bool isConn = false;
            NetworkCollection networks = NetworkListManager.GetNetworks(NetworkConnectivityLevels.All);
            foreach (Network n in networks)
            {
                if (n.IsConnected)//&& n.IsConnectedToInternet)
                {
                    isConn = true;
                    break;
                }
            }
            return isConn;
        }

        // 检查一个Socket是否可连接
        private bool IsSocketConnected(Socket client)
        {
            if (null == client) 
            {
                return false;
            }

            bool blockingState = client.Blocking;
            try
            {
                byte[] tmp = new byte[1];
                client.Blocking = false;
                client.Send(tmp, 0, 0);
                return false;
            }
            catch (SocketException e)
            {
                // 产生 10035 == WSAEWOULDBLOCK 错误，说明被阻止了，但是还是连接的
                if (e.NativeErrorCode.Equals(10035))
                    return false;
                else
                    return true;
            }
            finally
            {
                client.Blocking = blockingState;    // 恢复状态
            }
        }


        /// <summary>
        /// 开机自动启动
        /// </summary>
        /// <param name="started">设置开机启动，或取消开机启动</param>
        /// <param name="exeName">注册表中的名称</param>
        /// <returns>开启或停用是否成功</returns>
        public static bool SetSelfStarting(bool started, string exeName)
        {
            RegistryKey key = null;
            try
            {

                string exeDir = Process.GetCurrentProcess().MainModule.FileName;;
                key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);//打开注册表子项

                if (key == null)//如果该项不存在的话，则创建该子项
                {
                    key = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                }
                if (started)
                {
                    try
                    {
                        object ob = key.GetValue(exeName, -1);

                        if (!ob.ToString().Equals(exeDir))
                        {
                            if (!ob.ToString().Equals("-1"))
                            {
                                key.DeleteValue(exeName);//取消开机启动
                            }
                            key.SetValue(exeName, exeDir);//设置为开机启动
                        }
                        key.Close();

                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        key.DeleteValue(exeName);//取消开机启动
                        key.Close();
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                if (key != null)
                {
                    key.Close();
                }
                return false;
            }
        }

    }
}
