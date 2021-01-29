using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftEtherVPN_AutoMacro
{
    struct VPNSERVERITEM
    {
        public int nItemIndex;
        public String raw_ip;
        public String raw_country;
        public String raw_speed;

        public String qual_ip;
        public PingTest pingTest;
    }

    delegate bool ProcSequence();

    public partial class Form1 : Form
    {
        List<VPNSERVERITEM> m_vpnServerList;
        ProcSequence m_procSequence;
        int m_nConnectionRetryCount;
        int m_nConnectionRetryMaxLimit;
        int m_nSeqRetryTimeout;

        public Form1()
        {
            InitializeComponent();

            m_vpnServerList = null;
            m_procSequence = checkVpnClientNormalManager;

            Log("App Run..");
        }

        private void Log(String msg)
        {
            listBox2.Items.Insert(0, msg);
            if( listBox2.Items.Count > 2000)
            {
                listBox2.Items.RemoveAt(listBox2.Items.Count-1);
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            if(m_procSequence == null)
            {
                timer1.Interval = 50;
                timer1.Enabled = true;
            }

            if( m_procSequence() )
            {
                timer1.Enabled = true;
            }
        }


        private void UpdateVpnServerList(IntPtr wndListView)
        {
            ListView listView = new ListView(wndListView);
            int count = listView.ItemCount;
            List<VPNSERVERITEM> vpnServerList = new List<VPNSERVERITEM>();
            Log(String.Format("[UpdateVpnServerList] ItemCount={0}", count));

            for (int i = 0; i < count; i++)
            {
                VPNSERVERITEM item = new VPNSERVERITEM();

                item.raw_ip = listView.GetItem(i, 1);
                item.raw_country = listView.GetItem(i, 2);
                item.raw_speed = listView.GetItem(i, 5);

                item.nItemIndex = i;
                item.qual_ip = item.raw_ip;
                int idx = item.qual_ip.IndexOf("(");
                if (idx >= 0)
                {
                    item.qual_ip = item.raw_ip.Substring(0, idx);
                    item.qual_ip.Trim();
                }

                vpnServerList.Add(item);
            }

            if (vpnServerList.Count == 0)
            {
                Log(String.Format("Retrieved server list ItemCount={0}", count));
                return;
            }

            vpnServerList.Select(x => x.raw_ip).Distinct();

            List<String> FilterCountry = new List<string>();
            if (checkBox1.Checked)
            {
                // 필터 적용할 국가 선택
                foreach (var item in vpnServerList)
                {
                    if (FilterCountry.FindIndex(x => x.Equals(item.raw_country)) == -1)
                    {
                        FilterCountry.Add(item.raw_country);
                    }
                }

                // 필터 국가를 FilterCountry에서 제거.
                foreach (String item in listBox1.Items)
                {
                    int idx = FilterCountry.FindIndex(x => x.Equals(item));
                    if (idx >= 0)
                    {
                        FilterCountry.RemoveAt(idx);
                    }
                }
            }
            else
            {
                FilterCountry.Clear();
            }


            // 남은 국가의 아이템을 모두 삭제
            foreach (String item in FilterCountry)
            {
                while (true)
                {
                    int idx = vpnServerList.FindIndex(x => x.raw_country.Equals(item));
                    if (idx < 0) break;

                    vpnServerList.RemoveAt(idx);
                }
            }

            m_vpnServerList = vpnServerList;

            List<String> oldServerIP = new List<string>();

            foreach (ListViewItem item in listView1.Items)
            {
                oldServerIP.Add(item.Text);
            }

            foreach (var item in vpnServerList)
            {
                int index = oldServerIP.FindIndex(x => x.Equals(item.raw_ip));
                if (index == -1)
                {
                    ListViewItem lvItem = new ListViewItem();
                    lvItem.Text = item.raw_ip;
                    lvItem.SubItems.Add(item.raw_country);
                    lvItem.SubItems.Add(item.raw_speed);
                    lvItem.SubItems.Add("");
                    lvItem.SubItems.Add("");
                    listView1.Items.Add(lvItem);
                }
                else
                {
                    oldServerIP.RemoveAt(index);
                }
            }

            if (oldServerIP.Count > 0)
            {
                foreach (var item in oldServerIP)
                {
                    ListViewItem lvItem = listView1.FindItemWithText(item);
                    if(lvItem != null)
                    {
                        listView1.Items.Remove(lvItem);
                    }
                }
            }
        }

        private bool checkVpnServerListWindow()
        {
            Log("[checkVpnServerListWindow]");
        
            //VPN Gate Academic Experimental Project Plugin for SoftEther VPN Client
            IntPtr hWnd = Interop.FindWindow(null, "VPN Gate Academic Experimental Project Plugin for SoftEther VPN Client");
            if (hWnd == IntPtr.Zero)
            {
                Log("No windows found.. wait..");
                timer1.Interval = 100;
                return true;
            }

            //SysListView32
            IntPtr wndListView = Interop.FindWindowEx(hWnd, IntPtr.Zero, "SysListView32", null);
            if (wndListView == IntPtr.Zero)
            {
                Log("No ListView for the server list found.. wait..");
                timer1.Interval = 100;
                return true;
            }

            Thread.Sleep(500);
            if(m_vpnServerList ==null)
            {
                Log("Current VpnServer List = null");
            }
            else
            {
                Log(String.Format("Current VpnServer List items={0}", m_vpnServerList.Count));
            }

            if (m_vpnServerList ==null || m_vpnServerList.Count == 0 )
            {
                UpdateVpnServerList(wndListView);

                if(m_vpnServerList != null && m_vpnServerList.Count > 0 )
                {
                    timer1.Interval = 10;
                    m_procSequence = checkVpnServerPingTest;
                    Log("Now ping test to the servers..");
                    return true;
                }
            }
            else
            {
                m_procSequence = connectTryVPNServerItem;
                return true;
            }

            Log("No more run... stop the sequence..");
            return false;
        }




        private bool checkVpnClientOperatingMode()
        {
            Log("[checkVpnClientOperatingMode]");
            IntPtr hWnd = Interop.FindWindow(null, "Switch SoftEther VPN Client Operating Mode");
            if (hWnd == IntPtr.Zero)
            {
                Log("OperatingMode Window Not Found.. Now checking the normal manager..");
                m_procSequence = checkVpnClientNormalManager;
                timer1.Interval = 100;
                return true;
            }

            List<Window> childWindows = Window.GetAllChildWindows(hWnd);
            Window easyModeButton = null;
            foreach (var wnd in childWindows)
            {
                if (wnd.Caption.CompareTo("&Easy Mode") == 0)
                {
                    Log("Radio button[Easy Mode] was found..");
                    easyModeButton = wnd;
                    break;
                }
            }

            if(easyModeButton!= null)
            {
                Log("Now click and select the radio button..");
                easyModeButton.PostMessage(Interop.Messages.WM_LBUTTONDOWN, new IntPtr(1), new IntPtr(0xC0026));
                Thread.Sleep(50);
                easyModeButton.PostMessage(Interop.Messages.WM_LBUTTONUP, new IntPtr(0), new IntPtr(0xC0026));
                Thread.Sleep(50);


                Log("Select OK and close the dialog..");
                Window window = new Window(hWnd);
                window.PostMessage(Interop.Messages.WM_COMMAND, new IntPtr(1), IntPtr.Zero);
                timer1.Interval = 500;
                m_procSequence = checkVpnClientEasyManager;

                return true;
            }


            return false;
        }


        private bool checkVpnClientEasyManager()
        {
            m_nConnectionRetryCount = 0;

            Log("[checkVpnClientEasyManager]");
            //SoftEther VPN Client Easy Manager
            IntPtr hWnd = Interop.FindWindow(null, "SoftEther VPN Client Easy Manager");
            if (hWnd == IntPtr.Zero)
            {
                Log("Easy Mananger Not found..");
                m_procSequence = determineOperationModeNormalOrEasy;
                timer1.Interval = 100;
                return true;
            }

            // 
            Log("Now call the server list dialog..");

            IntPtr hServerListWnd = Interop.FindWindow(null, "VPN Gate Academic Experimental Project Plugin for SoftEther VPN Client");
            if (hServerListWnd == IntPtr.Zero)
            {
                Window window = new Window(hWnd);
                window.PostMessage(Interop.Messages.WM_COMMAND, new IntPtr(0x00459), IntPtr.Zero);
                timer1.Interval = 200;
            }
            m_procSequence = checkVpnServerListWindow;

            return true;
        }

        private bool runEtherVPNProcess__()
        {
            Log("[runEtherVPNProcess]");
            String process_name = "vpncmgr_x64.exe";

            Process[] pname = Process.GetProcessesByName(process_name);
            if (pname.Length == 0)
            {
                Log("vpncmgr_x64.exe Process not found... Now run it...");

                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.Verb = "open";
                    psi.FileName = @"C:\\Program Files\\SoftEther VPN Client\\" + process_name;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    timer1.Interval = 500;
                }
                catch (Exception)
                {
                    Log("Ether VPN 프로세스 실행 실패..");
                    MessageBox.Show("Ether VPN 프로세스 실행 실패!", Text);
                    //e.Message;
                    button2_Click(null, null);
                    return false;
                }
            }
            else
            {
                Log("SoftEtherVPN process is running already..");
                timer1.Interval = 100;
            }

            m_procSequence = determineOperationModeNormalOrEasy;

            return true;
        }




        private bool checkVpnClientNormalManager()
        {
            Log("[checkVpnClientNormalManager]");
            //
            IntPtr hWnd = Interop.FindWindow(null, "SoftEther VPN Client Manager");
            if (hWnd == IntPtr.Zero)
            {
                Log("Not found.. move to check Easy Manager..");
                m_procSequence = checkVpnClientEasyManager;
                timer1.Interval = 100;
                return true;
            }

            Window window = new Window(hWnd);
            Log("Normal Manager found.. Check it visible.");
            if(!window.Visible)
            {
                Log("Normal Manager is invisible.. check it EasyManager.");
                m_procSequence = checkVpnClientEasyManager;
                timer1.Interval = 10;
                return true;
            }

            Log("Call the operation mode dialog to change to the easy manager.");
            // Normal Mode이면 Simple Mode로 Mode를 바꾼다.
            window.PostMessage(Interop.Messages.WM_COMMAND, new IntPtr(0x9c9E), IntPtr.Zero);
            timer1.Interval = 500;
            m_procSequence = checkVpnClientOperatingMode;
            return true;

            /*


            List<Window>  childWindows = Window.GetAllChildWindows(hWnd);
            ListView listView = null;
            foreach ( var wnd in childWindows)
            {
                if( wnd.ClassName == "SysListView32")
                {
                    if( wnd.ControlID == 0x0417 )
                    {
                        listView = new ListView(wnd.Handle);
                        break;
                    }
                }
            }

            if (listView == null)
            {
                timer1.Interval = 200;
                return true;
            }

            timer1.Interval = 200;
            int count = listView.ItemCount;

            // item이 2개면 IDLE 상태.
            if( count == 2 )
            {
                listView.SelectItem(1);
                listView.PostMessage(Interop.Messages.WM_LBUTTONDOWN, new IntPtr(1), new IntPtr(0x4000C5));
                Thread.Sleep(50);
                listView.PostMessage(Interop.Messages.WM_LBUTTONUP, new IntPtr(0), new IntPtr(0x4000C5));
                Thread.Sleep(50);
                listView.PostMessage(Interop.Messages.WM_LBUTTONDBLCLK, new IntPtr(1), new IntPtr(0x4000C5));
                return true;
            }
            //item이 3개면 접속 시도 상태.
            else if( count == 3 )
            {

            }*/
        }

        private bool determineOperationModeNormalOrEasy()
        {
            Window wndNormalMode = null;
            Window wndEasyMode = null;

            IntPtr hWnd = Interop.FindWindow(null, "SoftEther VPN Client Manager");
            if(hWnd != IntPtr.Zero)
            {
                Log("Normal Mode Window Found...");
                wndNormalMode = new Window(hWnd);
            }

            timer1.Interval = 10;

            hWnd = Interop.FindWindow(null, "SoftEther VPN Client Easy Manager");
            if (hWnd != IntPtr.Zero)
            {
                Log("Easy Mode Window Found...");
                wndEasyMode = new Window(hWnd);
            }

            if (wndEasyMode != null && wndEasyMode.Visible)
            {
                Log("Easy mode is visible ...");
                m_procSequence = checkVpnClientEasyManager;
            }
            else if (wndNormalMode != null && wndNormalMode.Visible)
            {
                Log("Normal mode is visible ...");
                m_procSequence = checkVpnClientNormalManager;
            }
            else
            {
                Log("Waiting for the SoftEtherVPN ...");
                timer1.Interval = 500;
            }

            return true;
        }

        private void PingTestThreadStart(PingTest ping)
        {
            VPNSERVERITEM item = (VPNSERVERITEM)ping.UserData;

            Invoke((MethodInvoker)delegate
            {
                ListViewItem lvItem = listView1.FindItemWithText(item.raw_ip);
                if( lvItem != null )
                {
                    lvItem.SubItems[4].Text = "PING START";
                }
            });
        }
        private void PingTestThreadError(PingTest ping, String msg)
        {
            VPNSERVERITEM item = (VPNSERVERITEM)ping.UserData;

            Invoke((MethodInvoker)delegate
            {
                ListViewItem lvItem = listView1.FindItemWithText(item.raw_ip);
                if (lvItem != null)
                {
                    lvItem.SubItems[4].Text = msg;
                }
            });
        }
        private void PingTestThreadFinish(PingTest ping, int pingSpeed)
        {
            VPNSERVERITEM item = (VPNSERVERITEM)ping.UserData;

            Invoke((MethodInvoker)delegate
            {
                ListViewItem lvItem = listView1.FindItemWithText(item.raw_ip);
                if (lvItem != null)
                {
                    listView1.TopItem = lvItem;
                    lvItem.SubItems[4].Text = "PING FIN";
                    lvItem.SubItems[3].Text = String.Format("{0}ms", pingSpeed);
                }
            });
        }

        private bool checkVpnServerPingTest()
        {
            Log("[checkVpnServerPingTest]");

            int threadCount = 0;
            foreach( var item in m_vpnServerList)
            {
                if( item.pingTest != null && !item.pingTest.JobFinish)
                {
                    threadCount++;
                }
            }

            Log(String.Format("Check Current PingTest = {0}", threadCount));

            if ( threadCount >= 10)
            {
                timer1.Interval = 300;
                return true;
            }

            for( int i = 0; i < m_vpnServerList.Count; i ++)
            {
                VPNSERVERITEM item = m_vpnServerList[i];

                if (item.pingTest == null)
                {
                    item.pingTest = new PingTest(item.qual_ip);
                    item.pingTest.UserData = item;
                    item.pingTest.PingThreadStartEvent += PingTestThreadStart;
                    item.pingTest.PingThreadErrorEvent += PingTestThreadError;
                    item.pingTest.PingThreadFinshEvent += PingTestThreadFinish;
                    item.pingTest.Start(true);
                    threadCount++;
                    m_vpnServerList[i] = item;

                    if (threadCount >= 10)
                    {
                        break;
                    }
                }
            }
            Log(String.Format("Adjusted Current PingTest = {0}", threadCount));

            if (threadCount > 0)
            {
                timer1.Interval = 300;
                return true;
            }

            List<String> itemsToRemove = new List<string>();

            // 이제 PING Test가 완료 됨.
            for( int i= m_vpnServerList.Count-1; i >= 0; i --)
            {
                VPNSERVERITEM item = m_vpnServerList[i];

                if(item.pingTest == null)
                {
                    itemsToRemove.Add(item.raw_ip);
                    m_vpnServerList.RemoveAt(i);
                    continue;
                }

                if (item.pingTest.JobFinish && !item.pingTest.ResultOK)
                {
                    itemsToRemove.Add(item.raw_ip);
                    m_vpnServerList.RemoveAt(i);
                }
            }

            // 이제 List View에서도 삭제 한다.
            foreach(String item in itemsToRemove)
            {
                ListViewItem lvItem = listView1.FindItemWithText(item);
                if(lvItem != null)
                {
                    listView1.Items.Remove(lvItem);
                }
            }

            for( int i= listView1.Items.Count-1; i >= 0; i --)
            {
                ListViewItem item = listView1.Items[i];
                if (item.SubItems[3].Text.Length == 0 && item.SubItems[4].Text.Length== 0)
                {
                    listView1.Items.RemoveAt(i);
                }
            }
            Log(String.Format("Ultimate VPN Server Count = {0}", m_vpnServerList.Count));

//            m_vpnServerList.OrderByDescending(p => p.pingTest.PingSpeed);


            m_vpnServerList.Sort(
                    delegate (VPNSERVERITEM p1, VPNSERVERITEM p2)
                    {
                        int compareDate = 1;

                        if (p1.pingTest.PingSpeed < p2.pingTest.PingSpeed)
                        {
                            compareDate = -1;
                        }

                        return compareDate;
                    }
                );


            timer1.Interval = 10;
            m_procSequence = connectTryVPNServerItem;

            return true;
        }

        private bool connectTryVPNServerItem()
        {
            Log("[connectTryVPNServerItem]");

            IntPtr hWnd = Interop.FindWindow(null, "VPN Gate Academic Experimental Project Plugin for SoftEther VPN Client");
            if (hWnd == IntPtr.Zero)
            {
                Log("No windows found.. wait..");
                timer1.Interval = 100;
                return true;
            }

            //SysListView32
            IntPtr wndListView = Interop.FindWindowEx(hWnd, IntPtr.Zero, "SysListView32", null);
            if (wndListView == IntPtr.Zero)
            {
                Log("No ListView for the server list found.. wait..");
                timer1.Interval = 100;
                return true;
            }

            ListView listView = new ListView(wndListView);
            listView.SelectItem(m_vpnServerList[0].nItemIndex);
            Thread.Sleep(100);
            m_nConnectionRetryCount = 0;
            ListViewItem lvItem = listView1.FindItemWithText(m_vpnServerList[0].raw_ip);
            if(lvItem != null)
            {
                lvItem.SubItems[4].Text = "Connecting..";
                lvItem.Focused = true;
                listView1.TopItem = lvItem;
            }

            Window dlg = new Window(hWnd);
            dlg.PostMessage( Interop.Messages.WM_COMMAND, new IntPtr(1), IntPtr.Zero);
            m_procSequence = determineWindowProtocolSelectConnectErrorConnecting;
            m_nSeqRetryTimeout = 0;
            timer1.Interval = 200;

            return true;
        }

        private bool determineWindowProtocolSelectConnectErrorConnecting()
        {
            IntPtr hWndProtocol = Interop.FindWindow(null, "Select VPN Protocol to Connect");
            IntPtr hWndConnectError = Interop.FindWindow(null, "Connect Error - VPN Gate Connection");
            IntPtr hWndConnecting = Interop.FindWindow(null, "Connecting to \"VPN Gate Connection\"...");
            Log("[determineWindowProtocolSelectConnectErrorConnecting]");

            timer1.Interval = 20;
            if (hWndProtocol != IntPtr.Zero && hWndConnectError == IntPtr.Zero && hWndConnecting == IntPtr.Zero)
            {
                m_procSequence = checkConnectionProtocolSelection;
            }
            else if(hWndProtocol == IntPtr.Zero && hWndConnectError != IntPtr.Zero && hWndConnecting == IntPtr.Zero)
            {
                m_procSequence = waitForConnectionEstablishOrRetry;
            }
            else if (hWndProtocol == IntPtr.Zero && hWndConnectError == IntPtr.Zero && hWndConnecting != IntPtr.Zero)
            {
                if(m_vpnServerList.Count> 0)
                {
                    Log(String.Format("Now connecting.. {0}", m_vpnServerList[0].qual_ip));
                }
                else
                {
                    Log("Now connecting.. UnknownIP");
                }
                timer1.Interval = 1000;
                m_procSequence = waitForConnectionEstablishOrRetry;
            }
            else if(m_nSeqRetryTimeout > 3)
            {
                timer1.Interval = 500;
                m_nSeqRetryTimeout = m_nSeqRetryTimeout + 1;
            }
            else
            {
                m_nSeqRetryTimeout = 0;
                m_procSequence = checkVpnClientEasyManager;
            }

            return true;
        }

        private bool checkConnectionProtocolSelection()
        {
            //Select VPN Protocol to Connect
            Log("[checkConnectionProtocolSelection]");

            IntPtr hWnd = Interop.FindWindow(null, "Select VPN Protocol to Connect");
            if (hWnd == IntPtr.Zero)
            {
                Log("No windows found.. wait..");
                timer1.Interval = 100;
                return true;
            }

            //Use &TCP Protocol (Ethernet over HTTPS VPN) (Recommended)

            List<Window> childWindows = Window.GetAllChildWindows(hWnd);
            Window protoSelect = null;
            foreach (var wnd in childWindows)
            {
                if (wnd.Caption.CompareTo("Use &TCP Protocol (Ethernet over HTTPS VPN) (Recommended)") == 0)
                {
                    Log("Radio button[Easy Mode] was found..");
                    protoSelect = wnd;
                    break;
                }
            }

            if (protoSelect != null)
            {
                Log("Now click and select the radio button..");
                protoSelect.PostMessage(Interop.Messages.WM_LBUTTONDOWN, new IntPtr(1), new IntPtr(0xC0026));
                Thread.Sleep(50);
                protoSelect.PostMessage(Interop.Messages.WM_LBUTTONUP, new IntPtr(0), new IntPtr(0xC0026));
                Thread.Sleep(50);


                Log("Select OK and close the dialog..");
                Window window = new Window(hWnd);
                window.PostMessage(Interop.Messages.WM_COMMAND, new IntPtr(1), IntPtr.Zero);
                timer1.Interval = 500;
                m_procSequence = determineWindowProtocolSelectConnectErrorConnecting;

                return true;
            }

            return false;
        }

        private bool waitForConnectionEstablishOrRetry()
        {
            Log("[waitForConnectionEstablishOrRetry]");
            //Connect Error - VPN Gate Connection
            IntPtr hWnd = Interop.FindWindow(null, "Connect Error - VPN Gate Connection");
            if (hWnd == IntPtr.Zero)
            {
                timer1.Interval = 500;

                hWnd = Interop.FindWindow(null, "SoftEther VPN Client Easy Manager");
                if (hWnd != IntPtr.Zero)
                {
                    IntPtr hWnd2 = Interop.FindWindowEx(hWnd, IntPtr.Zero, "SysListView32", null);
                    if (hWnd2 != IntPtr.Zero)
                    {
                        ListView listView = new ListView(hWnd2);
                        if( listView.ItemCount == 0)
                        {
                            timer1.Interval = 10;
                            m_procSequence = checkVpnClientEasyManager;
                        }
                    }
                    else
                    {
                        Log("No windows found.. wait..");
                    }
                }
                else
                {
                    Log("No windows found.. wait..");
                }

                return true;
            }

            Window window = new Window(hWnd);
            Log(String.Format("Cur Retry={0}  Max Retry={1}", m_nConnectionRetryCount, m_nConnectionRetryMaxLimit));

            // 최대 시도 회수를 넘으면 연결 취소한다.
            if ( m_nConnectionRetryCount >= m_nConnectionRetryMaxLimit)
            {
                Log("Now cancel retrying..");
                ListViewItem lvItem = listView1.FindItemWithText(m_vpnServerList[0].raw_ip);
                if (lvItem != null)
                {
                    lvItem.SubItems[4].Text = "Reconn Fail";
                }

                if (m_vpnServerList.Count > 0)
                {
                    m_vpnServerList.RemoveAt(0);
                }

                window.PostMessage(Interop.Messages.WM_COMMAND, new IntPtr((int)DialogResult.Cancel), IntPtr.Zero);
                m_procSequence = checkVpnClientEasyManager;
            }
            // 최대 시도 회수를 넘지 않았으면 재시도 한다.
            else
            {
                Log("Now retrying..");

                ListViewItem lvItem = listView1.FindItemWithText(m_vpnServerList[0].raw_ip);
                if (lvItem != null)
                {
                    lvItem.SubItems[4].Text = String.Format("Reconn [{0}]", m_nConnectionRetryCount);
                }

                m_nConnectionRetryCount = m_nConnectionRetryCount + 1;

                window.PostMessage(Interop.Messages.WM_COMMAND, new IntPtr((int)DialogResult.OK), IntPtr.Zero);
            }
            timer1.Interval = 200;
            return true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                m_nConnectionRetryMaxLimit = Int32.Parse(textBox1.Text);
            }
            catch (FormatException)
            {
                Log("Retry count parse error...");
                return;
            }

            Log("Button Start..");
            button1.Enabled = false;
            button2.Enabled = true;
            button1.Visible = false;
            label2.Left = button1.Left;
            label2.Top = button1.Top;
            label2.Visible = true;
            timer2.Enabled = true;
            label2.Text = "작동 중";
            timer2.Interval = 500;
            // Normal Manager 체크 하는 것 부터 시작.
            m_nSeqRetryTimeout = 0;
            m_procSequence = determineOperationModeNormalOrEasy;
            m_vpnServerList = null;
            m_nConnectionRetryCount = 0;
            listView1.Items.Clear();

            UpdateUI();

            timer1_Tick(null, e);
        }

        private void UpdateUI()
        {
            bool bEnableWindow = button1.Enabled;

            checkBox1.Enabled = bEnableWindow;
            listBox1.Enabled = bEnableWindow && checkBox1.Checked;
            comboBox1.Enabled = bEnableWindow && checkBox1.Checked;
            button3.Enabled = bEnableWindow && checkBox1.Checked;
            button4.Enabled = bEnableWindow && checkBox1.Checked;
            button5.Enabled = bEnableWindow && checkBox1.Checked;

            checkBox2.Enabled = bEnableWindow;
            label1.Enabled = bEnableWindow;
            textBox1.Enabled = bEnableWindow;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Log("Button Stop..");
            timer1.Enabled = false;
            button1.Enabled = true;
            button2.Enabled = false;
            button1.Visible = true;
            label2.Visible = false;
            timer2.Enabled = false;
            UpdateUI();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Left = label1.Right + 2;

            comboBox1.Items.Add("Japan");
            comboBox1.Items.Add("India");
            comboBox1.Items.Add("Russian Federation");
            comboBox1.Items.Add("Thailand");
            comboBox1.Items.Add("Malaysia");
            comboBox1.Items.Add("Ukraine");
            comboBox1.Items.Add("Brazil");
            comboBox1.Items.Add("Viet Nam");
            comboBox1.Items.Add("Indonesia");
            comboBox1.Items.Add("Spain");
            comboBox1.Items.Add("Israel");
            comboBox1.Items.Add("Trinidad and Tobago");
            comboBox1.Items.Add("United Kingdom");
            comboBox1.Items.Add("United States");
            comboBox1.Items.Add("Serbia");
            comboBox1.Items.Add("Austria");
            comboBox1.Items.Add("Suriname");
            comboBox1.Items.Add("China");
            comboBox1.Items.Add("Egypt");
            comboBox1.Items.Add("Colombia");
            comboBox1.Items.Add("Argentina");
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (timer2.Interval == 200)
            {
                timer2.Interval = 500;
                label2.Text = "작동 중";
            }
            else
            {
                timer2.Interval = 200;
                label2.Text = "";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex < 0)
            {
                MessageBox.Show("Select an item first", Text);
                return;
            }

            String val = comboBox1.Items[comboBox1.SelectedIndex].ToString();
            val.Trim();

            int idx = listBox1.FindString(val);
            if (idx >= 0) return;

            listBox1.Items.Add(val);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count == 0)
            {
                MessageBox.Show("No Selections", Text);
                return;
            }

//            if (MessageBox.Show("Remove Selected?", Text, MessageBoxButtons.YesNo) == DialogResult.No) return;

            ListBox.SelectedObjectCollection selectedItems = new ListBox.SelectedObjectCollection(listBox1);
            selectedItems = listBox1.SelectedItems;

            if (listBox1.SelectedIndex != -1)
            {
                for (int i = selectedItems.Count - 1; i >= 0; i--)
                    listBox1.Items.Remove(selectedItems[i]);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("All Clear?", Text, MessageBoxButtons.YesNo) == DialogResult.No) return;
            listBox1.Items.Clear();
        }
    }
}
