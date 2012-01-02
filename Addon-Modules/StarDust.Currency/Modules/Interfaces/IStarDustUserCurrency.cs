using System.Collections.Generic;
using Aurora.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace StarDust.Currency.Interfaces
{
    public class RegionTransactionDetails
    {
        public UUID RegionID = UUID.Zero;
        public string RegionName = "";
        public string RegionPosition = "";

        public RegionTransactionDetails()
        {

        }

        public RegionTransactionDetails(OSDMap osdMap)
        {
            if (osdMap != null)
                FromOSD(osdMap);
        }

        public bool FromOSD(OSDMap osdMap)
        {
            if (UUID.TryParse(osdMap["RegionID"].AsString(), out RegionID) &&
                osdMap.ContainsKey("RegionName") &&
                osdMap.ContainsKey("RegionPosition"))
            {
                RegionName = osdMap["RegionName"].ToString();
                RegionPosition = osdMap["RegionPosition"].ToString();
                return true;
            }
            return false;
        }

        public OSDMap ToOSD()
        {
            return new OSDMap
                       {
                           {"RegionID",RegionID},
                           {"RegionName", RegionName},
                           {"RegionPosition", RegionPosition}
                       };
        }
    }
    public class Transaction
    {
        public UUID TransactionID = UUID.Zero;
        public string Description = "";
        public UUID FromID = UUID.Zero;
        public string FromName = "";
        public UUID FromObjectID = UUID.Zero;
        public string FromObjectName = "";
        public UUID ToID = UUID.Zero;
        public string ToName = "";
        public UUID ToObjectID = UUID.Zero;
        public string ToObjectName = "";
        public uint Amount;
        public int Complete;
        public string CompleteReason = "";
        public RegionTransactionDetails Region = new RegionTransactionDetails();
        public uint Created = Utils.GetUnixTime();
        public uint Updated = Utils.GetUnixTime();
        public TransactionType TypeOfTrans;
        public uint ToBalance;
        public uint FromBalance;

        public Transaction() { }

        public Transaction(OSDMap osdMap)
        {
            FromOSD(osdMap);
        }



        public OSDMap ToOSD(string method, string function)
        {
            OSDMap osdMap = ToOSD();
            osdMap.Add(method, function);
            return osdMap;
        }

        public bool FromOSD(OSDMap osdMap)
        {
            if (UUID.TryParse(osdMap["FromID"].AsString(), out FromID) &&
                uint.TryParse(osdMap["Created"].AsString(), out Created) &&
                uint.TryParse(osdMap["Updated"].AsString(), out Updated) &&
                osdMap.ContainsKey("Region"))
            {
                UUID.TryParse(osdMap["TransactionID"].AsString(), out TransactionID);
                UUID.TryParse(osdMap["ToID"].AsString(), out ToID);
                UUID.TryParse(osdMap["ToObjectID"].AsString(), out ToObjectID);
                UUID.TryParse(osdMap["FromObjectID"].AsString(), out FromObjectID);

                uint.TryParse(osdMap["ToBalance"].AsString(), out ToBalance);
                uint.TryParse(osdMap["FromBalance"].AsString(), out FromBalance);

                uint.TryParse(osdMap["Amount"].AsString(), out Amount);
                int typeOfTrans;
                int.TryParse(osdMap["TypeOfTrans"].AsString(), out typeOfTrans);
                int.TryParse(osdMap["Complete"].AsString(), out Complete);

                Region = new RegionTransactionDetails((OSDMap)osdMap["Region"]);
                TypeOfTrans = (TransactionType)typeOfTrans;

                FromName = (osdMap.ContainsKey("FromName")) ? osdMap["FromName"].AsString() : "";
                ToName = (osdMap.ContainsKey("ToName")) ? osdMap["ToName"].AsString() : "";
                FromObjectName = (osdMap.ContainsKey("FromObjectName")) ? osdMap["FromObjectName"].AsString() : "";
                ToObjectName = (osdMap.ContainsKey("ToObjectName")) ? osdMap["ToObjectName"].AsString() : "";
                CompleteReason = (osdMap.ContainsKey("CompleteReason")) ? osdMap["CompleteReason"].AsString() : "";
                Description = (osdMap.ContainsKey("Description")) ? osdMap["Description"].AsString() : "";
                return true;
            }
            return false;
        }

        public OSDMap ToOSD()
        {
            return new OSDMap
            {
                {"TransactionID", TransactionID},
                {"Description", Description},
                {"FromID", FromID},
                {"FromObjectID", FromObjectID},
                {"FromObjectName", FromObjectName},
                {"FromName", FromName},
                {"ToID", ToID},
                {"ToName", ToName},
                {"ToObjectName", ToObjectName},
                {"ToObjectID", ToObjectID},
                {"Amount", Amount},
                {"Complete", Complete},
                {"CompleteReason", CompleteReason},
                {"Region", Region.ToOSD()},
                {"Created", Created},
                {"Updated", Updated},
                {"TypeOfTrans", ((int)TypeOfTrans).ToString()},
                {"FromBalance", FromBalance},
                {"ToBalance", ToBalance}
            };
        }
    }
    public class StarDustUserCurrency
    {
        public UUID PrincipalID;
        public uint Amount;
        public uint LandInUse;
        public uint Tier;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="osdMap"></param>
        public StarDustUserCurrency(OSDMap osdMap)
        {
            if (osdMap != null)
                FromOSD(osdMap);
        }

        public StarDustUserCurrency() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="osdMap"></param>
        public bool FromOSD(OSDMap osdMap)
        {
            if (UUID.TryParse(osdMap["PrincipalID"].AsString(), out PrincipalID) &&
                uint.TryParse(osdMap["Amount"].AsString(), out Amount) &&
                uint.TryParse(osdMap["LandInUse"].AsString(), out LandInUse) &&
                uint.TryParse(osdMap["Tier"].AsString(), out Tier))
                return true;
            return false;
        }

        public bool FromArray(List<string> queryResults)
        {
            return UUID.TryParse(queryResults[0], out PrincipalID) &&
                   uint.TryParse(queryResults[1], out Amount) &&
                   uint.TryParse(queryResults[2], out LandInUse) &&
                   uint.TryParse(queryResults[3], out Tier);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public OSDMap ToOSD()
        {
            return
                new OSDMap
                    {
                        {"PrincipalID", PrincipalID},
                        {"Amount", Amount},
                        {"LandInUse", LandInUse},
                        {"Tier", Tier}
                    };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> ToKeyValuePairs()
        {
            return
                new Dictionary<string, object>
                    {
                        {"PrincipalID", PrincipalID},
                        {"Amount", Amount},
                        {"LandInUse", LandInUse},
                        {"Tier", Tier}
                    };
        }
    }

    public interface IStarDustCurrencyService
    {
        /// <summary>
        /// Get information about the given users currency
        /// </summary>
        /// <param name="agentID"></param>
        /// <returns></returns>
        StarDustUserCurrency UserCurrencyInfo(UUID agentID);

        /// <summary>
        /// Update the currency for the given user (This does not update the user's balance!)
        /// </summary>
        /// <param name="agent"></param>
        bool UserCurrencyUpdate(StarDustUserCurrency agent);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns></returns>
        Transaction UserCurrencyTransfer(Transaction transaction);

        StarDustConfig GetConfig();

        bool SendGridMessage(UUID toID, string message, bool goDeep, UUID transactionId);

        bool FinishPurchase(OSDMap resp, string rawResponse);

        OSDMap PrePurchaseCheck(UUID purchaseId);
        OSDMap OrderSubscription(UUID toId, string regionName, string notes, string subscriptionID);
    }

    public interface IStarDustCurrencyConnector : IAuroraDataPlugin
    {
        /// <summary>
        /// Get information about the given users currency
        /// </summary>
        /// <param name="agentID"></param>
        /// <returns></returns>
        StarDustUserCurrency GetUserCurrency(UUID agentID);

        /// <summary>
        /// Update the currency for the given user (This does not update the user's balance!)
        /// </summary>
        /// <param name="agent"></param>
        bool UserCurrencyUpdate(StarDustUserCurrency agent);

        /// <summary>
        /// Buy currency for the given user
        /// </summary>
        /// <param name="principalID"></param>
        /// <param name="userName"></param>
        /// <param name="amount"></param>
        /// <param name="purchaseID"></param>
        /// <param name="conversionFactor"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        bool UserCurrencyBuy(UUID purchaseID, UUID principalID, string userName, uint amount, float conversionFactor, RegionTransactionDetails region);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="purchaseID"></param>
        /// <param name="isComplete"></param>
        /// <param name="completeMethod"></param>
        /// <param name="completeReference"></param>
        /// <param name="rawdata"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        bool UserCurrencyBuyComplete(UUID purchaseID, int isComplete, string completeMethod, string completeReference, string rawdata, out Transaction transaction);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns></returns>
        Transaction UserCurrencyTransaction(Transaction transaction);

        bool FinishPurchase(OSDMap payPalResponse, string raw, out Transaction transaction, out int purchaseType);

        OSDMap PrePurchaseCheck(UUID purchaseID);


        OSDMap OrderSubscription(UUID toId, string toName, string regionName, string notes, string subscriptionID);
    }
}