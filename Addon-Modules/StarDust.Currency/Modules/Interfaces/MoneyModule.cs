using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace StarDust.Currency.Interfaces
{
    public class MoneyModule : IMoneyModule
    {
        protected StarDustConfig m_options = null;
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected bool m_enabled;
        protected IStarDustCurrencyService m_connector;
        protected const string version = "0.1";
        private readonly List<Scene> m_scenes = new List<Scene>();
        protected IRegistryCore m_registry = null;

        public virtual StarDustUserCurrency UserCurrencyInfo(UUID agentId)
        {
            throw new NotImplementedException();
        }

        public bool ObjectGiveMoney(UUID fromObjectID, UUID fromID, UUID toID, int amount)
        {
            return UserCurrencyTransfer(toID, fromID, UUID.Zero, fromObjectID, (uint)amount, "", TransactionType.ObjectPay, UUID.Random());
        }

        public virtual int Balance(IClientAPI client)
        {
            return (int)UserCurrencyInfo(client.AgentId).Amount;
        }

        #region IMoneyModule

        #region Charge/Transfer

        public bool Charge(IClientAPI from, int amount)
        {
            return Charge(from.AgentId, amount, "");
        }

        public bool Charge(UUID fromID, int amount, string text)
        {
            return UserCurrencyTransfer(UUID.Zero, fromID, UUID.Zero, UUID.Zero, (uint)amount, "", (TransactionType)0, UUID.Random());
        }

        public bool Transfer(UUID toID, UUID fromID, int amount, string description)
        {
            return UserCurrencyTransfer(toID, fromID, UUID.Zero, UUID.Zero, (uint)amount, description, (TransactionType)0, UUID.Random());
        }

        public bool Transfer(UUID toID, UUID fromID, int amount, string description, TransactionType type)
        {
            return UserCurrencyTransfer(toID, fromID, UUID.Zero, UUID.Zero, (uint)amount, description, (TransactionType)type, UUID.Random());
        }

        public bool Transfer(UUID toID, UUID fromID, UUID toObjectID, UUID fromObjectID, int amount, string description, TransactionType type)
        {
            return UserCurrencyTransfer(toID, fromID, toObjectID, fromObjectID, (uint)amount, description, type, UUID.Random());
        }

        /// <summary>
        /// This is the function that everythign really happens Grid and Region Side
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
            Scene scene = null;
            string fromObjectName = "";
            string toObjectName = "";


            if (fromObjectID != UUID.Zero)
            {
                ISceneChildEntity ce = FindObject(fromObjectID, out scene);
                if (ce != null)
                {
                    fromObjectName = ce.Name;
                    transactionPosition = ce.AbsolutePosition.ToString();
                }
            }

            if (toObjectID != UUID.Zero)
            {
                ISceneChildEntity ce = FindObject(toObjectID, out scene);
                if (ce != null)
                {
                    toObjectName = ce.Name;
                    transactionPosition = ce.AbsolutePosition.ToString();
                }
            }

            if (scene == null)
            {
                scene = FindScene(fromID);
                if ((scene != null) && (transactionPosition.Length == 0))
                    transactionPosition = scene.GetScenePresence(fromID).AbsolutePosition.ToString();
            }

            if ((scene == null) && (toID != UUID.Zero))
            {
                scene = FindScene(toID);
                if ((scene != null) && (transactionPosition.Length == 0))
                    transactionPosition = scene.GetScenePresence(toID).AbsolutePosition.ToString();
            }

            if (transactionPosition.Length == 0)
                transactionPosition = "Unknown";
            if ((scene == null) && (m_scenes.Count > 0))
                scene = m_scenes[0];

            RegionTransactionDetails r = new RegionTransactionDetails();
            ICapsService capsService;
            IClientCapsService client;
            IRegionClientCapsService regionClient;
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
                capsService = m_registry.RequestModuleInterface<ICapsService>();
                if (capsService != null)
                {
                    client = capsService.GetClientCapsService(fromID);
                    if (client != null)
                    {
                        regionClient = client.GetRootCapsService();
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



            IClientAPI icapiFrom = null;
            string fromName = "";
            if (fromID != UUID.Zero)
            {
                icapiFrom = GetUserClient(fromID);
                UserAccount ua = GetUserAccount(fromID);
                if (ua != null)
                    fromName = (icapiFrom != null) ? icapiFrom.Name : ua.Name;
                else
                {
                    if (!isgridServer)
                    {
                        ISceneChildEntity ce = FindObject(fromID, out scene);
                        if (ce != null)
                        {
                            fromObjectID = fromID;
                            fromID = ce.OwnerID;
                            ua = GetUserAccount(fromID);

                            if (ua != null) fromName = ua.Name;
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
            IClientAPI icapiTo = null;
            string toName = "";
            if (toID != UUID.Zero)
            {
                icapiTo = GetUserClient(toID);
                UserAccount ua = GetUserAccount(toID);
                if (ua != null)
                    toName = (icapiTo != null) ? icapiTo.Name : ua.Name;
                else
                {
                    if (!isgridServer)
                    {
                        ISceneChildEntity ce = FindObject(toID, out scene);
                        if (ce != null)
                        {
                            toObjectID = toID;
                            toID = ce.OwnerID;
                            ua = GetUserAccount(toID);
                            if (ua != null)
                            {
                                toName = ua.Name;
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


            if ((toObjectID != UUID.Zero) && (!isgridServer))
            {
                FireObjectPaid(toObjectID, fromID, int.Parse(amount.ToString()));
            }

            #endregion

            #region Perform transaction

            Transaction transaction = m_connector.
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
                else if (transaction.TypeOfTrans == TransactionType.Upload)
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
                SendGridMessage(transaction.FromID, "Transaction Failed", !isgridServer, transaction.TransactionID);
            }

            if ((toObjectID != UUID.Zero) && (!isgridServer))
            {
                FirePostObjectPaid(objectLocalId, regionHandel, fromID, int.Parse(amount.ToString()));
            }

            #endregion

            return returnvalue;
        }
        #endregion

        #region properties

        public int UploadCharge
        {
            get { return (m_options == null) ? 0 : m_options.PriceUpload; }
        }

        public int GroupCreationCharge
        {
            get { return (m_options == null) ? 0 : m_options.PriceGroupCreate; }
        }

        #endregion

        #region event
        public event ObjectPaid OnObjectPaid;
        public event PostObjectPaid OnPostObjectPaid;


        public void FireObjectPaid(UUID uuid1, UUID uuid2, int p)
        {
            if (OnObjectPaid != null)
                OnObjectPaid(uuid1, uuid2, p);
        }

        public void FirePostObjectPaid(uint localID, ulong regionHandle, UUID agentID, int amount)
        {
            if (OnPostObjectPaid != null)
                OnPostObjectPaid(localID, regionHandle, agentID, amount);
        }

        #endregion

        #endregion

        #region other

        protected void DisplayLogo()
        {
            m_log.Warn("====================================================================");
            m_log.Warn("====================== STARDUST CURRENCY 2011 ======================");
            m_log.Warn("====================================================================");
            m_log.Warn("[StarDustStartup]: Version: " + version + "\n");
        }

        protected bool CheckEnabled(string localOrRemote, IConfigSource source)
        {
            // check to see if it should be enabled and then load the config
            if (source == null) throw new ArgumentNullException("source");
            IConfig economyConfig = source.Configs["StarDustCurrency"];
            m_enabled = (economyConfig != null)
                            ? (economyConfig.GetString("CurrencyConnector", "Remote") == localOrRemote)
                            : "Remote" == localOrRemote;
            if (!m_enabled)
            {
                // check to ensure this is the right area that was trying to load before I spit out debug info for why it didn't load
                if ((economyConfig != null) && (economyConfig.GetString("CurrencyConnector", "Remote") != localOrRemote)) return m_enabled;
                else if ((economyConfig == null) && ("Remote" != localOrRemote)) return m_enabled;
                m_log.Info("Stardust is not loading.");
                if ("Remote" != localOrRemote)
                {
                    m_log.Info("economyConfig = " + (economyConfig == null) + " " +
                               ((economyConfig == null) ? "Bad" : "Good"));
                    if (economyConfig == null) return m_enabled;
                    m_log.Info("CurrencyConnector = " + economyConfig.GetString("CurrencyConnector", "Remote") + " " +
                               ((economyConfig.GetString("CurrencyConnector", "Remote") != localOrRemote)
                                    ? "Bad"
                                    : "Good"));
                }
                m_log.Info("End StarDust Info");
            }
            return m_enabled;
        }


        public virtual Scene FindScene(UUID fromID)
        {
            return null;
        }

        protected virtual ISceneChildEntity FindObject(UUID fromObjectID, out Scene scene)
        {
            scene = null;
            return null;
        }

        protected virtual UserAccount GetUserAccount(UUID fromID)
        {
            return null;
        }

        protected virtual IClientAPI GetUserClient(UUID fromID)
        {
            return null;
        }

        public virtual bool SendGridMessage(UUID toId, string message, bool goDeep, UUID transactionId)
        {
            return false;
        }

        #endregion

    }
}
