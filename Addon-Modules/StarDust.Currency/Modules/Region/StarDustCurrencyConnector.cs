using System;
using System.Collections.Generic;
using System.Reflection;
using Aurora.Framework;
using Aurora.Simulation.Base;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Region
{
    public class StarDustCurrencyConnector : IStarDustCurrencyService, IService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IRegistryCore m_registry;
        private bool m_enabled;

        public string Name
        {
            get { return GetType().Name; }
        }

        #region IService

        public void Initialize(IConfigSource source, IRegistryCore registry)
        {
            IConfig economyConfig = source.Configs["StarDustCurrency"];
            m_enabled = (economyConfig != null)
                            ? (economyConfig.GetString("CurrencyConnector", "Remote") == "Remote")
                            : true;
            if (!m_enabled) return;
            m_registry = registry;
            m_registry.RegisterModuleInterface<IStarDustCurrencyService>(this);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {

        }

        public void FinishedStartup()
        {

        }

        public virtual IStarDustCurrencyService InnerService
        {
            get { return this; }
        }

        #endregion

        #region IStarDustCurrencyService

        public StarDustUserCurrency UserCurrencyInfo(UUID agentId)
        {
            return new StarDustUserCurrency(MakeCallOSDMAP(new OSDMap
                                                               {
                                                                   {"Method", "stardust_currencyinfo"},
                                                                   {"AgentId", agentId}
                                                               }, "UserCurrencyInfo"));
        }

        public bool UserCurrencyUpdate(StarDustUserCurrency agent)
        {
            OSDMap osdMap = agent.ToOSD();
            osdMap.Add("Method", "stardust_currencyupdate");
            return MakeCall(osdMap, "UserCurrencyUpdate");
        }

        public Transaction UserCurrencyTransfer(Transaction transaction)
        {
            return new Transaction(MakeCallOSDMAP(transaction.ToOSD("Method", "stardust_currencytransfer"), "UserCurrencyTransaction"));
        }

        public StarDustConfig GetConfig()
        {
            OSDMap tempmap = MakeCallOSDMAP(new OSDMap()
                                                {
                                                    {"Method", "getconfig"}
                                                }, "GetConfig");
            if (tempmap != null)
                return new StarDustConfig(tempmap);
            else
            {
                m_log.Error("No configuration was returned from the grid server.");
                List<string> serverUrIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf("StarDustURI");
                foreach (string serverUri in serverUrIs)
                {
                    m_log.Info("Stardust Currency Server is " + serverUri);
                }
                return null;
            }
        }

        public bool SendGridMessage(UUID toId, string message, bool goDeep, UUID transactionId)
        {
            return MakeCall(new OSDMap
                                {
                                    {"toId", toId},
                                    {"message", message},
                                    {"Method", "sendgridmessage"},
                                    {"goDeep", goDeep},
                                    {"transactionId", transactionId}
                                }, "UserCurrencyUpdate");
        }

        public bool FinishPurchase(OSDMap resp, string rawResponse)
        {
            throw new NotImplementedException();
        }

        public OSDMap PrePurchaseCheck(UUID purchaseId)
        {
            throw new NotImplementedException();
        }

        public OSDMap OrderSubscription(UUID toId, string regionName, string notes, string subscriptionID)
        {
            throw new NotImplementedException();
        }

        public bool CheckiFAlreadyComplete(OSDMap map)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private Functions

        private bool MakeCall(OSDMap reqString, string function)
        {
            List<string> serverURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf("StarDustURI");
            foreach (string serverURI in serverURIs)
            {
                try
                {
                    OSDMap replyData = WebUtils.PostToService(serverURI, reqString, true, true);
                    if (replyData["Success"].AsBoolean())
                    {
                        if (replyData["_Result"].Type != OSDType.Map)
                        {
                            // don't return check the other servers uris
                            m_log.Warn("[StarDustCurrencyConnector]: Unable to connect successfully to " + serverURI +
                                       ", connection did not have all the required data.");
                        }
                        else
                        {
                            OSDMap innerReply = (OSDMap)replyData["_Result"];
                            if (innerReply["Result"].AsString() == "Successful")
                                return true;
                            m_log.Warn("[STARDUSTCURRENCYCONNECTOR]: Unable to connect successfully to " + serverURI + ", " + innerReply["Result"]);
                        }
                    }
                    m_log.Warn("[STARDUSTCURRENCYCONNECTOR]: Unable to connect successfully to " + serverURI);
                }
                catch (Exception e)
                {
                    m_log.Error("[STARDUSTCURRENCYCONNECTOR] " + function + " to m_ServerURI", e);
                }
            }
            return false;
        }

        private OSDMap MakeCallOSDMAP(OSDMap reqString, string function)
        {
            List<string> serverURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf("StarDustURI");
            foreach (string serverURI in serverURIs)
            {
                try
                {
                    OSDMap replyData = WebUtils.PostToService(serverURI, reqString, true, true);
                    if (replyData["Success"].AsBoolean())
                    {
                        if (replyData["_Result"].Type != OSDType.Map)
                        {
                            // don't return check the other servers uris
                            m_log.Warn("[StarDustCurrencyConnector]: Unable to connect successfully to " + serverURI +
                                       ", connection did not have all the required data.");
                        }
                        else
                        {
                            OSDMap innerReply = (OSDMap)replyData["_Result"];
                            if (innerReply["Result"].AsString() == "Successful")
                                return innerReply;
                            m_log.Warn("[STARDUSTCURRENCYCONNECTOR]: Unable to connect successfully to " + serverURI + ", " + innerReply["Result"]);
                        }
                    }
                    m_log.Warn("[STARDUSTCURRENCYCONNECTOR]: Unable to connect successfully to " + serverURI);
                }
                catch (Exception e)
                {
                    m_log.Error("[STARDUSTCURRENCYCONNECTOR] " + function + " to m_ServerURI", e);
                }
            }
            return null;
        }

        #endregion

    }
}
