using System;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260904184500_ContentRevisionsAndReleases")]
public partial class ContentRevisionsAndReleases : Migration
{
    private static readonly string[] RevisionVersionsIndexColumns =
        ["ContentVersion", "BalanceVersion"];
    private static readonly string[] ReleaseRevisionPublishedIndexColumns =
        ["RevisionId", "PublishedAtUtc"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "content_revisions",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ContentVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                BalanceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SourcePublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                PayloadSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_content_revisions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "content_releases",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_content_releases", x => x.Id);
                table.ForeignKey(
                    name: "fk_content_releases_content_revisions_revision_id",
                    column: x => x.RevisionId,
                    principalSchema: "game",
                    principalTable: "content_revisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "content_audit_entries",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                RevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                ReleaseId = table.Column<Guid>(type: "uuid", nullable: true),
                Actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DetailsJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_content_audit_entries", x => x.Id);
                table.ForeignKey(
                    name: "fk_content_audit_entries_content_releases_release_id",
                    column: x => x.ReleaseId,
                    principalSchema: "game",
                    principalTable: "content_releases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_content_audit_entries_content_revisions_revision_id",
                    column: x => x.RevisionId,
                    principalSchema: "game",
                    principalTable: "content_revisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_content_revisions_versions",
            schema: "game",
            table: "content_revisions",
            columns: RevisionVersionsIndexColumns);
        migrationBuilder.CreateIndex(
            name: "ix_content_revisions_payload_sha256",
            schema: "game",
            table: "content_revisions",
            column: "PayloadSha256");
        migrationBuilder.CreateIndex(
            name: "ix_content_revisions_created_at",
            schema: "game",
            table: "content_revisions",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "ix_content_releases_published_at",
            schema: "game",
            table: "content_releases",
            column: "PublishedAtUtc");
        migrationBuilder.CreateIndex(
            name: "ix_content_releases_revision_published_at",
            schema: "game",
            table: "content_releases",
            columns: ReleaseRevisionPublishedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ix_content_audit_entries_occurred_at",
            schema: "game",
            table: "content_audit_entries",
            column: "OccurredAtUtc");
        migrationBuilder.CreateIndex(
            name: "ix_content_audit_entries_revision_id",
            schema: "game",
            table: "content_audit_entries",
            column: "RevisionId");
        migrationBuilder.CreateIndex(
            name: "ix_content_audit_entries_release_id",
            schema: "game",
            table: "content_audit_entries",
            column: "ReleaseId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "content_audit_entries", schema: "game");
        migrationBuilder.DropTable(name: "content_releases", schema: "game");
        migrationBuilder.DropTable(name: "content_revisions", schema: "game");
    }
}
