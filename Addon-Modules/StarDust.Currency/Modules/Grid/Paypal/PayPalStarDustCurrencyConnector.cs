/*
* Copyright (c) 2009 Adam Frisby (adam@deepthink.com.au), Snoopy Pfeffer (snoopy.pfeffer@yahoo.com)
*
* Copyright (c) 2010 BlueWall Information Technologies, LLC
* James Hughes (jamesh@bluewallgroup.com)
 * 
* Copyright (c) 2011 Revolution Smythe
* Revolution Smythe (asdfisbetterthanjkl@gmail.com)
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
* * Redistributions of source code must retain the above copyright
* notice, this list of conditions and the following disclaimer.
* * Redistributions in binary form must reproduce the above copyright
* notice, this list of conditions and the following disclaimer in the
* documentation and/or other materials provided with the distribution.
* * Neither the name of the OpenSimulator Project nor the
* names of its contributors may be used to endorse or promote products
* derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using OpenMetaverse;
using Aurora.Framework;
using Aurora.Simulation.Base;
using Nini.Config;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using log4net;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Grid.Paypal
{
    public class PayPalStarDustCurrencyConnector : IStarDustCurrencyConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger (MethodBase.GetCurrentMethod ().DeclaringType);

        private string m_ppurl = "www.paypal.com";
        // Change to www.sandbox.paypal.com for testing.
        private readonly Dictionary<UUID, string> m_usersemail = new Dictionary<UUID, string> ();
        private const int m_maxBalance = 100000;
        private readonly Dictionary<UUID, PayPalTransaction> m_transactionsInProgress =
            new Dictionary<UUID, PayPalTransaction> ();
        private List<IScene> m_scenes = new List<IScene> ();
        private IMoneyModule m_module;
        private bool m_enabled = false;

        private bool m_allowGridEmails = false;
        private bool m_allowGroups = false;

        public void Initialize (IGenericData GD, IConfigSource source, IRegistryCore simBase, string DefaultConnectionString)
        {
            if (source.Configs["Handlers"].GetString("CurrencyHandler", "") != "StarDust")
                return;

            IConfig economyConfig = source.Configs["StarDustCurrency"];
            m_enabled = ((economyConfig != null) &&
                (economyConfig.GetString("CurrencyModule", "Dust") == "PayPal") &&
                (economyConfig.GetString("CurrencyConnector", "Local") == "Local"));

            if (!m_enabled)
                return;

            if (economyConfig != null)
            {
                m_ppurl = economyConfig.GetString ("PayPalURL", m_ppurl);
                m_allowGridEmails = economyConfig.GetBoolean ("AllowGridEmails", false);
                m_allowGroups = economyConfig.GetBoolean ("AllowGroups", false);
            }

            m_log.Info ("[PayPal] Loading predefined users and groups.");

            // Users
            IConfig users = source.Configs["PayPal Users"];

            if (null == users)
            {
                m_log.Warn ("[PayPal] No users specified in local ini file.");
            }
            else
            {
                IUserAccountService userAccountService = m_scenes[0].UserAccountService;

                // This aborts at the slightest provocation
                // We realise this may be inconvenient for you,
                // however it is important when dealing with
                // financial matters to error check everything.

                foreach (string user in users.GetKeys ())
                {
                    UUID tmp;
                    if (UUID.TryParse (user, out tmp))
                    {
                        m_log.Debug ("[PayPal] User is UUID, skipping lookup...");
                        string email = users.GetString (user);
                        m_usersemail[tmp] = email;
                        continue;
                    }

                    m_log.Debug ("[PayPal] Looking up UUID for user " + user);
                    string[] username = user.Split (new[] { ' ' }, 2);
                    UserAccount ua = userAccountService.GetUserAccount (UUID.Zero, username[0], username[1]);

                    if (ua != null)
                    {
                        m_log.Debug ("[PayPal] Found user, " + user + " = " + ua.PrincipalID);
                        string email = users.GetString (user);

                        if (string.IsNullOrEmpty (email))
                        {
                            m_log.Error ("[PayPal] PayPal email address not set for user " + user +
                                         " in [PayPal Users] config section. Skipping.");
                            m_usersemail[ua.PrincipalID] = "";
                        }
                        else
                        {
                            if (!PayPalHelpers.IsValidEmail (email))
                            {
                                m_log.Error ("[PayPal] PayPal email address not valid for user " + user +
                                             " in [PayPal Users] config section. Skipping.");
                                m_usersemail[ua.PrincipalID] = "";
                            }
                            else
                            {
                                m_usersemail[ua.PrincipalID] = email;
                            }
                        }
                        // UserProfileData was null
                    }
                    else
                    {
                        m_log.Error ("[PayPal] Error, User Profile not found for user " + user +
                                     ". Check the spelling and/or any associated grid services.");
                    }
                }
            }

            // Groups
            IConfig groups = source.Configs["PayPal Groups"];

            if (!m_allowGroups || null == groups)
            {
                m_log.Warn ("[PayPal] Groups disabled or no groups specified in local ini file.");
            }
            else
            {
                // This aborts at the slightest provocation
                // We realise this may be inconvenient for you,
                // however it is important when dealing with
                // financial matters to error check everything.

                foreach (string @group in groups.GetKeys ())
                {
                    m_log.Debug ("[PayPal] Defining email address for UUID for group " + @group);
                    UUID groupID = new UUID (@group);
                    string email = groups.GetString (@group);

                    if (string.IsNullOrEmpty (email))
                    {
                        m_log.Error ("[PayPal] PayPal email address not set for group " +
                                     @group + " in [PayPal Groups] config section. Skipping.");
                        m_usersemail[groupID] = "";
                    }
                    else
                    {
                        if (!PayPalHelpers.IsValidEmail (email))
                        {
                            m_log.Error ("[PayPal] PayPal email address not valid for group " +
                                         @group + " in [PayPal Groups] config section. Skipping.");
                            m_usersemail[groupID] = "";
                        }
                        else
                        {
                            m_usersemail[groupID] = email;
                        }
                    }
                }
            }

            Aurora.DataManager.DataManager.RegisterPlugin (Name, this);
            // Add HTTP Handlers (user, then PP-IPN)
            MainServer.Instance.AddHTTPHandler ("/pp/", UserPage);
            MainServer.Instance.AddHTTPHandler ("/ppipn/", IPN);
        }

        public string Name
        {
            get { return "IStarDustCurrencyConnector"; }
        }

        public void Dispose()
        {
        }

        public StarDustUserCurrency GetUserCurrency(UUID agentId)
        {
            //Give them huge amounts as we don't keep track of how much money they have here
            return new StarDustUserCurrency () { PrincipalID = agentId, Amount = m_maxBalance, Tier = m_maxBalance, LandInUse = 0 };
        }

        public bool UserCurrencyCharge (StarDustUserCurrency agent, int charge, string description)
        {
            return false;
        }

        public bool UserCurrencyBuy(UUID purchaseID, UUID principalID, int amount, float conversionFactor, string regionName, UUID regionID)
        {
            throw new NotImplementedException();
        }

        public bool UserCurrencyBuyComplete(UUID purchaseID, UUID principalID, string completeMethod, string completeReference)
        {
            throw new NotImplementedException();
        }

        private bool GetEmail (UUID scope, UUID key, out string email)
        {
            if (m_usersemail.TryGetValue (key, out email))
                return !string.IsNullOrEmpty (email);

            if (!m_allowGridEmails)
                return false;

            m_log.Info ("[PayPal] Fetching email address from grid for " + key);

            IUserAccountService userAccountService = m_scenes[0].UserAccountService;

            UserAccount ua = userAccountService.GetUserAccount (scope, key);

            if (ua == null)
                return false;

            if (string.IsNullOrEmpty (ua.Email))
                return false;

            // return email address found and cache it
            email = ua.Email;
            m_usersemail[ua.PrincipalID] = email;
            return true;
        }

        public bool UserCurrencyUpdate(StarDustUserCurrency agent)
        {
            return false;
        }

        public bool UserCurrencyBuy(UUID purchaseID, UUID principalID, string userName, uint amount, float conversionFactor, RegionTransactionDetails region)
        {
            throw new NotImplementedException();
        }

        public bool UserCurrencyBuyComplete(UUID purchaseID, int isComplete, string completeMethod, string completeReference, string rawdata, out Transaction transaction)
        {
            throw new NotImplementedException();
        }

        public Transaction UserCurrencyTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        public bool FinishPurchase(OSDMap payPalResponse, string raw, out Transaction transaction, out int purchaseType)
        {
            throw new NotImplementedException();
        }

        public OSDMap PrePurchaseCheck(UUID purchaseID)
        {
            throw new NotImplementedException();
        }

        public OSDMap OrderSubscription(UUID toId, string toName, string regionName, string notes, string subscriptionID)
        {
            throw new NotImplementedException();
        }

        public bool UserCurrencyTransfer(UUID toID, string toName, UUID toObjectID, string toObjectName, UUID fromID, string fromName, UUID fromObjectID, string fromObjectName, uint amount, string description, RegionTransactionDetails region)
        {
            throw new NotImplementedException();
        }

        public bool UserCurrencyBuy (UUID agentId, int amount)
        {
            //No buying, its all from paypal
            return false;
        }

        #region IStarDustCurrencyConnector Members

        public void AddScene (IMoneyModule module, IScene scene)
        {
            if (m_module == null)
                m_module = module;
            m_scenes.Add (scene);
        }

        public bool UserCurrencyTransfer (UUID sender, UUID reciever, int amount, string description)
        {
            string email;
            if (!GetEmail (UUID.Zero, reciever, out email))
            {
                m_log.Warn ("[PayPal] Unknown email address of user " + reciever);
                return false;
            }

            m_log.Info ("[PayPal] Start: " + sender + " wants to pay user " + reciever + " with email " +
                        email + " US$ cents " + amount);

            PayPalTransaction txn = new PayPalTransaction (sender, reciever, email, amount, m_scenes[0], description, 
                PayPalTransaction.InternalTransactionType.Payment);
            // Add transaction to queue
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Add (txn.TxID, txn);

            string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;

            IScenePresence SP = this.LocateClient (sender);
            SP.ControllingClient.SendLoadURL ("PayPal", txn.ObjectID, txn.To, false, "Confirm payment?", "http://" +
                              baseUrl + "/pp/?txn=" + txn.TxID);
            return true;
        }

        void TransferSuccess (PayPalTransaction transaction)
        {
            if (transaction.InternalType == PayPalTransaction.InternalTransactionType.Payment)
            {
                if (transaction.ObjectID == UUID.Zero)
                {
                    // User 2 User Transaction
                    m_log.Info ("[PayPal] Success: " + transaction.From + " did pay user " +
                                transaction.To + " US$ cents " + transaction.Amount);

                    IUserAccountService userAccountService = m_scenes[0].UserAccountService;
                    UserAccount ua;

                    // Notify receiver
                    ua = userAccountService.GetUserAccount (transaction.From, "", "");
                    SendInstantMessage (transaction.To, ua.FirstName + " " + ua.LastName +
                                        " did pay you US$ cent " + transaction.Amount);

                    // Notify sender
                    ua = userAccountService.GetUserAccount (transaction.To, "", "");
                    SendInstantMessage (transaction.From, "You did pay " + ua.FirstName + " " +
                                        ua.LastName + " US$ cent " + transaction.Amount);
                }
                else
                {
                    m_log.Info ("[PayPal] Success: " + transaction.From + " did pay object " +
                                transaction.ObjectID + " owned by " + transaction.To +
                                " US$ cents " + transaction.Amount);
                    m_module.Transfer (transaction.ObjectID, transaction.From, transaction.Amount, "");
                }
            }
            else if (transaction.InternalType == PayPalTransaction.InternalTransactionType.Purchase)
            {
                if (transaction.ObjectID == UUID.Zero)
                {
                    m_log.Error ("[PayPal] Unable to find Object bought! UUID Zero.");
                }
                else
                {
                    IScene s = LocateSceneClientIn (transaction.From);
                    ISceneChildEntity part = s.GetSceneObjectPart (transaction.ObjectID);
                    if (part == null)
                    {
                        m_log.Error ("[PayPal] Unable to find Object bought! UUID = " + transaction.ObjectID);
                        return;
                    }

                    m_log.Info ("[PayPal] Success: " + transaction.From + " did buy object " +
                                transaction.ObjectID + " from " + transaction.To + " paying US$ cents " +
                                transaction.Amount);

                    IBuySellModule module = s.RequestModuleInterface<IBuySellModule> ();
                    if (module == null)
                    {
                        m_log.Error ("[PayPal] Missing BuySellModule! Transaction failed.");
                    }
                    else
                        module.BuyObject (s.GetScenePresence (transaction.From).ControllingClient,
                                          transaction.InternalPurchaseFolderID, part.LocalId,
                                          transaction.InternalPurchaseType, transaction.Amount);
                }
            }
            else if (transaction.InternalType == PayPalTransaction.InternalTransactionType.Land)
            {
                // User 2 Land Transaction
                EventManager.LandBuyArgs e = transaction.E;

                lock (e)
                {
                    e.economyValidated = true;
                }

                IScene s = LocateSceneClientIn (transaction.From);
                ILandObject land = s.RequestModuleInterface<IParcelManagementModule> ().GetLandObject (e.parcelLocalID);

                if (land == null)
                {
                    m_log.Error ("[PayPal] Unable to find Land bought! UUID = " + e.parcelLocalID);
                    return;
                }

                m_log.Info ("[PayPal] Success: " + e.agentId + " did buy land from " + e.parcelOwnerID +
                            " paying US$ cents " + e.parcelPrice);

                land.UpdateLandSold (e.agentId, e.groupId, e.groupOwned, (uint)e.transactionID,
                                     e.parcelPrice, e.parcelArea);
            }
            else
            {
                m_log.Error ("[PayPal] Unknown Internal Transaction TypeOfTrans.");
                return;
            }
            // Cleanup.
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Remove (transaction.TxID);
        }

        private IScene LocateSceneClientIn (UUID agentID)
        {
            IScenePresence avatar = null;

            foreach (IScene scene in m_scenes)
            {
                if (scene.TryGetScenePresence (agentID, out avatar))
                {
                    if (!avatar.IsChildAgent)
                    {
                        return avatar.Scene;
                    }
                }
            }

            return null;
        }

        private IScenePresence LocateClient (UUID agentID)
        {
            IScenePresence avatar = null;

            foreach (IScene scene in m_scenes)
            {
                if (scene.TryGetScenePresence (agentID, out avatar))
                {
                    if (!avatar.IsChildAgent)
                    {
                        return avatar;
                    }
                }
            }

            return null;
        }

        private void SendInstantMessage (UUID dest, string message)
        {
            IClientAPI user = null;

            // Find the user's controlling client.
            lock (m_scenes)
            {
                foreach (IScene sc in m_scenes)
                {
                    IScenePresence av = sc.GetScenePresence (dest);

                    if ((av != null) && (av.IsChildAgent == false))
                    {
                        // Found the client,
                        // and their root scene.
                        user = av.ControllingClient;
                    }
                }
            }

            if (user == null)
                return;

            UUID transaction = UUID.Random ();

            GridInstantMessage msg = new GridInstantMessage ();
            msg.fromAgentID = new Guid (UUID.Zero.ToString ());
            // From server
            msg.toAgentID = new Guid (dest.ToString ());
            msg.imSessionID = new Guid (transaction.ToString ());
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch ();
            msg.fromAgentName = "PayPal";
            msg.dialog = (byte)19;
            // Object msg
            msg.fromGroup = false;
            msg.offline = (byte)1;
            msg.ParentEstateID = (uint)0;
            msg.Position = Vector3.Zero;
            msg.RegionID = new Guid (UUID.Zero.ToString ());
            msg.binaryBucket = new byte[0];
            msg.message = message;

            user.SendInstantMessage (msg);
        }

        public Hashtable UserPage (Hashtable request)
        {
            UUID txnID = new UUID ((string)request["txn"]);

            if (!m_transactionsInProgress.ContainsKey (txnID))
            {
                Hashtable ereply = new Hashtable ();

                ereply["int_response_code"] = 404;
                // 200 OK
                ereply["str_response_string"] = "<h1>Invalid Transaction</h1>";
                ereply["content_type"] = "text/html";

                return ereply;
            }

            PayPalTransaction txn = m_transactionsInProgress[txnID];

            string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;

            // Ouch. (This is the PayPal Request URL)
            // TODO: Add in a return page
            // TODO: Add in a cancel page
            string url = "https://" + m_ppurl + "/cgi-bin/webscr?cmd=_xclick" + "&business=" +
                HttpUtility.UrlEncode (txn.SellersEmail) + "&item_name=" + HttpUtility.UrlEncode (txn.Description) +
                    "&item_number=" + HttpUtility.UrlEncode (txn.TxID.ToString ()) + "&amount=" +
                    HttpUtility.UrlEncode (String.Format ("{0:0.00}", ConvertAmountToCurrency (txn.Amount))) +
                    "&page_style=" + HttpUtility.UrlEncode ("Paypal") + "&no_shipping=" +
                    HttpUtility.UrlEncode ("1") + "&return=" + HttpUtility.UrlEncode ("http://" + baseUrl + "/") +
                    "&cancel_return=" + HttpUtility.UrlEncode ("http://" + baseUrl + "/") + "&notify_url=" +
                    HttpUtility.UrlEncode ("http://" + baseUrl + "/ppipn/") + "&no_note=" +
                    HttpUtility.UrlEncode ("1") + "&currency_code=" + HttpUtility.UrlEncode ("USD") + "&lc=" +
                    HttpUtility.UrlEncode ("US") + "&bn=" + HttpUtility.UrlEncode ("PP-BuyNowBF") + "&charset=" +
                    HttpUtility.UrlEncode ("UTF-8") + "";

            Dictionary<string, string> replacements = new Dictionary<string, string> ();
            replacements.Add ("{ITEM}", txn.Description);
            replacements.Add ("{AMOUNT}", String.Format ("{0:0.00}", ConvertAmountToCurrency (txn.Amount)));
            replacements.Add ("{AMOUNTOS}", txn.Amount.ToString ());
            replacements.Add ("{CURRENCYCODE}", "USD");
            replacements.Add ("{BILLINGLINK}", url);
            replacements.Add ("{OBJECTID}", txn.ObjectID.ToString ());
            replacements.Add ("{SELLEREMAIL}", txn.SellersEmail);

            string template;

            try
            {
                template = File.ReadAllText ("paypal-template.htm");
            }
            catch (IOException)
            {
                template = "Error: paypal-template.htm does not exist.";
                m_log.Error ("[PayPal] Unable to load template file.");
            }

            foreach (KeyValuePair<string, string> pair in replacements)
            {
                template = template.Replace (pair.Key, pair.Value);
            }

            Hashtable reply = new Hashtable ();

            reply["int_response_code"] = 200;
            // 200 OK
            reply["str_response_string"] = template;
            reply["content_type"] = "text/html";

            return reply;
        }

        static decimal ConvertAmountToCurrency (int amount)
        {
            return amount / (decimal)100;
        }

        public Hashtable IPN (Hashtable request)
        {
            Hashtable reply = new Hashtable ();

            // Does not matter what we send back to PP here.
            reply["int_response_code"] = 200;
            // 200 OK
            reply["str_response_string"] = "IPN Processed - Have a nice day.";
            reply["content_type"] = "text/html";

            Dictionary<string, object> postvals = WebUtils.ParseQueryString ((string)request["body"]);
            string originalPost = (string)request["body"];

            string modifiedPost = originalPost + "&cmd=_notify-validate";

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create ("https://" + m_ppurl +
                                                                               "/cgi-bin/webscr");
            httpWebRequest.Method = "POST";

            httpWebRequest.ContentLength = modifiedPost.Length;
            StreamWriter streamWriter = new StreamWriter (httpWebRequest.GetRequestStream ());
            streamWriter.Write (modifiedPost);
            streamWriter.Close ();

            string response;

            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse ();
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ()))
            {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }

            if (httpWebResponse.StatusCode != HttpStatusCode.OK)
            {
                m_log.Error ("[PayPal] IPN Status code != 200. Aborting.");
                debugStringDict (postvals);
                return reply;
            }

            if (!response.Contains ("VERIFIED"))
            {
                m_log.Error ("[PayPal] IPN was NOT verified. Aborting.");
                debugStringDict (postvals);
                return reply;
            }

            // Handle IPN Components
            try
            {
                if ((string)postvals["payment_status"] != "Completed")
                {
                    m_log.Warn ("[PayPal] Transaction not confirmed. Aborting.");
                    debugStringDict (postvals);
                    return reply;
                }

                if (((string)postvals["mc_currency"]).ToUpper () != "USD")
                {
                    m_log.Error ("[PayPal] Payment was made in an incorrect currency (" +
                                 postvals["mc_currency"] + "). Aborting.");
                    debugStringDict (postvals);
                    return reply;
                }

                // Check we have a transaction with the listed ID.
                UUID txnID = new UUID ((string)postvals["item_number"]);
                PayPalTransaction txn;

                lock (m_transactionsInProgress)
                {
                    if (!m_transactionsInProgress.ContainsKey (txnID))
                    {
                        m_log.Error ("[PayPal] Recieved IPN request for Payment that is not in progress. Aborting.");
                        debugStringDict (postvals);
                        return reply;
                    }

                    txn = m_transactionsInProgress[txnID];
                }

                // Check user paid correctly...
                Decimal amountPaid = Decimal.Parse ((string)postvals["mc_gross"]);
                if (System.Math.Abs (ConvertAmountToCurrency (txn.Amount) - amountPaid) > (Decimal)0.001)
                {
                    m_log.Error ("[PayPal] Expected payment was " + ConvertAmountToCurrency (txn.Amount) +
                                 " but recieved " + amountPaid + " " + postvals["mc_currency"] + " instead. Aborting.");
                    debugStringDict (postvals);
                    return reply;
                }

                // At this point, the user has paid, paid a correct amount, in the correct currency.
                // Time to deliver their items. Do it in a seperate thread, so we can return "OK" to PP.
                Util.FireAndForget (delegate { TransferSuccess (txn); });
            }
            catch (KeyNotFoundException)
            {
                m_log.Error ("[PayPal] Received badly formatted IPN notice. Aborting.");
                debugStringDict (postvals);
                return reply;
            }
            // Wheeeee

            return reply;
        }

        static internal void debugStringDict (Dictionary<string, object> strs)
        {
            foreach (KeyValuePair<string, object> str in strs)
            {
                m_log.Debug ("[PayPal] '" + str.Key + "' = '" + (string)str.Value + "'");
            }
        }

        #endregion
    }
}
