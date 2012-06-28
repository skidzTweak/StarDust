using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using Aurora.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Services.Interfaces;
using StarDust.Currency;
using StarDust;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Grid
{
    class GiveStipends
    {
        private readonly Timer taskTimer = new Timer();
        private readonly bool m_enabled = false;
        private StarDustConfig m_options;
        readonly IScheduleService m_scheduler;
        private readonly IRegistryCore m_registry;
        private readonly DustCurrencyService m_dustCurrencyService;

        public GiveStipends(StarDustConfig options, IRegistryCore registry, DustCurrencyService dustCurrencyService)
        {
            m_enabled = options.GiveStipends;
            if (!m_enabled) return;

            m_dustCurrencyService = dustCurrencyService;
            m_options = options;
            m_registry = registry;
            taskTimer.Interval = 360000;
            taskTimer.Elapsed +=TimerElasped;
            m_scheduler = registry.RequestModuleInterface<IScheduleService>();
            m_scheduler.Register("StipendsPayout", StupendsPayOutEvent);
            if (m_options.StipendsLoadOldUsers) taskTimer.Enabled = true;
            registry.RequestModuleInterface<ISimulationBase>().EventManager.RegisterEventHandler("DeleteUserInformation", DeleteUserInformation);
            registry.RequestModuleInterface<ISimulationBase>().EventManager.RegisterEventHandler("CreateUserInformation", CreateUserInformation);
            registry.RequestModuleInterface<ISimulationBase>().EventManager.RegisterEventHandler("UpdateUserInformation", CreateUserInformation);
            

        }

        private object CreateUserInformation(string functionname, object parameters)
        {
            UUID userid = (UUID)parameters;
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = userService.GetUserAccount(UUID.Zero, userid);
            if (user == null) return null;
            if ((m_options.StipendsPremiumOnly) && ((user.UserFlags & 600) != 600)) return null;

            SchedulerItem i = m_scheduler.Get(user.PrincipalID.ToString(), "StipendsPayout");
            if (i != null) return null;
            RepeatType runevertype = (RepeatType)Enum.Parse(typeof(RepeatType), m_options.StipendsEveryType);
            int runevery = m_options.StipendsEvery;
            m_scheduler.Save(new SchedulerItem("StipendsPayout",
                                                OSDParser.SerializeJsonString(
                                                    new StipendsInfo() { AgentID = user.PrincipalID }.ToOSD()),
                                                false, UnixTimeStampToDateTime(user.Created), runevery,
                                                runevertype, user.PrincipalID) { HisotryKeep = true, HistoryReciept = true });
            return null;

        }

        private object DeleteUserInformation(string functionname, object parameters)
        {
            UUID user = (UUID)parameters;
            SchedulerItem i = m_scheduler.Get(user.ToString(), "StipendsPayout");
            if (i != null)
                m_scheduler.Remove(i.id);
            return null;
        }

        private object StupendsPayOutEvent(string functionName, object parameters)
        {
            if (functionName != "StipendsPayout") return null;
            StipendsInfo si = new StipendsInfo();
            si.FromOSD((OSDMap)OSDParser.DeserializeJson(parameters.ToString()));
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount ua = userService.GetUserAccount(UUID.Zero, si.AgentID);
            if ((ua != null) && (ua.UserFlags >= 0) && ((!m_options.StipendsPremiumOnly) || ((ua.UserLevel & 600) == 600)))
            {
                if (m_options.GiveStipendsOnlyWhenLoggedIn)
                {
                    ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                    IClientCapsService client = capsService.GetClientCapsService(ua.PrincipalID);
                    if (client == null) return "";
                }
                IMoneyModule mo = m_registry.RequestModuleInterface<IMoneyModule>();
                if (mo == null) return null;
                UUID transid = UUID.Random();
                if (m_dustCurrencyService.UserCurrencyTransfer(ua.PrincipalID, m_options.BankerPrincipalID, UUID.Zero, UUID.Zero, (uint)m_options.Stipend, "Stipend Payment", TransactionType.StipendPayment, transid))
                {
                    return transid.ToString();
                }
            }
            return "";
        }

        private void TimerElasped(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            taskTimer.Enabled = false;
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            List<UserAccount> users = new List<UserAccount>();
            users = userService.GetUserAccounts(UUID.Zero, 0, m_options.StipendsPremiumOnly ? 600 : 0);
            foreach (UserAccount user in users)
            {
                SchedulerItem i = m_scheduler.Get(user.PrincipalID.ToString(), "StipendsPayout");
                if (i != null) continue;
                RepeatType runevertype = (RepeatType)Enum.Parse(typeof(RepeatType), m_options.StipendsEveryType);
                int runevery = m_options.StipendsEvery;
                m_scheduler.Save(new SchedulerItem("StipendsPayout",
                                                   OSDParser.SerializeJsonString(
                                                       new StipendsInfo() {AgentID = user.PrincipalID}.ToOSD()),
                                                   false, UnixTimeStampToDateTime(user.Created), runevery,
                                                   runevertype, user.PrincipalID){HisotryKeep = true, HistoryReciept = true });
            }
        }

        private static DateTime UnixTimeStampToDateTime(int unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
    }

    public class StipendsInfo : IDataTransferable
    {
        public UUID AgentID { get; set; }

        #region IDataTransferable
        /// <summary>
        ///   Serialize the module to OSD
        /// </summary>
        /// <returns></returns>
        public override OSDMap ToOSD()
        {
            return new OSDMap()
                       {
                           {"AgentID", OSD.FromUUID(AgentID)}
                       };
        }

        /// <summary>
        ///   Deserialize the module from OSD
        /// </summary>
        /// <param name = "map"></param>
        public override void FromOSD(OSDMap map)
        {
            AgentID = map["AgentID"].AsUUID();
        }

        #endregion
    }
}
