using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260909063000_ProceduralItemizationV1")]
public partial class ProceduralItemizationV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ActualItemPower",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BindState",
            schema: "game",
            table: "character_items",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "UNBOUND");

        migrationBuilder.AddColumn<int>(
            name: "EnhancementLevel",
            schema: "game",
            table: "character_items",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "GeneratedDisplayName",
            schema: "game",
            table: "character_items",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GeneratedPrefixId",
            schema: "game",
            table: "character_items",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GeneratedSuffixId",
            schema: "game",
            table: "character_items",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GenerationSeedHash",
            schema: "game",
            table: "character_items",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "GenerationVersion",
            schema: "game",
            table: "character_items",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "IsPerfect",
            schema: "game",
            table: "character_items",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "ItemLevel",
            schema: "game",
            table: "character_items",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MaxTemplateItemPower",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MinimumTemplateItemPower",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PerfectOrigin",
            schema: "game",
            table: "character_items",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ReforgeCount",
            schema: "game",
            table: "character_items",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "ReforgeSlotKey",
            schema: "game",
            table: "character_items",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RollQuality",
            schema: "game",
            table: "character_items",
            type: "numeric(7,2)",
            precision: 7,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceOperationId",
            schema: "game",
            table: "character_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceEntryId",
            schema: "game",
            table: "character_items",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceType",
            schema: "game",
            table: "character_items",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Stars",
            schema: "game",
            table: "character_items",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TransactionLockId",
            schema: "game",
            table: "character_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GeneratedItemJson",
            schema: "game",
            table: "pending_loot_items",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GenerationSeedHash",
            schema: "game",
            table: "pending_loot_items",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceOperationId",
            schema: "game",
            table: "pending_loot_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceEntryId",
            schema: "game",
            table: "pending_loot_items",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceType",
            schema: "game",
            table: "pending_loot_items",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GeneratedItemJson",
            schema: "game",
            table: "combat_loot_rolls",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceQualityProfileId",
            schema: "game",
            table: "combat_loot_rolls",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "NORMAL");

        migrationBuilder.CreateTable(
            name: "character_item_affixes",
            schema: "game",
            columns: table => new
            {
                ItemInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                SlotKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                AffixDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                StatId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                MinAtGeneration = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                MaxAtGeneration = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                StepAtGeneration = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                AffixTier = table.Column<int>(type: "integer", nullable: false),
                IsGuaranteed = table.Column<bool>(type: "boolean", nullable: false),
                IsReforgeSlot = table.Column<bool>(type: "boolean", nullable: false),
                GenerationOrdinal = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_character_item_affixes",
                    x => new { x.ItemInstanceId, x.SlotKey });
                table.CheckConstraint(
                    "ck_character_item_affixes_range",
                    "\"MaxAtGeneration\" >= \"MinAtGeneration\"");
                table.CheckConstraint(
                    "ck_character_item_affixes_step_positive",
                    "\"StepAtGeneration\" > 0");
                table.CheckConstraint(
                    "ck_character_item_affixes_value_range",
                    "\"Value\" >= \"MinAtGeneration\" AND \"Value\" <= \"MaxAtGeneration\"");
                table.ForeignKey(
                    name: "fk_character_item_affixes_character_items_item_id",
                    column: x => x.ItemInstanceId,
                    principalSchema: "game",
                    principalTable: "character_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "item_reforge_operations",
            schema: "game",
            columns: table => new
            {
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                SlotKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CostGold = table.Column<int>(type: "integer", nullable: false),
                MaterialItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                MaterialQuantity = table.Column<int>(type: "integer", nullable: false),
                CatalystItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CatalystQuantity = table.Column<int>(type: "integer", nullable: false),
                CurrentItemJson = table.Column<string>(type: "jsonb", nullable: false),
                ProposedItemJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_item_reforge_operations", x => x.OperationId);
                table.CheckConstraint(
                    "ck_item_reforge_operations_catalyst_quantity",
                    "\"CatalystQuantity\" >= 0");
                table.CheckConstraint(
                    "ck_item_reforge_operations_cost_gold",
                    "\"CostGold\" >= 0");
                table.CheckConstraint(
                    "ck_item_reforge_operations_material_quantity",
                    "\"MaterialQuantity\" >= 0");
            });

        migrationBuilder.CreateIndex(
            name: "ix_character_item_affixes_item_id",
            schema: "game",
            table: "character_item_affixes",
            column: "ItemInstanceId");

        migrationBuilder.CreateIndex(
            name: "ix_character_item_affixes_stat_id",
            schema: "game",
            table: "character_item_affixes",
            column: "StatId");

        migrationBuilder.CreateIndex(
            name: "ix_character_items_source_operation",
            schema: "game",
            table: "character_items",
            column: "SourceOperationId");

        migrationBuilder.CreateIndex(
            name: "ix_character_items_transaction_lock",
            schema: "game",
            table: "character_items",
            column: "TransactionLockId");

        migrationBuilder.CreateIndex(
            name: "ix_item_reforge_operations_character_item_state",
            schema: "game",
            table: "item_reforge_operations",
            columns: ReforgeOperationStateIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ix_item_reforge_operations_item_id",
            schema: "game",
            table: "item_reforge_operations",
            column: "ItemInstanceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_item_affixes",
            schema: "game");

        migrationBuilder.DropTable(
            name: "item_reforge_operations",
            schema: "game");

        migrationBuilder.DropIndex(
            name: "ix_character_items_source_operation",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropIndex(
            name: "ix_character_items_transaction_lock",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropColumn(name: "ActualItemPower", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "BindState", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "EnhancementLevel", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "GeneratedDisplayName", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "GeneratedPrefixId", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "GeneratedSuffixId", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "GenerationSeedHash", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "GenerationVersion", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "IsPerfect", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "ItemLevel", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "MaxTemplateItemPower", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "MinimumTemplateItemPower", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "PerfectOrigin", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "ReforgeCount", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "ReforgeSlotKey", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "RollQuality", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "SourceEntryId", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "SourceOperationId", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "SourceType", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "Stars", schema: "game", table: "character_items");
        migrationBuilder.DropColumn(name: "TransactionLockId", schema: "game", table: "character_items");

        migrationBuilder.DropColumn(name: "GeneratedItemJson", schema: "game", table: "pending_loot_items");
        migrationBuilder.DropColumn(name: "GenerationSeedHash", schema: "game", table: "pending_loot_items");
        migrationBuilder.DropColumn(name: "SourceEntryId", schema: "game", table: "pending_loot_items");
        migrationBuilder.DropColumn(name: "SourceOperationId", schema: "game", table: "pending_loot_items");
        migrationBuilder.DropColumn(name: "SourceType", schema: "game", table: "pending_loot_items");

        migrationBuilder.DropColumn(name: "GeneratedItemJson", schema: "game", table: "combat_loot_rolls");
        migrationBuilder.DropColumn(name: "SourceQualityProfileId", schema: "game", table: "combat_loot_rolls");
    }

    private static readonly string[] ReforgeOperationStateIndexColumns =
    [
        "CharacterId",
        "ItemInstanceId",
        "State"
    ];
}
