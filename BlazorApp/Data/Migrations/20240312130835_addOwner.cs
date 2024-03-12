using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class addOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmailStatesId",
                table: "ImagesInfo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalOwner",
                table: "ImagesInfo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Object",
                table: "ImagesInfo",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImagesInfo_EmailStatesId",
                table: "ImagesInfo",
                column: "EmailStatesId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImagesInfo_EmailStates_EmailStatesId",
                table: "ImagesInfo",
                column: "EmailStatesId",
                principalTable: "EmailStates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImagesInfo_EmailStates_EmailStatesId",
                table: "ImagesInfo");

            migrationBuilder.DropIndex(
                name: "IX_ImagesInfo_EmailStatesId",
                table: "ImagesInfo");

            migrationBuilder.DropColumn(
                name: "EmailStatesId",
                table: "ImagesInfo");

            migrationBuilder.DropColumn(
                name: "ExternalOwner",
                table: "ImagesInfo");

            migrationBuilder.DropColumn(
                name: "Object",
                table: "ImagesInfo");
        }
    }
}
