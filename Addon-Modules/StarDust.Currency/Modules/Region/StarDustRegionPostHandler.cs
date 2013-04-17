using System;
using System.IO;
using System.Reflection;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using Nini.Config;
using OpenMetaverse;
using log4net;
using OpenMetaverse.StructuredData;
using StarDust.Currency.Interfaces;
using Aurora.Framework.SceneInfo;
using Aurora.Framework.Services;
using Aurora.Framework.Modules;
using Aurora.Framework.PresenceInfo;
using System.Threading;

namespace StarDust.Currency.Region
{
    public class StarDustRegionPostHandler : IService, IStarDustRegionPostHandler
    {
        private readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IRegistryCore m_registry;
        private IStarDustCurrencyService m_currencyService;
        private ISyncMessagePosterService m_syncMessagePosterService;
        private ISyncMessageRecievedService m_syncMessageReceivedService;

        #region Functions
        public bool SendGridMessageRegionPostHandler(Aurora.Framework.Services.GridRegion region, UUID agentID, string message, UUID transactionID)
        {
            OSDMap map = new OSDMap();
            map["AgentID"] = agentID;
            map["Message"] = message;
            map["TransactionID"] = transactionID;
            m_syncMessagePosterService.Post(region.ServerURI, map);
            return true;
        }

        public LandData ParcelDetailsRegionPostHandler(Aurora.Framework.Services.GridRegion region, UUID agentid)
        {
            OSDMap map = new OSDMap();
            map["AgentID"] = agentid;
            bool complete = false;
            OSDMap result = null;
            m_syncMessagePosterService.Get(region.ServerURI, map, (r) => { result = r; complete = true; });
            while (!complete) Thread.Sleep(50);

            if (result == null || result.Type != OSDType.Map) return null;
            LandData data = new LandData();
            data.FromOSD((OSDMap)result);
            return data;
        }
        #endregion

        #region Implementation of IService

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            m_currencyService = m_registry.RequestModuleInterface<IStarDustCurrencyService>();
            if (m_currencyService == null)
                return;
            m_registry.RegisterModuleInterface<IStarDustRegionPostHandler>(this);
            m_syncMessagePosterService = m_registry.RequestModuleInterface<ISyncMessagePosterService>();
            m_syncMessageReceivedService = m_registry.RequestModuleInterface<ISyncMessageRecievedService>();
            m_syncMessageReceivedService.OnMessageReceived += m_syncMessageReceivedService_OnMessageReceived;
        }

        OSDMap m_syncMessageReceivedService_OnMessageReceived(OSDMap message)
        {
            if (!message.ContainsKey("Method"))
                return null;
            if (message["Method"] == "SendGridMessage")
            {
                bool result = m_currencyService.StarDustRegionService.SendGridMessage(message["AgentID"], message["Message"], false, message["TransactionID"]);
                OSDMap retVal = new OSDMap();
                retVal["Result"] = result;
                return retVal;
            }
            if (message["Method"] == "SendGridMessage")
            {
                IScenePresence sp;
                if (m_currencyService.StarDustRegionService.Scene.TryGetScenePresence(message["AgentID"], out sp))
                {
                    IParcelManagementModule parcelManagement = sp.Scene.RequestModuleInterface<IParcelManagementModule>();
                    ILandObject parcel = parcelManagement.GetLandObject(sp.AbsolutePosition.X, sp.AbsolutePosition.Y);
                    return parcel.LandData.ToOSD();
                }
            }
            return null;
        }

        #endregion
    }

    public interface IStarDustRegionPostHandler
    {
        LandData ParcelDetailsRegionPostHandler(Aurora.Framework.Services.GridRegion region, UUID agentid);
        bool SendGridMessageRegionPostHandler(Aurora.Framework.Services.GridRegion region, UUID agentID, string message, UUID transactionID);
    }
}
