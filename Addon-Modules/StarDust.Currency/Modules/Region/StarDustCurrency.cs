///*
// * Copyright (c) Contributors, OpenCurrency Team
// * See CONTRIBUTORS.TXT for a full list of copyright holders.
// *
// * Redistribution and use in source and binary forms, with or without
// * modification, are permitted provided that the following conditions are met:
// *     * Redistributions of source code must retain the above copyright
// *       notice, this list of conditions and the following disclaimer.
// *     * Redistributions in binary form must reproduce the above copyright
// *       notice, this list of conditions and the following disclaimer in the
// *       documentation and/or other materials provided with the distribution.
// *     * Neither the name of the OpenSim Project nor the
// *       names of its contributors may be used to endorse or promote products
// *       derived from this software without specific prior written permission.
// *
// * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
// * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
// * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// */

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Net;
//using System.Reflection;
//using Aurora.DataManager;
//using Aurora.Framework;
//using log4net;
//using Nini.Config;
//using Nwc.XmlRpc;
//using OpenMetaverse;
//using OpenSim.Framework;
//using OpenSim.Framework.Servers.HttpServer;
//using OpenSim.Region.Framework.Interfaces;
//using OpenSim.Region.Framework.Scenes;
//using StarDust.Currency.Interfaces;

//namespace StarDust.Currency.Region
//{
//    /// <summary>
//    /// This is a demo for you to use when making one that works for you.
//    ///  // To use the following you need to add:
//    /// -helperuri ADDRESS TO HERE OR grid MONEY SERVER
//    /// to the command line parameters you use to start up your client
//    /// This commonly looks like -helperuri http://127.0.0.1:9000/
//    ///
//    /// </summary>
//    public class StarDustCurrency : IMoneyModule, ISharedRegionModule
//    {
//        #region Declarations

//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

//        private IStarDustCurrencyConnector m_connector;

//        /// <summary>
//        /// Where Stipends come from and Fees go to.
//        /// </summary>
//        private UUID m_economyBaseAccount = UUID.Zero;

//public enum TransactionType : int
//{
//    SystemGenerated = 0,
//    RegionMoneyRequest = 1,
//    Gift = 2,
//    Purchase = 3,
//    Upload = 4,
//    ObjectPay = 5008
//}

//#pragma warning disable 67

//        public event ObjectPaid OnObjectPaid;
//        public event PostObjectPaid OnPostObjectPaid;

//        public void Transfer(UUID objectID, UUID agentID, int amount)
//        {
//            if (OnObjectPaid != null)
//                OnObjectPaid(objectID, agentID, amount);
//        }

//#pragma warning restore 67

//        private IConfigSource m_gConfig;

//        private int m_minFundsBeforeRefresh = 100;

//        private int m_stipend = 1000;

//        private int m_objectCapacity = 45000;
//        private int m_priceEnergyUnit;
//        private int m_priceGroupCreate;
//        private int m_priceObjectClaim;
//        private float m_priceObjectRent;
//        private float m_priceObjectScaleFactor;
//        private int m_priceParcelClaim;
//        private float m_priceParcelClaimFactor;
//        private int m_priceParcelRent;
//        private int m_pricePublicObjectDecay;
//        private int m_pricePublicObjectDelete;
//        private int m_priceRentLight;
//        private int m_priceUpload;
//        private int m_teleportMinPrice = 0;
//        private int m_realCurrencyConversionFactor = 1;
//        private string m_upgradeMembershipUri = "";

//        private float m_teleportPriceExponent = 0f;
//        private Dictionary<UUID, LandData> LastRequestedParcel = new Dictionary<UUID, LandData>();

//        private List<Scene> m_scenes = new List<Scene>();
//        private bool m_enabled = false;

//        #endregion

//        #region IMoneyModule Members

//        public int UploadCharge
//        {
//            get { return m_priceUpload; }
//        }

//        public int GroupCreationCharge
//        {
//            get { return m_priceGroupCreate; }
//        }

//        /// <summary>
//        /// Gets the money that the client has
//        /// </summary>
//        /// <param name="agentID"></param>
//        public int Balance(UUID agentID)
//        {
//            return (int)m_connector.GetUserCurrency(agentID).Amount;
//        }

//        /// <summary>
//        /// Gets the money that the client has
//        /// </summary>
//        /// <param name="client"></param>
//        public int Balance(IClientAPI client)
//        {
//            return Balance(client.AgentId);
//        }

//        public bool Charge(IClientAPI client, int amount)
//        {
//            return Charge(client.AgentId, amount, "Unknown");
//        }

//        public bool Charge(UUID agentID, int amount, string text)
//        {
//            m_log.Debug("[CURRENCY]: Base account: " + m_economyBaseAccount + " Agent ID: " + agentID + " " + text);
//            bool giveResult = ProcessMoneyTransferRequest(agentID, m_economyBaseAccount, amount, TransactionType.Gift, text);
//            BalanceUpdate(agentID, m_economyBaseAccount, giveResult, text);
//            return giveResult;
//        }

//        public bool ObjectGiveMoney(UUID objectId, UUID fromId, UUID toId, int amount)
//        {
//            string description = "Object gives " + amount.ToString() + " to " + toId.ToString();
//            bool giveResult = ProcessMoneyTransferRequest(fromId, toId, amount, TransactionType.ObjectPay, description);
//            BalanceUpdate(fromId, toId, giveResult, description);
//            return giveResult;
//        }

//        #endregion

//        #region IRegionModuleBase Members

//        public void Initialise(IConfigSource config)
//        {
//            IConfig economyConfig = config.Configs["StarDustCurrency"];
//            m_enabled = ((economyConfig != null) &&
//                (economyConfig.GetString("CurrencyConnector", "Local") == "Remote") &&
//                (config.Configs["Handlers"].GetString("CurrencyHandler", "") == "StarDustOLD"));
//            if (!m_enabled) return;

//            m_gConfig = config;
//            if (economyConfig != null)
//            {
//                m_priceEnergyUnit = economyConfig.GetInt("PriceEnergyUnit", 100);
//                m_priceObjectClaim = economyConfig.GetInt("PriceObjectClaim", 10);
//                m_pricePublicObjectDecay = economyConfig.GetInt("PricePublicObjectDecay", 4);
//                m_pricePublicObjectDelete = economyConfig.GetInt("PricePublicObjectDelete", 4);
//                m_priceParcelClaim = economyConfig.GetInt("PriceParcelClaim", 1);
//                m_priceParcelClaimFactor = economyConfig.GetFloat("PriceParcelClaimFactor", 1f);
//                m_priceUpload = economyConfig.GetInt("PriceUpload", 0);
//                m_priceRentLight = economyConfig.GetInt("PriceRentLight", 5);
//                m_teleportMinPrice = economyConfig.GetInt("TeleportMinPrice", 2);
//                m_teleportPriceExponent = economyConfig.GetFloat("TeleportPriceExponent", 2f);
//                m_priceObjectRent = economyConfig.GetFloat("PriceObjectRent", 1);
//                m_priceObjectScaleFactor = economyConfig.GetFloat("PriceObjectScaleFactor", 10);
//                m_priceParcelRent = economyConfig.GetInt("PriceParcelRent", 1);
//                m_priceGroupCreate = economyConfig.GetInt("PriceGroupCreate", -1);
//                m_economyBaseAccount = new UUID(economyConfig.GetString("EconomyBaseAccount", UUID.Zero.ToString()));

//                // UserLevelPaysFees = economyConfig.GetInt("UserLevelPaysFees", -1);
//                m_realCurrencyConversionFactor = economyConfig.GetInt("RealCurrencyConversionFactor", 1);
//                m_stipend = economyConfig.GetInt("UserStipend", 0);
//                m_minFundsBeforeRefresh = economyConfig.GetInt("IssueStipendWhenClientIsBelowAmount", 0);
//                m_upgradeMembershipUri = economyConfig.GetString("UpgradeMembershipURI", "");
//            }
//        }

//        public void PostInitialise()
//        {
//        }

//        public void AddRegion(Scene scene)
//        {
//            if (!m_enabled)
//                return;

//            m_connector = DataManager.RequestPlugin<IStarDustCurrencyConnector>("IStarDustCurrencyConnector");
//            // Send ObjectCapacity to Scene..  Which sends it to the SimStatsReporter.
//            scene.RegisterModuleInterface<IMoneyModule>(this);
//            IHttpServer httpServer = MainServer.Instance;

//            // XMLRPCHandler = scene;
//            // To use the following you need to add:
//            // -helperuri <ADDRESS TO HERE OR grid MONEY SERVER>
//            // to the command line parameters you use to start up your client
//            // This commonly looks like -helperuri http://127.0.0.1:9000/
//            httpServer.AddXmlRPCHandler("getCurrencyQuote", QuoteFunc);
//            //httpServer.AddXmlRPCHandler("buyCurrency", BuyFunc);
//            httpServer.AddXmlRPCHandler("preflightBuyLandPrep", PreflightBuyLandPrepFunc);
//            httpServer.AddXmlRPCHandler("buyLandPrep", LandBuyFunc);
//            httpServer.AddXmlRPCHandler("Balance", GetbalanceFunc);

//            //Get Other config paramaters
//            m_objectCapacity = scene.RegionInfo.ObjectCapacity;

//            m_scenes.Add(scene);
//            scene.EventManager.OnNewClient += OnNewClient;
//            scene.EventManager.OnClosingClient += OnClosingClient;
//            scene.EventManager.OnValidateBuyLand += ValidateLandBuy;
//        }

//        public void RemoveRegion(Scene scene)
//        {
//        }

//        public void RegionLoaded(Scene scene)
//        {
//        }

//        public void Close()
//        {
//        }

//        public TypeOfTrans ReplaceableInterface
//        {
//            get { return typeof(IMoneyModule); }
//        }

//        public string Name
//        {
//            get { return "StarDustCurrency"; }
//        }

//        #endregion

//        #region Client Events

//        /// <summary>
//        /// New Client Event Handler
//        /// </summary>
//        /// <param name="client"></param>
//        private void OnNewClient(IClientAPI client)
//        {
//            // Subscribe to Money messages
//            client.OnEconomyDataRequest += EconomyDataRequestHandler;
//            client.OnMoneyBalanceRequest += SendMoneyBalance;
//            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
//            /*client.OnGroupAccountDetailsRequest += GroupAccountDetailsRequest;
//            client.OnGroupAccountTransactionsRequest += GroupAccountTransactionsRequest;
//            client.OnGroupAccountSummaryRequest += GroupAccountSummaryRequest;*/
//            client.OnParcelBuyPass += ClientOnParcelBuyPass;
//        }

//        protected void OnClosingClient(IClientAPI client)
//        {
//            // Subscribe to Money messages
//            client.OnEconomyDataRequest -= EconomyDataRequestHandler;
//            client.OnMoneyBalanceRequest -= SendMoneyBalance;
//            client.OnMoneyTransferRequest -= ProcessMoneyTransferRequest;
//            /*client.OnGroupAccountDetailsRequest -= GroupAccountDetailsRequest;
//            client.OnGroupAccountTransactionsRequest -= GroupAccountTransactionsRequest;
//            client.OnGroupAccountSummaryRequest -= GroupAccountSummaryRequest;*/
//            client.OnParcelBuyPass -= ClientOnParcelBuyPass;
//        }

//        /// <summary>
//        /// The client wants to transfer money to another user
//        /// </summary>
//        /// <param name="source"></param>
//        /// <param name="destination"></param>
//        /// <param name="amount"></param>
//        /// <param name="transactiontype"></param>
//        /// <param name="description"></param>
//        private void ProcessMoneyTransferRequest(UUID source, UUID destination, int amount,
//                                                        int transactiontype, string description)
//        {
//            ProcessMoneyTransferRequest(source, destination, amount, (TransactionType)transactiontype, description);
//        }

//        /// <summary>
//        /// The client wants to buy a pass for a parcel
//        /// </summary>
//        /// <param name="client"></param>
//        /// <param name="agentId"></param>
//        /// <param name="parcelLocalId"></param>
//        private void ClientOnParcelBuyPass(IClientAPI client, UUID agentId, int parcelLocalId)
//        {
//            m_log.InfoFormat("[StarDustCurrency]: ClientOnParcelBuyPass {0}, {1}, {2}", client.Name, agentId, parcelLocalId);
//            IScenePresence agentSp = FindScene(client.AgentId).GetScenePresence(client.AgentId);
//            IParcelManagementModule parcelManagement = agentSp.Scene.RequestModuleInterface<IParcelManagementModule>();
//            ILandObject landParcel = null;
//            List<ILandObject> land = parcelManagement.AllParcels();
//            foreach (ILandObject landObject in land)
//            {
//                if (landObject.LandData.LocalID == parcelLocalId)
//                {
//                    landParcel = landObject;
//                }
//            }
//            if (landParcel != null)
//            {
//                m_log.Debug("[CURRENCY]: Base account: " + landParcel.LandData.OwnerID + " Agent ID: " + agentId + " Price:" +
//                            landParcel.LandData.PassPrice);
//                bool giveResult = ProcessMoneyTransferRequest(agentId, landParcel.LandData.OwnerID, landParcel.LandData.PassPrice, TransactionType.Purchase,
//                                                   "Parcel Pass");
//                if (giveResult)
//                {
//                    BalanceUpdate(agentId, landParcel.LandData.OwnerID, true, "Parcel Pass");
//                    ParcelManager.ParcelAccessEntry entry
//                        = new ParcelManager.ParcelAccessEntry
//                        {
//                            AgentID = agentId,
//                            Flags = AccessList.Access,
//                            Time = DateTime.Now.AddHours(landParcel.LandData.PassHours)
//                        };
//                    landParcel.LandData.ParcelAccessList.Add(entry);
//                    agentSp.ControllingClient.SendAgentAlertMessage("You have been added to the parcel access list.",
//                                                                    false);
//                }
//            }
//            else
//            {
//                m_log.ErrorFormat("[StarDustCurrency]: No parcel found for parcel id {0}", parcelLocalId);
//            }
//        }

//        /*#region Group Money (UNFINISHED)
//        public void GroupAccountDetailsRequest(IClientAPI client, UUID agentID, UUID groupID, UUID transactionID, UUID sessionID)
//        {
//            ScenePresence agentSP = FindScene(client.AgentId).GetScenePresence(client.AgentId);
//            int amt = (int)GroupCheckExistAndRefreshFunds(groupID);
//            agentSP.ControllingClient.SendGroupAccountingDetails(client, groupID, transactionID, sessionID, amt);
//        }

//        #region Appearantly decapriated?
//        public void GroupAccountSummaryRequest(IClientAPI client, UUID agentID, UUID groupID)
//        {
//            ScenePresence agentSP = FindScene(client.AgentId).GetScenePresence(client.AgentId);
            
//            uint groupAmt = 0;
//            int totalTier = 0;
//            int usedTier = 0;
//            try
//            {
//                groupAmt = GroupCheckExistAndRefreshFunds(groupID);
//                totalTier = GroupCheckTotalTier(groupID);
//                usedTier = GroupCheckUsedTier(groupID);
//            }
//            catch (Exception ex)
//            {
//                ex = new Exception();
//            }
//            agentSP.ControllingClient.SendGroupAccountingSummary(client, groupID, groupAmt, totalTier, usedTier);
//        }
//        #endregion

//        #region Check Group Parts
//        public uint GroupCheckExistAndRefreshFunds(UUID groupID)
//        {
//            string money = GenericData.GetSQL("select moneyAmt from groupaccounting where groupID = '" + groupID.ToString() + "'", m_gConfig);
//            uint numVal;
//            try
//            {
//                numVal = Convert.ToUInt32(money);
//            }
//            catch (Exception ex)
//            {
//                m_log.Error("[CURRENCY] Get Money returned non-int value, " + ex);
//                numVal = 0;
//            }
//            return numVal;
//        }

//        public int GroupCheckTotalTier(UUID groupID)
//        {
//            string money = GenericData.GetSQL("select totalTier from groupaccounting where groupID = '" + groupID.ToString() + "'", m_gConfig);
//            int numVal;
//            try
//            {
//                numVal = Convert.ToInt32(money);
//            }
//            catch (Exception ex)
//            {
//                m_log.Error("[CURRENCY] Get Money returned non-int value, " + ex);
//                numVal = 0;
//            }
//            return numVal;
//        }

//        public int GroupCheckUsedTier(UUID groupID)
//        {
//            string money = GenericData.GetSQL("select usedTier from groupaccounting where groupID = '" + groupID.ToString() + "'", m_gConfig);
//            int numVal;
//            try
//            {
//                numVal = Convert.ToInt32(money);
//            }
//            catch (Exception ex)
//            {
//                m_log.Error("[CURRENCY] Get Money returned non-int value, " + ex);
//                numVal = 0;
//            }
//            return numVal;
//        }
//        #endregion

//        public void GroupAccountTransactionsRequest(IClientAPI client, UUID agentID, UUID groupID, UUID transactionID, UUID sessionID)
//        {
//            ScenePresence agentSP = FindScene(client.AgentId).GetScenePresence(client.AgentId);
            
//            int amt = (int)GroupCheckExistAndRefreshFunds(groupID);
//            agentSP.ControllingClient.SendGroupTransactionsSummaryDetails(client, groupID, transactionID, sessionID, amt);
//        }
//        #endregion*/

//        /// <summary>
//        /// Event called Economy Data Request handler.
//        /// </summary>
//        /// <param name="agentId"></param>
//        private void EconomyDataRequestHandler(IClientAPI remoteClient)
//        {
//            remoteClient.SendEconomyData(0, m_objectCapacity, remoteClient.Scene.RegionInfo.ObjectCapacity, m_priceEnergyUnit, m_priceGroupCreate,
//                                     m_priceObjectClaim, m_priceObjectRent, m_priceObjectScaleFactor, m_priceParcelClaim, m_priceParcelClaimFactor,
//                                     m_priceParcelRent, m_pricePublicObjectDecay, m_pricePublicObjectDelete, m_priceRentLight, m_priceUpload,
//                                     m_teleportMinPrice, m_teleportPriceExponent);
//        }

//        /// <summary>
//        /// This is when the viewer has requested to buy a parcel
//        /// </summary>
//        /// <param name="e"></param>
//        /// <returns></returns>
//        private bool ValidateLandBuy(EventManager.LandBuyArgs e)
//        {
//            StarDustUserCurrency currency = m_connector.GetUserCurrency(e.agentId);
//            IUserProfileInfo profile = DataManager.RequestPlugin<IProfileConnector>("IProfileConnector").GetUserProfile(e.agentId);

//            if (e.agentId != UUID.Zero)
//            {
//                if (profile.MembershipGroup == "")
//                {
//                    return CheckAndApplyBalance(currency, e);
//                }
//                else if (profile.MembershipGroup == "Premium")
//                {
//                    return CheckAndApplyBalance(currency, e);
//                }
//                else if (profile.MembershipGroup == "Banned")
//                {
//                    return false;
//                }
//            }
//            return true;
//        }

//        #endregion

//        #region Local Fund Management

//        /// <summary>
//        /// Transfer money
//        /// </summary>
//        /// <param name="sender"></param>
//        /// <param name="receiver"></param>
//        /// <param name="amount"></param>
//        /// <param name="transactiontype"></param>
//        /// <param name="description"></param>
//        /// <returns></returns>
//        private bool ProcessMoneyTransferRequest(UUID sender, UUID receiver, int amount, TransactionType transactiontype, string description)
//        {
//            bool result = false;
//            if (amount >= 0)
//            {
//                int senderAmt = Balance(sender);
//                int receiverAmt = Balance(receiver);
//                if (senderAmt >= amount) //Check whether they have the money or not
//                {
//                    if (!TransferBalance(sender, receiver, senderAmt, description))
//                        return false;
//                    result = true;

//                    //Send both clients their money
//                    IClientAPI senderClient = LocateClientObject(sender);
//                    if (senderClient != null)
//                    {
//                        senderClient.SendMoneyBalance(UUID.Random(), true, new byte[0], Balance(sender));
//                    }

//                    IClientAPI receiverClient = LocateClientObject(receiver);
//                    if (receiverClient != null)
//                    {
//                        receiverClient.SendMoneyBalance(UUID.Random(), true, Utils.StringToBytes(description), Balance(receiver));
//                    }
//                }
//            }
//            return result;
//        }

//        /// <summary>
//        /// Sends the money balance to the client
//        /// </summary>
//        /// <param name="client"></param>
//        /// <param name="agentId"></param>
//        /// <param name="sessionId"></param>
//        /// <param name="transactionId"></param>
//        public void SendMoneyBalance(IClientAPI client, UUID agentId, UUID sessionId, UUID transactionId)
//        {
//            if (client.AgentId == agentId && client.SessionId == sessionId)
//                client.SendMoneyBalance(transactionId, true, new byte[0], Balance(agentId));
//            else
//                client.SendAlertMessage("Unable to send your money balance to you!");
//        }

//        /// <summary>
//        /// Sends new money information to the two clients given
//        /// </summary>
//        /// <param name="senderId"></param>
//        /// <param name="receiverId"></param>
//        /// <param name="transactionresult"></param>
//        /// <param name="description"></param>
//        private void BalanceUpdate(UUID senderId, UUID receiverId, bool transactionresult, string description)
//        {
//            IClientAPI sender = LocateClientObject(senderId);
//            IClientAPI receiver = LocateClientObject(receiverId);

//            if (senderId != receiverId)
//            {
//                if (sender != null)
//                    sender.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(description), Balance(senderId));
//                if (receiver != null)
//                    receiver.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(description), Balance(receiverId));
//            }
//        }

//        /// <summary>
//        /// Updates the given agents balance if it is valid
//        /// </summary>
//        /// <param name="agentId"></param>
//        /// <param name="amount"></param>
//        /// <returns></returns>
//        private bool UpdateBalance(UUID agentId, int amount, string description)
//        {
//            if (amount >= 0)
//            {
//                StarDustUserCurrency currency = m_connector.GetUserCurrency(agentId);
//                int charge = (int)currency.Amount - amount;
//                return m_connector.UserCurrencyCharge(currency, charge, description);
//            }
//            return false;
//        }

//        private bool TransferBalance(UUID sender, UUID reciever, int amount, string description)
//        {
//            return m_connector.UserCurrencyTransaction(sender, reciever, amount, description);
//        }

//        /// <summary>
//        /// Check whether the client can buy the given piece of land
//        /// </summary>
//        /// <param name="currency"></param>
//        /// <param name="e"></param>
//        /// <returns></returns>
//        private bool CheckAndApplyBalance(StarDustUserCurrency currency, EventManager.LandBuyArgs e)
//        {
//            int totalLandUse = (int)(e.parcelArea + currency.LandInUse);
//            IClientAPI client = LocateClientObject(e.agentId);
//            if (client != null)
//            {
//                if (totalLandUse > currency.Tier)
//                {
//                    client.SendBlueBoxMessage(UUID.Zero, "", "You do not have enough tier to buy this parcel.");
//                    return false;
//                }
//                else if (totalLandUse <= currency.Tier) //They have enough
//                {
//                    currency.LandInUse = (uint)totalLandUse;
//                    m_connector.UserCurrencyUpdate(currency);

//                    if (!TransferBalance(e.agentId, e.parcelOwnerID, e.parcelPrice, "Buy Land")) //if its not successfull, reset the first person
//                    {
//                        client.SendBlueBoxMessage(UUID.Zero, "", "ERROR IN TRANSACTION.");
//                        return false;
//                    }

//                    //Send both the money updates
//                    SendMoneyBalance(client, e.parcelOwnerID, client.SessionId, UUID.Zero);
//                    SendMoneyBalance(client, e.agentId, client.SessionId, UUID.Zero);
//                    client.SendBlueBoxMessage(UUID.Zero, "", string.Format("You paid {0} {1}$ for {2} sq meters of land.", e.parcelOwnerID, e.parcelPrice, e.parcelArea));
//                    return true;
//                }
//            }
//            return false;
//        }

//        #endregion

//        #region Buy Currency and Land

//        /// <summary>
//        /// The viewer wants to know how much money is 
//        /// </summary>
//        /// <param name="request"></param>
//        /// <param name="ep"></param>
//        /// <returns></returns>
//        public XmlRpcResponse QuoteFunc(XmlRpcRequest request, IPEndPoint ep)
//        {
//            Hashtable requestData = (Hashtable)request.Params[0];
//            int amount = 0;
//            Hashtable quoteResponse = new Hashtable();
//            XmlRpcResponse returnval = new XmlRpcResponse();

//            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
//            {
//                amount = (Int32)requestData["currencyBuy"];
//                Hashtable currencyResponse = new Hashtable { { "estimatedCost", amount * m_realCurrencyConversionFactor }, { "currencyBuy", amount } };
//                quoteResponse.Add("success", true);
//                quoteResponse.Add("currency", currencyResponse);
//                quoteResponse.Add("confirm", "asdfad9fj39ma9fj");

//                returnval.Value = quoteResponse;
//                return returnval;
//            }

//            quoteResponse.Add("success", false);
//            quoteResponse.Add("errorMessage", "Invalid parameters passed to the quote box");
//            quoteResponse.Add("errorURI", "http://aurora-sim.org/");
//            returnval.Value = quoteResponse;
//            return returnval;
//        }

//        /// <summary>
//        /// The viewer wants to buy currency
//        /// </summary>
//        /// <param name="request"></param>
//        /// <param name="ep"></param>
//        /// <returns></returns>
//        //public XmlRpcResponse BuyFunc(XmlRpcRequest request, IPEndPoint ep)
//        //{
//        //    Hashtable requestData = (Hashtable)request.Params[0];
//        //    UUID agentId = UUID.Zero;
//        //    bool success = false;
//        //    if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
//        //    {
//        //        UUID.TryParse((string)requestData["agentId"], out agentId);
//        //        int amount = (Int32)requestData["currencyBuy"];
//        //        if (agentId != UUID.Zero)
//        //        {
//        //            success = m_connector.UserCurrencyBuy(agentId, amount);
//        //        }
//        //    }
//        //    XmlRpcResponse returnval = new XmlRpcResponse();
//        //    Hashtable returnresp = new Hashtable { { "success", success } };
//        //    returnval.Value = returnresp;
//        //    return returnval;
//        //}

//        /// <summary>
//        /// The viewer wants to know whether it has enough funds to buy this land
//        /// </summary>
//        /// <param name="request"></param>
//        /// <param name="ep"></param>
//        /// <returns></returns>
//        public XmlRpcResponse PreflightBuyLandPrepFunc(XmlRpcRequest request, IPEndPoint ep)
//        {
//            Hashtable requestData = (Hashtable)request.Params[0];
//            XmlRpcResponse ret = new XmlRpcResponse();
//            Hashtable retparam = new Hashtable();

//            Hashtable membershiplevels = new Hashtable();
//            membershiplevels.Add("levels", membershiplevels);

//            Hashtable landuse = new Hashtable();

//            Hashtable level = new Hashtable
//                                  {
//                                      {"id", "00000000-0000-0000-0000-000000000000"},
//                                      {m_upgradeMembershipUri, "Premium Membership"}
//                                  };

//            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
//            {
//                UUID agentId;
//                UUID.TryParse((string)requestData["agentId"], out agentId);
//                StarDustUserCurrency currency = m_connector.GetUserCurrency(agentId);
//                IUserProfileInfo profile = DataManager.RequestPlugin<IProfileConnector>("IProfileConnector").GetUserProfile(agentId);

//                IScenePresence sp;
//                LocateSceneClientIn(agentId).TryGetScenePresence(agentId, out sp);

//                IParcelManagementModule parcelManagement = sp.Scene.RequestModuleInterface<IParcelManagementModule>();
//                ILandObject parcel = parcelManagement.GetLandObject(sp.AbsolutePosition.X, sp.AbsolutePosition.Y);
//                LastRequestedParcel[agentId] = parcel.LandData;

//                Hashtable currencytable = new Hashtable();
//                currencytable.Add("estimatedCost", parcel.LandData.SalePrice);

//                int landTierNeeded = (int)(currency.LandInUse + parcel.LandData.Area);
//                bool needsUpgrade = false;
//                if (profile.MembershipGroup == "" || profile.MembershipGroup == "Premium")
//                {
//                    if (landTierNeeded >= currency.Tier)
//                        needsUpgrade = true;
//                    else
//                        needsUpgrade = false;
//                }
//                else if (profile.MembershipGroup == "Banned")
//                    needsUpgrade = true;

//                landuse.Add("action", m_upgradeMembershipUri);
//                landuse.Add("action", needsUpgrade);

//                retparam.Add("success", true);
//                retparam.Add("currency", currency);
//                retparam.Add("membership", level);
//                retparam.Add("landuse", landuse);
//                retparam.Add("confirm", "asdfajsdkfjasdkfjalsdfjasdf");
//                ret.Value = retparam;
//            }
//            return ret;
//        }

//        /// <summary>
//        /// The viewer wants to know how much money it has
//        /// </summary>
//        /// <param name="request"></param>
//        /// <param name="ep"></param>
//        /// <returns></returns>
//        public XmlRpcResponse GetbalanceFunc(XmlRpcRequest request, IPEndPoint ep)
//        {
//            XmlRpcResponse ret = new XmlRpcResponse();
//            Hashtable retparam = new Hashtable();
//            Hashtable requestData = (Hashtable)request.Params[0];
//            UUID agentId;

//            if (requestData.ContainsKey("agentId"))
//            {
//                UUID.TryParse((string)requestData["agentId"], out agentId);
//                retparam.Add("success", true);
//                retparam.Add("clientBalance", Balance(agentId));
//                ret.Value = retparam;
//            }

//            return ret;
//        }

//        /// <summary>
//        /// The viewer sends this before it clicks buy on the land buy form
//        /// </summary>
//        /// <param name="request"></param>
//        /// <param name="ep"></param>
//        /// <returns></returns>
//        public XmlRpcResponse LandBuyFunc(XmlRpcRequest request, IPEndPoint ep)
//        {
//            XmlRpcResponse ret = new XmlRpcResponse();
//            Hashtable retparam = new Hashtable();
//            Hashtable requestData = (Hashtable)request.Params[0];

//            UUID agentId = UUID.Zero;
//            LandData parcel = LastRequestedParcel[agentId];
//            bool success = false;
//            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
//            {
//                UUID.TryParse((string)requestData["agentId"], out agentId);

//                StarDustUserCurrency currency = m_connector.GetUserCurrency(agentId);
//                IUserProfileInfo profile = DataManager.RequestPlugin<IProfileConnector>("IProfileConnector").GetUserProfile(agentId);

//                int totalLandUse = (int)(parcel.Area + currency.LandInUse);
//                if (agentId != UUID.Zero)
//                {
//                    if (profile.MembershipGroup == "" || profile.MembershipGroup == "Premium")
//                    {
//                        if (totalLandUse >= currency.Tier)
//                            success = false;
//                        else if (currency.LandInUse + parcel.Area < currency.Tier)
//                            success = true;
//                    }
//                    else if (profile.MembershipGroup == "Banned")
//                        success = false;
//                }
//            }
//            retparam.Add("success", success);
//            ret.Value = retparam;

//            return ret;
//        }

//        #endregion

//        #region Utility Helpers

//        #region Resolving objects, agents, etc

//        private ISceneChildEntity FindPrim(UUID objectID)
//        {
//            lock (m_scenes)
//            {
//                foreach (Scene s in m_scenes)
//                {
//                    ISceneChildEntity part = s.GetSceneObjectPart(objectID);
//                    if (part != null)
//                    {
//                        return part;
//                    }
//                }
//            }
//            return null;
//        }

//        private string ResolveAgentName(UUID agentID)
//        {
//            // try avatar username surname
//            Scene scene = m_scenes[0];
//            IScenePresence profile = scene.GetScenePresence(agentID);
//            if (profile != null)
//            {
//                string avatarname = profile.Firstname + " " + profile.Lastname;
//                return avatarname;
//            }
//            m_log.ErrorFormat(
//                "[CURRENCY]: Could not resolve user {0}, maybe this is a deeded object to a group ",
//                agentID);

//            return String.Empty;
//        }

//        private string ResolveGroupName(UUID groupId)
//        {
//            Scene scene = m_scenes[0];
//            IGroupsModule gm = scene.RequestModuleInterface<IGroupsModule>();
//            string group = gm.GetGroupRecord(groupId).GroupName;
//            if (group != null)
//            {
//                m_log.DebugFormat(
//                        "[CURRENCY]: Resolved group {0} to " + group,
//                        groupId);

//                return group;
//            }
//            m_log.ErrorFormat(
//                "[CURRENCY]: Could not resolve group {0}",
//                groupId);

//            return String.Empty;
//        }
//        #endregion

//        private Scene FindScene(UUID agentId)
//        {
//            foreach (Scene s in m_scenes)
//            {
//                IScenePresence presence = s.GetScenePresence(agentId);
//                if (presence != null && !presence.IsChildAgent)
//                    return s;
//            }
//            return null;
//        }

//        /// <summary>
//        /// Locates a IClientAPI for the client specified
//        /// </summary>
//        /// <param name="agentID"></param>
//        /// <returns></returns>
//        private IClientAPI LocateClientObject(UUID agentID)
//        {
//            IScenePresence tPresence;
//            IClientAPI rclient = null;

//            lock (m_scenes)
//            {
//                foreach (Scene scene in m_scenes)
//                {
//                    tPresence = scene.GetScenePresence(agentID);
//                    if (tPresence != null)
//                    {
//                        if (!tPresence.IsChildAgent)
//                        {
//                            rclient = tPresence.ControllingClient;
//                        }
//                    }
//                    if (rclient != null)
//                    {
//                        return rclient;
//                    }
//                }
//            }
//            return null;
//        }

//        private Scene LocateSceneClientIn(UUID agentId)
//        {
//            lock (m_scenes)
//            {
//                foreach (Scene scene in m_scenes)
//                {
//                    IScenePresence tPresence = scene.GetScenePresence(agentId);
//                    if (tPresence != null)
//                    {
//                        if (!tPresence.IsChildAgent)
//                        {
//                            return scene;
//                        }
//                    }
//                }
//            }
//            return null;
//        }

//        #endregion
//    }
//}