using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AccessLab.Infrastructure.Persistence.Migrations;

public partial class CompleteAccessLab : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("TemporaryPasswordExpiresAtUtc", "AspNetUsers",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("PasswordChangedAtUtc", "AspNetUsers",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<bool>("IsTwoFactorPending", "UserSessions", type: "boolean",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>("TwoFactorFailures", "UserSessions", type: "integer",
            nullable: false, defaultValue: 0);

        migrationBuilder.CreateTable("SecuritySettings", columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false),
            MinimumPasswordLength = table.Column<int>(type: "integer", nullable: false),
            RequirePasswordDistance = table.Column<bool>(type: "boolean", nullable: false),
            PasswordValidityDays = table.Column<int>(type: "integer", nullable: false),
            SessionIdleMinutes = table.Column<int>(type: "integer", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_SecuritySettings", x => x.Id));

        migrationBuilder.CreateTable("PasswordHistories", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            UserId = table.Column<string>(type: "text", nullable: false),
            PasswordHash = table.Column<string>(type: "text", nullable: false),
            CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_PasswordHistories", x => x.Id);
            table.ForeignKey("FK_PasswordHistories_AspNetUsers_UserId", x => x.UserId,
                "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_PasswordHistories_UserId", "PasswordHistories", "UserId");

        migrationBuilder.CreateTable("Notes", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            Content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
            AuthorId = table.Column<string>(type: "text", nullable: false),
            CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Notes", x => x.Id);
            table.ForeignKey("FK_Notes_AspNetUsers_AuthorId", x => x.AuthorId,
                "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_Notes_AuthorId", "Notes", "AuthorId");
        migrationBuilder.InsertData("SecuritySettings",
            new[] { "Id", "MinimumPasswordLength", "RequirePasswordDistance", "PasswordValidityDays", "SessionIdleMinutes" },
            new object[] { 1, 16, false, 90, 30 });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Notes");
        migrationBuilder.DropTable("PasswordHistories");
        migrationBuilder.DropTable("SecuritySettings");
        migrationBuilder.DropColumn("IsTwoFactorPending", "UserSessions");
        migrationBuilder.DropColumn("TwoFactorFailures", "UserSessions");
        migrationBuilder.DropColumn("TemporaryPasswordExpiresAtUtc", "AspNetUsers");
        migrationBuilder.DropColumn("PasswordChangedAtUtc", "AspNetUsers");
    }
}
