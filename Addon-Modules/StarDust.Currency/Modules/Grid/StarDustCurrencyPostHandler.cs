using System;
using System.IO;
using System.Reflection;
using Aurora.Simulation.Base;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Grid
{
    class StarDustCurrencyPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IStarDustCurrencyService m_starDustCurrencyService;
        private readonly ulong m_regionHandle;
        private readonly IRegistryCore m_registry;

        public StarDustCurrencyPostHandler(string url, IStarDustCurrencyService service, ulong regionHandle, IRegistryCore registry) :
                base("POST", url)
        {
            m_starDustCurrencyService = service;
            m_regionHandle = regionHandle;
            m_registry = registry;
        }

        #region BaseStreamHandler

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            m_log.DebugFormat("[StarDustCurrencyPostHandler]: query String: {0}", body);

            try
            {
                OSDMap map = WebUtils.GetOSDMap(body);
                IGridRegistrationService urlModule =
                            m_registry.RequestModuleInterface<IGridRegistrationService>();
                if ((map == null) || (!map.ContainsKey("Method")) ||
                    ((urlModule != null) && (!urlModule.CheckThreatLevel("", m_regionHandle, map["Method"].AsString(), ThreatLevel.High)))) 
                    return FailureResult();
                
                switch (map["Method"].AsString())
                {
                    case "usercurrencyinfo":
                        return UserCurrencyInfo(map);

                    case "usercurrencyupdate":
                        return UserCurrencyUpdate(map);

                    case "usercurrencytransfer":
                        return UserCurrencyTransfer(map);

                    case "getconfig":
                        return GetConfig(map);

                    case "sendgridmessage":
                        return SendGridMessage(map);
                }
                m_log.DebugFormat("[CURRENCY HANDLER]: unknown method {0} request {1}", map["Method"].AsString().Length, map["Method"].AsString());
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[CURRENCY HANDLER]: Exception {0}", e);
            }
            return FailureResult();
        }

        private byte[] SendGridMessage(OSDMap map)
        {
            return m_starDustCurrencyService.SendGridMessage(map["toId"].AsUUID(), map["message"].ToString(), map["goDeep"].AsBoolean(), map["transactionId"].AsUUID())
                       ? SuccessfulResult()
                       : FailureResult();
        }

        private byte[] GetConfig(OSDMap map)
        {
            OSDMap map2 = m_starDustCurrencyService.GetConfig().ToOSD();
            map2.Add("Result", "Successful");
            return Return(map2);
        }

        #endregion
        #region Currency Functions
        private byte[] UserCurrencyTransfer(OSDMap request)
        {
            Transaction trans = new Transaction();
            if (trans.FromOSD(request))
            {
                OSDMap trans2 = m_starDustCurrencyService.UserCurrencyTransfer(trans).ToOSD();
                trans2.Add("Result", "Successful");
                return Return(trans2);
            }
            return FailureResult();
        }

        private byte[] UserCurrencyUpdate(OSDMap request)
        {
            StarDustUserCurrency agent = new StarDustUserCurrency();
            if (agent.FromOSD(request) &&
                m_starDustCurrencyService.UserCurrencyUpdate(agent))
                return SuccessfulResult();
            return FailureResult();
        }

        private byte[] UserCurrencyInfo(OSDMap request)
        {
            UUID agentId;
            if (UUID.TryParse(request["AgentId"].AsString(), out agentId))
            {
                OSDMap results = m_starDustCurrencyService.UserCurrencyInfo(agentId).ToOSD();
                results.Add("Result", "Successful");
                return Return(results);
            }
            return FailureResult();
        }
        #endregion
        #region Misc

        private byte[] FailureResult()
        {
            return Return(new OSDMap
                              {
                        {"Result", "Failure"}
                    });
        }

        private byte[] SuccessfulResult()
        {
            return Return(new OSDMap
                              {
                        {"Result", "Successful"}
                    });
        }

        private byte[] Return(OSDMap result)
        {
            //m_log.DebugFormat("[AuroraDataServerPostHandler]: resp string: {0}", xmlString);
            return Util.UTF8.GetBytes(OSDParser.SerializeJsonString(result));
        }

        #endregion
    }
}
