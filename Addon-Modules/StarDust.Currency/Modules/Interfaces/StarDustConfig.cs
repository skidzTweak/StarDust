using System;
using System.Linq;
using System.Reflection;
using Aurora.Framework;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;

namespace StarDust.Currency.Interfaces
{
    public class StarDustConfig : IDataTransferable
    {
        private readonly ILog m_log =
                LogManager.GetLogger (
                MethodBase.GetCurrentMethod ().DeclaringType);

        #region declarations
        private int m_priceEnergyUnit = 100;
        private int m_priceObjectClaim = 10;
        private int m_pricePublicObjectDecay = 4;
        private int m_pricePublicObjectDelete = 4;
        private int m_priceParcelClaim = 1;
        private float m_priceParcelClaimFactor = 1f;
        private uint m_priceUpload = 0;
        private int m_priceRentLight = 5;
        private int m_teleportMinPrice = 2;
        private float m_teleportPriceExponent = 2f;
        private float m_priceObjectRent = 1;
        private float m_priceObjectScaleFactor = 10;
        private int m_priceParcelRent = 1;
        private uint m_priceGroupCreate = 0;
        private UUID m_bankerPrincipalID = new UUID("11111111-1111-0000-0000-000100bba000");
        private int m_realCurrencyConversionFactor = 1;
        private int m_stipend = 0;
        private int m_minFundsBeforeRefresh = 5;
        private string m_upgradeMembershipUri = "";
        private int m_objectCapacity = 0;

        private uint m_currencyInHandlerPort = 8007;
        private string m_currencyModule = "Dust";
        private string m_currencyConnector = "Local";
        private string m_afterCurrencyPurchaseMessage = "";
        private bool m_autoApplyCurrency = false;
        private bool m_allowBankerToHaveNoMoney = true;
        private string m_errorURI = "";
        private string m_messgeBeforeBuyLand = "";
        private int m_AdditionPercentage = 291;
        private int m_AdditionAmount = 30;
        private string m_payPalAuthToken = "";
        private string m_payPalUrl = "";
        private int m_restrictMoneyHoursAfterPurchase = 0;
        private int m_restrictMoneyCanSpendAfterPurchase = 0;
        private int m_maxAmountPurchaseDays = 0;
        private int m_maxAmountPurchase = 100000;
		
		private bool m_DisplayPayMessages = true;

        #endregion
        #region functions
        public StarDustConfig() { }
        public StarDustConfig(IConfig economyConfig)
        {
            foreach (PropertyInfo propertyInfo in GetType().GetProperties())
            {
                try
                {
                    if (propertyInfo.PropertyType.IsAssignableFrom (typeof (float)))
                        propertyInfo.SetValue (this, economyConfig.GetFloat (propertyInfo.Name, float.Parse (propertyInfo.GetValue (this, new object[0]).ToString ())), new object[0]);
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (int)))
                        propertyInfo.SetValue (this, economyConfig.GetInt (propertyInfo.Name, int.Parse (propertyInfo.GetValue (this, new object[0]).ToString ())), new object[0]);
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (bool)))
                        propertyInfo.SetValue (this, economyConfig.GetBoolean (propertyInfo.Name, bool.Parse (propertyInfo.GetValue (this, new object[0]).ToString ())), new object[0]);
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (string)))
                        propertyInfo.SetValue (this, economyConfig.GetString (propertyInfo.Name, propertyInfo.GetValue (this, new object[0]).ToString ()), new object[0]);
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (UUID)))
                        propertyInfo.SetValue (this, new UUID (economyConfig.GetString (propertyInfo.Name, propertyInfo.GetValue (this, new object[0]).ToString ())), new object[0]);
                }
                catch(Exception ex)
                {
                    m_log.Warn ("[StarDust]: Exception reading economy config: " + ex.ToString ());
                }
            }
        }

        public StarDustConfig(OSDMap values)
        {
            FromOSD(values);
        }

        public override OSDMap ToOSD()
        {
            OSDMap returnvalue = new OSDMap();
            foreach (PropertyInfo propertyInfo in GetType().GetProperties())
            {
                try
                {
                    if (propertyInfo.PropertyType.IsAssignableFrom (typeof (float)))
                        returnvalue.Add (propertyInfo.Name, (float)propertyInfo.GetValue (this, new object[0]));
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (int)))
                        returnvalue.Add (propertyInfo.Name, (int)propertyInfo.GetValue (this, new object[0]));
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (bool)))
                        returnvalue.Add (propertyInfo.Name, (bool)propertyInfo.GetValue (this, new object[0]));
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (string)))
                        returnvalue.Add (propertyInfo.Name, (string)propertyInfo.GetValue (this, new object[0]));
                    else if (propertyInfo.PropertyType.IsAssignableFrom (typeof (UUID)))
                        returnvalue.Add (propertyInfo.Name, (UUID)propertyInfo.GetValue (this, new object[0]));
                }
                catch (Exception ex)
                {
                    m_log.Warn ("[StarDust]: Exception toOSD() config: " + ex.ToString ());
                }
            }
            return returnvalue;
        }

        public override sealed void FromOSD(OSDMap values)
        {
            foreach (PropertyInfo propertyInfo in GetType().GetProperties())
            {
                if (values.ContainsKey(propertyInfo.Name))
                {
                    try
                    {
                        if (propertyInfo.PropertyType.IsAssignableFrom(typeof (float)))
                            propertyInfo.SetValue(this, float.Parse(values[propertyInfo.Name].AsString()), new object[0]);
                        else if (propertyInfo.PropertyType.IsAssignableFrom(typeof (int)))
                            propertyInfo.SetValue(this, values[propertyInfo.Name].AsInteger(), new object[0]);
                        else if (propertyInfo.PropertyType.IsAssignableFrom(typeof (bool)))
                            propertyInfo.SetValue(this, values[propertyInfo.Name].AsBoolean(), new object[0]);
                        else if (propertyInfo.PropertyType.IsAssignableFrom(typeof (string)))
                            propertyInfo.SetValue(this, values[propertyInfo.Name].AsString(), new object[0]);
                        else if (propertyInfo.PropertyType.IsAssignableFrom(typeof (UUID)))
                            propertyInfo.SetValue(this, values[propertyInfo.Name].AsUUID(), new object[0]);
                    }
                    catch (Exception ex)
                    {
                        m_log.Warn("[StarDust]: Exception reading fromOSD() config: " + ex.ToString());
                    }
                }
            }
        }

        #endregion
        #region properties

        public bool DisplayPayMessages { 
			get{return m_DisplayPayMessages;}
			set{m_DisplayPayMessages = value;}	
		}

        public string ErrorURI
        {
            get { return m_errorURI; }
            set { m_errorURI = value; }
        }

        public bool AutoApplyCurrency
        {
            get { return m_autoApplyCurrency; }
            set { m_autoApplyCurrency = value; }
        }

        public bool AllowBankerToHaveNoMoney
        {
            get { return m_allowBankerToHaveNoMoney; }
            set { m_allowBankerToHaveNoMoney = value; }
        }

        public string AfterCurrencyPurchaseMessage
        {
            get { return m_afterCurrencyPurchaseMessage; }
            set { m_afterCurrencyPurchaseMessage = value; }
        }

        public string CurrencyConnector
        {
            get { return m_currencyConnector; }
            set { m_currencyConnector = value; }
        }

        public uint CurrencyInHandlerPort
        {
            get { return m_currencyInHandlerPort; }
            set { m_currencyInHandlerPort = value; }
        }

        public string CurrencyModule
        {
            get { return m_currencyModule; }
            set { m_currencyModule = value; }
        }

        public int ObjectCapacity
        {
            get { return m_objectCapacity; }
            set { m_objectCapacity = value; }
        }

        public string UpgradeMembershipUri
        {
            get { return m_upgradeMembershipUri; }
            set { m_upgradeMembershipUri = value; }
        }

        public int MinFundsBeforeRefresh
        {
            get { return m_minFundsBeforeRefresh; }
            set { m_minFundsBeforeRefresh = value; }
        }

        public int Stipend
        {
            get { return m_stipend; }
            set { m_stipend = value; }
        }

        public int RealCurrencyConversionFactor
        {
            get { return m_realCurrencyConversionFactor; }
            set { m_realCurrencyConversionFactor = value; }
        }

        public UUID BankerPrincipalID
        {
            get { return m_bankerPrincipalID; }
            set { m_bankerPrincipalID = value; }
        }

        public int PriceGroupCreate
        {
            get { return (int)m_priceGroupCreate; }
            set { m_priceGroupCreate = (uint)value; }
        }

        public int PriceParcelRent
        {
            get { return m_priceParcelRent; }
            set { m_priceParcelRent = value; }
        }

        public float PriceObjectScaleFactor
        {
            get { return m_priceObjectScaleFactor; }
            set { m_priceObjectScaleFactor = value; }
        }

        public float PriceObjectRent
        {
            get { return m_priceObjectRent; }
            set { m_priceObjectRent = value; }
        }

        public float TeleportPriceExponent
        {
            get { return m_teleportPriceExponent; }
            set { m_teleportPriceExponent = value; }
        }

        public int TeleportMinPrice
        {
            get { return m_teleportMinPrice; }
            set { m_teleportMinPrice = value; }
        }

        public int PriceRentLight
        {
            get { return m_priceRentLight; }
            set { m_priceRentLight = value; }
        }

        public int PriceUpload
        {
            get { return (int)m_priceUpload; }
            set { m_priceUpload = (uint)value; }
        }

        public float PriceParcelClaimFactor
        {
            get { return m_priceParcelClaimFactor; }
            set { m_priceParcelClaimFactor = value; }
        }

        public int PriceParcelClaim
        {
            get { return m_priceParcelClaim; }
            set { m_priceParcelClaim = value; }
        }

        public int PricePublicObjectDelete
        {
            get { return m_pricePublicObjectDelete; }
            set { m_pricePublicObjectDelete = value; }
        }

        public int PricePublicObjectDecay
        {
            get { return m_pricePublicObjectDecay; }
            set { m_pricePublicObjectDecay = value; }
        }

        public int PriceObjectClaim
        {
            get { return m_priceObjectClaim; }
            set { m_priceObjectClaim = value; }
        }

        public int PriceEnergyUnit
        {
            get { return m_priceEnergyUnit; }
            set { m_priceEnergyUnit = value; }
        }

        public string MessgeBeforeBuyLand
        {
            get { return m_messgeBeforeBuyLand; }
            set { m_messgeBeforeBuyLand = value; }
        }

        public int AdditionPercentage
        {
            get { return m_AdditionPercentage; }
            set { m_AdditionPercentage = value; }
        }

        public int AdditionAmount
        {
            get { return m_AdditionAmount; }
            set { m_AdditionAmount = value; }
        }

        public string PayPalAuthToken
        {
            get { return m_payPalAuthToken; }
            set { m_payPalAuthToken = value; }
        }

        public string PayPalURL
        {
            get { return m_payPalUrl; }
            set { m_payPalUrl = value; }
        }

        public int RestrictMoneyHoursAfterPurchase
        {
            get { return m_restrictMoneyHoursAfterPurchase; }
            set { m_restrictMoneyHoursAfterPurchase = value; }
        }

        public int RestrictMoneyCanSpendAfterPurchase
        {
            get { return m_restrictMoneyCanSpendAfterPurchase; }
            set { m_restrictMoneyCanSpendAfterPurchase = value; }
        }

        public int MaxAmountPurchaseDays
        {
            get { return m_maxAmountPurchaseDays; }
            set { m_maxAmountPurchaseDays = value; }
        }

        public int MaxAmountPurchase
        {
            get { return m_maxAmountPurchase; }
            set { m_maxAmountPurchase = value; }
        }
        #endregion
    }
}
