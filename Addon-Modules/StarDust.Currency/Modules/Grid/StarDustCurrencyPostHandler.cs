using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using Aurora.Simulation.Base;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Grid
{
    class StarDustCurrencyPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IStarDustCurrencyService m_starDustCurrencyService;
        private readonly IRegistryCore m_registry;
        private readonly string m_sessionID;

        public StarDustCurrencyPostHandler(string url, IStarDustCurrencyService service, IRegistryCore registry, string sessionID) :
            base("POST", url)
        {
            m_starDustCurrencyService = service;
            m_registry = registry;
            m_sessionID = sessionID;
        }

        #region BaseStreamHandler

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {

            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            m_log.DebugFormat("[StarDustCurrencyPostHandler]: query String: {0}", body);

            try
            {
                OSDMap map = WebUtils.GetOSDMap(body);
                IGridRegistrationService urlModule =
                            m_registry.RequestModuleInterface<IGridRegistrationService>();
                if ((map == null) || (!map.ContainsKey("Method")) ||
                    ((urlModule != null) && (!urlModule.CheckThreatLevel(m_sessionID, map["Method"].AsString(), ThreatLevel.High))))
                {
                    m_log.Error("[StarDustCurrencyPostHandler] Failed CheckThreatLevel for " + ((!map.ContainsKey("Method")) ? "NO METHED SENT" : map["Method"].AsString()));
                    return FailureResult();
                }

                switch (map["Method"].AsString())
                {
                    case "stardust_currencyinfo":
                        return UserCurrencyInfo(map);

                    case "stardust_currencyupdate":
                        return UserCurrencyUpdate(map);

                    case "stardust_currencytransfer":
                        return UserCurrencyTransfer(map);

                    case "getconfig":
                        return GetConfig(map);

                    case "sendgridmessage":
                        return SendGridMessage(map);
                }
                m_log.DebugFormat("[CURRENCY HANDLER]: unknown method {0} request {1}", map["Method"].AsString().Length, map["Method"].AsString());
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[CURRENCY HANDLER]: Exception {0}", e);
            }
            return FailureResult();
        }

        private byte[] SendGridMessage(OSDMap map)
        {
            return m_starDustCurrencyService.SendGridMessage(map["toId"].AsUUID(), map["message"].ToString(), map["goDeep"].AsBoolean(), map["transactionId"].AsUUID())
                       ? SuccessfulResult()
                       : FailureResult();
        }

        private byte[] GetConfig(OSDMap map)
        {
            m_log.Info("[StarDustCurrencyPostHandler] Sending config");
            OSDMap map2 = m_starDustCurrencyService.GetConfig().ToOSD();
            map2.Add("Result", "Successful");
            return Return(map2);
        }

        #endregion
        #region Currency Functions
        private byte[] UserCurrencyTransfer(OSDMap request)
        {
            Transaction trans = new Transaction();
            if (trans.FromOSD(request))
            {
                OSDMap trans2 = m_starDustCurrencyService.UserCurrencyTransfer(trans).ToOSD();
                trans2.Add("Result", "Successful");
                return Return(trans2);
            }
            return FailureResult();
        }

        private byte[] UserCurrencyUpdate(OSDMap request)
        {
            StarDustUserCurrency agent = new StarDustUserCurrency();
            if (agent.FromOSD(request) &&
                m_starDustCurrencyService.UserCurrencyUpdate(agent))
                return SuccessfulResult();
            return FailureResult();
        }

        private byte[] UserCurrencyInfo(OSDMap request)
        {
            UUID agentId;
            if (UUID.TryParse(request["AgentId"].AsString(), out agentId))
            {
                OSDMap results = m_starDustCurrencyService.UserCurrencyInfo(agentId).ToOSD();
                results.Add("Result", "Successful");
                return Return(results);
            }
            return FailureResult();
        }
        #endregion
        #region Misc

        private byte[] FailureResult()
        {
            return Return(new OSDMap
                              {
                        {"Result", "Failure"}
                    });
        }

        private byte[] SuccessfulResult()
        {
            return Return(new OSDMap
                              {
                        {"Result", "Successful"}
                    });
        }

        private byte[] Return(OSDMap result)
        {
            //m_log.DebugFormat("[AuroraDataServerPostHandler]: resp string: {0}", xmlString);
            return Util.UTF8.GetBytes(OSDParser.SerializeJsonString(result));
        }

        #endregion
    }



    class StarDustCurrencyPostHandlerWebUI : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IStarDustCurrencyService m_starDustCurrencyService;
        private readonly IRegistryCore m_registry;
        private readonly StarDustConfig m_options;
        private readonly string m_password = "";

        public StarDustCurrencyPostHandlerWebUI(string url, IStarDustCurrencyService service, IRegistryCore registry, string password, StarDustConfig options)
            : base("POST", url)
        {
            m_options = options;
            m_starDustCurrencyService = service;
            m_registry = registry;
            m_password = Util.Md5Hash(password);
        }

        #region Overrides of BaseStreamHandler

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {

            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);
            try
            {
                OSDMap map = (OSDMap)OSDParser.DeserializeJson(body);
                //Make sure that the person who is calling can access the web service
                if (VerifyPassword(map))
                {
                    string method = map["Method"].AsString();
                    if (method == "Validate")
                    {
                        return Validate(map);
                    }
                    if (method == "CheckPurchaseStatus")
                    {
                        return PrePurchaseCheck(map);
                    }
                    if (method == "OrderSubscription")
                    {
                        return OrderSubscription(map);
                    }
                }
            }
            catch (Exception)
            {
            }
            OSDMap resp = new OSDMap { { "response", OSD.FromString("Failed") } };
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private bool VerifyPassword(OSDMap map)
        {
            if (map.ContainsKey("WebPassword"))
            {
                return map["WebPassword"] == m_password;
            }
            return false;
        }

        #endregion

        #region functions

        private byte[] Validate(OSDMap map)
        {
            string tx = map["tx"].AsString();
            string raw;
            OSDMap resp;
            if ((GetPayPalData(tx, out raw, out resp)) && (m_starDustCurrencyService.FinishPurchase(resp, raw)))
            {
                if (resp.ContainsKey("Verified"))
                    resp["Verified"] = OSD.FromBoolean(true);
                else
                    resp.Add("Verified", OSD.FromBoolean(true));
                resp.Add("STARDUSTCOMPLETE", true);
            }
            else
                resp["Verified"] = OSD.FromBoolean(false);

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] PrePurchaseCheck(OSDMap map)
        {
            UUID purchaseId = map["purchase_id"].AsUUID();
            OSDMap resp = m_starDustCurrencyService.PrePurchaseCheck(purchaseId);
            resp.Add("Verified", OSD.FromBoolean(false));
            resp.Add("FailNumber", "0");
            resp.Add("Reason", "");
            if (resp["Complete"].AsInteger() != 0)
            {
                resp["Reason"] = "This purchase is already complete.";
                resp["FailNumber"] = 1;
            }
            else if (map["principalId"].AsUUID() != resp["PrincipalID"].AsUUID())
            {
                resp["Reason"] = "Not logged in as correct user.";
                resp["FailNumber"] = 2;
            }
            else
                resp["Verified"] = OSD.FromBoolean(true);

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] OrderSubscription(OSDMap map)
        {
            UUID toId = map["toId"].AsUUID();
            string regionName = map["regionName"].ToString();
            string notes = map["notes"].AsString();
            string subscription_id = map["subscription_id"].AsString();
            OSDMap response = m_starDustCurrencyService.OrderSubscription(toId, regionName, notes, subscription_id);

            response.Add("Verified", OSD.FromBoolean(response.ContainsKey("purchaseID")));

            string xmlString = OSDParser.SerializeJsonString(response);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);

        }

        #endregion

        #region PayPal

        private bool GetPayPalData(string tx, out string raw, out OSDMap results)
        {
            string paypalURL = "https://" + m_options.PayPalURL + "/cgi-bin/webscr";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(paypalURL);

            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            string strRequest = "cmd=_notify-synch&tx=" + tx + "&at=" + m_options.PayPalAuthToken;
            req.ContentLength = strRequest.Length;

            //for proxy
            //WebProxy proxy = new WebProxy(new Uri("http://url:port#"));
            //req.Proxy = proxy;

            //Send the request to PayPal and get the response
            StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), Encoding.ASCII);
            streamOut.Write(strRequest);
            streamOut.Close();
            StreamReader streamIn = null;
            string strResponse = "";
            try
            {
                // ReSharper disable AssignNullToNotNullAttribute
                streamIn = new StreamReader(req.GetResponse().GetResponseStream());
                // ReSharper restore AssignNullToNotNullAttribute
                strResponse = streamIn.ReadToEnd();
            }
            catch (Exception e)
            {
                m_log.Error("[StarDustCurrencyPostHandlerWebUI] Error connecting to paypal", e);
            }
            finally
            {
                if (streamIn != null) streamIn.Close();
            }



            raw = strResponse;
            OSDMap returnResults = new OSDMap();
            results = returnResults;
            if (strResponse.Length <= 0)
                return false;

            string[] temp_results = raw.Split('\n');
            foreach (string[] thisLine in
                temp_results.Select(tempResult => tempResult.Split('=')).Where(thisLine => thisLine.Length == 2))
                returnResults.Add(Uri.UnescapeDataString(thisLine[0]), Uri.UnescapeDataString(thisLine[1]));

            results = returnResults;
            return strResponse.Substring(0, "SUCCESS".Length) == "SUCCESS";
        }

        #endregion
    }
}
