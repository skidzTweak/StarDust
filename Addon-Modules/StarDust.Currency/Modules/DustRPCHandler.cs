using System;
using System.Collections;
using System.Net;
using System.Reflection;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;
using log4net;

namespace StarDust.Currency
{
    class DustRPCHandler
    {
        private readonly DustCurrencyService m_dustCurrencyService;
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        

        public DustRPCHandler(DustCurrencyService mainService, IConfigSource source, IRegistryCore registry)
        {
            m_dustCurrencyService = mainService;
            IHttpServer server = registry.RequestModuleInterface<ISimulationBase>().GetHttpServer((uint)source.Configs["Handlers"].GetInt("LLLoginHandlerPort", 0));
            server.AddXmlRPCHandler("getCurrencyQuote", QuoteFunc);
            server.AddXmlRPCHandler("buyCurrency", BuyFunc);
            server.AddXmlRPCHandler("preflightBuyLandPrep", PreflightBuyLandPrepFunc);
            server.AddXmlRPCHandler("buyLandPrep", LandBuyFunc);
            server.AddXmlRPCHandler("getBalance", GetbalanceFunc);
        }

        #region RPC Calls

        public XmlRpcResponse GetbalanceFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            m_log.Error("Stardust Remote procdure calls GetbalanceFunc was called.");
            throw new NotImplementedException();
        }
        public XmlRpcResponse LandBuyFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            m_log.Error("Stardust Remote procdure calls LandBuyFunc was called.");
            throw new NotImplementedException();
        }

        public XmlRpcResponse QuoteFunc(XmlRpcRequest request, IPEndPoint ep)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            XmlRpcResponse returnval = new XmlRpcResponse();

            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                uint amount = uint.Parse(requestData["currencyBuy"].ToString());
                UUID theagent = new UUID(requestData["agentId"].ToString());
                StarDustUserCurrency currency = m_dustCurrencyService.UserCurrencyInfo(theagent);
                bool successful = !(m_dustCurrencyService.m_options.MaxAmountPurchase < currency.RestrictPurchaseAmount + amount);
                if (!successful)
                    m_dustCurrencyService.SendGridMessage(theagent, string.Format("Amount was over the limit. {0} is the max amount.", m_dustCurrencyService.m_options.MaxAmountPurchase - currency.RestrictPurchaseAmount), false, UUID.Zero);
                returnval.Value = new Hashtable
                                          {
                                              {"success", successful},
                                              {
                                                  "currency",
                                                  new Hashtable
                                                      {
                                                          {
                                                              "estimatedCost",
                                                              Convert.ToInt32(
                                                                  Math.Round(((float.Parse(amount.ToString())/
                                                                               m_dustCurrencyService.m_options.
                                                                                   RealCurrencyConversionFactor) +
                                                                              ((float.Parse(amount.ToString())/
                                                                                m_dustCurrencyService.m_options.
                                                                                    RealCurrencyConversionFactor)*
                                                                               (m_dustCurrencyService.m_options.
                                                                                    AdditionPercentage/10000.0)) +
                                                                              (m_dustCurrencyService.m_options.
                                                                                   AdditionAmount/100.0))*100))
                                                              },
                                                          {"currencyBuy", (int) amount}
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
                {"errorURI", m_dustCurrencyService.m_options.ErrorURI}
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
                    
                    uint amountBuying = uint.Parse(requestData["currencyBuy"].ToString());
                    m_dustCurrencyService.StartPurchaseOrATMTransfer(agentId, amountBuying, PurchaseType.InWorldPurchaseOfCurrency, "");
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
                    {m_dustCurrencyService.m_options.UpgradeMembershipUri, "Premium Membership"}
                };

            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID agentId;
                UUID.TryParse((string)requestData["agentId"], out agentId);
                StarDustUserCurrency currency = m_dustCurrencyService.UserCurrencyInfo(agentId);
                IUserProfileInfo profile = DataManager.RequestPlugin<IProfileConnector>("IProfileConnector").GetUserProfile(agentId);


                IClientCapsService client = m_dustCurrencyService.Registry.RequestModuleInterface<ICapsService>().GetClientCapsService(agentId);
                OSDMap replyData = m_dustCurrencyService.GetParcelDetails(agentId);
                if (client != null)
                {
                    m_dustCurrencyService.SendGridMessage(agentId, String.Format(m_dustCurrencyService.m_options.MessgeBeforeBuyLand, profile.DisplayName, replyData.ContainsKey("SalePrice")), false, UUID.Zero);
                }
                if (replyData.ContainsKey("SalePrice"))
                {
                    // I think, this might be usable if they don't have the money
                    // Hashtable currencytable = new Hashtable { { "estimatedCost", replyData["SalePrice"].AsInteger() } };

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
                    // landuse.Add("action", m_DustCurrencyService.m_options.upgradeMembershipUri);
                    landuse.Add("action", needsUpgrade);

                    retparam.Add("success", true);
                    retparam.Add("currency", currency);
                    retparam.Add("membership", level);
                    retparam.Add("landuse", landuse);
                    retparam.Add("confirm", "asdfajsdkfjasdkfjalsdfjasdf");
                    ret.Value = retparam;
                }
            }

            return ret;
        }
        #endregion
    }
}
