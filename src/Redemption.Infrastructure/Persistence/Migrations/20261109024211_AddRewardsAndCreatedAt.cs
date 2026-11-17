using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Redemption.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardsAndCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Redemptions",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.Sku);
                });

            migrationBuilder.InsertData(
                table: "Rewards",
                columns: new[] { "Sku", "Category", "IsActive", "Name", "Points" },
                values: new object[,]
                {
                    { "BOTTLE-01", "Merch", true, "Reusable bottle", 1000 },
                    { "CAP-01", "Merch", true, "Branded cap", 500 },
                    { "COOLER-01", "Home", true, "Portable cooler", 5000 },
                    { "RW-001", "Beverages", true, "Reward 001", 137 },
                    { "RW-002", "Electronics", true, "Reward 002", 174 },
                    { "RW-003", "Home", true, "Reward 003", 211 },
                    { "RW-004", "Vouchers", true, "Reward 004", 248 },
                    { "RW-005", "Merch", true, "Reward 005", 285 },
                    { "RW-006", "Beverages", true, "Reward 006", 322 },
                    { "RW-007", "Electronics", true, "Reward 007", 359 },
                    { "RW-008", "Home", true, "Reward 008", 396 },
                    { "RW-009", "Vouchers", true, "Reward 009", 433 },
                    { "RW-010", "Merch", true, "Reward 010", 470 },
                    { "RW-011", "Beverages", true, "Reward 011", 507 },
                    { "RW-012", "Electronics", true, "Reward 012", 544 },
                    { "RW-013", "Home", true, "Reward 013", 581 },
                    { "RW-014", "Vouchers", true, "Reward 014", 618 },
                    { "RW-015", "Merch", true, "Reward 015", 655 },
                    { "RW-016", "Beverages", true, "Reward 016", 692 },
                    { "RW-017", "Electronics", true, "Reward 017", 729 },
                    { "RW-018", "Home", true, "Reward 018", 766 },
                    { "RW-019", "Vouchers", true, "Reward 019", 803 },
                    { "RW-020", "Merch", true, "Reward 020", 840 },
                    { "RW-021", "Beverages", true, "Reward 021", 877 },
                    { "RW-022", "Electronics", true, "Reward 022", 914 },
                    { "RW-023", "Home", true, "Reward 023", 951 },
                    { "RW-024", "Vouchers", true, "Reward 024", 988 },
                    { "RW-025", "Merch", true, "Reward 025", 1025 },
                    { "RW-026", "Beverages", true, "Reward 026", 1062 },
                    { "RW-027", "Electronics", true, "Reward 027", 1099 },
                    { "RW-028", "Home", true, "Reward 028", 1136 },
                    { "RW-029", "Vouchers", true, "Reward 029", 1173 },
                    { "RW-030", "Merch", true, "Reward 030", 1210 },
                    { "RW-031", "Beverages", true, "Reward 031", 1247 },
                    { "RW-032", "Electronics", true, "Reward 032", 1284 },
                    { "RW-033", "Home", true, "Reward 033", 1321 },
                    { "RW-034", "Vouchers", true, "Reward 034", 1358 },
                    { "RW-035", "Merch", true, "Reward 035", 1395 },
                    { "RW-036", "Beverages", true, "Reward 036", 1432 },
                    { "RW-037", "Electronics", true, "Reward 037", 1469 },
                    { "RW-038", "Home", true, "Reward 038", 1506 },
                    { "RW-039", "Vouchers", true, "Reward 039", 1543 },
                    { "RW-040", "Merch", true, "Reward 040", 1580 },
                    { "RW-041", "Beverages", true, "Reward 041", 1617 },
                    { "RW-042", "Electronics", true, "Reward 042", 1654 },
                    { "RW-043", "Home", true, "Reward 043", 1691 },
                    { "RW-044", "Vouchers", true, "Reward 044", 1728 },
                    { "RW-045", "Merch", true, "Reward 045", 1765 },
                    { "RW-046", "Beverages", true, "Reward 046", 1802 },
                    { "RW-047", "Electronics", true, "Reward 047", 1839 },
                    { "RW-048", "Home", true, "Reward 048", 1876 },
                    { "RW-049", "Vouchers", true, "Reward 049", 1913 },
                    { "RW-050", "Merch", true, "Reward 050", 1950 },
                    { "RW-051", "Beverages", true, "Reward 051", 1987 },
                    { "RW-052", "Electronics", true, "Reward 052", 2024 },
                    { "RW-053", "Home", true, "Reward 053", 2061 },
                    { "RW-054", "Vouchers", true, "Reward 054", 2098 },
                    { "RW-055", "Merch", true, "Reward 055", 2135 },
                    { "RW-056", "Beverages", true, "Reward 056", 2172 },
                    { "RW-057", "Electronics", true, "Reward 057", 2209 },
                    { "RW-058", "Home", true, "Reward 058", 2246 },
                    { "RW-059", "Vouchers", true, "Reward 059", 2283 },
                    { "RW-060", "Merch", true, "Reward 060", 2320 },
                    { "RW-061", "Beverages", true, "Reward 061", 2357 },
                    { "RW-062", "Electronics", true, "Reward 062", 2394 },
                    { "RW-063", "Home", true, "Reward 063", 2431 },
                    { "RW-064", "Vouchers", true, "Reward 064", 2468 },
                    { "RW-065", "Merch", true, "Reward 065", 2505 },
                    { "RW-066", "Beverages", true, "Reward 066", 2542 },
                    { "RW-067", "Electronics", true, "Reward 067", 2579 },
                    { "RW-068", "Home", true, "Reward 068", 2616 },
                    { "RW-069", "Vouchers", true, "Reward 069", 2653 },
                    { "RW-070", "Merch", true, "Reward 070", 2690 },
                    { "RW-071", "Beverages", true, "Reward 071", 2727 },
                    { "RW-072", "Electronics", true, "Reward 072", 2764 },
                    { "RW-073", "Home", true, "Reward 073", 2801 },
                    { "RW-074", "Vouchers", true, "Reward 074", 2838 },
                    { "RW-075", "Merch", true, "Reward 075", 2875 },
                    { "RW-076", "Beverages", true, "Reward 076", 2912 },
                    { "RW-077", "Electronics", true, "Reward 077", 2949 },
                    { "RW-078", "Home", true, "Reward 078", 2986 },
                    { "RW-079", "Vouchers", true, "Reward 079", 3023 },
                    { "RW-080", "Merch", true, "Reward 080", 3060 },
                    { "RW-081", "Beverages", true, "Reward 081", 3097 },
                    { "RW-082", "Electronics", true, "Reward 082", 3134 },
                    { "RW-083", "Home", true, "Reward 083", 3171 },
                    { "RW-084", "Vouchers", true, "Reward 084", 3208 },
                    { "RW-085", "Merch", true, "Reward 085", 3245 },
                    { "RW-086", "Beverages", true, "Reward 086", 3282 },
                    { "RW-087", "Electronics", true, "Reward 087", 3319 },
                    { "RW-088", "Home", true, "Reward 088", 3356 },
                    { "RW-089", "Vouchers", true, "Reward 089", 3393 },
                    { "RW-090", "Merch", true, "Reward 090", 3430 },
                    { "RW-091", "Beverages", true, "Reward 091", 3467 },
                    { "RW-092", "Electronics", true, "Reward 092", 3504 },
                    { "RW-093", "Home", true, "Reward 093", 3541 },
                    { "RW-094", "Vouchers", true, "Reward 094", 3578 },
                    { "RW-095", "Merch", true, "Reward 095", 3615 },
                    { "RW-096", "Beverages", true, "Reward 096", 3652 },
                    { "RW-097", "Electronics", true, "Reward 097", 3689 },
                    { "RW-098", "Home", true, "Reward 098", 3726 },
                    { "RW-099", "Vouchers", true, "Reward 099", 3763 },
                    { "RW-100", "Merch", true, "Reward 100", 3800 },
                    { "RW-101", "Beverages", true, "Reward 101", 3837 },
                    { "RW-102", "Electronics", true, "Reward 102", 3874 },
                    { "RW-103", "Home", true, "Reward 103", 3911 },
                    { "RW-104", "Vouchers", true, "Reward 104", 3948 },
                    { "RW-105", "Merch", true, "Reward 105", 3985 },
                    { "RW-106", "Beverages", true, "Reward 106", 4022 },
                    { "RW-107", "Electronics", true, "Reward 107", 4059 },
                    { "RW-108", "Home", true, "Reward 108", 4096 },
                    { "RW-109", "Vouchers", true, "Reward 109", 4133 },
                    { "RW-110", "Merch", true, "Reward 110", 4170 },
                    { "RW-111", "Beverages", true, "Reward 111", 4207 },
                    { "RW-112", "Electronics", true, "Reward 112", 4244 },
                    { "RW-113", "Home", true, "Reward 113", 4281 },
                    { "RW-114", "Vouchers", true, "Reward 114", 4318 },
                    { "RW-115", "Merch", true, "Reward 115", 4355 },
                    { "RW-116", "Beverages", true, "Reward 116", 4392 },
                    { "RW-117", "Electronics", true, "Reward 117", 4429 },
                    { "RW-118", "Home", true, "Reward 118", 4466 },
                    { "RW-119", "Vouchers", true, "Reward 119", 4503 },
                    { "RW-120", "Merch", true, "Reward 120", 4540 },
                    { "RW-121", "Beverages", true, "Reward 121", 4577 },
                    { "RW-122", "Electronics", true, "Reward 122", 4614 },
                    { "RW-123", "Home", true, "Reward 123", 4651 },
                    { "RW-124", "Vouchers", true, "Reward 124", 4688 },
                    { "RW-125", "Merch", true, "Reward 125", 4725 },
                    { "RW-126", "Beverages", true, "Reward 126", 4762 },
                    { "RW-127", "Electronics", true, "Reward 127", 4799 },
                    { "RW-128", "Home", true, "Reward 128", 4836 },
                    { "RW-129", "Vouchers", true, "Reward 129", 4873 },
                    { "RW-130", "Merch", true, "Reward 130", 4910 },
                    { "RW-131", "Beverages", true, "Reward 131", 4947 },
                    { "RW-132", "Electronics", true, "Reward 132", 4984 },
                    { "RW-133", "Home", true, "Reward 133", 121 },
                    { "RW-134", "Vouchers", true, "Reward 134", 158 },
                    { "RW-135", "Merch", true, "Reward 135", 195 },
                    { "RW-136", "Beverages", true, "Reward 136", 232 },
                    { "RW-137", "Electronics", true, "Reward 137", 269 },
                    { "RW-138", "Home", true, "Reward 138", 306 },
                    { "RW-139", "Vouchers", true, "Reward 139", 343 },
                    { "RW-140", "Merch", true, "Reward 140", 380 },
                    { "RW-141", "Beverages", true, "Reward 141", 417 },
                    { "RW-142", "Electronics", true, "Reward 142", 454 },
                    { "RW-143", "Home", true, "Reward 143", 491 },
                    { "RW-144", "Vouchers", true, "Reward 144", 528 },
                    { "RW-145", "Merch", true, "Reward 145", 565 },
                    { "RW-146", "Beverages", true, "Reward 146", 602 },
                    { "RW-147", "Electronics", true, "Reward 147", 639 },
                    { "RW-148", "Home", true, "Reward 148", 676 },
                    { "RW-149", "Vouchers", true, "Reward 149", 713 },
                    { "RW-150", "Merch", true, "Reward 150", 750 },
                    { "RW-151", "Beverages", true, "Reward 151", 787 },
                    { "RW-152", "Electronics", true, "Reward 152", 824 },
                    { "RW-153", "Home", true, "Reward 153", 861 },
                    { "RW-154", "Vouchers", true, "Reward 154", 898 },
                    { "RW-155", "Merch", true, "Reward 155", 935 },
                    { "RW-156", "Beverages", true, "Reward 156", 972 },
                    { "RW-157", "Electronics", true, "Reward 157", 1009 },
                    { "RW-158", "Home", true, "Reward 158", 1046 },
                    { "RW-159", "Vouchers", true, "Reward 159", 1083 },
                    { "RW-160", "Merch", true, "Reward 160", 1120 },
                    { "RW-161", "Beverages", true, "Reward 161", 1157 },
                    { "RW-162", "Electronics", true, "Reward 162", 1194 },
                    { "RW-163", "Home", true, "Reward 163", 1231 },
                    { "RW-164", "Vouchers", true, "Reward 164", 1268 },
                    { "RW-165", "Merch", true, "Reward 165", 1305 },
                    { "RW-166", "Beverages", true, "Reward 166", 1342 },
                    { "RW-167", "Electronics", true, "Reward 167", 1379 },
                    { "RW-168", "Home", true, "Reward 168", 1416 },
                    { "RW-169", "Vouchers", true, "Reward 169", 1453 },
                    { "RW-170", "Merch", true, "Reward 170", 1490 },
                    { "RW-171", "Beverages", true, "Reward 171", 1527 },
                    { "RW-172", "Electronics", true, "Reward 172", 1564 },
                    { "RW-173", "Home", true, "Reward 173", 1601 },
                    { "RW-174", "Vouchers", true, "Reward 174", 1638 },
                    { "RW-175", "Merch", true, "Reward 175", 1675 },
                    { "RW-176", "Beverages", true, "Reward 176", 1712 },
                    { "RW-177", "Electronics", true, "Reward 177", 1749 },
                    { "RW-178", "Home", true, "Reward 178", 1786 },
                    { "RW-179", "Vouchers", true, "Reward 179", 1823 },
                    { "RW-180", "Merch", true, "Reward 180", 1860 },
                    { "RW-181", "Beverages", true, "Reward 181", 1897 },
                    { "RW-182", "Electronics", true, "Reward 182", 1934 },
                    { "RW-183", "Home", true, "Reward 183", 1971 },
                    { "RW-184", "Vouchers", true, "Reward 184", 2008 },
                    { "RW-185", "Merch", true, "Reward 185", 2045 },
                    { "RW-186", "Beverages", true, "Reward 186", 2082 },
                    { "RW-187", "Electronics", true, "Reward 187", 2119 },
                    { "RW-188", "Home", true, "Reward 188", 2156 },
                    { "RW-189", "Vouchers", true, "Reward 189", 2193 },
                    { "RW-190", "Merch", true, "Reward 190", 2230 },
                    { "RW-191", "Beverages", true, "Reward 191", 2267 },
                    { "RW-192", "Electronics", true, "Reward 192", 2304 },
                    { "RW-193", "Home", true, "Reward 193", 2341 },
                    { "RW-194", "Vouchers", true, "Reward 194", 2378 },
                    { "RW-195", "Merch", true, "Reward 195", 2415 },
                    { "RW-196", "Beverages", true, "Reward 196", 2452 },
                    { "RW-197", "Electronics", true, "Reward 197", 2489 },
                    { "RW-198", "Home", true, "Reward 198", 2526 },
                    { "RW-199", "Vouchers", true, "Reward 199", 2563 },
                    { "RW-200", "Merch", true, "Reward 200", 2600 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_CustomerId_CreatedAt",
                table: "Redemptions",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_Category_IsActive",
                table: "Rewards",
                columns: new[] { "Category", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_CustomerId_CreatedAt",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Redemptions");
        }
    }
}
