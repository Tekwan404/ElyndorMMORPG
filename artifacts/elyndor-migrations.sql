CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'game') THEN
            CREATE SCHEMA game;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE TABLE game.accounts (
        "Id" uuid NOT NULL,
        "TelegramUserId" bigint NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "LastSeenAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_accounts PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE TABLE game.characters (
        "Id" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "CreationRequestId" uuid NOT NULL,
        "Name" character varying(16) NOT NULL,
        "NormalizedName" character varying(16) NOT NULL,
        "RaceId" character varying(16) NOT NULL,
        "GenderId" character varying(16) NOT NULL,
        "ClassId" character varying(16) NOT NULL,
        "Level" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_characters PRIMARY KEY ("Id"),
        CONSTRAINT fk_characters_accounts_account_id FOREIGN KEY ("AccountId") REFERENCES game.accounts ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE TABLE game.character_locations (
        "CharacterId" uuid NOT NULL,
        "LocationId" character varying(64) NOT NULL,
        "Version" bigint NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_locations PRIMARY KEY ("CharacterId"),
        CONSTRAINT fk_character_locations_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE TABLE game.travel_operations (
        "CharacterId" uuid NOT NULL,
        "RequestId" uuid NOT NULL,
        "TargetLocationId" character varying(64) NOT NULL,
        "ResultLocationId" character varying(64) NOT NULL,
        "ResultVersion" bigint NOT NULL,
        "CompletedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_travel_operations PRIMARY KEY ("CharacterId", "RequestId"),
        CONSTRAINT fk_travel_operations_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE UNIQUE INDEX uq_accounts_telegram_user_id ON game.accounts ("TelegramUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE UNIQUE INDEX uq_characters_account_id ON game.characters ("AccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE UNIQUE INDEX uq_characters_creation_request_id ON game.characters ("CreationRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    CREATE UNIQUE INDEX uq_characters_normalized_name ON game.characters ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830075709_PhaseOneIdentityWorld') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260830075709_PhaseOneIdentityWorld', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830115817_PhaseTwoCharacterVitals') THEN
    CREATE TABLE game.character_vitals (
        "CharacterId" uuid NOT NULL,
        "CurrentHp" numeric(12,3) NOT NULL,
        "CurrentResource" numeric(12,3) NOT NULL,
        "CheckpointedAtUtc" timestamp with time zone NOT NULL,
        "ContextStartedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_vitals PRIMARY KEY ("CharacterId"),
        CONSTRAINT ck_character_vitals_hp_non_negative CHECK ("CurrentHp" >= 0),
        CONSTRAINT ck_character_vitals_resource_non_negative CHECK ("CurrentResource" >= 0),
        CONSTRAINT fk_character_vitals_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830115817_PhaseTwoCharacterVitals') THEN
    INSERT INTO game.character_vitals
        ("CharacterId", "CurrentHp", "CurrentResource", "CheckpointedAtUtc", "ContextStartedAtUtc")
    SELECT
        "Id",
        50 + 10 * (
            CASE "ClassId"
                WHEN 'WARRIOR' THEN 10 + ("Level" - 1) * 2
                WHEN 'ARCHER' THEN 7 + ("Level" - 1) * 2
                WHEN 'MAGE' THEN 6 + ("Level" - 1) * 2
                ELSE 0
            END),
        CASE "ClassId" WHEN 'WARRIOR' THEN 0 ELSE 100 END,
        "CreatedAtUtc",
        "CreatedAtUtc"
    FROM game.characters;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260830115817_PhaseTwoCharacterVitals') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260830115817_PhaseTwoCharacterVitals', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901051839_TelegramAdministration') THEN
    CREATE TABLE game.admin_command_audits (
        "UpdateId" bigint NOT NULL,
        "AdministratorTelegramUserId" bigint NOT NULL,
        "CommandName" character varying(32) NOT NULL,
        "TargetTelegramUserId" bigint,
        "ResultCode" character varying(64) NOT NULL,
        "ResultSummary" character varying(1024) NOT NULL,
        "ReceivedAtUtc" timestamp with time zone NOT NULL,
        "CompletedAtUtc" timestamp with time zone,
        CONSTRAINT pk_admin_command_audits PRIMARY KEY ("UpdateId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901051839_TelegramAdministration') THEN
    CREATE INDEX ix_admin_command_audits_administrator_received_at ON game.admin_command_audits ("AdministratorTelegramUserId", "ReceivedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901051839_TelegramAdministration') THEN
    CREATE INDEX ix_admin_command_audits_target_received_at ON game.admin_command_audits ("TargetTelegramUserId", "ReceivedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901051839_TelegramAdministration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260901051839_TelegramAdministration', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901060315_PhaseThreeTalentEngine') THEN
    CREATE TABLE game.character_talent_states (
        "CharacterId" uuid NOT NULL,
        "TalentTreeId" character varying(64) NOT NULL,
        "ActiveLoadoutId" character varying(16) NOT NULL,
        "Loadout1RanksJson" jsonb NOT NULL,
        "Loadout2RanksJson" jsonb NOT NULL,
        "TalentVersion" integer NOT NULL,
        "StateVersion" bigint NOT NULL,
        "LastChangedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_talent_states PRIMARY KEY ("CharacterId"),
        CONSTRAINT ck_character_talent_states_active_loadout CHECK ("ActiveLoadoutId" IN ('LOADOUT_1', 'LOADOUT_2')),
        CONSTRAINT ck_character_talent_states_loadout_1_json CHECK (jsonb_typeof("Loadout1RanksJson") = 'object'),
        CONSTRAINT ck_character_talent_states_loadout_2_json CHECK (jsonb_typeof("Loadout2RanksJson") = 'object'),
        CONSTRAINT ck_character_talent_states_state_version CHECK ("StateVersion" > 0),
        CONSTRAINT fk_character_talent_states_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901060315_PhaseThreeTalentEngine') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260901060315_PhaseThreeTalentEngine', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901121333_PhaseThreeTalentMutationIdempotency') THEN
    ALTER TABLE game.character_talent_states ADD "LastMutationId" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901121333_PhaseThreeTalentMutationIdempotency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260901121333_PhaseThreeTalentMutationIdempotency', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    ALTER TABLE game.characters ADD "Experience" bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    CREATE TABLE game.character_items (
        "Id" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "ItemDefinitionId" character varying(64) NOT NULL,
        "Quantity" integer NOT NULL,
        "AcquiredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_items PRIMARY KEY ("Id"),
        CONSTRAINT ck_character_items_quantity_positive CHECK ("Quantity" > 0),
        CONSTRAINT fk_character_items_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    CREATE TABLE game.combat_reward_grants (
        "CombatSessionId" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "MonsterId" character varying(64) NOT NULL,
        "XpEarned" integer NOT NULL,
        "GrantedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_combat_reward_grants PRIMARY KEY ("CombatSessionId"),
        CONSTRAINT ck_combat_reward_grants_xp_non_negative CHECK ("XpEarned" >= 0),
        CONSTRAINT fk_combat_reward_grants_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    CREATE TABLE game.character_equipment (
        "CharacterId" uuid NOT NULL,
        "Slot" character varying(24) NOT NULL,
        "CharacterItemId" uuid NOT NULL,
        CONSTRAINT pk_character_equipment PRIMARY KEY ("CharacterId", "Slot"),
        CONSTRAINT fk_character_equipment_character_items_item_id FOREIGN KEY ("CharacterItemId") REFERENCES game.character_items ("Id") ON DELETE CASCADE,
        CONSTRAINT fk_character_equipment_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    ALTER TABLE game.characters ADD CONSTRAINT ck_characters_experience_non_negative CHECK ("Experience" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    CREATE UNIQUE INDEX uq_character_equipment_item_id ON game.character_equipment ("CharacterItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    CREATE INDEX ix_character_items_character_definition ON game.character_items ("CharacterId", "ItemDefinitionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    CREATE INDEX ix_combat_reward_grants_character_id ON game.combat_reward_grants ("CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902053157_PhaseFiveProgressionLootEquipment') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902053157_PhaseFiveProgressionLootEquipment', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902161251_PersistCharacterGoldRewards') THEN
    ALTER TABLE game.combat_reward_grants ADD "GoldEarned" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902161251_PersistCharacterGoldRewards') THEN
    ALTER TABLE game.characters ADD "Gold" bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902161251_PersistCharacterGoldRewards') THEN
    ALTER TABLE game.combat_reward_grants ADD CONSTRAINT ck_combat_reward_grants_gold_non_negative CHECK ("GoldEarned" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902161251_PersistCharacterGoldRewards') THEN
    ALTER TABLE game.characters ADD CONSTRAINT ck_characters_gold_non_negative CHECK ("Gold" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902161251_PersistCharacterGoldRewards') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902161251_PersistCharacterGoldRewards', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904064000_PhaseD1MerchantMutationSafety') THEN
    CREATE TABLE game.character_mutations (
        "CharacterId" uuid NOT NULL,
        "MutationId" uuid NOT NULL,
        "OperationType" character varying(32) NOT NULL,
        "RequestFingerprint" character varying(64) NOT NULL,
        "CommittedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_mutations PRIMARY KEY ("CharacterId", "MutationId"),
        CONSTRAINT fk_character_mutations_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904064000_PhaseD1MerchantMutationSafety') THEN
    CREATE INDEX ix_character_mutations_character_committed_at ON game.character_mutations ("CharacterId", "CommittedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904064000_PhaseD1MerchantMutationSafety') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260904064000_PhaseD1MerchantMutationSafety', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE TABLE game.content_revisions (
        "Id" uuid NOT NULL,
        "ContentVersion" character varying(64) NOT NULL,
        "BalanceVersion" character varying(64) NOT NULL,
        "SourcePublishedAtUtc" timestamp with time zone NOT NULL,
        "PayloadJson" text NOT NULL,
        "PayloadSha256" character varying(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(128) NOT NULL,
        "Note" character varying(1024),
        CONSTRAINT pk_content_revisions PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE TABLE game.content_releases (
        "Id" uuid NOT NULL,
        "RevisionId" uuid NOT NULL,
        "PublishedAtUtc" timestamp with time zone NOT NULL,
        "PublishedBy" character varying(128) NOT NULL,
        "Note" character varying(1024),
        CONSTRAINT pk_content_releases PRIMARY KEY ("Id"),
        CONSTRAINT fk_content_releases_content_revisions_revision_id FOREIGN KEY ("RevisionId") REFERENCES game.content_revisions ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE TABLE game.content_audit_entries (
        "Id" uuid NOT NULL,
        "Action" character varying(32) NOT NULL,
        "RevisionId" uuid,
        "ReleaseId" uuid,
        "Actor" character varying(128) NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        "DetailsJson" jsonb NOT NULL,
        CONSTRAINT pk_content_audit_entries PRIMARY KEY ("Id"),
        CONSTRAINT fk_content_audit_entries_content_releases_release_id FOREIGN KEY ("ReleaseId") REFERENCES game.content_releases ("Id") ON DELETE RESTRICT,
        CONSTRAINT fk_content_audit_entries_content_revisions_revision_id FOREIGN KEY ("RevisionId") REFERENCES game.content_revisions ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_revisions_versions ON game.content_revisions ("ContentVersion", "BalanceVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_revisions_payload_sha256 ON game.content_revisions ("PayloadSha256");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_revisions_created_at ON game.content_revisions ("CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_releases_published_at ON game.content_releases ("PublishedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_releases_revision_published_at ON game.content_releases ("RevisionId", "PublishedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_audit_entries_occurred_at ON game.content_audit_entries ("OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_audit_entries_revision_id ON game.content_audit_entries ("RevisionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    CREATE INDEX ix_content_audit_entries_release_id ON game.content_audit_entries ("ReleaseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904184500_ContentRevisionsAndReleases') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260904184500_ContentRevisionsAndReleases', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906055000_DurableItemLock') THEN
    ALTER TABLE game.character_items ADD "IsLocked" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906055000_DurableItemLock') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906055000_DurableItemLock', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906074500_ItemInstancePrimaryStatRolls') THEN
    ALTER TABLE game.character_items ADD "DefinitionVersion" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906074500_ItemInstancePrimaryStatRolls') THEN
    ALTER TABLE game.character_items ADD "RolledAgility" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906074500_ItemInstancePrimaryStatRolls') THEN
    ALTER TABLE game.character_items ADD "RolledIntellect" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906074500_ItemInstancePrimaryStatRolls') THEN
    ALTER TABLE game.character_items ADD "RolledStamina" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906074500_ItemInstancePrimaryStatRolls') THEN
    ALTER TABLE game.character_items ADD "RolledStrength" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906074500_ItemInstancePrimaryStatRolls') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906074500_ItemInstancePrimaryStatRolls', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906180500_MultiEnemyCombatRewards') THEN
    ALTER TABLE game.combat_reward_grants ADD "RewardSourcesJson" jsonb NOT NULL DEFAULT ('[]'::jsonb);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906180500_MultiEnemyCombatRewards') THEN
    ALTER TABLE game.combat_reward_grants ADD CONSTRAINT ck_combat_reward_grants_sources_json CHECK (jsonb_typeof("RewardSourcesJson") = 'array');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906180500_MultiEnemyCombatRewards') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906180500_MultiEnemyCombatRewards', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906194500_BossUnlockContracts') THEN
    CREATE TABLE game.character_contract_completions (
        "CharacterId" uuid NOT NULL,
        "ContractId" character varying(64) NOT NULL,
        "TargetMonsterId" character varying(64) NOT NULL,
        "CombatSessionId" uuid NOT NULL,
        "CompletedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_contract_completions PRIMARY KEY ("CharacterId", "ContractId"),
        CONSTRAINT fk_character_contract_completions_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906194500_BossUnlockContracts') THEN
    CREATE INDEX ix_character_contract_completions_combat_session_id ON game.character_contract_completions ("CombatSessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906194500_BossUnlockContracts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906194500_BossUnlockContracts', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906220500_ContractAcceptanceAndCombatCooldowns') THEN
    CREATE TABLE game.character_ability_cooldowns (
        "CharacterId" uuid NOT NULL,
        "AbilityId" character varying(64) NOT NULL,
        "ReadyAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_ability_cooldowns PRIMARY KEY ("CharacterId", "AbilityId"),
        CONSTRAINT fk_character_ability_cooldowns_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906220500_ContractAcceptanceAndCombatCooldowns') THEN
    CREATE TABLE game.character_contract_acceptances (
        "CharacterId" uuid NOT NULL,
        "ContractId" character varying(64) NOT NULL,
        "AcceptedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_contract_acceptances PRIMARY KEY ("CharacterId", "ContractId"),
        CONSTRAINT fk_character_contract_acceptances_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906220500_ContractAcceptanceAndCombatCooldowns') THEN
    CREATE INDEX ix_character_ability_cooldowns_ready_at_utc ON game.character_ability_cooldowns ("ReadyAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906220500_ContractAcceptanceAndCombatCooldowns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906220500_ContractAcceptanceAndCombatCooldowns', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE TABLE game.active_combat_sessions (
        "SessionId" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "StartedAtUtc" timestamp with time zone NOT NULL,
        "ContentVersion" character varying(32) NOT NULL,
        "BalanceVersion" character varying(32) NOT NULL,
        CONSTRAINT pk_active_combat_sessions PRIMARY KEY ("SessionId"),
        CONSTRAINT fk_active_combat_sessions_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE TABLE game.character_travel_states (
        "CharacterId" uuid NOT NULL,
        "RequestId" uuid NOT NULL,
        "FromLocationId" character varying(64) NOT NULL,
        "TargetLocationId" character varying(64) NOT NULL,
        "StartedAtUtc" timestamp with time zone NOT NULL,
        "EndsAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_character_travel_states PRIMARY KEY ("CharacterId"),
        CONSTRAINT ck_character_travel_states_duration CHECK ("EndsAtUtc" > "StartedAtUtc"),
        CONSTRAINT fk_character_travel_states_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE TABLE game.pending_loot_items (
        "Id" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "RewardResolutionId" uuid NOT NULL,
        "ItemDefinitionId" character varying(64) NOT NULL,
        "Quantity" integer NOT NULL,
        "DefinitionVersion" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "RolledStrength" numeric(18,4),
        "RolledAgility" numeric(18,4),
        "RolledIntellect" numeric(18,4),
        "RolledStamina" numeric(18,4),
        CONSTRAINT pk_pending_loot_items PRIMARY KEY ("Id"),
        CONSTRAINT ck_pending_loot_items_quantity_positive CHECK ("Quantity" > 0),
        CONSTRAINT fk_pending_loot_items_characters_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE TABLE game.combat_consumable_uses (
        "SessionId" uuid NOT NULL,
        "CommandId" character varying(128) NOT NULL,
        "CharacterId" uuid NOT NULL,
        "ItemDefinitionId" character varying(64) NOT NULL,
        "DefinitionVersion" integer NOT NULL,
        "MaxStack" integer NOT NULL,
        "UsedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_combat_consumable_uses PRIMARY KEY ("SessionId", "CommandId"),
        CONSTRAINT ck_combat_consumable_uses_definition_version CHECK ("DefinitionVersion" > 0),
        CONSTRAINT ck_combat_consumable_uses_max_stack CHECK ("MaxStack" >= 2),
        CONSTRAINT fk_combat_consumable_uses_active_combat_session_id FOREIGN KEY ("SessionId") REFERENCES game.active_combat_sessions ("SessionId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE UNIQUE INDEX uq_active_combat_sessions_character_id ON game.active_combat_sessions ("CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE INDEX ix_active_combat_sessions_started_at_utc ON game.active_combat_sessions ("StartedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE INDEX ix_character_travel_states_ends_at_utc ON game.character_travel_states ("EndsAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE UNIQUE INDEX uq_character_travel_states_character_request ON game.character_travel_states ("CharacterId", "RequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE INDEX ix_pending_loot_items_character_created_at ON game.pending_loot_items ("CharacterId", "CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE INDEX ix_pending_loot_items_reward_resolution_id ON game.pending_loot_items ("RewardResolutionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    CREATE INDEX ix_combat_consumable_uses_character_id ON game.combat_consumable_uses ("CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907093000_RedPriorityCoreCorrectness') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260907093000_RedPriorityCoreCorrectness', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    ALTER TABLE game.characters ADD "PublicCode" character varying(14) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    UPDATE game.characters SET "PublicCode" = 'ELY-' || upper(substr(replace("Id"::text, '-', ''), 1, 10)) WHERE "PublicCode" = '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    ALTER TABLE game.characters ALTER COLUMN "PublicCode" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    ALTER TABLE game.accounts ADD "NormalizedTelegramUsername" character varying(32);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    ALTER TABLE game.accounts ADD "TelegramUsername" character varying(33);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE TABLE game.friend_requests (
        "Id" uuid NOT NULL,
        "RequesterCharacterId" uuid NOT NULL,
        "TargetCharacterId" uuid NOT NULL,
        "PairKey" character varying(73) NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DecidedAtUtc" timestamp with time zone,
        "DecidedByCharacterId" uuid,
        CONSTRAINT pk_friend_requests PRIMARY KEY ("Id"),
        CONSTRAINT fk_friend_requests_requester_character FOREIGN KEY ("RequesterCharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE,
        CONSTRAINT fk_friend_requests_target_character FOREIGN KEY ("TargetCharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE TABLE game.friendships (
        "PairKey" character varying(73) NOT NULL,
        "CharacterAId" uuid NOT NULL,
        "CharacterBId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_friendships PRIMARY KEY ("PairKey"),
        CONSTRAINT fk_friendships_character_a FOREIGN KEY ("CharacterAId") REFERENCES game.characters ("Id") ON DELETE CASCADE,
        CONSTRAINT fk_friendships_character_b FOREIGN KEY ("CharacterBId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE UNIQUE INDEX uq_characters_public_code ON game.characters ("PublicCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE UNIQUE INDEX uq_accounts_normalized_telegram_username ON game.accounts ("NormalizedTelegramUsername");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE INDEX "IX_friend_requests_RequesterCharacterId" ON game.friend_requests ("RequesterCharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE INDEX ix_friend_requests_target_character_id ON game.friend_requests ("TargetCharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE UNIQUE INDEX uq_friend_requests_pending_pair ON game.friend_requests ("PairKey", "Status") WHERE "Status" = 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE INDEX ix_friendships_character_a_id ON game.friendships ("CharacterAId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    CREATE INDEX ix_friendships_character_b_id ON game.friendships ("CharacterBId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908070104_PhaseSixSocialFriends') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260908070104_PhaseSixSocialFriends', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE TABLE game.parties (
        "Id" uuid NOT NULL,
        "CreationRequestId" uuid NOT NULL,
        "LeaderCharacterId" uuid NOT NULL,
        "State" integer NOT NULL,
        "Version" bigint NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_parties PRIMARY KEY ("Id"),
        CONSTRAINT fk_parties_leader_character FOREIGN KEY ("LeaderCharacterId") REFERENCES game.characters ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE TABLE game.party_invites (
        "Id" uuid NOT NULL,
        "PartyId" uuid NOT NULL,
        "InviterCharacterId" uuid NOT NULL,
        "TargetCharacterId" uuid NOT NULL,
        "Mode" integer NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "DecidedAtUtc" timestamp with time zone,
        CONSTRAINT pk_party_invites PRIMARY KEY ("Id"),
        CONSTRAINT fk_party_invites_inviter_character_id FOREIGN KEY ("InviterCharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE,
        CONSTRAINT fk_party_invites_party_id FOREIGN KEY ("PartyId") REFERENCES game.parties ("Id") ON DELETE CASCADE,
        CONSTRAINT fk_party_invites_target_character_id FOREIGN KEY ("TargetCharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE TABLE game.party_members (
        "PartyId" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "JoinedAtUtc" timestamp with time zone NOT NULL,
        "State" integer NOT NULL,
        CONSTRAINT pk_party_members PRIMARY KEY ("PartyId", "CharacterId"),
        CONSTRAINT fk_party_members_character_id FOREIGN KEY ("CharacterId") REFERENCES game.characters ("Id") ON DELETE CASCADE,
        CONSTRAINT fk_party_members_parties_party_id FOREIGN KEY ("PartyId") REFERENCES game.parties ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE INDEX ix_parties_leader_character_id ON game.parties ("LeaderCharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE UNIQUE INDEX uq_parties_creation_request_id ON game.parties ("CreationRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE INDEX "IX_party_invites_InviterCharacterId" ON game.party_invites ("InviterCharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE INDEX "IX_party_invites_PartyId" ON game.party_invites ("PartyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE INDEX ix_party_invites_target_status ON game.party_invites ("TargetCharacterId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    CREATE UNIQUE INDEX uq_party_members_character_id ON game.party_members ("CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908071000_PhaseSixParties') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260908071000_PhaseSixParties', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090325_PhaseSevenMultiplayerCombatParticipants') THEN
    ALTER TABLE game.combat_consumable_uses DROP CONSTRAINT fk_combat_consumable_uses_active_combat_session_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090325_PhaseSevenMultiplayerCombatParticipants') THEN
    ALTER TABLE game.active_combat_sessions DROP CONSTRAINT pk_active_combat_sessions;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090325_PhaseSevenMultiplayerCombatParticipants') THEN
    ALTER TABLE game.active_combat_sessions ADD CONSTRAINT pk_active_combat_sessions PRIMARY KEY ("SessionId", "CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090325_PhaseSevenMultiplayerCombatParticipants') THEN
    CREATE INDEX "IX_combat_consumable_uses_SessionId_CharacterId" ON game.combat_consumable_uses ("SessionId", "CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090325_PhaseSevenMultiplayerCombatParticipants') THEN
    CREATE INDEX ix_active_combat_sessions_session_id ON game.active_combat_sessions ("SessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090325_PhaseSevenMultiplayerCombatParticipants') THEN
    ALTER TABLE game.combat_consumable_uses ADD CONSTRAINT fk_combat_consumable_uses_active_combat_session FOREIGN KEY ("SessionId", "CharacterId") REFERENCES game.active_combat_sessions ("SessionId", "CharacterId") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090325_PhaseSevenMultiplayerCombatParticipants') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260908090325_PhaseSevenMultiplayerCombatParticipants', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090717_PhaseEightPerParticipantCombatRewards') THEN
    ALTER TABLE game.combat_reward_grants DROP CONSTRAINT pk_combat_reward_grants;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090717_PhaseEightPerParticipantCombatRewards') THEN
    ALTER TABLE game.combat_reward_grants ADD CONSTRAINT pk_combat_reward_grants PRIMARY KEY ("CombatSessionId", "CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908090717_PhaseEightPerParticipantCombatRewards') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260908090717_PhaseEightPerParticipantCombatRewards', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908091938_PhaseNinePersistedCombatLootRolls') THEN
    CREATE TABLE game.combat_loot_rolls (
        "LootRollId" uuid NOT NULL,
        "DungeonRunId" uuid NOT NULL,
        "CombatSessionId" uuid NOT NULL,
        "ItemDefinitionId" character varying(64) NOT NULL,
        "Rarity" character varying(16) NOT NULL,
        "Quantity" integer NOT NULL,
        "ItemInstanceSeed" uuid NOT NULL,
        "EligibleCharacterIdsJson" jsonb NOT NULL,
        "ChoicesJson" jsonb NOT NULL,
        "RollsJson" jsonb NOT NULL,
        "EndsAtUtc" timestamp with time zone NOT NULL,
        "State" character varying(16) NOT NULL,
        "WinnerCharacterId" uuid,
        "ResolvedAtUtc" timestamp with time zone,
        CONSTRAINT pk_combat_loot_rolls PRIMARY KEY ("LootRollId"),
        CONSTRAINT ck_combat_loot_rolls_quantity_positive CHECK ("Quantity" > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908091938_PhaseNinePersistedCombatLootRolls') THEN
    CREATE INDEX ix_combat_loot_rolls_state_ends_at_utc ON game.combat_loot_rolls ("State", "EndsAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908091938_PhaseNinePersistedCombatLootRolls') THEN
    CREATE UNIQUE INDEX uq_combat_loot_rolls_session_item ON game.combat_loot_rolls ("CombatSessionId", "ItemDefinitionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908091938_PhaseNinePersistedCombatLootRolls') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260908091938_PhaseNinePersistedCombatLootRolls', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE TABLE game.dungeon_runs (
        "Id" uuid NOT NULL,
        "PartyId" uuid NOT NULL,
        "DungeonId" character varying(64) NOT NULL,
        "State" character varying(16) NOT NULL,
        "CurrentEncounterIndex" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "CompletedAtUtc" timestamp with time zone,
        CONSTRAINT pk_dungeon_runs PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE TABLE game.dungeon_encounters (
        "Id" uuid NOT NULL,
        "RunId" uuid NOT NULL,
        "EncounterIndex" integer NOT NULL,
        "MonsterId" character varying(64) NOT NULL,
        "State" character varying(16) NOT NULL,
        "CombatSessionId" uuid,
        "WipeCount" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "CompletedAtUtc" timestamp with time zone,
        CONSTRAINT pk_dungeon_encounters PRIMARY KEY ("Id"),
        CONSTRAINT "FK_dungeon_encounters_dungeon_runs_RunId" FOREIGN KEY ("RunId") REFERENCES game.dungeon_runs ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE TABLE game.dungeon_run_members (
        "RunId" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "JoinedAtUtc" timestamp with time zone NOT NULL,
        "State" character varying(16) NOT NULL,
        CONSTRAINT pk_dungeon_run_members PRIMARY KEY ("RunId", "CharacterId"),
        CONSTRAINT "FK_dungeon_run_members_dungeon_runs_RunId" FOREIGN KEY ("RunId") REFERENCES game.dungeon_runs ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE TABLE game.dungeon_encounter_members (
        "EncounterId" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "JoinedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT pk_dungeon_encounter_members PRIMARY KEY ("EncounterId", "CharacterId"),
        CONSTRAINT "FK_dungeon_encounter_members_dungeon_encounters_EncounterId" FOREIGN KEY ("EncounterId") REFERENCES game.dungeon_encounters ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE INDEX ix_dungeon_encounter_members_character_id ON game.dungeon_encounter_members ("CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE INDEX ix_dungeon_encounters_combat_session_id ON game.dungeon_encounters ("CombatSessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE UNIQUE INDEX uq_dungeon_encounters_run_index ON game.dungeon_encounters ("RunId", "EncounterIndex");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE INDEX ix_dungeon_run_members_character_id ON game.dungeon_run_members ("CharacterId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    CREATE INDEX ix_dungeon_runs_party_state ON game.dungeon_runs ("PartyId", "State");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908093518_PhaseTenDungeonRuns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260908093518_PhaseTenDungeonRuns', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908094839_PhaseElevenDungeonRunIdempotency') THEN
    ALTER TABLE game.dungeon_runs ADD "CreationRequestId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908094839_PhaseElevenDungeonRunIdempotency') THEN
    UPDATE game.dungeon_runs SET "CreationRequestId" = md5("Id"::text)::uuid WHERE "CreationRequestId" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908094839_PhaseElevenDungeonRunIdempotency') THEN
    ALTER TABLE game.dungeon_runs ALTER COLUMN "CreationRequestId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908094839_PhaseElevenDungeonRunIdempotency') THEN
    CREATE UNIQUE INDEX uq_dungeon_runs_creation_request_id ON game.dungeon_runs ("CreationRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260908094839_PhaseElevenDungeonRunIdempotency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260908094839_PhaseElevenDungeonRunIdempotency', '10.0.11');
    END IF;
END $EF$;
COMMIT;

