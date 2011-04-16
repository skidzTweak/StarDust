using System;
using System.Collections.Generic;
using Aurora.DataManager.Migration;
using C5;
using Aurora.Framework;

namespace StarDust.Currency.Grid.Dust
{
    public class CurrencyMigrator_0 : Migrator
    {
        public CurrencyMigrator_0()
        {
            Version = new Version(0, 0, 0);
            MigrationName = "Currency";

            schema = new List<Rec<string, ColumnDefinition[]>>();

            AddSchema("usercurrency", ColDefs(
                ColDef("PrincipalID", ColumnTypes.String50, true),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("LandInUse", ColumnTypes.Integer30),
                ColDef("Tier", ColumnTypes.Integer30)));

            AddSchema("usercurrency_purchased", ColDefs(
                ColDef("PurchaseID", ColumnTypes.String36, true),
                ColDef("PrincipalID", ColumnTypes.String36, true),
                ColDef("UserName", ColumnTypes.String128, true),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("ConversionFactor", ColumnTypes.Integer30),
                ColDef("RegionName", ColumnTypes.String128),
                ColDef("RegionID", ColumnTypes.String36),
                ColDef("RegionPos", ColumnTypes.String128),
                ColDef("Complete", ColumnTypes.Integer11),
                ColDef("CompleteMethod", ColumnTypes.String128),
                ColDef("CompleteReference", ColumnTypes.String128),
                ColDef("TransactionID", ColumnTypes.String36),
                ColDef("Created", ColumnTypes.Integer30),
                ColDef("Updated", ColumnTypes.Integer30),
                ColDef("raw_paypal_transaction_data", ColumnTypes.Text)));

            AddSchema("usercurrency_history", ColDefs(
                ColDef("TransactionID", ColumnTypes.String36, true),
                ColDef("Description", ColumnTypes.String128, true),
                ColDef("FromPrincipalID", ColumnTypes.String36, true),
                ColDef("FromName", ColumnTypes.String128, true),
                ColDef("FromObjectID", ColumnTypes.String36, true),
                ColDef("FromObjectName", ColumnTypes.String128, true),
                ColDef("ToPrincipalID", ColumnTypes.String36, true),
                ColDef("ToName", ColumnTypes.String36, true),
                ColDef("ToObjectID", ColumnTypes.String36, true),
                ColDef("ToObjectName", ColumnTypes.String128, true),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("Complete", ColumnTypes.Integer11),
                ColDef("CompleteReason", ColumnTypes.String128),
                ColDef("RegionName", ColumnTypes.String128),
                ColDef("RegionID", ColumnTypes.String36),
                ColDef("RegionPos", ColumnTypes.String128),
                ColDef("TransType", ColumnTypes.Integer11),
                ColDef("Created", ColumnTypes.Integer30),
                ColDef("Updated", ColumnTypes.Integer30),
                ColDef("ToBalance", ColumnTypes.Integer30),
                ColDef("FromBalance", ColumnTypes.Integer30)
                )
                ); 
        }

        protected override void DoCreateDefaults(IDataConnector genericData)
        {
            EnsureAllTablesInSchemaExist(genericData);
        }

        protected override bool DoValidate(IDataConnector genericData)
        {
            return TestThatAllTablesValidate(genericData);
        }

        protected override void DoMigrate(IDataConnector genericData)
        {
            DoCreateDefaults(genericData);
        }

        protected override void DoPrepareRestorePoint(IDataConnector genericData)
        {
            CopyAllTablesToTempVersions(genericData);
        }
    }
}