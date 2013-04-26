using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using StarDust.Currency.Interfaces;
using Aurora.Framework.Servers.HttpServer.Implementation;
using Aurora.Framework.Modules;
using Aurora.Framework.Utilities;
using Aurora.Framework.Servers.HttpServer.Interfaces;
using Aurora.Framework.ConsoleFramework;

namespace StarDust.Currency.Grid
{
    class StarDustCurrencyPostHandlerWebUI : BaseRequestHandler, IStreamedRequestHandler
    {
        private readonly DustCurrencyService m_starDustCurrencyService;
        private readonly IRegistryCore m_registry;
        private readonly StarDustConfig m_options;
        private readonly string m_password = "";

        public StarDustCurrencyPostHandlerWebUI(string url, DustCurrencyService service, IRegistryCore registry, string password, StarDustConfig options)
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

            //MainConsole.Instance.DebugFormat("[XXX]: query String: {0}", body);
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
                    if (method == "IPNData")
                    {
                        return Validate2(map);
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
                else
                {
                    MainConsole.Instance.Error("[Stardust] Web password did not match.");
                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[Stardust] Error processing method: {0}", e.ToString());
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
            bool confirmPaypal = GetPayPalData(tx, out raw, out resp);
            if (confirmPaypal)
            {
                bool checkIfAlreadyComplete = CheckiFAlreadyComplete(resp);
                if (checkIfAlreadyComplete)
                {
                    resp["Verified"] = OSD.FromBoolean(true);
                    resp["CompleteType"] = OSD.FromString("ALREADYDONE");

                }
                else if (FinishPurchase(resp, raw))
                {
                    if (resp.ContainsKey("Verified"))
                        resp["Verified"] = OSD.FromBoolean(true);
                    else
                        resp.Add("Verified", OSD.FromBoolean(true));
                    resp["CompleteType"] = OSD.FromString("JUSTFINISHED");
                    resp.Add("STARDUSTCOMPLETE", true);
                }
                else
                {
                    resp["Verified"] = OSD.FromBoolean(false);
                    resp["CompleteType"] = OSD.FromString("UNKNOWISSUE");
                }
            }
            else
            {
                resp["Verified"] = OSD.FromBoolean(false);
                resp["CompleteType"] = OSD.FromString("UNKNOWISSUE");
            }

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] Validate2(OSDMap map)
        {
            // it really doesn't matter what we return here since this is only called from paypal
            string raw = map["req"].AsString();
            OSDMap resp = new OSDMap();
            bool IPNCheck = IPNData(raw);
            if ((IPNCheck) && (FinishPurchase(map, raw)))
            {
                resp["Verified"] = OSD.FromBoolean(true);
                resp.Add("STARDUSTCOMPLETE", true);
            }
            else if (IPNCheck)
            {
                resp["Verified"] = OSD.FromBoolean(true);
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
            OSDMap resp = PrePurchaseCheck(purchaseId);
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
            OSDMap response = OrderSubscription(toId, regionName, notes, subscription_id);

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
                MainConsole.Instance.ErrorFormat("[StarDustCurrencyPostHandlerWebUI] Error connecting to paypal: {0}", e.ToString());
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
            foreach (string tempResult in temp_results)
            {
                string[] thisLine = tempResult.Split('=');
                if (thisLine.Length == 2) returnResults.Add(Uri.UnescapeDataString(thisLine[0]), Uri.UnescapeDataString(thisLine[1]));
            }

            results = returnResults;
            return strResponse.Substring(0, "SUCCESS".Length) == "SUCCESS";
        }

        private bool IPNData(string postedData)
        {
            //Post back to either sandbox or live
            string strSandbox = "https://" + m_options.PayPalURL + "/cgi-bin/webscr";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(strSandbox);

            //Set values for the request back
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            string strRequest = postedData;
            strRequest += "&cmd=_notify-validate";
            req.ContentLength = strRequest.Length;

            //for proxy
            //WebProxy proxy = new WebProxy(new Uri("http://url:port#"));
            //req.Proxy = proxy;

            //Send the request to PayPal and get the response
            StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), Encoding.ASCII);
            streamOut.Write(strRequest);
            streamOut.Close();
            StreamReader streamIn = new StreamReader(req.GetResponse().GetResponseStream());
            string strResponse = streamIn.ReadToEnd();
            streamIn.Close();
            if (strResponse == "VERIFIED")
            {
                //check the payment_status is Completed
                //check that txn_id has not been previously processed
                //check that receiver_email is your Primary PayPal email
                //check that payment_amount/payment_currency are correct
                //process payment
                return true;
            }
            else if (strResponse == "INVALID")
            {
                //log for manual investigation
                return false;
            }
            else
            {
                
            }
            return false;
        }


        #endregion

        #region WebUI Functions

        public bool CheckiFAlreadyComplete(OSDMap payPalResponse)
        {
            return m_starDustCurrencyService.Database.CheckIfPurchaseComplete(payPalResponse);
        }

        public bool FinishPurchase(OSDMap payPalResponse, string rawResponse)
        {
            Transaction transaction;
            int purchaseType;
            if (m_starDustCurrencyService.Database.FinishPurchase(payPalResponse, rawResponse, out transaction, out purchaseType))
            {
                if (purchaseType == 1)
                {
                    if (m_starDustCurrencyService.UserCurrencyTransfer(transaction.ToID, m_options.BankerPrincipalID, UUID.Zero, UUID.Zero,
                                                 transaction.Amount, "Currency Purchase",
                                                 TransactionType.SystemGenerated, transaction.TransactionID))
                    {
                        m_starDustCurrencyService.RestrictCurrency(m_starDustCurrencyService.UserCurrencyInfo(transaction.ToID), transaction, transaction.ToID);
                        return true;
                    }
                }
                return true;
            }
            return false;
        }

        public OSDMap PrePurchaseCheck(UUID purchaseId)
        {
            return m_starDustCurrencyService.Database.PrePurchaseCheck(purchaseId);
        }

        public OSDMap OrderSubscription(UUID toId, string regionName, string notes, string subscriptionID)
        {

            string toName = m_starDustCurrencyService.GetUserAccount(toId).Name;
            return m_starDustCurrencyService.Database.OrderSubscription(toId, toName, regionName, notes, subscriptionID);
        }

        #endregion
    }
}
