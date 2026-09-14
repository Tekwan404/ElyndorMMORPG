using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Elyndor.Infrastructure.Persistence.Migrations;

// The existing generated snapshot is the frozen model before professions. Keep it as the
// historical base and extend it with the two profession entities introduced by this migration.
// The abstract modifier keeps EF from selecting the base snapshot itself.
abstract partial class GameDbContextModelSnapshot;

[DbContext(typeof(GameDbContext))]
internal sealed class GameDbContextProfessionModelSnapshot : GameDbContextModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        base.BuildModel(modelBuilder);

        modelBuilder.Entity("Elyndor.Core.Professions.CharacterProfession", builder =>
        {
            builder.Property<Guid>("CharacterId")
                .HasColumnType("uuid")
                .HasColumnName("character_id");

            builder.Property<string>("ProfessionId")
                .HasMaxLength(64)
                .HasColumnType("character varying(64)")
                .HasColumnName("profession_id");

            builder.Property<DateTimeOffset>("LearnedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("learned_at_utc");

            builder.Property<int>("Skill")
                .HasColumnType("integer")
                .HasColumnName("skill");

            builder.Property<DateTimeOffset>("UpdatedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at_utc");

            builder.HasKey("CharacterId", "ProfessionId");
            builder.HasIndex("CharacterId");
            builder.ToTable("character_professions", "game");
        });

        modelBuilder.Entity("Elyndor.Core.Professions.SkinnableCorpse", builder =>
        {
            builder.Property<Guid>("CharacterId")
                .HasColumnType("uuid")
                .HasColumnName("character_id");

            builder.Property<Guid>("CombatSessionId")
                .HasColumnType("uuid")
                .HasColumnName("combat_session_id");

            builder.Property<Guid>("EnemyActorId")
                .HasColumnType("uuid")
                .HasColumnName("enemy_actor_id");

            builder.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at_utc");

            builder.Property<DateTimeOffset>("ExpiresAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("expires_at_utc");

            builder.Property<string>("MonsterDefinitionId")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("monster_definition_id");

            builder.Property<bool>("SkillIncreased")
                .HasColumnType("boolean")
                .HasColumnName("skill_increased");

            builder.Property<DateTimeOffset?>("SkinnedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("skinned_at_utc");

            builder.Property<Guid?>("SkinningMutationId")
                .HasColumnType("uuid")
                .HasColumnName("skinning_mutation_id");

            builder.Property<string>("YieldItemId")
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("yield_item_id");

            builder.Property<int?>("YieldQuantity")
                .HasColumnType("integer")
                .HasColumnName("yield_quantity");

            builder.HasKey("CharacterId", "CombatSessionId", "EnemyActorId");
            builder.HasIndex("CharacterId", "ExpiresAtUtc");
            builder.ToTable("character_skinnable_corpses", "game");
        });

        modelBuilder.Entity("Elyndor.Core.Professions.CharacterProfession", builder =>
        {
            builder.HasOne("Elyndor.Core.Characters.Character", null)
                .WithMany()
                .HasForeignKey("CharacterId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("Elyndor.Core.Professions.SkinnableCorpse", builder =>
        {
            builder.HasOne("Elyndor.Core.Characters.Character", null)
                .WithMany()
                .HasForeignKey("CharacterId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
