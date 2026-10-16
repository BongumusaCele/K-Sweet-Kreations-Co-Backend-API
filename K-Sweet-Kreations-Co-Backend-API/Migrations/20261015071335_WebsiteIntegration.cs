using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K_Sweet_Kreations_Co_Backend_API.Migrations
{
    /// <inheritdoc />
    public partial class WebsiteIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerOrders_QuotationId",
                table: "CustomerOrders");

            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Servings",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoredProofName",
                table: "Payments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CancelRequested",
                table: "CustomerOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "CustomerOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "CustomerOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "CustomerOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RequestId",
                table: "CustomerOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlotApproved",
                table: "CustomerOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "CustomerOrders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "CustomCakeRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "CustomCakeRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BlockedCollectionDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedCollectionDates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReviews", x => x.Id);
                });

            migrationBuilder.Sql(@"
                UPDATE UserAccounts SET Role = 'Staff' WHERE Role = 'Consultant';
                UPDATE CustomCakeRequests SET Status = CASE Status
                    WHEN 'Pending' THEN 'Submitted' WHEN 'Under Review' THEN 'Under review'
                    WHEN 'Rejected' THEN 'Declined' ELSE Status END;
                UPDATE Quotations SET Status = 'Quoted' WHERE Status = 'Pending';
                UPDATE CustomCakeRequests SET Status = 'Accepted' WHERE EXISTS (
                    SELECT 1 FROM Quotations q WHERE q.CustomCakeRequestId = CustomCakeRequests.CustomCakeRequestId AND q.Status = 'Accepted');
                UPDATE CustomerOrders SET OrderStatus = CASE OrderStatus
                    WHEN 'Pending' THEN 'Awaiting confirmation' WHEN 'Pending Deposit' THEN 'Awaiting confirmation'
                    WHEN 'Deposit Paid' THEN 'Awaiting confirmation' WHEN 'Paid' THEN 'Awaiting confirmation'
                    WHEN 'In Preparation' THEN 'In progress' WHEN 'Ready for Collection' THEN 'Ready for collection'
                    ELSE OrderStatus END;
                UPDATE Enquiries SET Status = CASE Status WHEN 'Pending' THEN 'New' WHEN 'In Progress' THEN 'In progress' ELSE Status END;
                UPDATE o SET ContactName = u.FullName, ContactPhone = u.PhoneNumber,
                    SlotApproved = CASE WHEN OrderStatus IN ('In progress', 'Ready for collection', 'Collected') THEN 1 ELSE 0 END
                    FROM CustomerOrders o JOIN UserAccounts u ON u.UserId = o.CustomerId;
                UPDATE r SET ContactName = u.FullName, ContactPhone = u.PhoneNumber
                    FROM CustomCakeRequests r JOIN UserAccounts u ON u.UserId = r.CustomerId;
                UPDATE o SET RequestId = q.CustomCakeRequestId FROM CustomerOrders o JOIN Quotations q ON q.QuotationId = o.QuotationId;
                UPDATE Payments SET PaymentReference = NULLIF(UPPER(LTRIM(RTRIM(PaymentReference))), '');
                IF EXISTS (SELECT PaymentReference FROM Payments WHERE PaymentReference IS NOT NULL GROUP BY PaymentReference HAVING COUNT(*) > 1)
                    THROW 51000, 'Resolve duplicate historical payment references before applying WebsiteIntegration.', 1;
                IF EXISTS (SELECT RequestId FROM CustomerOrders WHERE RequestId IS NOT NULL GROUP BY RequestId HAVING COUNT(*) > 1)
                    THROW 51001, 'Resolve duplicate orders for the same cake request before applying WebsiteIntegration.', 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentReference",
                table: "Payments",
                column: "PaymentReference",
                unique: true,
                filter: "[PaymentReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_QuotationId",
                table: "CustomerOrders",
                column: "QuotationId",
                unique: true,
                filter: "[QuotationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_RequestId",
                table: "CustomerOrders",
                column: "RequestId",
                unique: true,
                filter: "[RequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BlockedCollectionDates_Date",
                table: "BlockedCollectionDates",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedCollectionDates");

            migrationBuilder.DropTable(
                name: "CustomerReviews");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentReference",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerOrders_QuotationId",
                table: "CustomerOrders");

            migrationBuilder.DropIndex(
                name: "IX_CustomerOrders_RequestId",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Servings",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StoredProofName",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CancelRequested",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "SlotApproved",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "CustomCakeRequests");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "CustomCakeRequests");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_QuotationId",
                table: "CustomerOrders",
                column: "QuotationId");
        }
    }
}
