using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RBiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tenants",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SyncOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_User_Sync_RowVersion",
                table: "Users",
                columns: new[] { "IsDeleted", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Sync_RowVersion",
                table: "Tenants",
                columns: new[] { "IsDeleted", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperations_AppliedAtUtc",
                table: "SyncOperations",
                column: "AppliedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_SyncOperations_Tenant_Operation",
                table: "SyncOperations",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncOperations");

            migrationBuilder.DropIndex(
                name: "IX_User_Sync_RowVersion",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tenant_Sync_RowVersion",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tenants");
        }
    }
}
