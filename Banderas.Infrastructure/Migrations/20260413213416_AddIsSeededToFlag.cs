using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banderas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSeededToFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeeded",
                table: "flags",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSeeded",
                table: "flags");
        }
    }
}
