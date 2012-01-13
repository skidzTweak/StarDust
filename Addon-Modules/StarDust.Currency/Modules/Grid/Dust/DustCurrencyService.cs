using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Grid.Dust
{
    public class DustCurrencyService : MoneyModule, IStarDustCurrencyService, IService, IGridRegistrationUrlModule
    {
        private IStarDustCurrencyConnector m_database;
        private readonly Dictionary<string, object> m_regions = new Dictionary<string, object>();

        #region Properties
        public string Name
        {
            get { return GetType().Name; }
        }

        #endregion

        #region IService
        public void Initialize(IConfigSource source, IRegistryCore registry)
        {
            if (!CheckEnabled("Local", source))
                return;
            DisplayLogo();

            m_connector = this;
            m_registry = registry;
            m_registry.RegisterModuleInterface<IStarDustCurrencyService>(this);
            m_registry.RegisterModuleInterface<IMoneyModule>(this);
            m_options = new StarDustConfig(source.Configs["StarDustCurrency"]);

            IConfig handlerConfig = source.Configs["Handlers"];
            IHttpServer server = registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer ((uint)source.Configs["Handlers"].GetInt ("LLLoginHandlerPort", 0));
            m_registry.RequestModuleInterface<IGridRegistrationService>().RegisterModule(this);

            server.AddXmlRPCHandler("getCurrencyQuote", QuoteFunc);
            server.AddXmlRPCHandler("buyCurrency", BuyFunc);
            server.AddXmlRPCHandler("preflightBuyLandPrep", PreflightBuyLandPrepFunc);
            server.AddXmlRPCHandler("buyLandPrep", LandBuyFunc);
            server.AddXmlRPCHandler("getBalance", GetbalanceFunc);

            string Password = handlerConfig.GetString("WireduxHandlerPassword", String.Empty);
            if (Password == "") return;
            IHttpServer m_server = registry.RequestModuleInterface<ISimulationBase>().GetHttpServer(handlerConfig.GetUInt("WireduxHandlerPort"));
            m_server.AddStreamHandler(new StarDustCurrencyPostHandlerWebUI("/StarDustWebUI", this, m_registry, Password, m_options));
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_database = DataManager.RequestPlugin<IStarDustCurrencyConnector>();
        }

        public void FinishedStartup()
        {

        }
        #endregion

        #region IStarDustCurrencyService

        public override StarDustUserCurrency UserCurrencyInfo(UUID agentID)
        {
            return m_database.GetUserCurrency(agentID);
        }

        public bool UserCurrencyUpdate(StarDustUserCurrency agent)
        {
            return m_database.UserCurrencyUpdate(agent);
        }

        public Transaction UserCurrencyTransfer(Transaction transaction)
        {
            transaction = m_database.UserCurrencyTransaction(transaction);
            return transaction;
        }

        public StarDustConfig GetConfig()
        {
            return m_options;
        }

        #endregion

        #region RPC Calls

        public XmlRpcResponse GetbalanceFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            throw new NotImplementedException();
        }
        public XmlRpcResponse LandBuyFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            throw new NotImplementedException();
        }

        public XmlRpcResponse QuoteFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            XmlRpcResponse returnval = new XmlRpcResponse();

            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {


                uint amount = uint.Parse(requestData["currencyBuy"].ToString());
                returnval.Value = new Hashtable
                                          {
                                              {"success", true},
                                              {
                                                  "currency",
                                                  new Hashtable
                                                      {
                                                          {
                                                              "estimatedCost",
                                                              Convert.ToInt32(Math.Round(((float.Parse(amount.ToString()) / m_options.RealCurrencyConversionFactor) + ((float.Parse(amount.ToString()) / m_options.RealCurrencyConversionFactor) * (m_options.AdditionPercentage / 10000.0)) + (m_options.AdditionAmount / 100.0)) * 100))},
                                                          {"currencyBuy", (int)amount}
                                                      }
                                                  },
                                              {"confirm", "asdfad9fj39ma9fj"}
                                          };
                return returnval;

            }
            returnval.Value = new Hashtable
            {
                {"success", false},
                {"errorMessage", "Invalid parameters passed to the quote box"},
                {"errorURI", m_options.ErrorURI}
            };
            return returnval;
        }

        public XmlRpcResponse BuyFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            bool success = false;
            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID agentId;
                if (UUID.TryParse((string)requestData["agentId"], out agentId))
                {
                    UserAccount ua = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(UUID.Zero, agentId);
                    IClientCapsService client = m_registry.RequestModuleInterface<ICapsService>().GetClientCapsService(agentId);
                    if ((client != null) && (ua != null))
                    {
                        IRegionClientCapsService regionClient = client.GetRootCapsService();
                        UUID purchaseID = UUID.Random();

                        success = m_database.UserCurrencyBuy(purchaseID, agentId, ua.Name, uint.Parse(requestData["currencyBuy"].ToString()),
                                                             m_options.RealCurrencyConversionFactor,
                                                             new RegionTransactionDetails()
                                                             {
                                                                 RegionID = regionClient.Region.RegionID,
                                                                 RegionName = regionClient.Region.RegionName
                                                             }
                                                             );

                        if (m_options.AutoApplyCurrency && success)
                        {
                            Transaction transaction;
                            m_database.UserCurrencyBuyComplete(purchaseID, 1, "AutoComplete",
                                                               m_options.AutoApplyCurrency.ToString(), "Auto Complete",
                                                               out transaction);
                            UserCurrencyTransfer(transaction.ToID, m_options.BankerPrincipalID, UUID.Zero, UUID.Zero,
                                                 transaction.Amount, "Currency Purchase",
                                                 TransactionType.SystemGenerated, transaction.TransactionID);
                        }
                        else if (success && m_options.AfterCurrencyPurchaseMessage != string.Empty)
                            SendGridMessage(agentId, String.Format(m_options.AfterCurrencyPurchaseMessage, purchaseID.ToString()), false, UUID.Zero);
                    }
                }
            }
            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable returnresp = new Hashtable { { "success", success } };
            returnval.Value = returnresp;
            return returnval;
        }

        public XmlRpcResponse PreflightBuyLandPrepFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();

            Hashtable membershiplevels = new Hashtable();
            membershiplevels.Add("levels", membershiplevels);

            Hashtable landuse = new Hashtable();

            Hashtable level = new Hashtable
                {
                    {"id", "00000000-0000-0000-0000-000000000000"},
                    {m_options.UpgradeMembershipUri, "Premium Membership"}
                };

            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID agentId;
                UUID.TryParse((string)requestData["agentId"], out agentId);
                StarDustUserCurrency currency = UserCurrencyInfo(agentId);
                IUserProfileInfo profile = DataManager.RequestPlugin<IProfileConnector>("IProfileConnector").GetUserProfile(agentId);


                IClientCapsService client = m_registry.RequestModuleInterface<ICapsService>().GetClientCapsService(agentId);
                OSDMap replyData = GetParcelDetails(agentId);
                if (client != null)
                {
                    SendGridMessage(agentId, String.Format(m_options.MessgeBeforeBuyLand, profile.DisplayName, replyData.ContainsKey("SalePrice")), false, UUID.Zero);
                }
                if (replyData.ContainsKey("SalePrice"))
                {
                    Hashtable currencytable = new Hashtable { { "estimatedCost", replyData["SalePrice"].AsInteger() } };

                    int landTierNeeded = (int)(currency.LandInUse + replyData["Area"].AsInteger());
                    bool needsUpgrade = false;
                    switch (profile.MembershipGroup)
                    {
                        case "Premium":
                        case "":
                            needsUpgrade = landTierNeeded >= currency.Tier;
                            break;
                        case "Banned":
                            needsUpgrade = true;
                            break;
                    }
                    // landuse.Add("action", m_options.upgradeMembershipUri);
                    landuse.Add("action", needsUpgrade);

                    retparam.Add("success", true);
                    retparam.Add("currency", currency);
                    retparam.Add("membership", level);
                    retparam.Add("landuse", landuse);
                    retparam.Add("confirm", "asdfajsdkfjasdkfjalsdfjasdf");
                    ret.Value = retparam;
                }
                else
                {

                }
            }

            return ret;
        }
        #endregion

        #region parcelFunctions
        private OSDMap GetParcelDetails(UUID agentId)
        {
            try
            {
                ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                IClientCapsService client = capsService.GetClientCapsService(agentId);
                if (client != null)
                {
                    IRegionClientCapsService regionClient = client.GetRootCapsService();
                    if (regionClient != null)
                    {
                        string serverURI = regionClient.Region.ServerURI + "/StarDustRegion";
                        OSDMap replyData = WebUtils.PostToService(serverURI, new OSDMap
                                                                                 {
                                                                                     {"Method", "parceldetails"},
                                                                                     {"agentid", agentId}
                                                                                 }, true, true);
                        if (replyData["Success"].AsBoolean())
                        {
                            if (replyData["_Result"].Type != OSDType.Map)
                            {
                                // don't return check the other servers uris
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI +
                                           ", connection did not have all the required data.");
                            }
                            else
                            {
                                OSDMap innerReply = (OSDMap)replyData["_Result"];
                                if (innerReply["Result"].AsString() == "Successful")
                                    return innerReply;
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI + ", " +
                                           innerReply["Result"]);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[CURRENCY CONNECTOR] UserCurrencyUpdate to m_ServerURI", e);
            }
            return null;
        }

        public override bool SendGridMessage(UUID toId, string message, bool goDeep, UUID transactionId)
        {
            try
            {
                ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                IClientCapsService client = capsService.GetClientCapsService(toId);
                if (client != null)
                {
                    IRegionClientCapsService regionClient = client.GetRootCapsService();
                    if (regionClient != null)
                    {
                        string serverURI = regionClient.Region.ServerURI + "/StarDustRegion";
                        OSDMap replyData = WebUtils.PostToService(serverURI, new OSDMap
                                                                                 {
                                                                                     {"Method", "sendgridmessage"},
                                                                                     {"toId", toId},
                                                                                     {"message", message},
                                                                                     {"goDeep", false}, 
                                                                                     {"transactionId", transactionId}
                                                                                 }, true, true);
                        if (replyData["Success"].AsBoolean())
                        {
                            if (replyData["_Result"].Type != OSDType.Map)
                            {
                                // don't return check the other servers uris
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI);
                            }
                            else
                            {
                                OSDMap innerReply = (OSDMap)replyData["_Result"];
                                if (innerReply["Result"].AsString() == "Successful")
                                    return true;
                                m_log.Warn("[CURRENCY CONNECTOR]: Unable to connect successfully to " + serverURI + ", " +
                                           innerReply["Result"]);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[CURRENCY CONNECTOR] UserCurrencyUpdate to m_ServerURI", e);
            }
            return false;
        }

        #endregion

        #region Agent functions

        protected override UserAccount GetUserAccount(UUID agentId)
        {
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = userService.GetUserAccount(new UUID(), agentId);
            if (user == null)
            {
                m_log.Info("[DustCurrencyService] Unable to find agent.");
                return null;
            }
            return user;
        }

        #endregion

        #region IGridRegionModuleRegister

        public string UrlName
        {
            get { return "StarDustURI"; }
        }

        public string GetUrlForRegisteringClient(string sessionID, uint port)
        {
            string url = "/StarDust" + UUID.Random();
            IHttpServer server = m_registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (port);
            m_options.CurrencyInHandlerPort = server.Port;
            server.AddStreamHandler(new StarDustCurrencyPostHandler(url, this, m_registry, sessionID));
            m_log.DebugFormat("[DustCurrencyService] GetUrlForRegisteringClient {0}", server.ServerURI);
            return url;
        }

        public void AddExistingUrlForClient(string sessionID, string url, uint port)
        {
            if (url == "")
                return;
            IHttpServer server = m_registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (port);
            m_options.CurrencyInHandlerPort = server.Port;
            server.AddStreamHandler(new StarDustCurrencyPostHandler(url, this, m_registry, sessionID));
            m_log.DebugFormat("[DustCurrencyService] AddExistingUrlForClient {0}", server.ServerURI);
        }

        public void RemoveUrlForClient(string sessionID, string url, uint port)
        {
            IHttpServer server = m_registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (port);
            server.RemoveHTTPHandler("POST", url);
        }

        #endregion

        #region WebUI Functions

        public bool CheckiFAlreadyComplete(OSDMap PayPalResponse)
        {
            return m_database.CheckIfPurchaseComplete(PayPalResponse);
        }

        public bool FinishPurchase(OSDMap PayPalResponse, string rawResponse)
        {
            Transaction transaction;
            int purchaseType;
            if (m_database.FinishPurchase(PayPalResponse, rawResponse, out transaction, out purchaseType))
            {
                if (purchaseType == 1)
                {
                    if (UserCurrencyTransfer(transaction.ToID, m_options.BankerPrincipalID, UUID.Zero, UUID.Zero,
                                                 transaction.Amount, "Currency Purchase",
                                                 TransactionType.SystemGenerated, transaction.TransactionID))
                    {
                        return true;
                    }
                }
                return true;
            }
            return false;
        }

        public OSDMap PrePurchaseCheck(UUID purchaseId)
        {
            return m_database.PrePurchaseCheck(purchaseId);
        }

        public OSDMap OrderSubscription(UUID toId, string RegionName, string notes, string subscription_id)
        {

            string toName = GetUserAccount(toId).Name;
            return m_database.OrderSubscription(toId, toName, RegionName, notes, subscription_id);
        }

        #endregion
    }


}
