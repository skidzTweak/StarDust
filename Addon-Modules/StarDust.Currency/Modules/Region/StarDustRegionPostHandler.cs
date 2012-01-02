using System;
using System.IO;
using System.Reflection;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using log4net;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Region
{
    class StarDustRegionPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly StarDustCurrencyNew m_currenyService;

        public StarDustRegionPostHandler(string url, StarDustCurrencyNew service, ulong regionHandle, IRegistryCore registry) :
            base("POST", url)
        {
            m_currenyService = service;
        }

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            m_log.DebugFormat("[StarDustRegionPostHandler]: query String: {0}", body);

            try
            {
                OSDMap map = WebUtils.GetOSDMap(body);
                if ((map == null) || (!map.ContainsKey("Method")))
                    return FailureResult();

                switch (map["Method"].AsString())
                {
                    case "parceldetails":
                        return ParcelDetails(map);
                    case "sendgridmessage":
                        return SendGridMessage(map);
                }
                m_log.DebugFormat("[StarDustRegionPostHandler]: unknown method {0} request {1}", map["Method"].AsString().Length, map["Method"].AsString());
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[StarDustRegionPostHandler]: Exception {0}", e);
            }
            return FailureResult();
        }

        private byte[] SendGridMessage(OSDMap map)
        {
            return m_currenyService.SendGridMessage(map["toId"].AsUUID(), map["message"].AsString(), false, map["transactionId"].AsUUID())
                       ? SuccessfulResult()
                       : FailureResult();
        }

        #region Functions
        private byte[] ParcelDetails(OSDMap map)
        {
            IScenePresence sp;
            if (
                (map.ContainsKey("agentid")) &&
                m_currenyService.FindScene(map["agentid"].AsUUID()).TryGetScenePresence(map["agentid"].AsUUID(), out sp)
                )
            {
                IParcelManagementModule parcelManagement = sp.Scene.RequestModuleInterface<IParcelManagementModule>();
                ILandObject parcel = parcelManagement.GetLandObject(sp.AbsolutePosition.X, sp.AbsolutePosition.Y);
                OSDMap results = parcel.LandData.ToOSD();
                results.Add("Result", "Successful");
                return Util.UTF8.GetBytes(OSDParser.SerializeJsonString(results));
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
