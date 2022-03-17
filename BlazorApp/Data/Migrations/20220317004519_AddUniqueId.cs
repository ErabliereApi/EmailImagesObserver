using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    public partial class AddUniqueId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailStates");

            migrationBuilder.CreateTable(
                name: "EmailStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessagesCount = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailStates", x => x.Id);
                });

            migrationBuilder.DropTable(
                name: "ImagesInfo");

            migrationBuilder.CreateTable(
                name: "ImagesInfo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AzureImageAPIInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    DateAjout = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateEmail = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagesInfo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "Index_DateAjout",
                table: "ImagesInfo",
                column: "DateAjout");

            migrationBuilder.AddColumn<long>(
                name: "UniqueId",
                table: "ImagesInfo",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "Index_UniqueId",
                table: "ImagesInfo",
                column: "UniqueId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "Index_UniqueId",
                table: "ImagesInfo");

            migrationBuilder.DropColumn(
                name: "UniqueId",
                table: "ImagesInfo");
        }
    }
}
