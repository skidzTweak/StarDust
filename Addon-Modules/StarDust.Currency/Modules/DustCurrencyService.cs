﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using StarDust.Currency.Grid;
using StarDust.Currency.Region;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using StarDust.Currency.Interfaces;
using Aurora.Framework.Services;
using Aurora.Framework.Utilities;
using Aurora.Framework.Modules;
using Aurora.Framework.SceneInfo;
using Aurora.Framework.PresenceInfo;
using Aurora.Framework.Servers.HttpServer.Interfaces;
using Aurora.Framework.ConsoleFramework;

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
        protected bool m_enabled;
        protected const string Version = "0.19";
        private DustRPCHandler m_rpc;
        private MoneyModule m_moneyModule = null;
        private IScheduleService m_scheduler;
        private GiveStipends m_stupends;
		private IStarDustRegionPostHandler regionPostHandler;

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
            if (m_registry == null) return;
            if (!m_doRemoteCalls)
            {
                m_scheduler.Register("RestrictedCurrencyPurchaseRemove", RestrictedCurrencyPurchaseRemove_Event);
                m_scheduler.Register("RestrictedCurrencySpendRemove", RestrictedCurrencySpendRemove_Event);
            }
			regionPostHandler = m_registry.RequestModuleInterface<IStarDustRegionPostHandler>();
			m_moneyModule = m_registry.RequestModuleInterface<IMoneyModule>() as MoneyModule;
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

                IHttpServer httpServer = registry.RequestModuleInterface<ISimulationBase>().GetHttpServer(
                        handlerConfig.GetUInt("WireduxHandlerPort", handlerConfig.GetUInt("WebUIHTTPPort", 8002)));

                if (password != "")
                    httpServer.AddStreamHandler(new StarDustCurrencyPostHandlerWebUI("/StarDustWebUI", this, m_registry,
                                                                                 password, m_options));

                if ((m_options.ATMGridURL != "") && (m_options.ATMPassword != ""))
                    httpServer.AddStreamHandler(new StarDustCurrencyPostHandlerATM("/StardustATM_" + m_options.ATMGridURL, this, m_registry, m_options));

                if ((m_options.GiveStipends) && (m_options.Stipend > 0))
                    m_stupends = new GiveStipends(m_options, m_registry, this);
            }
        }

        protected void DisplayLogo()
        {
            MainConsole.Instance.Warn("====================================================================");
            MainConsole.Instance.Warn("====================== STARDUST CURRENCY 2012 ======================");
            MainConsole.Instance.Warn("====================================================================");
            MainConsole.Instance.Warn("Stardust TOS/License Agreement - READ ME <<<<<<<<<<<<<<<<<<<<<<<<<<<");
            MainConsole.Instance.Warn("====================================================================");
            MainConsole.Instance.Warn("* Do NOT use this module in a production environment. ");
            MainConsole.Instance.Warn("* Do NOT use this module with real money. ");
            MainConsole.Instance.Warn("* This module is for educational purposes only, and can NOT be");
            MainConsole.Instance.Warn("  used with real money or in a production enviroment!");
            MainConsole.Instance.Warn("* By using this module you agree that Skidz Tweak, Aurora-Sim, or");
            MainConsole.Instance.Warn("  other contributing developers ARE IN NO WAY responsible for any");
            MainConsole.Instance.Warn("  damages that may occur as a result of using this module.");
            MainConsole.Instance.Warn("* By using this module you agree that you understand the risks of");
            MainConsole.Instance.Warn("  running this module and are fully willing to accept those risks");
            MainConsole.Instance.Warn("  and any consequences that may occur.");
            MainConsole.Instance.Warn("* By downing and using this module you are agreeing to everything");
            MainConsole.Instance.Warn("  listed above. If you do not agree, stop useing it.");
            MainConsole.Instance.Warn("====================================================================");
            MainConsole.Instance.Warn("[StarDustStartup]: Version: " + Version + " Beta\n");
            MainConsole.Instance.Warn("====================================================================");
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
        public GroupBalance GetGroupBalance(UUID groupID)
        {
            if (m_doRemoteCalls)
                return (GroupBalance)DoRemote(groupID);
            return m_database.GetGroupBalance(groupID);
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

        public DustRegionService StarDustRegionService { get; set; }

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
                        LandData land = regionPostHandler.ParcelDetailsRegionPostHandler(regionClient.Region, client.AgentID);
                        return land.ToOSD();
                    }
                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[CURRENCY CONNECTOR] UserCurrencyUpdate to m_ServerURI: {0}", e.ToString());
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
                        regionPostHandler.SendGridMessageRegionPostHandler(regionClient.Region, toId, message,
                                                                     transactionId);
                    }
                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[CURRENCY CONNECTOR] UserCurrencyUpdate to m_ServerURI: {0}", e.ToString());
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
            UserAccount user = userService.GetUserAccount(null, agentId);
            if (user == null)
            {
                MainConsole.Instance.Info("[DustCurrencyService] Unable to find agent.");
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


            if ((fromObjectID != UUID.Zero) && (StarDustRegionService != null))
            {
                ISceneChildEntity ce = StarDustRegionService.FindObject(fromObjectID, out scene);
                if (ce != null)
                {
                    fromObjectName = ce.Name;
                    transactionPosition = ce.AbsolutePosition.ToString();
                }
            }

            if ((toObjectID != UUID.Zero) && (StarDustRegionService != null))
            {
                ISceneChildEntity ce = StarDustRegionService.FindObject(toObjectID, out scene);
                if (ce != null)
                {
                    toObjectName = ce.Name;
                    transactionPosition = ce.AbsolutePosition.ToString();
                }
            }

            if (transactionPosition.Length == 0)
                transactionPosition = scene.GetScenePresence(fromID).AbsolutePosition.ToString();

            if(transactionPosition.Length == 0)
                transactionPosition = scene.GetScenePresence(toID).AbsolutePosition.ToString();

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
                if (StarDustRegionService != null)
                {
                    IClientAPI icapiFrom = StarDustRegionService.GetUserClient(fromID);
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
                    if (StarDustRegionService != null)
                    {
                        ISceneChildEntity ce = StarDustRegionService.FindObject(fromID, out scene);
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
                MainConsole.Instance.Debug("[StarDust MoneyModule.cs] Could not find who the money was coming from.");
                return false;
            }

            string toName = "";
            if (toID != UUID.Zero)
            {
                if (StarDustRegionService != null)
                {
                    IClientAPI icapiFrom = StarDustRegionService.GetUserClient(toID);
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
                    if (StarDustRegionService != null)
                    {
                        ISceneChildEntity ce = StarDustRegionService.FindObject(toID, out scene);
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
                m_moneyModule.FireObjectPaid(toObjectID, fromID, (int)amount);
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
                else if (transaction.TypeOfTrans == TransactionType.UploadCharge)
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
                m_moneyModule.FireObjectPaid(toObjectID, fromID, (int)amount);
            }

            #endregion

            return returnvalue;
        }
        #endregion

        #region Purchase

        public UUID StartPurchaseOrATMTransfer(UUID agentId, uint amountBuying, PurchaseType purchaseType, string gridName)
        {
            bool success = false;
            UserAccount ua = Registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(null, agentId);
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
