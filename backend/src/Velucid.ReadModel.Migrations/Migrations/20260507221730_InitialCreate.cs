using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velucid.ReadModel.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "org_projection",
                schema: "public",
                columns: table => new
                {
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_projection", x => x.org_id);
                });

            migrationBuilder.CreateTable(
                name: "user_projection",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_projection", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "org_invitation_projection",
                schema: "public",
                columns: table => new
                {
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    invited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_invitation_projection", x => new { x.org_id, x.email });
                    table.CheckConstraint("ck_invitation_role", "role IN ('Owner', 'Member')");
                    table.CheckConstraint("ck_invitation_status", "status IN ('Pending', 'Accepted', 'Declined')");
                    table.ForeignKey(
                        name: "fk_org_invitation_projection_org_projection_org_id",
                        column: x => x.org_id,
                        principalSchema: "public",
                        principalTable: "org_projection",
                        principalColumn: "org_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_member_projection",
                schema: "public",
                columns: table => new
                {
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_member_projection", x => new { x.org_id, x.user_id });
                    table.CheckConstraint("ck_org_member_role", "role IN ('Owner', 'Member')");
                    table.ForeignKey(
                        name: "fk_org_member_projection_org_projection_org_id",
                        column: x => x.org_id,
                        principalSchema: "public",
                        principalTable: "org_projection",
                        principalColumn: "org_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_org_member_projection_user_projection_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user_projection",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_identity",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<string>(type: "text", nullable: false),
                    provider_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_identity", x => new { x.user_id, x.provider_id });
                    table.ForeignKey(
                        name: "fk_user_identity_user_projection_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user_projection",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_org_projection",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_org_projection", x => new { x.user_id, x.org_id });
                    table.ForeignKey(
                        name: "fk_user_org_projection_org_projection_org_id",
                        column: x => x.org_id,
                        principalSchema: "public",
                        principalTable: "org_projection",
                        principalColumn: "org_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_org_projection_user_projection_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user_projection",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_org_invitation_projection_email_status",
                schema: "public",
                table: "org_invitation_projection",
                columns: new[] { "email", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_org_member_projection_user_id",
                schema: "public",
                table: "org_member_projection",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_identity_email",
                schema: "public",
                table: "user_identity",
                column: "email",
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_identity_provider_id",
                schema: "public",
                table: "user_identity",
                column: "provider_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_org_projection_org_id",
                schema: "public",
                table: "user_org_projection",
                column: "org_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_invitation_projection",
                schema: "public");

            migrationBuilder.DropTable(
                name: "org_member_projection",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_identity",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_org_projection",
                schema: "public");

            migrationBuilder.DropTable(
                name: "org_projection",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_projection",
                schema: "public");
        }
    }
}
