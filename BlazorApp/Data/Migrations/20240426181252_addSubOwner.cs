using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class addSubOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubFilter",
                table: "Mappings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubValue",
                table: "Mappings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalSubOwner",
                table: "ImagesInfo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendTo",
                table: "Alertes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextTo",
                table: "Alertes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubFilter",
                table: "Mappings");

            migrationBuilder.DropColumn(
                name: "SubValue",
                table: "Mappings");

            migrationBuilder.DropColumn(
                name: "ExternalSubOwner",
                table: "ImagesInfo");

            migrationBuilder.DropColumn(
                name: "SendTo",
                table: "Alertes");

            migrationBuilder.DropColumn(
                name: "TextTo",
                table: "Alertes");
        }
    }
}
