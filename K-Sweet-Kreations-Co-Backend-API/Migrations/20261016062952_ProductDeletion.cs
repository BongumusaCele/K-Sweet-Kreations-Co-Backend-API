using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K_Sweet_Kreations_Co_Backend_API.Migrations
{
    /// <inheritdoc />
    public partial class ProductDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Products",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Products");
        }
    }
}
