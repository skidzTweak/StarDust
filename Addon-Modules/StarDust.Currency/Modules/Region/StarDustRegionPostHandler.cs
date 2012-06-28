using System;
using System.IO;
using System.Reflection;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Services.Interfaces;
using log4net;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Region
{
    public class StarDustRegionPostHandler : ConnectorBaseG2R, IService, IStarDustRegionPostHandler
    {
        private readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IStarDustCurrencyService m_currenyService;

        #region Functions
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool SendGridMessageRegionPostHandler(UUID regionID, UUID agentID, string message, UUID transactionID)
        {
            if (m_doRemoteCalls)
                return (bool)DoRemote(regionID, agentID, message, transactionID);
            return m_currenyService.StarDustRegionService.SendGridMessage(agentID, message, false, transactionID);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public LandData ParcelDetailsRegionPostHandler(UUID regionID, UUID agentid)
        {
            if (m_doRemoteCalls)
                return (LandData)DoRemote(regionID, agentid);
            IScenePresence sp;
            if (m_currenyService.StarDustRegionService.FindScene(agentid).TryGetScenePresence(agentid, out sp))
            {
                IParcelManagementModule parcelManagement = sp.Scene.RequestModuleInterface<IParcelManagementModule>();
                ILandObject parcel = parcelManagement.GetLandObject(sp.AbsolutePosition.X, sp.AbsolutePosition.Y);
                return parcel.LandData;
            }
            return null;
        }
        #endregion

        #region Implementation of IService

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            Init(registry, "StarDustRegionPostHandler");
            m_registry.RegisterModuleInterface<IStarDustRegionPostHandler>(this);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {

        }

        public void FinishedStartup()
        {
            m_currenyService = m_registry.RequestModuleInterface<IStarDustCurrencyService>();
        }

        #endregion
    }

    public interface IStarDustRegionPostHandler
    {
        LandData ParcelDetailsRegionPostHandler(UUID regionID, UUID agentid);
        bool SendGridMessageRegionPostHandler(UUID regionID, UUID agentID, string message, UUID transactionID);
    }
}
