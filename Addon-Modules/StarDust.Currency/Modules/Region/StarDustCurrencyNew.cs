using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Aurora.Framework;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Region
{
    public class StarDustCurrencyNew : MoneyModule, ISharedRegionModule
    {
        public static RSACryptoServiceProvider rsa;
        private readonly List<IScene> m_scenes = new List<IScene> ();
        private int m_objectCapacity;

        #region ISharedRegionModule
        public void Initialise(IConfigSource source)
        {
            if (!CheckEnabled("Remote", source))
                return;
            DisplayLogo();
            //MainConsole.Instance.Commands.AddCommand("StarDust Generate Keys", "Generates the security keys for coms",
            //                                         "Generates the security keys for coms", GenerateKeys);
            rsa = new RSACryptoServiceProvider(new CspParameters(1)
            {
                KeyContainerName = "StarDustContainer",
                Flags = CspProviderFlags.UseMachineKeyStore,
                ProviderName = "Microsoft Strong Cryptographic Provider"
            });
        }

        public void AddRegion (IScene scene)
        {
            if (scene == null) throw new ArgumentNullException("scene");
            if (!m_enabled)
                return;

            if (m_connector == null) m_connector = scene.RequestModuleInterface<IStarDustCurrencyService>();
            if (m_connector == null)
            {
                m_log.Error("[StarDustCurrencyNew] IStarDustCurrencyService is null");
                return;
            }

            MainServer.Instance.AddStreamHandler(new StarDustRegionPostHandler("/StarDustRegion", this, 0, scene));

            m_objectCapacity = scene.RegionInfo.ObjectCapacity;
            scene.RegisterModuleInterface<IMoneyModule> (this);
            scene.RegisterModuleInterface<StarDustCurrencyNew> (this);
            m_scenes.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
            scene.EventManager.OnValidateBuyLand += ValidateLandBuy;

            m_log.DebugFormat("[DustCurrencyService] DustCurrencyService Initialize on {0} ", MainServer.Instance.ServerURI);
        }

        public void RegionLoaded (IScene scene)
        {
            if (m_connector == null) return;
            m_options = m_connector.GetConfig();
        }

        public void RemoveRegion (IScene scene)
        {
            // clean up on removing region
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
            scene.EventManager.OnValidateBuyLand -= ValidateLandBuy;

            scene.UnregisterModuleInterface<IMoneyModule>(this);

            MainServer.Instance.RemoveStreamHandler("POST", "/StarDustRegion");
            m_scenes.Remove(scene);
        }

        public void Close()
        {
            m_scenes.Clear();
        }

        public string Name
        {
            get { return "StarDustCurrency"; }
        }

        public Type ReplaceableInterface
        {
            get { return typeof(IMoneyModule); }
        }

        public void PostInitialise()
        {
            //throw new NotImplementedException();
        }
        #endregion

        #region IStarDustCurrencyService calls

        /// <summary>
        /// Get information about the given users currency
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public override StarDustUserCurrency UserCurrencyInfo(UUID agentId)
        {
            return m_connector.UserCurrencyInfo(agentId);
        }

        /// <summary>
        /// Update the currency for the given user (This does not update the user's balance!)
        /// </summary>
        /// <param name="agent"></param>
        // ReSharper disable UnusedMember.Local
        bool UserCurrencyUpdate(StarDustUserCurrency agent)
        // ReSharper restore UnusedMember.Local
        {
            return m_connector.UserCurrencyUpdate(agent);
        }

        #endregion

        #region region functions
        /// <summary>
        /// Locates a IClientAPI for the client specified
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        protected override IClientAPI GetUserClient(UUID agentId)
        {
            return (from scene in m_scenes
                    where scene.GetScenePresence(agentId) != null
                    where !scene.GetScenePresence(agentId).IsChildAgent
                    select scene.GetScenePresence(agentId).ControllingClient).FirstOrDefault();
        }

        protected override UserAccount GetUserAccount(UUID agentId)
        {
            return m_scenes[0].UserAccountService.GetUserAccount(UUID.Zero, agentId);
        }

        protected override ISceneChildEntity FindObject (UUID objectID, out IScene scene)
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

        public override IScene FindScene (UUID agentId)
        {
            return (from s in m_scenes
                    let presence = s.GetScenePresence(agentId)
                    where presence != null && !presence.IsChildAgent
                    select s).FirstOrDefault();
        }
        #endregion

        #region region client events

        private void OnNewClient(IClientAPI client)
        {
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest2;
            client.OnParcelBuyPass += ClientOnParcelBuyPass;
            client.SendMoneyBalance(UUID.Zero, true, new byte[0], Balance(client));
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
            UserCurrencyTransfer(toID, fromID, UUID.Zero, UUID.Zero, (uint)amount, description, (TransactionType)type, UUID.Random());
        }

        private bool ValidateLandBuy(EventManager.LandBuyArgs e)
        {
            return Transfer(e.parcelOwnerID, e.agentId, e.parcelPrice, "Land Purchase", TransactionType.Purchase);
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
                client.SendMoneyBalance(transactionId, true, new byte[0], Balance(client));
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
                bool giveResult = UserCurrencyTransfer(landParcel.LandData.OwnerID, fromID, UUID.Zero, UUID.Zero,
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

        #endregion

        #region IMoneyModule

        public override int Balance(IClientAPI client)
        {
            return (int)UserCurrencyInfo(client.AgentId).Amount;
        }

        public override GroupBalance GetGroupBalance(UUID groupID)
        {
            return m_connector.GetGroupBalance(groupID);
        }

        #endregion

        #region other


        /// <summary>
        /// All message for money actually go through this function. Which also update the balance
        /// </summary>
        /// <param name="toId"></param>
        /// <param name="message"></param>
        /// <param name="goDeep"></param>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        public override bool SendGridMessage(UUID toId, string message, bool goDeep, UUID transactionId)
        {
            IScene agentSp = FindScene (toId);
            if (agentSp == null)
                return (goDeep) ? m_connector.SendGridMessage(toId, message, false, transactionId) : false;
            else
            {
                IDialogModule dialogModule = agentSp.RequestModuleInterface<IDialogModule>();
                if (dialogModule != null)
                {
                    IClientAPI icapiTo = GetUserClient(toId);
                    if ((message.IndexOf("http") > -1) && (icapiTo != null))
                    {
                        icapiTo.SendMoneyBalance(transactionId, true, new byte[0], Balance(icapiTo));
                        dialogModule.SendUrlToUser(toId, "", UUID.Zero, UUID.Zero, false, message, message.Substring(message.IndexOf("http")));
                        icapiTo.SendAlertMessage(message.Substring(message.IndexOf("http")));
                    }
                    else if (icapiTo != null)
                        icapiTo.SendMoneyBalance(transactionId, true, Utils.StringToBytes(message), Balance(icapiTo));
                    else
                        dialogModule.SendAlertToUser(toId, message);

                    return true;
                }
                else
                    return (goDeep) ? m_connector.SendGridMessage(toId, message, false, transactionId) : false;
            }
        }

        #endregion

        #region console commands

        //private void GenerateKeys(string module, string[] cmd)
        //{
        //    StreamWriter writer = new StreamWriter("StarDustPrivateKey.xml");
        //    string publicPrivateKeyXML = rsa.ToXmlString(true);
        //    writer.Write(publicPrivateKeyXML);
        //    writer.Close();


        //    writer = new StreamWriter("StarDustPublicKey-" +  + ".xml");
        //    string publicOnlyKeyXML = rsa.ToXmlString(false);
        //    writer.Write(publicOnlyKeyXML);
        //    writer.Close();
        //}

        #endregion
    }


}
