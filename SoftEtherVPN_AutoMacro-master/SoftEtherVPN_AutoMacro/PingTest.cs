using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace SoftEtherVPN_AutoMacro
{
    delegate void PingThreadStartEvent(PingTest sender);
    delegate void PingThreadErrorEvent(PingTest sender, String error);
    delegate void PingThreadFinshEvent(PingTest sender,int pingSpeed);

    class PingTest : Threading.ThreadWrapper
    {
        private int m_nPingSpeed;
        private bool m_bJobFinish;
        private bool m_bResultOK;
        private String m_strTargetIP;
        private object m_objectData;
        private PingThreadStartEvent m_pingThreadStartEvent;
        private PingThreadErrorEvent m_pingThreadErrorEvent;
        private PingThreadFinshEvent m_pingThreadFinishEvent;

        public PingTest(String targetIP)
        {
            m_nPingSpeed = Int32.MaxValue;
            m_bJobFinish = false;
            m_bResultOK = false;
            m_strTargetIP = targetIP;
        }

        public int PingSpeed
        {
            get
            {
                return m_nPingSpeed;
            }
        }

        public bool JobFinish
        {
            get
            {
                return m_bJobFinish;
            }
        }

        public bool ResultOK
        {
            get
            {
                return m_bResultOK;
            }
        }

        public object UserData
        {
            get
            {
                return m_objectData;
            }
            set
            {
                m_objectData = value;
            }
        }


        public PingThreadStartEvent PingThreadStartEvent
        {
            get
            {
                return m_pingThreadStartEvent;
            }
            set
            {
                m_pingThreadStartEvent = value;
            }
        }

        public PingThreadErrorEvent PingThreadErrorEvent
        {
            get
            {
                return m_pingThreadErrorEvent;
            }
            set
            {
                m_pingThreadErrorEvent = value;
            }
        }

        public PingThreadFinshEvent PingThreadFinshEvent
        {
            get
            {
                return m_pingThreadFinishEvent;
            }
            set
            {
                m_pingThreadFinishEvent = value;
            }
        }





        protected override void Body()
        {
            if(m_pingThreadStartEvent != null)
            {
                m_pingThreadStartEvent(this);
            }

            Ping ping = new Ping();

            PingOptions options = new PingOptions();
            options.DontFragment = true;

            //전송할 데이터를 입력
            string data = "aaaaaaaaaaaaaa";
            byte[] buffer = ASCIIEncoding.ASCII.GetBytes(data);
            int timeout = 1000;

            try
            {
                //IP 주소를 입력
                PingReply reply = ping.Send(IPAddress.Parse(m_strTargetIP), timeout, buffer, options);

                if (reply.Status == IPStatus.Success)
                {
                    m_nPingSpeed = (int)reply.RoundtripTime;
                    m_bResultOK = true;
                    if (m_pingThreadFinishEvent != null)
                    {
                        m_pingThreadFinishEvent(this,m_nPingSpeed);
                    }
                }
                else
                {
                    m_bResultOK = false;
                    if (m_pingThreadErrorEvent != null)
                    {
                        m_pingThreadErrorEvent(this, "Ping 실패");
                    }
                }
            }
            catch(Exception)
            {
                m_bResultOK = false;
                if (m_pingThreadErrorEvent != null)
                {
                    m_pingThreadErrorEvent(this, "Ping 실패");
                }
            }

            m_bJobFinish = true;
        }
    }
}
