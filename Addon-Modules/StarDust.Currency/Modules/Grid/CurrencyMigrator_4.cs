using System;
using System.Collections.Generic;
using Aurora.DataManager.Migration;

using Aurora.Framework;

namespace StarDust.Currency.Grid
{
    public class CurrencyMigrator_4 : Migrator
    {
        public CurrencyMigrator_4()
        {
            Version = new Version(0, 0, 4);
            MigrationName = "Currency";

            schema = new List<SchemaDefinition>();

            AddSchema("stardust_currency", ColDefs(
                ColDef("PrincipalID", ColumnTypes.String50),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("LandInUse", ColumnTypes.Integer30),
                ColDef("Tier", ColumnTypes.Integer30),
                ColDef("IsGroup", ColumnTypes.TinyInt1),
                ColDef("RestrictedAmount", ColumnTypes.Integer30),
                ColDef("RestrictPurchaseAmount", ColumnTypes.Integer30),
                new ColumnDefinition
                {
                    Name = "StipendsBalance",
                    Type = new ColumnTypeDef
                    {
                        Type = ColumnType.Integer,
                        Size = 11,
                        defaultValue = "0"
                    }
                }
                ),
                IndexDefs(
                    IndexDef(new string[1] { "PrincipalID" }, IndexType.Primary)
                ));

            // this is actually used for all purchases now.. a better name would be _purchased
            AddSchema("stardust_purchased", ColDefs(
                ColDef("PurchaseID", ColumnTypes.String36),
                ColDef("PurchaseType", ColumnTypes.Integer11),
                ColDef("PrincipalID", ColumnTypes.String36),
                ColDef("UserName", ColumnTypes.String128),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("USDAmount", ColumnTypes.Integer30),
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
                ColDef("RawPayPalTransactionData", ColumnTypes.Text),
                ColDef("Notes", ColumnTypes.Text)),
                IndexDefs(
                    IndexDef(new string[1] { "PurchaseID" }, IndexType.Primary)
                ));

            AddSchema("stardust_currency_history", ColDefs(
                ColDef("TransactionID", ColumnTypes.String36),
                ColDef("Description", ColumnTypes.String128),
                ColDef("FromPrincipalID", ColumnTypes.String36),
                ColDef("FromName", ColumnTypes.String128),
                ColDef("FromObjectID", ColumnTypes.String36),
                ColDef("FromObjectName", ColumnTypes.String128),
                ColDef("ToPrincipalID", ColumnTypes.String36),
                ColDef("ToName", ColumnTypes.String36),
                ColDef("ToObjectID", ColumnTypes.String36),
                ColDef("ToObjectName", ColumnTypes.String128),
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
                ), IndexDefs(
                    IndexDef(new string[1] { "TransactionID" }, IndexType.Primary),
                    IndexDef(new string[1] { "FromPrincipalID" }, IndexType.Index),
                    IndexDef(new string[1] { "ToPrincipalID" }, IndexType.Index)
                ));

            AddSchema("stardust_subscriptions", ColDefs(
                ColDef("id", ColumnTypes.String36),
                ColDef("name", ColumnTypes.String64),
                ColDef("description", ColumnTypes.String1024),
                ColDef("price", ColumnTypes.Integer11),
                ColDef("active", ColumnTypes.Integer11)
                ), IndexDefs(
                    IndexDef(new string[1] { "id" }, IndexType.Primary)
                ));

            AddSchema("stardust_group_teir_donation", ColDefs(
                ColDef("avatar_id", ColumnTypes.String36),
                ColDef("group_id", ColumnTypes.String36),
                ColDef("teir", ColumnTypes.Integer30)
                ), IndexDefs(
                    IndexDef(new[] { "avatar_id", "group_id" }, IndexType.Primary)
                ));

            AddSchema("stardust_atm_grids", ColDefs(
                ColDef("grid_name", ColumnTypes.String36),
                ColDef("per_dollar", ColumnTypes.Integer30)
                ), IndexDefs(
                    IndexDef(new[] { "grid_name" }, IndexType.Primary)
                ));


            AddSchema("stardust_atm_machine_history", ColDefs(
                ColDef("id", ColumnTypes.String36),
                ColDef("in_or_out", ColumnTypes.Integer30),
                ColDef("purchase_id", ColumnTypes.String36),
                ColDef("atm_name", ColumnTypes.String36),
                ColDef("grid_name", ColumnTypes.String36),
                ColDef("amount_paid", ColumnTypes.Integer30),
                ColDef("this_grid_per_dollar", ColumnTypes.Integer30),
                ColDef("that_grid_per_dollar", ColumnTypes.Integer30),
                ColDef("amount_give", ColumnTypes.Integer30),
                ColDef("from_name", ColumnTypes.String128),
                ColDef("from_key", ColumnTypes.String36),
                ColDef("to_name", ColumnTypes.String128),
                ColDef("to_key", ColumnTypes.String36),
                ColDef("created", ColumnTypes.Integer30),
                ColDef("updated", ColumnTypes.Integer30)
                ), IndexDefs(
                    IndexDef(new[] { "id" }, IndexType.Primary)
                ));
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