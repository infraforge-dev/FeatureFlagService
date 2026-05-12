using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banderas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlagDescriptionAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "flags",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "flags",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "flags");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "flags");
        }
    }
}
