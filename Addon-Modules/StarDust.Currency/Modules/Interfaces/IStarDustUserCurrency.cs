using System.Collections.Generic;
using Aurora.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Services.Interfaces;

namespace StarDust.Currency.Interfaces
{
    public sealed class RegionTransactionDetails: IDataTransferable
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

        public override void FromOSD(OSDMap osdMap)
        {
            if (UUID.TryParse(osdMap["RegionID"].AsString(), out RegionID) &&
                osdMap.ContainsKey("RegionName") &&
                osdMap.ContainsKey("RegionPosition"))
            {
                RegionName = osdMap["RegionName"].ToString();
                RegionPosition = osdMap["RegionPosition"].ToString();
            }
        }

        public override OSDMap ToOSD()
        {
            return new OSDMap
                       {
                           {"RegionID",RegionID},
                           {"RegionName", RegionName},
                           {"RegionPosition", RegionPosition}
                       };
        }
    }
    public class Transaction: IDataTransferable
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

        public override sealed void FromOSD(OSDMap osdMap)
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
            }
        }

        public override OSDMap ToOSD()
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
    public class StarDustUserCurrency : IDataTransferable
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
        public override sealed void FromOSD(OSDMap osdMap)
        {
            if (UUID.TryParse(osdMap["PrincipalID"].AsString(), out PrincipalID) &&
                uint.TryParse(osdMap["Amount"].AsString(), out Amount) &&
                uint.TryParse(osdMap["LandInUse"].AsString(), out LandInUse) &&
                uint.TryParse(osdMap["Tier"].AsString(), out Tier))
                return;
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
        public override OSDMap ToOSD()
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
        public override Dictionary<string, object> ToKVP()
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
        /// <param name="agentId"></param>
        /// <returns></returns>
        StarDustUserCurrency UserCurrencyInfo(UUID agentId);

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
        bool UserCurrencyTransfer(UUID toID, UUID fromID, UUID toObjectID, UUID fromObjectID, uint amount,
                                  string description, TransactionType type, UUID transactionID);
        StarDustConfig GetConfig();
        bool SendGridMessage(UUID toID, string message, bool goDeep, UUID transactionId);
        bool CheckEnabled();
        void SetMoneyModule(MoneyModule moneyModule);
        UserAccount GetUserAccount(UUID fromID);
    }

    public interface IStardustRegionService
    {
        ISceneChildEntity FindObject(UUID fromObjectID, out IScene scene);
        IScene FindScene(UUID fromID);
        IClientAPI GetUserClient(UUID fromID);
        bool SendGridMessage(UUID toID, string message, bool goDeep, UUID transactionId);
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
        bool CheckIfPurchaseComplete(OSDMap payPalResponse);
        GroupBalance GetGroupBalance(UUID groupID);
    }
}