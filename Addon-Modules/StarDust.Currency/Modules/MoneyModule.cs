using System.Collections.Generic;
using Aurora.Framework;
using Aurora.Simulation.Base;
using Nini.Config;
using OpenMetaverse;
using StarDust.Currency.Interfaces;
using System;
using Aurora.Framework.Modules;
using Aurora.Framework.Services;
using Aurora.Framework.PresenceInfo;
using Aurora.Framework.Servers;

namespace StarDust.Currency
{
    public class MoneyModule : IMoneyModule, IService
    {
        private bool m_enabled = false;
        private IRegistryCore m_registry;
        private IStarDustCurrencyService m_stardustservice;
        private StarDustConfig m_options;
        private IConfigSource m_config;

        public int ClientPort
        {
            get
            {
                return m_config.Configs["Handlers"].GetInt("LLLoginHandlerPort", (int)MainServer.Instance.Port);
            }
        }

        #region Implementation of IService

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            m_config = config;
            if (!CheckEnabled(config))
                return;
            m_enabled = true;
            m_registry.RegisterModuleInterface<IMoneyModule>(this);
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

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_stardustservice = m_registry.RequestModuleInterface<IStarDustCurrencyService>() as DustCurrencyService;
        }

        public void FinishedStartup()
        {
        }

        #endregion

        #region IMoneyModule

        #region Charge/Transfer

        public bool ObjectGiveMoney(UUID fromObjectID, UUID fromID, UUID toID, int amount)
        {
            return m_stardustservice.UserCurrencyTransfer(toID, fromID, UUID.Zero, fromObjectID, (uint)amount, "", TransactionType.ObjectPaysAvatar, UUID.Random());
        }

        public bool Charge(IClientAPI from, int amount)
        {
            return Charge(from.AgentId, amount, "");
        }

        public bool Charge(UUID fromID, int amount, string text)
        {
            return m_stardustservice.UserCurrencyTransfer(UUID.Zero, fromID, UUID.Zero, UUID.Zero, (uint)amount, "", 0, UUID.Random());
        }

        public bool Transfer(UUID toID, UUID fromID, int amount, string description)
        {
            return m_stardustservice.UserCurrencyTransfer(toID, fromID, UUID.Zero, UUID.Zero, (uint)amount, description, 0, UUID.Random());
        }

        public bool Transfer(UUID toID, UUID fromID, int amount, string description, TransactionType type)
        {
            return m_stardustservice.UserCurrencyTransfer(toID, fromID, UUID.Zero, UUID.Zero, (uint)amount, description, type, UUID.Random());
        }

        public bool Transfer(UUID toID, UUID fromID, UUID toObjectID, UUID fromObjectID, int amount, string description, TransactionType type)
        {
            return m_stardustservice.UserCurrencyTransfer(toID, fromID, toObjectID, fromObjectID, (uint)amount, description, type, UUID.Random());
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

        public StarDustConfig Options
        {
            get { return m_options; }
            set { m_options = value; }
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

        #region Balance

        public int Balance(UUID agentID)
        {
            return (int)m_stardustservice.UserCurrencyInfo(agentID).Amount;
        }

        public virtual int Balance(IClientAPI client)
        {
            return (int)m_stardustservice.UserCurrencyInfo(client.AgentId).Amount;
        }

        public List<GroupAccountHistory> GetTransactions(UUID groupID, UUID agentID, int currentInterval, int intervalDays)
        {
            // not done yet
            return new List<GroupAccountHistory>();
        }

        public virtual GroupBalance GetGroupBalance(UUID groupID)
        {
            return m_stardustservice.GetGroupBalance(groupID);
        }

        #endregion

        #endregion
    }
}
