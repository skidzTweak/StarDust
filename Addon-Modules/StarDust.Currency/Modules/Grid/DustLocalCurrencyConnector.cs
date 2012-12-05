using System;
using System.Collections.Generic;
using System.Reflection;
using Aurora.DataManager;
using Aurora.Framework;
using StarDust.Currency.Region;
using log4net;
using OpenMetaverse;
using Nini.Config;
using OpenMetaverse.StructuredData;
using OpenSim.Services.Interfaces;
using StarDust.Currency.Interfaces;

namespace StarDust.Currency.Grid
{
    public class DustLocalCurrencyConnector : IStarDustCurrencyConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IGenericData m_gd;
        private bool m_enabled;
        private StarDustConfig m_options;
        private IRegistryCore m_registry;

        #region IAuroraDataPlugin

        public void Initialize(IGenericData gd, IConfigSource source, IRegistryCore registry, string defaultConnectionString)
        {
            if (source.Configs["AuroraConnectors"].GetString("CurrencyConnector", "LocalConnector") != "LocalConnector")
                return;
            m_gd = gd;
            m_registry = registry;

            if (source.Configs["Currency"].GetString("Module", "") != "StarDust")
                return;

            IConfig economyConfig = source.Configs["StarDustCurrency"];
            m_enabled = ((economyConfig != null) &&
                (economyConfig.GetString("CurrencyModule", "Dust") == "Dust") &&
                (economyConfig.GetString("CurrencyConnector", "Local") == "Local"));

            if (!m_enabled)
                return;

            if (source.Configs[Name] != null)
                defaultConnectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);

            gd.ConnectToDatabase(defaultConnectionString, "Currency", true);
            DataManager.RegisterPlugin(Name, this);

            m_options = new StarDustConfig(economyConfig);

            MainConsole.Instance.Commands.AddCommand("money add", "money add", "Adds money to a user's account.",
                AddMoney);
            MainConsole.Instance.Commands.AddCommand("money set", "money set", "Sets the amount of money a user has.",
                AddMoney);
            MainConsole.Instance.Commands.AddCommand("money get", "money get", "Gets the amount of money a user has.",
                AddMoney);
        }

        public string Name
        {
            get { return "IStarDustCurrencyConnector"; }
        }

        public void Dispose()
        {
        }

        #endregion

        #region Console Functiosn

        public void AddMoney(string[] cmd)
        {
            string name = MainConsole.Instance.Prompt("User Name: ");
            uint amount = 0;
            while(!uint.TryParse(MainConsole.Instance.Prompt("Amount: ", "0"), out amount))
                MainConsole.Instance.Info("Bad input, must be a number > 0");

            UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(new List<UUID> { UUID.Zero }, name);
            if(account == null)
            {
                MainConsole.Instance.Info("No account found");
                return;
            }
            var currency = GetUserCurrency(account.PrincipalID);
            Transaction transaction;
            if (!WriteHistory(new Transaction() 
            {
                Amount = currency.Amount + amount,
                Complete = 1,
                Description = "Console command set money to " + currency.Amount + amount,
                CompleteReason = "Console command set money to " + currency.Amount + amount,
                FromID = UUID.Zero,
                FromBalance = 0,
                FromName = "",
                FromObjectID = UUID.Zero,
                FromObjectName = "",
                ToBalance = currency.Amount + amount,
                ToID = account.PrincipalID,
                ToName = account.Name,
                ToObjectID = UUID.Zero,
                ToObjectName = "",
                TransactionID = UUID.Random(),
                TypeOfTrans = TransactionType.SystemGenerated 
            } , out transaction))
            {
                MainConsole.Instance.Info("Failed to write transaction history");
            }
            m_gd.Update("stardust_currency",
                        new Dictionary<string, object> { 
                        {
                            "Amount", currency.Amount + amount }
                        }, null, new QueryFilter() 
                        { 
                            andFilters = new Dictionary<string, object> { { "PrincipalID", account.PrincipalID } }
                        }, null, null);
            MainConsole.Instance.Info(account.Name + " now has $" + (currency.Amount + amount));
        }

        public void SetMoney(string[] cmd)
        {
            string name = MainConsole.Instance.Prompt("User Name: ");
            uint amount = 0;
            while(!uint.TryParse(MainConsole.Instance.Prompt("Set User's Money Amount: ", "0"), out amount))
                MainConsole.Instance.Info("Bad input, must be a number > 0");

            UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(new List<UUID>{UUID.Zero}, name);
            if(account == null)
            {
                MainConsole.Instance.Info("No account found");
                return;
            }
            var currency = GetUserCurrency(account.PrincipalID);
            Transaction transaction;
            if (!WriteHistory(new Transaction() 
            {
                Amount = amount,
                Complete = 1,
                Description = "Console command set money to " + amount,
                CompleteReason = "Console command set money to " + amount,
                FromID = UUID.Zero,
                FromBalance = 0,
                FromName = "",
                FromObjectID = UUID.Zero,
                FromObjectName = "",
                ToBalance = amount,
                ToID = account.PrincipalID,
                ToName = account.Name,
                ToObjectID = UUID.Zero,
                ToObjectName = "",
                TransactionID = UUID.Random(),
                TypeOfTrans = TransactionType.SystemGenerated 
            } , out transaction))
            {
                MainConsole.Instance.Info("Failed to write transaction history");
            }
            m_gd.Update("stardust_currency",
                        new Dictionary<string, object> { 
                        {
                            "Amount", amount }
                        }, null, new QueryFilter() 
                        { 
                            andFilters = new Dictionary<string, object> { { "PrincipalID", account.PrincipalID } }
                        }, null, null);
            MainConsole.Instance.Info(account.Name + " now has $" + amount);
        }

        public void GetMoney(string[] cmd)
        {
            string name = MainConsole.Instance.Prompt("User Name: ");
            uint amount = 0;
            while(!uint.TryParse(MainConsole.Instance.Prompt("Set User's Money Amount: ", "0"), out amount))
                MainConsole.Instance.Info("Bad input, must be a number > 0");

            UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(new List<UUID>{UUID.Zero}, name);
            if(account == null)
            {
                MainConsole.Instance.Info("No account found");
                return;
            }
            var currency = GetUserCurrency(account.PrincipalID);
            MainConsole.Instance.Info(account.Name + " has $" + currency.Amount);
        }

        #endregion

        #region public currency functions

        public StarDustUserCurrency GetUserCurrency(UUID agentId)
        {
            StarDustUserCurrency starDustUser = new StarDustUserCurrency { PrincipalID = agentId, Amount = 0, LandInUse = 0, Tier = 0 };
            Dictionary<string, object> where = new Dictionary<string, object>(1);
            where["PrincipalID"] = agentId;
            List<string> query = m_gd.Query(new string[] { "*" }, "stardust_currency", new QueryFilter()
            {
                andFilters = where
            }, null, null, null);

            if (query.Count == 0)
            {
                UserCurrencyCreate(agentId);
                return starDustUser;
            }
            starDustUser.FromArray(query);
            return starDustUser;
        }

        public Transaction UserCurrencyTransaction(Transaction transaction)
        {
            UserCurrencyTransaction(transaction, out transaction);
            return transaction;
        }

        public bool UserCurrencyUpdate(StarDustUserCurrency agent)
        {
            m_gd.Update("stardust_currency",
                        new Dictionary<string, object>
                            {
                                {"LandInUse", agent.LandInUse}, 
                                {"Tier", agent.Tier},
                                {"IsGroup", agent.IsGroup},
                                {"RestrictedAmount", agent.RestrictedAmount},
                                {"RestrictPurchaseAmount", agent.RestrictPurchaseAmount}
                            }, null,
                        new QueryFilter()
                            {andFilters = new Dictionary<string, object> {{"PrincipalID", agent.PrincipalID}}}
                        , null, null);
            return true;
        }


        public GroupBalance GetGroupBalance(UUID groupID)
        {
            GroupBalance gb = new GroupBalance()
                                  {
                                      GroupFee = 0,
                                      LandFee = 0,
                                      ObjectFee = 0,
                                      ParcelDirectoryFee = 0,
                                      TotalTierCredits = 0,
                                      TotalTierDebit = 0,
                                      StartingDate = DateTime.UtcNow
                                  };
            Dictionary<string, object> where = new Dictionary<string, object>(1);
            where["PrincipalID"] = groupID;
            List<string> queryResults = m_gd.Query(new string[] { "*" }, "stardust_currency", new QueryFilter()
            {
                andFilters = where
            }, null, null, null);

            if (queryResults.Count == 0)
            {
                GroupCurrencyCreate(groupID);
                return gb;
            }

            int.TryParse(queryResults[1], out gb.TotalTierCredits);
            return gb;

        }

        #endregion

        #region purchase currency

        /// <summary>
        /// User purchased currency, we are saving it here. But it is not yet complete.
        /// </summary>
        /// <param name="purchaseID"></param>
        /// <param name="principalID"></param>
        /// <param name="userName"></param>
        /// <param name="amount"></param>
        /// <param name="conversionFactor"></param>
        /// <param name="region"></param>
        /// <param name="puchaseType"> </param>
        /// <returns></returns>
        public bool UserCurrencyBuy(UUID purchaseID, UUID principalID, string userName, uint amount, float conversionFactor, RegionTransactionDetails region, int puchaseType)
        {
            List<object> values = new List<object>
            {
                purchaseID.ToString(),                  // PurchaseID
                puchaseType,                            // PurchaseType
                principalID.ToString(),                 // PrincipalID
                userName,                               // PrincipalID
                amount,                                 // Amount
                Convert.ToInt32(Math.Round(((float.Parse(amount.ToString()) / m_options.RealCurrencyConversionFactor) + ((float.Parse(amount.ToString()) / m_options.RealCurrencyConversionFactor) * (m_options.AdditionPercentage / 10000.0)) + (m_options.AdditionAmount / 100.0)) * 100)),
                Convert.ToInt32(conversionFactor),// ConversionFactor
                region.RegionName,                      // RegionName
                region.RegionID.ToString(),             // RegionID
                region.RegionPosition,                  // RegionPos
                0,                                      // Complete
                "",                                     // CompleteMethod
                "",                                     // CompleteReference
                "",                                     // TransactionID
                Utils.GetUnixTime(),                    // Created
                Utils.GetUnixTime(),                    // Updated
                "",                                     // pyapal raw data
                ""                                      //notes
            };
            m_gd.Insert("stardust_purchased", values.ToArray());
            return true;
        }

        /// <summary>
        /// This function transfered a past purchase of money to their account
        /// </summary>
        /// <param name="purchaseID"></param>
        /// <param name="isComplete"></param>
        /// <param name="completeMethod"></param>
        /// <param name="completeReference"></param>
        /// <param name="rawdata"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public bool UserCurrencyBuyComplete(UUID purchaseID, int isComplete, string completeMethod, string completeReference, string rawdata, out Transaction transaction)
        {
            Transaction trans = TransactionFromPurchase(purchaseID);
            if (trans.Complete == 0)
            {
                Dictionary<string, object> where = new Dictionary<string, object>() { { "PurchaseID", purchaseID.ToString() } };
                m_gd.Update("stardust_purchased",
                            new Dictionary<string, object>
                                {
                                    {"TransactionID", trans.TransactionID},
                                    {"Complete", isComplete},
                                    {"CompleteMethod", completeMethod},
                                    {"CompleteReference", completeReference},
                                    {"Updated", Utils.GetUnixTime()},
                                    {"RawPayPalTransactionData", rawdata}
                                }, null,
                            new QueryFilter() { andFilters = where }, null, null);
                transaction = TransactionFromPurchase(purchaseID);
                return true;
            }
            transaction = trans;
            m_log.WarnFormat("[DustLocalCurrencyConnector] Purchase ID {0} is already complete", purchaseID);
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="purchaseID"></param>
        /// <returns></returns>
        private Transaction TransactionFromPurchase(UUID purchaseID)
        {
            Dictionary<string, object> where = new Dictionary<string, object>(1);
            where["PurchaseID"] = purchaseID;
            List<string> query = m_gd.Query(new string[] { "Amount", "Complete", "Updated", "PrincipalID", "RegionName", "RegionID", "RegionPos", "userName", "PurchaseType", "TransactionID" }, "stardust_purchased", new QueryFilter()
            {
                andFilters = where
            }, null, null, null);


            if (query.Count == 0)
            {
                m_log.Warn("[DustLocalCurrencyConnector] Purchase ID not found");
                return new Transaction();
            }
            return new Transaction
            {
                Amount = uint.Parse(query[0]),
                Updated = Utils.GetUnixTime(),
                Complete = int.Parse(query[1]),
                Created = Utils.GetUnixTime(),
                FromID = m_options.BankerPrincipalID,
                FromName = "Banker",
                Region = new RegionTransactionDetails
                {
                    RegionID = UUID.Parse(query[5]),
                    RegionName = query[4],
                    RegionPosition = query[6]
                },
                ToID = UUID.Parse(query[3]),
                ToName = query[7],
                Description = (query[6] == "1") ? "Purchase Currency" : "Purchase Region",
                TransactionID = ((query[9] == "") || ((query[9] == UUID.Zero.ToString()))) ? UUID.Random() : UUID.Parse(query[9])
            };
        }

        public bool FinishPurchase(OSDMap payPalResponse, string raw, out Transaction transaction, out int purchaseType)
        {
            if (payPalResponse.ContainsKey("custom"))
            {
                UUID purchaseID = payPalResponse["custom"].AsUUID();

                Dictionary<string, object> where = new Dictionary<string, object>(1);
                where["PurchaseID"] = purchaseID;
                List<string> query =
                    m_gd.Query(
                        new string[]
                            {
                                "Amount", "Complete", "PurchaseType", "Updated", "PrincipalID", "RegionName", "RegionID",
                                "RegionPos", "userName", "USDAmount"
                            }, "stardust_purchased", new QueryFilter()
                                                         {
                                                             andFilters = where
                                                         }, null, null, null);
                if (query.Count != 10)
                {
                    m_log.Error("No such purchase ID");
                    transaction = null;
                    purchaseType = -1;
                    return false;
                }
                if (query[1] == "1")
                {
                    m_log.Error("This purchase has already been completed");
                    transaction = null;
                    purchaseType = -1;
                    return false;
                }
                if (payPalResponse["payment_status"].AsString() != "Completed")
                {
                    Transaction transaction_temp1;
                    UserCurrencyBuyComplete(purchaseID, 0,
                                            "payment_status = " + payPalResponse["payment_status"].AsString(),
                                            payPalResponse["txn_id"].AsString(), raw, out transaction_temp1);
                    transaction = transaction_temp1;
                    purchaseType = -1;
                    return false;
                }
                Transaction transaction_temp2;
                if (
                    !UserCurrencyBuyComplete(purchaseID, 1, "PayPal", payPalResponse["txn_id"].AsString(), raw,
                                             out transaction_temp2))
                {
                    transaction = null;
                    purchaseType = -1;
                    return false;
                }
                transaction = transaction_temp2;
                purchaseType = int.Parse(query[2]);
                return true;
            }
            transaction = null;
            purchaseType = -999;
            return false;
        }

        public OSDMap PrePurchaseCheck(UUID purchaseID)
        {
            Dictionary<string, object> where = new Dictionary<string, object>(1);
            where["PurchaseID"] = purchaseID;
            List<string> query = m_gd.Query(new string[] { "Amount", "Complete", "PurchaseType", "PrincipalID", "RegionName", "ConversionFactor", "USDAmount" }, "stardust_purchased", new QueryFilter()
            {
                andFilters = where
            }, null, null, null);


            if (query.Count > 0)
            {
                return new OSDMap
                           {
                               {"Amount", query[0]},
                               {"Complete", query[1]},
                               {"PurchaseType", query[2]},
                               {"PrincipalID", query[3]},
                               {"RegionName", query[4]},
                               {"ConversionFactor", query[5]},
                               {"USDAmount", query[6]}
                           };
            }
            return new OSDMap();
        }

        public OSDMap OrderSubscription(UUID toId, string toName, string RegionName, string notes, string subscription_id)
        {
            Dictionary<string, object> where = new Dictionary<string, object>(1);
            where["id"] = subscription_id;
            List<string> query = m_gd.Query(new string[] { "name", "description", "price", "active" }, "stardust_subscriptions", new QueryFilter()
            {
                andFilters = where
            }, null, null, null);

            OSDMap response = new OSDMap();
            if (query.Count == 4)
            {

                response = new OSDMap
                                      {
                                          {"name", query[0]},
                                          {"description", query[1]},
                                          {"price", query[2]},
                                          {"active", query[3]}
                                      };
                string price = query[2];
                UUID purchaseID = UUID.Random();
                response.Add("purchaseID", purchaseID);
                List<object> values = new List<object>
                                          {
                                              purchaseID.ToString(),// PurchaseID
                                              2,// PurchaseType
                                              toId.ToString(),// PrincipalID
                                              toName,// PrincipalID
                                              0,// Amount
                                              response["price"],//USDAmount
                                              0,// ConversionFactor
                                              RegionName,// RegionName
                                              UUID.Zero.ToString(),// RegionID
                                              "",// RegionPos
                                              0,// Complete
                                              "",// CompleteMethod
                                              "",// CompleteReference
                                              "",// TransactionID
                                              Utils.GetUnixTime(),// Created
                                              Utils.GetUnixTime(),// Updated
                                              "", // pyapal raw data
                                              notes //notes
                                          };
                m_gd.Insert("stardust_purchased", values.ToArray());
            }
            return response;
        }

        public bool CheckIfPurchaseComplete(OSDMap payPalResponse)
        {
            if (payPalResponse.ContainsKey("custom"))
            {
                OSDMap result = PrePurchaseCheck(payPalResponse["custom"].AsUUID());
                if (result.ContainsKey("Complete"))
                    return result["Complete"].AsInteger() != 0;
            }
            else
            {
                m_log.ErrorFormat("[Stardust Currency] The paypal response did not contain the field 'custom' - paypal data: {0} ", payPalResponse);
            }
            return false;
        }


        #endregion

        #region private currency handlers

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="trans"></param>
        /// <returns></returns>
        private bool UserCurrencyTransaction(Transaction transaction, out Transaction trans)
        {
            // write the history
            if (!WriteHistory(transaction, out transaction))
            {
                trans = transaction;
                return false;
            }

            // get users currency

            StarDustUserCurrency fromBalance = GetUserCurrency(new UUID(transaction.FromID));

            // Ensure sender has enough money
            if ((!m_options.AllowBankerToHaveNoMoney ||
                (m_options.AllowBankerToHaveNoMoney && m_options.BankerPrincipalID != transaction.FromID)) &&
                fromBalance.Amount < transaction.Amount)
            {
                transaction.Complete = 0;
                transaction.CompleteReason = "Send amount is greater than sender has";
                WriteHistory(transaction, out transaction);
                trans = transaction;
                return false;
            }

            if (transaction.FromID != m_options.BankerPrincipalID)
            {
                if ((fromBalance.RestrictedAmount > 0) && ((fromBalance.Amount - fromBalance.RestrictedAmount) < transaction.Amount))
                {
                    transaction.Complete = 0;
                    transaction.CompleteReason = "Failed transaction. $" + fromBalance.RestrictedAmount + " of your currency is restricted for " + m_options.RestrictMoneyHoursAfterPurchase + " hours after purchase. You can spend " + (fromBalance.Amount - fromBalance.RestrictedAmount) + ". Sorry";
                    WriteHistory(transaction, out transaction);
                    trans = transaction;
                    return false;
                }
            }

            // update sender
            m_gd.Update("stardust_currency",
                        (fromBalance.Amount - transaction.Amount) >= fromBalance.StipendsBalance
                            ? new Dictionary<string, object> {{"Amount", fromBalance.Amount - transaction.Amount}}
                            : new Dictionary<string, object>
                                  {
                                      {"Amount", fromBalance.Amount - transaction.Amount},
                                      {
                                          "StipendsBalance",
                                          fromBalance.StipendsBalance -
                                          (fromBalance.StipendsBalance - (fromBalance.Amount - transaction.Amount))
                                          }
                                  }, null,
                        new QueryFilter()
                            {andFilters = new Dictionary<string, object> {{"PrincipalID", transaction.FromID}}}, null,
                        null);

            StarDustUserCurrency toBalance = GetUserCurrency(new UUID(transaction.ToID));
            if (transaction.TypeOfTrans == TransactionType.StipendPayment)
            {
                m_gd.Update("stardust_currency",
                        new Dictionary<string, object> { { "Amount", toBalance.Amount + transaction.Amount }, { "StipendsBalance", toBalance.StipendsBalance + transaction.Amount } }, null,
                        new QueryFilter() { andFilters = new Dictionary<string, object> { { "PrincipalID", transaction.ToID } } }, null,
                        null);
            }
            else
            {
                m_gd.Update("stardust_currency",
                            new Dictionary<string, object> {{"Amount", toBalance.Amount + transaction.Amount}}, null,
                            new QueryFilter()
                                {andFilters = new Dictionary<string, object> {{"PrincipalID", transaction.ToID}}}, null,
                            null);
            }

            // update logs
            transaction.Complete = 1;
            transaction.CompleteReason = "";

            transaction.ToBalance = toBalance.Amount + transaction.Amount;
            transaction.FromBalance = fromBalance.Amount - transaction.Amount;


            WriteHistory(transaction, out transaction);

            trans = transaction;
            return true;
        }

        private bool WriteHistory(Transaction transaction, out Transaction trans)
        {
            // since this is always the first thing that happens.. might as well confirm it here
            if (transaction.FromID == UUID.Zero)
            {
                m_log.Warn("[DustLocalCurrencyConnector] WriteHistory does not have from user data. FromName, and From Principle ID are required");
                trans = transaction;
                return false;
            }
            if (transaction.Amount == 0)
            {
                trans = transaction;
                return true;
            }
            if (transaction.ToID == UUID.Zero)
            {
                m_log.Warn("[DustLocalCurrencyConnector] WriteHistory does not have to user data. ToName, and To Principle ID are required");
                trans = transaction;
                return false;
            }


            if (transaction.TransactionID != UUID.Zero)
            {

                Dictionary<string, object> where = new Dictionary<string, object>() { { "TransactionID", transaction.TransactionID } };
                List<string> query = m_gd.Query(new string[] { "count(*)" }, "stardust_currency_history", new QueryFilter()
                {
                    andFilters = where
                }, null, null, null);

                if (int.Parse(query[0]) >= 1)
                {
                    m_gd.Update("stardust_currency_history",
                                new Dictionary<string, object>
                                    {
                                        {"Complete", transaction.Complete},
                                        {"CompleteReason", transaction.CompleteReason},
                                        {"Updated", Utils.GetUnixTime()},
                                        {"ToBalance", transaction.ToBalance},
                                        {"FromBalance", transaction.FromBalance}
                                    }, null,
                                new QueryFilter()
                                {
                                    andFilters =
                                        new Dictionary<string, object> { { "TransactionID", transaction.TransactionID } }
                                },
                                null, null);
                    trans = transaction;
                    return true;
                }
            }
            else
                transaction.TransactionID = UUID.Random();

            m_gd.Insert("stardust_currency_history", new object[]
            {
                transaction.TransactionID,              // TransactionID
                transaction.Description,                // Description
                transaction.FromID,                     // FromPrincipalID
                transaction.FromName,                   // FromName
                transaction.FromObjectID,               // FromObjectID
                transaction.FromObjectName,             // FromObjectName
                transaction.ToID,                       // ToPrincipalID
                transaction.ToName,                     // ToName
                transaction.ToObjectID,                 // ToObjectID
                transaction.ToObjectName,               // ToObjectName
                transaction.Amount,                     // Amount
                transaction.Complete,                   // Complete
                transaction.CompleteReason,             // CompleteReason
                transaction.Region.RegionName,          // RegionName
                transaction.Region.RegionID,            // RegionID
                transaction.Region.RegionPosition,      // RegionPos
                (int)transaction.TypeOfTrans,                // TransType 
                Utils.GetUnixTime(),                    // Created
                Utils.GetUnixTime(),                     // Updated
                0,                                      //ToBalance
                0                                       //FromBalance
            });
            trans = transaction;
            return true;
        }

        private void UserCurrencyCreate(UUID agentId)
        {
            m_gd.Insert("stardust_currency", new object[] { agentId.ToString(), 0, 0, 0, 0, 0, 0, 0 });
        }

        private void GroupCurrencyCreate(UUID agentId)
        {
            m_gd.Insert("stardust_currency", new object[] { agentId.ToString(), 0, 0, 0, 1, 0, 0, 0 });
        }
        #endregion

        #region ATM
        
        public int GetGridConversionFactor(string gridName)
        {
            QueryFilter q = new QueryFilter();
            q.andFilters.Add("grid_name", gridName);
            List<string> dr = m_gd.Query(new[] {"per_dollar"}, "stardust_atm_grids", q, new Dictionary<string, bool>(), null, null);
            if (dr.Count > 0)
                return int.Parse(dr[0]);
            return 0;
        }

        #endregion
    }
}
