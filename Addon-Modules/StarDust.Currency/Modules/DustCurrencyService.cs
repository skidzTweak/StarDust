using System;
using System.Reflection;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using StarDust.Currency.Grid;
using StarDust.Currency.Region;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency
{
    public enum PurchaseType
    {
        InWorldPurchaseOfCurrency = 1,
        WebsiteRegionPurchase = 2,
        ATMTransferFromAnotherGrid = 3
    }

    public class DustCurrencyService : ConnectorBase, IStarDustCurrencyService, IService
    {
        public IStarDustCurrencyConnector m_database;
        public StarDustConfig m_options = null;
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected bool m_enabled;
        protected const string Version = "0.19";
        private DustRPCHandler m_rpc;
        private MoneyModule m_moneyModule = null;
        private DustRegionService m_dustRegionService = null;
        private IScheduleService m_scheduler;
        private GiveStipends m_stupends;

        #region Properties
        public IScheduleService Scheduler
        {
            get { return m_scheduler; }
        }
        public string Name
        {
            get { return GetType().Name; }
        }

        public IStarDustCurrencyConnector Database
        {
            get { return m_database; }
        }

        public IRegistryCore Registry
        {
            get { return m_registry; }
        }

        #endregion

        #region IService

        #region ISericeMain

        public void Initialize(IConfigSource source, IRegistryCore registry)
        {
            if (!CheckEnabled(source))
                return;
            DisplayLogo();


            Init(registry, Name);

            m_registry = registry;
            m_registry.RegisterModuleInterface<IStarDustCurrencyService>(this);

            AddSecondaryFunctions(source, registry);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_database = DataManager.RequestPlugin<IStarDustCurrencyConnector>();
            m_scheduler = registry.RequestModuleInterface<IScheduleService>();
        }

        public void FinishedStartup()
        {
            if (!m_doRemoteCalls)
            {
                m_scheduler.Register("RestrictedCurrencyPurchaseRemove", RestrictedCurrencyPurchaseRemove_Event);
                m_scheduler.Register("RestrictedCurrencySpendRemove", RestrictedCurrencySpendRemove_Event);
            }

        }

        private object RestrictedCurrencySpendRemove_Event(string functionname, object parameters)
        {
            RestrictedCurrencyInfo pcri = new RestrictedCurrencyInfo();
            pcri.FromOSD((OSDMap)OSDParser.DeserializeJson(parameters.ToString()));
            StarDustUserCurrency starDustUserCurrency = UserCurrencyInfo(pcri.AgentID);
            starDustUserCurrency.RestrictedAmount -= pcri.Amount;
            m_database.UserCurrencyUpdate(starDustUserCurrency);
            return true;
        }

        private object RestrictedCurrencyPurchaseRemove_Event(string functionName, object parameters)
        {
            RestrictedCurrencyInfo pcri = new RestrictedCurrencyInfo();
            pcri.FromOSD((OSDMap)OSDParser.DeserializeJson(parameters.ToString()));
            StarDustUserCurrency starDustUserCurrency = UserCurrencyInfo(pcri.AgentID);
            starDustUserCurrency.RestrictPurchaseAmount -= pcri.Amount;
            m_database.UserCurrencyUpdate(starDustUserCurrency);
            return true;
        }

        #endregion

        #region Startup Helpers

        private void AddSecondaryFunctions(IConfigSource source, IRegistryCore registry)
        {
            m_rpc = new DustRPCHandler(this, source, registry);

            if (!m_doRemoteCalls)
            {
                m_options = new StarDustConfig(source.Configs["StarDustCurrency"]);

                IConfig handlerConfig = source.Configs["Handlers"];
                string password = handlerConfig.GetString("WireduxHandlerPassword",
                                                          handlerConfig.GetString("WebUIHandlerPassword", string.Empty));

                IHttpServer httpServer = null;
                if (password == "")
                httpServer = registry.RequestModuleInterface<ISimulationBase>().GetHttpServer(
                        handlerConfig.GetUInt("WireduxHandlerPort", handlerConfig.GetUInt("WebUIHTTPPort", 8002)));
                
                if (httpServer == null) throw new ArgumentNullException("httpServer");
                m_log.ErrorFormat("[]");

                httpServer.AddStreamHandler(new StarDustCurrencyPostHandlerWebUI("/StarDustWebUI", this, m_registry,
                                                                                 password, m_options));

                if ((m_options.ATMGridURL != "") && (m_options.ATMPassword != ""))
                    httpServer.AddStreamHandler(new StarDustCurrencyPostHandlerATM("/StardustATM_" + m_options.ATMGridURL, this, m_registry, m_options));

                if ((m_options.GiveStipends) && (m_options.Stipend > 0))
                {
                    m_stupends = new GiveStipends(m_options, m_registry, this);
                }
            }
        }

        protected void DisplayLogo()
        {
            m_log.Warn("====================================================================");
            m_log.Warn("====================== STARDUST CURRENCY 2012 ======================");
            m_log.Warn("====================================================================");
            m_log.Warn("Stardust TOS/License Agreement - READ ME <<<<<<<<<<<<<<<<<<<<<<<<<<<");
            m_log.Warn("====================================================================");
            m_log.Warn("* Do NOT use this module in a production environment. ");
            m_log.Warn("* Do NOT use this module with real money. ");
            m_log.Warn("* This module is for educational purposes only, and can NOT be");
            m_log.Warn("  used with real money or in a production enviroment!");
            m_log.Warn("* By using this module you agree that Skidz Tweak, Aurora-Sim, or");
            m_log.Warn("  other contributing developers ARE IN NO WAY responsible for any");
            m_log.Warn("  damages that may occur as a result of using this module.");
            m_log.Warn("* By using this module you agree that you understand the risks of");
            m_log.Warn("  running this module and are fully willing to accept those risks");
            m_log.Warn("  and any consequences that may occur.");
            m_log.Warn("* By downing and using this module you are agreeing to everything");
            m_log.Warn("  listed above. If you do not agree, stop useing it.");
            m_log.Warn("====================================================================");
            m_log.Warn("[StarDustStartup]: Version: " + Version + " Beta\n");
            m_log.Warn("====================================================================");
        }

        protected bool CheckEnabled(IConfigSource source)
        {
            // check to see if it should be enabled and then load the config
            if (source == null) throw new ArgumentNullException("source");
            IConfig economyConfig = source.Configs["StarDustCurrency"];
            m_enabled = (economyConfig != null);
            if (!m_enabled)
            {
                economyConfig = source.Configs["Currency"];
                if (economyConfig != null)
                    if (economyConfig.GetString("Module", "").ToLower() == "stardust")
                        m_enabled = true;
            }
            return m_enabled;
        }
        #endregion

        #endregion

        #region IStarDustCurrencyService

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public StarDustUserCurrency UserCurrencyInfo(UUID agentId)
        {
            if (m_doRemoteCalls)
                return (StarDustUserCurrency)DoRemote(agentId);
            return m_database.GetUserCurrency(agentId);
        }

        /// <summary>
        /// This will not update their currency, only other values
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool UserCurrencyUpdate(StarDustUserCurrency agent)
        {
            if (m_doRemoteCalls)
                return (bool)DoRemote(agent);
            return m_database.UserCurrencyUpdate(agent);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public Transaction UserCurrencyTransfer(Transaction transaction)
        {
            if (m_doRemoteCalls)
                return (Transaction)DoRemote(transaction);
            return m_database.UserCurrencyTransaction(transaction);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public StarDustConfig GetConfig()
        {
            if ((m_options == null) && (m_doRemoteCalls))
            {
                m_options = (StarDustConfig)DoRemote();
                if (m_options != null)
                {
                    m_moneyModule.Options = m_options;
                }
            }
            return m_options;
        }

        public bool CheckEnabled()
        {
            return m_enabled;
        }

        public void SetMoneyModule(MoneyModule moneyModule)
        {
            m_moneyModule = moneyModule;
        }

        public void SetRegionService(DustRegionService dustRegionService)
        {
            m_dustRegionService = dustRegionService;
        }

        #endregion

        #region parcelFunctions, calls from grid to region

        public OSDMap GetParcelDetails(UUID agentId)
        {
            try
            {
                ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                IClientCapsService client = capsService.GetClientCapsService(agentId);
                if (client != null)
                {
                    IRegionClientCapsService regionClient = client.GetRootCapsService();
                    if (regionClient != null)
                    {
                        string serverURI = regionClient.Region.ServerURI + "/StarDustRegion";
                        OSDMap replyData = WebUtils.PostToService(serverURI, new OSDMap
                                                                                 {
                                                                                     {"Method", "parceldetails"},
                                                                                     {"agentid", agentId}
                                                                                 }, true, true);
                        if (replyData["Success"].AsBoolean())
                        {
                            if (replyData["_Result"].Type != OSDType.Map)
                            {
                                // don't return check the other servers uris
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI +
                                           ", connection did not have all the required data.");
                            }
                            else
                            {
                                OSDMap innerReply = (OSDMap)replyData["_Result"];
                                if (innerReply["Result"].AsString() == "Successful")
                                    return innerReply;
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI + ", " +
                                           innerReply["Result"]);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[CURRENCY CONNECTOR] UserCurrencyUpdate to m_ServerURI", e);
            }
            return null;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool SendGridMessage(UUID toId, string message, bool goDeep, UUID transactionId)
        {
            try
            {
                if (m_doRemoteCalls)
                    return (bool)DoRemote(toId, message, goDeep, transactionId);
                ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                IClientCapsService client = capsService.GetClientCapsService(toId);
                if (client != null)
                {
                    IRegionClientCapsService regionClient = client.GetRootCapsService();
                    if (regionClient != null)
                    {
                        string serverURI = regionClient.Region.ServerURI + "/StarDustRegion";
                        OSDMap replyData = WebUtils.PostToService(serverURI, new OSDMap
                                                                                    {
                                                                                        {"Method", "sendgridmessage"},
                                                                                        {"toId", toId},
                                                                                        {"message", message},
                                                                                        {"goDeep", false},
                                                                                        {
                                                                                            "transactionId",
                                                                                            transactionId
                                                                                            }
                                                                                    }, true, true);
                        if (replyData["Success"].AsBoolean())
                        {
                            if (replyData["_Result"].Type != OSDType.Map)
                            {
                                // don't return check the other servers uris
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI);
                            }
                            else
                            {
                                OSDMap innerReply = (OSDMap)replyData["_Result"];
                                if (innerReply["Result"].AsString() == "Successful")
                                    return true;
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI +
                                            ", " +
                                            innerReply["Result"]);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[CURRENCY CONNECTOR] UserCurrencyUpdate to m_ServerURI", e);
            }
            return false;
        }

        #endregion

        #region Agent functions

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public UserAccount GetUserAccount(UUID agentId)
        {
            if (m_doRemoteCalls)
                return (UserAccount)DoRemote(agentId);
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = userService.GetUserAccount(new UUID(), agentId);
            if (user == null)
            {
                m_log.Info("[DustCurrencyService] Unable to find agent.");
                return null;
            }
            return user;
        }

        #endregion

        #region Money
        /// <summary>
        /// This is the function that everythign really happens Grid and Region Side. We build the transaction here.
        /// </summary>
        /// <param name="toID"></param>
        /// <param name="fromID"></param>
        /// <param name="toObjectID"></param>
        /// <param name="fromObjectID"></param>
        /// <param name="amount"></param>
        /// <param name="description"></param>
        /// <param name="type"></param>
        /// <param name="transactionID"></param>
        /// <returns></returns>
        public virtual bool UserCurrencyTransfer(UUID toID, UUID fromID, UUID toObjectID, UUID fromObjectID, uint amount, string description, TransactionType type, UUID transactionID)
        {
            bool isgridServer = false;

            #region Build the transaction



            string transactionPosition = "";
            IScene scene = null;
            string fromObjectName = "";
            string toObjectName = "";


            if ((fromObjectID != UUID.Zero) && (m_dustRegionService != null))
            {
                ISceneChildEntity ce = m_dustRegionService.FindObject(fromObjectID, out scene);
                if (ce != null)
                {
                    fromObjectName = ce.Name;
                    transactionPosition = ce.AbsolutePosition.ToString();
                }
            }

            if ((toObjectID != UUID.Zero) && (m_dustRegionService != null))
            {
                ISceneChildEntity ce = m_dustRegionService.FindObject(toObjectID, out scene);
                if (ce != null)
                {
                    toObjectName = ce.Name;
                    transactionPosition = ce.AbsolutePosition.ToString();
                }
            }

            if ((scene == null) && (m_dustRegionService != null))
            {
                scene = m_dustRegionService.FindScene(fromID);
                if ((scene != null) && (transactionPosition.Length == 0))
                    transactionPosition = scene.GetScenePresence(fromID).AbsolutePosition.ToString();
            }

            if ((scene == null) && (toID != UUID.Zero) && (m_dustRegionService != null))
            {
                scene = m_dustRegionService.FindScene(toID);
                if ((scene != null) && (transactionPosition.Length == 0))
                    transactionPosition = scene.GetScenePresence(toID).AbsolutePosition.ToString();
            }

            if (transactionPosition.Length == 0)
                transactionPosition = "Unknown";

            RegionTransactionDetails r = new RegionTransactionDetails();
            ulong regionHandel = 0;
            if (scene != null)
            {
                r = new RegionTransactionDetails
                {
                    RegionID = scene.RegionInfo.RegionID,
                    RegionName = scene.RegionInfo.RegionName,
                    RegionPosition = transactionPosition
                };
            }
            else if (m_registry != null)
            {
                ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                if (capsService != null)
                {
                    IClientCapsService client = capsService.GetClientCapsService(fromID);
                    if (client != null)
                    {
                        IRegionClientCapsService regionClient = client.GetRootCapsService();
                        if (regionClient != null)
                        {
                            regionHandel = regionClient.Region.RegionHandle;
                            r.RegionName = regionClient.Region.RegionName;
                            r.RegionPosition = "<128,128,128>";
                            r.RegionID = regionClient.Region.RegionID;
                            isgridServer = true;
                        }
                    }
                }
            }
            else return false;


            string fromName = "";
            if (fromID != UUID.Zero)
            {
                if (m_dustRegionService != null)
                {
                    IClientAPI icapiFrom = m_dustRegionService.GetUserClient(fromID);
                    if (icapiFrom != null)
                        fromName = icapiFrom.Name;
                }
                if (fromName == "")
                {
                    UserAccount ua = GetUserAccount(fromID);
                    if (ua != null)
                        fromName = ua.Name;
                }
                if (fromName == "")
                {
                    if (m_dustRegionService != null)
                    {
                        ISceneChildEntity ce = m_dustRegionService.FindObject(fromID, out scene);
                        if (ce != null)
                        {
                            fromObjectID = fromID;
                            fromID = ce.OwnerID;
                            UserAccount ua2 = GetUserAccount(fromID);

                            if (ua2 != null) fromName = ua2.Name;
                            else fromID = UUID.Zero;

                            fromObjectName = ce.Name;
                        }
                        else
                            fromID = UUID.Zero;
                    }
                    else
                        fromID = UUID.Zero;
                }
            }

            if (fromID == UUID.Zero)
            {
                m_log.Debug("[StarDust MoneyModule.cs] Could not find who the money was coming from.");
                return false;
            }

            uint objectLocalId = 0;
            string toName = "";
            if (toID != UUID.Zero)
            {
                if (m_dustRegionService != null)
                {
                    IClientAPI icapiFrom = m_dustRegionService.GetUserClient(toID);
                    if (icapiFrom != null)
                        toName = icapiFrom.Name;
                }
                if (toName == "")
                {
                    UserAccount ua = GetUserAccount(toID);
                    if (ua != null)
                        toName = ua.Name;
                }
                if (toName == "")
                {
                    if (m_dustRegionService != null)
                    {
                        ISceneChildEntity ce = m_dustRegionService.FindObject(toID, out scene);
                        if (ce != null)
                        {
                            toObjectID = toID;
                            toID = ce.OwnerID;
                            UserAccount ua3 = GetUserAccount(toID);
                            if (ua3 != null)
                            {
                                toName = ua3.Name;
                            }
                            toObjectName = ce.Name;
                            objectLocalId = ce.LocalId;
                        }
                        else
                        {
                            toName = "Group";
                        }
                    }
                    else
                    {
                        toName = "Group";
                    }
                }
            }
            else toID = m_options.BankerPrincipalID;
            //this ensure no matter what theres a place for the money to go
            UserCurrencyInfo(toID);

            if ((description == "") && ((int)type == 5001) && (fromObjectID == UUID.Zero) && (toObjectID == UUID.Zero))
                description = "Gift";
            if (description == "")
                description = Enum.GetName(typeof(TransactionType), type);
            if (description == "")
                description = type.ToString();





            #endregion

            #region Perform transaction

            Transaction transaction =
                UserCurrencyTransfer(new Transaction
                {
                    TransactionID = transactionID,
                    Amount = amount,
                    Description = description,
                    FromID = fromID,
                    FromName = fromName,
                    FromObjectID = fromObjectID,
                    FromObjectName = fromObjectName,
                    Region = r,
                    ToID = toID,
                    ToName = toName,
                    ToObjectID = toObjectID,
                    ToObjectName = toObjectName,
                    TypeOfTrans = type
                });
            bool returnvalue = transaction.Complete == 1;

            if (returnvalue)
            {
                m_moneyModule.FireObjectPaid(toObjectID, fromID, int.Parse(amount.ToString()));
            }

            #endregion

            #region notifications

            if (transaction.Complete == 1)
            {
                if (transaction.ToID != m_options.BankerPrincipalID)
                {
                    if (transaction.TypeOfTrans == TransactionType.Gift)
                    {
                        SendGridMessage(transaction.FromID,
                                        "You Paid " + transaction.ToName + " $" + transaction.Amount.ToString(), !isgridServer, transaction.TransactionID);
                    }
                    else
                    {
                        SendGridMessage(transaction.FromID,
                                        "You Paid $" + transaction.Amount + " to " + transaction.ToName, !isgridServer, transaction.TransactionID);
                    }
                    SendGridMessage(transaction.ToID,
                                        "You Were Paid $" + transaction.Amount + " by " + transaction.FromName, !isgridServer, transaction.TransactionID);
                }
                else if (transaction.TypeOfTrans == TransactionType.UploadFee)
                {
                    SendGridMessage(transaction.FromID, "You Paid $" + transaction.Amount + " to upload", !isgridServer, transaction.TransactionID);
                }
                else
                {
                    SendGridMessage(transaction.FromID, "You Paid $" + transaction.Amount, !isgridServer, transaction.TransactionID);
                }
            }
            else
            {
                if (transaction.CompleteReason != "")
                    SendGridMessage(transaction.FromID, "Transaction Failed - " + transaction.CompleteReason, !isgridServer, transaction.TransactionID);
                else
                    SendGridMessage(transaction.FromID, "Transaction Failed", !isgridServer, transaction.TransactionID);
            }

            if ((toObjectID != UUID.Zero) && (!isgridServer))
            {
                m_moneyModule.FirePostObjectPaid(objectLocalId, regionHandel, fromID, int.Parse(amount.ToString()));
            }

            #endregion

            return returnvalue;
        }
        #endregion

        #region Purchase

        public UUID StartPurchaseOrATMTransfer(UUID agentId, uint amountBuying, PurchaseType purchaseType, string gridName)
        {
            bool success = false;
            UserAccount ua = Registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(UUID.Zero, agentId);
            if (ua == null) return UUID.Zero;
            string agentName = ua.Name;

            UUID RegionID = UUID.Zero;
            string RegionName = "";
            if (purchaseType == PurchaseType.InWorldPurchaseOfCurrency)
            {
                IClientCapsService client = Registry.RequestModuleInterface<ICapsService>().GetClientCapsService(agentId);
                if (client != null)
                {
                    IRegionClientCapsService regionClient = client.GetRootCapsService();
                    RegionID = regionClient.Region.RegionID;
                    RegionName = regionClient.Region.RegionName;
                }
            }
            else if (purchaseType == PurchaseType.ATMTransferFromAnotherGrid)
            {
                RegionName = "Grid:" + gridName;
            }
            else
            {
                RegionName = "Unknown";
            }


            UUID purchaseID = UUID.Random();
            success = m_database.UserCurrencyBuy(purchaseID, agentId, agentName, amountBuying, m_options.RealCurrencyConversionFactor,
                new RegionTransactionDetails
                    {
                        RegionID = RegionID,
                        RegionName = RegionName
                    }, (int)purchaseType);
            StarDustUserCurrency currency = UserCurrencyInfo(agentId);



            if (m_options.AutoApplyCurrency && success)
            {
                Transaction transaction;
                m_database.UserCurrencyBuyComplete(purchaseID, 1, "AutoComplete",
                                                   m_options
                                                       .AutoApplyCurrency.ToString
                                                       (), "Auto Complete",
                                                   out transaction);

                UserCurrencyTransfer(transaction.ToID,
                                     m_options.
                                         BankerPrincipalID, UUID.Zero, UUID.Zero,
                                     transaction.Amount, "Currency Purchase",
                                     TransactionType.SystemGenerated,
                                     transaction.TransactionID);
                RestrictCurrency(currency, transaction, agentId);

            }
            else if (success && (m_options.AfterCurrencyPurchaseMessage != string.Empty) && (purchaseType == PurchaseType.InWorldPurchaseOfCurrency))
                SendGridMessage(agentId,String.Format(m_options.AfterCurrencyPurchaseMessage,purchaseID.ToString()), false, UUID.Zero);

            if (success)
                return purchaseID;
            return UUID.Zero;
        }

        #endregion

        public void RestrictCurrency(StarDustUserCurrency currency, Transaction transaction, UUID agentId)
        {
            if ((m_options.MaxAmountPurchaseDays > 0) && (m_options.MaxAmountPurchase > 0))
            {
                currency.RestrictPurchaseAmount += transaction.Amount;
                Scheduler.Save(new SchedulerItem("RestrictedCurrencyPurchaseRemove",
                                                 OSDParser.SerializeJsonString(new RestrictedCurrencyInfo()
                                                 {
                                                     AgentID = agentId,
                                                     Amount = transaction.Amount,
                                                     FromTransactionID = transaction.TransactionID
                                                 }.ToOSD()),
                                                 true, DateTime.UtcNow,
                                                 m_options.MaxAmountPurchaseDays,
                                                 RepeatType.days, agentId)
                {
                    HisotryKeep = true,
                    HistoryReciept = false,
                    RunOnce = true
                });
                UserCurrencyUpdate(currency);
            }
            if ((m_options.RestrictMoneyHoursAfterPurchase > 0) && (m_options.RestrictMoneyCanSpendAfterPurchase > 0))
            {
                uint amount2Restrict = 0;
                if (currency.RestrictedAmount == 0)
                {
                    if (transaction.Amount >= m_options.RestrictMoneyCanSpendAfterPurchase)
                        amount2Restrict = transaction.Amount - (uint)m_options.RestrictMoneyCanSpendAfterPurchase;
                    else //because its less than the amount we allow them to spend, we restricted it all.. why? because else they could buy over and over with no restrictions 
                        amount2Restrict = transaction.Amount / 2;
                }
                else // if they already have money restricted we restrict everything
                    amount2Restrict = transaction.Amount;

                currency.RestrictedAmount += amount2Restrict;

                Scheduler.Save(new SchedulerItem("RestrictedCurrencySpendRemove",
                                                 OSDParser.SerializeJsonString(new RestrictedCurrencyInfo()
                                                 {
                                                     AgentID = agentId,
                                                     Amount = amount2Restrict,
                                                     FromTransactionID = transaction.TransactionID
                                                 }.ToOSD()),
                                                 true, DateTime.UtcNow,
                                                 m_options.RestrictMoneyHoursAfterPurchase,
                                                 RepeatType.hours, agentId)
                {
                    HisotryKeep = true,
                    HistoryReciept = false,
                    RunOnce = true
                });
                UserCurrencyUpdate(currency);
            }
        }
    }


}
