using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K_Sweet_Kreations_Co_Backend_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomCakeImageStoredFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "CustomCakeImages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "CustomCakeImages");
        }
    }
}
