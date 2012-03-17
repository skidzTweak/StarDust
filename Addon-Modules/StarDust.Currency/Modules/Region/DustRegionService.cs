using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aurora.Framework;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using StarDust.Currency.Interfaces;
using log4net;

namespace StarDust.Currency.Region
{
    public class DustRegionService : ISharedRegionModule, IStardustRegionService
    {
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_enabled = true;
        private DustCurrencyService m_connector;
        private int m_objectCapacity;
        private readonly List<IScene> m_scenes = new List<IScene>();
        private StarDustConfig m_options;

        #region Implementation of IRegionModuleBase

        public string Name
        {
            get { return "DustRegionService"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            // throw new NotImplementedException();
        }

        public void AddRegion(IScene scene)
        {
            if (scene == null) throw new ArgumentNullException("scene");
            if (!m_enabled) return;

            if (m_connector == null)
            {
                m_connector = scene.RequestModuleInterface<IStarDustCurrencyService>() as DustCurrencyService;
                m_enabled = ((m_connector != null) && (m_connector.Enabled));
                if (!m_enabled) return;
                
                m_connector.SetRegionService(this);
            }
            
            MainServer.Instance.AddStreamHandler(new StarDustRegionPostHandler("/StarDustRegion", this));

            m_objectCapacity = scene.RegionInfo.ObjectCapacity;
            scene.RegisterModuleInterface<IStardustRegionService>(this);
            m_scenes.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
            scene.EventManager.OnValidateBuyLand += ValidateLandBuy;

            m_log.DebugFormat("[DustCurrencyService] DustCurrencyService Initialize on {0} ", MainServer.Instance.ServerURI);
        }

        public void RegionLoaded(IScene scene)
        {
            if (m_connector == null) return;
            m_options = m_connector.GetConfig();
        }

        public void RemoveRegion(IScene scene)
        {
            // clean up on removing region
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
            scene.EventManager.OnValidateBuyLand -= ValidateLandBuy;

            scene.UnregisterModuleInterface<IStardustRegionService>(this);

            MainServer.Instance.RemoveStreamHandler("POST", "/StarDustRegion");
            m_scenes.Remove(scene);
        }

        public void Close()
        {
            // anything else I should do here?
            m_scenes.Clear();
        }

        public void PostInitialise()
        {
            //throw new NotImplementedException();
        }

        #endregion

        #region Implementation of IStardustRegionService

        public ISceneChildEntity FindObject(UUID objectID, out IScene scene)
        {
            foreach (IScene s in m_scenes)
            {
                ISceneChildEntity obj;
                s.TryGetPart(objectID, out obj);
                if (obj == null) continue;
                scene = s;
                return obj;
            }
            scene = null;
            return null;
        }

        public IScene FindScene(UUID agentId)
        {
            return (from s in m_scenes
                    let presence = s.GetScenePresence(agentId)
                    where presence != null && !presence.IsChildAgent
                    select s).FirstOrDefault();
        }

        public IClientAPI GetUserClient(UUID agentId)
        {
            return (from scene in m_scenes
                    where scene.GetScenePresence(agentId) != null
                    where !scene.GetScenePresence(agentId).IsChildAgent
                    select scene.GetScenePresence(agentId).ControllingClient).FirstOrDefault();
        }

        #endregion

        #region region client events

        private void OnNewClient(IClientAPI client)
        {
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest2;
            client.OnParcelBuyPass += ClientOnParcelBuyPass;
            client.SendMoneyBalance(UUID.Zero, true, new byte[0],  (int) m_connector.UserCurrencyInfo(client.AgentId).Amount);
        }

        protected void OnClosingClient(IClientAPI client)
        {
            client.OnEconomyDataRequest -= EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest -= SendMoneyBalance;
            client.OnMoneyTransferRequest -= ProcessMoneyTransferRequest2;
            client.OnParcelBuyPass -= ClientOnParcelBuyPass;
        }

        private void ProcessMoneyTransferRequest2(UUID fromID, UUID toID, int amount, int type, string description)
        {
            m_connector.UserCurrencyTransfer(toID, fromID, UUID.Zero, UUID.Zero, (uint)amount, description, (TransactionType)type, UUID.Random());
        }

        private bool ValidateLandBuy(EventManager.LandBuyArgs e)
        {
            return m_connector.UserCurrencyTransfer(e.parcelOwnerID, e.agentId, UUID.Zero, UUID.Zero, (uint)e.parcelPrice, "Land Purchase", TransactionType.Purchase, UUID.Random());
        }

        private void EconomyDataRequestHandler(IClientAPI remoteClient)
        {
            bool wasnull = (m_options == null);
            if (wasnull) m_options = new StarDustConfig();
            remoteClient.SendEconomyData(0, m_objectCapacity, remoteClient.Scene.RegionInfo.ObjectCapacity,
                                         m_options.PriceEnergyUnit, m_options.PriceGroupCreate,
                                         m_options.PriceObjectClaim, m_options.PriceObjectRent,
                                         m_options.PriceObjectScaleFactor, m_options.PriceParcelClaim,
                                         m_options.PriceParcelClaimFactor,
                                         m_options.PriceParcelRent, m_options.PricePublicObjectDecay,
                                         m_options.PricePublicObjectDelete, m_options.PriceRentLight,
                                         m_options.PriceUpload,
                                         m_options.TeleportMinPrice, m_options.TeleportPriceExponent);
            if (wasnull) m_options = null;
        }

        private void SendMoneyBalance(IClientAPI client, UUID agentId, UUID sessionId, UUID transactionId)
        {
            if (client.AgentId == agentId && client.SessionId == sessionId)
                client.SendMoneyBalance(transactionId, true, new byte[0], (int)m_connector.UserCurrencyInfo(client.AgentId).Amount);
            else
                client.SendAlertMessage("Unable to send your money balance to you!");
        }



        /// <summary>
        /// The client wants to buy a pass for a parcel
        /// </summary>
        /// <param name="client"></param>
        /// <param name="fromID"></param>
        /// <param name="parcelLocalId"></param>
        private void ClientOnParcelBuyPass(IClientAPI client, UUID fromID, int parcelLocalId)
        {
            m_log.InfoFormat("[StarDustCurrency]: ClientOnParcelBuyPass {0}, {1}, {2}", client.Name, fromID,
                             parcelLocalId);
            IScenePresence agentSp = FindScene(client.AgentId).GetScenePresence(client.AgentId);
            IParcelManagementModule parcelManagement = agentSp.Scene.RequestModuleInterface<IParcelManagementModule>();
            ILandObject landParcel = null;
            List<ILandObject> land = parcelManagement.AllParcels();
            foreach (ILandObject landObject in land)
            {
                if (landObject.LandData.LocalID == parcelLocalId)
                {
                    landParcel = landObject;
                }
            }
            if (landParcel != null)
            {
                m_log.Debug("[StarDustCurrency]: Base account: " + landParcel.LandData.OwnerID + " Agent ID: " + fromID +
                            " Price:" +
                            landParcel.LandData.PassPrice);
                bool giveResult = m_connector.UserCurrencyTransfer(landParcel.LandData.OwnerID, fromID, UUID.Zero, UUID.Zero,
                                                       (uint)landParcel.LandData.PassPrice, "Parcel Pass",
                                                       TransactionType.Purchase, UUID.Random());
                if (giveResult)
                {
                    ParcelManager.ParcelAccessEntry entry
                        = new ParcelManager.ParcelAccessEntry
                        {
                            AgentID = fromID,
                            Flags = AccessList.Access,
                            Time = DateTime.Now.AddHours(landParcel.LandData.PassHours)
                        };
                    landParcel.LandData.ParcelAccessList.Add(entry);
                    agentSp.ControllingClient.SendAgentAlertMessage("You have been added to the parcel access list.",
                                                                    false);
                }
            }
            else
            {
                m_log.ErrorFormat("[StarDustCurrency]: No parcel found for parcel id {0}", parcelLocalId);
                agentSp.ControllingClient.SendAgentAlertMessage("Opps, the internet blew up! Unable to find parcel.", false);
            }
        }

        /// <summary>
        /// All message for money actually go through this function. Which also update the balance
        /// </summary>
        /// <param name="toId"></param>
        /// <param name="message"></param>
        /// <param name="goDeep"></param>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        public bool SendGridMessage(UUID toId, string message, bool goDeep, UUID transactionId)
        {
            if (!m_options.DisplayPayMessages) message = "";
            IScene agentSp = FindScene(toId);
            if (agentSp == null)
                return (goDeep) && m_connector.SendGridMessage(toId, message, false, transactionId);
            IDialogModule dialogModule = agentSp.RequestModuleInterface<IDialogModule>();
            if (dialogModule != null)
            {
                IClientAPI icapiTo = GetUserClient(toId);
                if ((message.IndexOf("http") > -1) && (icapiTo != null))
                {
                    icapiTo.SendMoneyBalance(transactionId, true, new byte[0], (int)m_connector.UserCurrencyInfo(icapiTo.AgentId).Amount);
                    dialogModule.SendUrlToUser(toId, "", UUID.Zero, UUID.Zero, false, message, message.Substring(message.IndexOf("http")));
                    icapiTo.SendAlertMessage(message.Substring(message.IndexOf("http")));
                }
                else if (icapiTo != null)
                    icapiTo.SendMoneyBalance(transactionId, true, Utils.StringToBytes(message),(int)m_connector.UserCurrencyInfo(icapiTo.AgentId).Amount);
                else
                    dialogModule.SendAlertToUser(toId, message);

                return true;
            }
            return (goDeep) && m_connector.SendGridMessage(toId, message, false, transactionId);
        }

        #endregion
    }
}
