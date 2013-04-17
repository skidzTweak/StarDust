using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Timers;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using StarDust.Currency.Interfaces;
using Aurora.Framework.Servers.HttpServer.Interfaces;
using Aurora.Framework.Modules;
using Aurora.Framework.Utilities;
using Aurora.Framework.Servers.HttpServer.Implementation;

namespace StarDust.Currency.Grid
{
    class StarDustCurrencyPostHandlerATM : BaseRequestHandler, IStreamedRequestHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly DustCurrencyService m_starDustCurrencyService;
        private readonly IRegistryCore m_registry;
        private readonly StarDustConfig m_options;
        private readonly string m_password = "";
        private const int numberofTries = 3;
        private readonly List<UUID> BanList = new List<UUID>();
        private readonly List<string> BanIPs = new List<string>();
        private readonly List<string> AllowedIPs = new List<string>();
        private readonly List<GridATM> ATMs = new List<GridATM>();
        private readonly Timer taskTimer = new Timer();


        public StarDustCurrencyPostHandlerATM(string url, DustCurrencyService service, IRegistryCore registry, StarDustConfig options)
            : base("POST", url)
        {
            m_options = options;
            m_starDustCurrencyService = service;
            m_registry = registry;
            m_password = Util.Md5Hash(m_options.ATMPassword);
            BanIPs = m_options.ATMIPBan.Split(';').ToList();
            AllowedIPs = m_options.ATMIPAllow.Split(';').ToList();
            taskTimer.Interval = (120*60)*60; // every 120 min
            taskTimer.Elapsed +=taskTimer_Elapsed;
        }

        private void taskTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (GridATM gridAtm in ATMs)
            {
                if (gridAtm.Registerd)
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(gridAtm.URL + "?data=" + gridAtm.ATMPassword + "&function=info");
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    Stream resStream = response.GetResponseStream();
                    if (resStream != null)
                    {
                        StreamReader reader = new StreamReader(resStream);
                        string responseFromServer = reader.ReadToEnd();
                        string[] info = Uri.UnescapeDataString(responseFromServer.Trim()).Split('|');
                        if (Util.Md5Hash(info[0]) != m_password)
                        {
                            m_log.ErrorFormat("[StarDustATM] ATM response was not correct in task checker - {0}", info);
                            continue;
                        }
                        
                        int per_dollar = m_starDustCurrencyService.m_database.GetGridConversionFactor(gridAtm.GridName);
                        if (per_dollar == 0)
                        {
                            m_log.ErrorFormat("[StarDustATM] Can not find grid named {0} in stardust_atm_grids.", gridAtm.GridName);
                            continue;
                        }
                        
                        for (int loop = 1; loop < info.Length; loop++)
                        {
                            string[] info2 = info[loop].Trim().Split('~');
                            if (info2[0] == "waspaid")
                            {
                                string from_agent_name = info2[1];
                                string from_agent_key = info2[2];
                                int amount_paid = int.Parse(info2[3]);
                                string to_name = info2[4];
                                string to_key = info2[5];

                                double myconversionfactor = m_options.RealCurrencyConversionFactor / per_dollar;
                                int get_amount = (int)Math.Floor(amount_paid*myconversionfactor);

                                UUID result = m_starDustCurrencyService.StartPurchaseOrATMTransfer(new UUID(to_key), (uint)get_amount, PurchaseType.ATMTransferFromAnotherGrid, "");


                            }

                        }
                       

                    }
                }
            }
        }

        #region Overrides of BaseStreamHandler

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string ipa = httpRequest.RemoteIPEndPoint.Address.ToString();
            if (BanIPs.Contains(ipa))
            {
                m_log.ErrorFormat("[StarDustATM] A Banned IPAddress attempted access ATM {0}", ipa);
                return new UTF8Encoding().GetBytes("");
            }
            if ((AllowedIPs.Count > 0) && (!AllowedIPs.Contains(ipa)) && (AllowedIPs[0] != ""))
            {
                m_log.ErrorFormat("[StarDustATM] This IP is not allowed {0}", ipa);
                return new UTF8Encoding().GetBytes("");
            } 

            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);
            try
            {
                string[] info = Uri.UnescapeDataString(body).Split('|');
                if (info.Length == 4)
                {
                    UUID hisID;
                    if (UUID.TryParse(info[0], out hisID))
                    {
                        if (BanList.Contains(hisID))
                        {
                            m_log.ErrorFormat("[StarDustATM] This object is banned for to many tried {0}", hisID);
                            return new UTF8Encoding().GetBytes("");
                        }
                        string weburl = info[1];
                        string tempPassword = info[2];
                        if (weburl.StartsWith("http"))
                        {
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(weburl + "?temp=" + tempPassword);
                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                            Stream resStream = response.GetResponseStream();
                            if (resStream != null)
                            {
                                StreamReader reader = new StreamReader(resStream);
                                string responseFromServer = reader.ReadToEnd();
                                if (Util.Md5Hash(responseFromServer) == m_password)
                                {
                                    string tempPass = GenTempPass();
                                    bool wasfound = false;
                                    foreach (GridATM gridAtm in ATMs.Where(gridAtm => gridAtm.ID == hisID))
                                    {
                                        wasfound = true;
                                        gridAtm.URL = weburl;
                                        gridAtm.Registerd = true;
                                        gridAtm.Tries = 0;
                                        gridAtm.ATMPassword = tempPass;
                                        gridAtm.GridName = info[3];
                                    }
                                    if (!wasfound)
                                    {
                                        ATMs.Add(new GridATM { ID = hisID, URL = weburl, Registerd = true, Tries = 0, ATMPassword = tempPass });
                                    }
                                    return new UTF8Encoding().GetBytes(tempPass);
                                }
                                else
                                {
                                    m_log.ErrorFormat("[StarDustATM] Failed to validate ATM {0} - {1}", ipa, hisID);
                                    // need to track the number of tries
                                    bool wasfound = false;
                                    foreach (GridATM gridAtm in ATMs.Where(gridAtm => gridAtm.ID == hisID))
                                    {
                                        wasfound = true;
                                        gridAtm.Registerd = false;
                                        gridAtm.Tries += 1;
                                        if (gridAtm.Tries <= numberofTries) continue;
                                        BanIPs.Add(ipa);
                                        BanList.Add(hisID);
                                        m_log.ErrorFormat("[StarDustATM] Has banned the following IP for not responding correctly {0}", ipa);
                                        m_log.ErrorFormat("[StarDustATM] Has banned the following object ID for not responding correctly {0}", hisID);
                                    }
                                    if (!wasfound)
                                    {
                                        ATMs.Add(new GridATM { ID = hisID, URL = weburl, Registerd = false, Tries = 1 });
                                    }
                                }
                            }


                            
                        }
                        else
                        {
                            m_log.ErrorFormat("[StarDustATM] The correct information was not passed in - {0}", body);
                        }
                    }
                    else
                    {
                        m_log.ErrorFormat("[StarDustATM] The correct information was not passed in - {0}", body);
                    }
                }
                else
                {
                    m_log.ErrorFormat("[StarDustATM] The correct information was not passed in - {0}", body);
                }
                return new UTF8Encoding().GetBytes("");
            }
            catch (Exception e)
            {
                m_log.Error("[StarDustATM] Error processing method", e);
            }
            return new UTF8Encoding().GetBytes("");
        }

        
        #endregion

        private string GenTempPass()
        {
            Random random = new Random((int)DateTime.Now.Ticks);//thanks to McAden
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < 26; i++)
            {
                char ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }
            return builder.ToString();
        }
    }

    public class GridATM
    {
        public UUID ID { get; set; }
        public string URL { get; set; }
        public int Tries { get; set; }
        public bool Registerd { get; set; }
        public string ATMPassword { get; set; }

        public string GridName { get; set; }
    }
}
