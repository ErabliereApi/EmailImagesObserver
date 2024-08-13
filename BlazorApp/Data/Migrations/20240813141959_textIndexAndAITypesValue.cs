using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class textIndexAndAITypesValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a full text index on the 'AzureImageAPIInfo' column of the 'ImagesInfo' table
            // migrationBuilder.Sql(
            //     "CREATE FULLTEXT CATALOG ftImageInfoCatalog AS DEFAULT;", 
            //     suppressTransaction: true);

            // migrationBuilder.Sql(
            //     "CREATE FULLTEXT INDEX ON ImagesInfo(AzureImageAPIInfo) KEY INDEX PK_ImagesInfo;",
            //     suppressTransaction: true);

            // Update the 'AITypes' column of the 'ImagesInfo' table
            migrationBuilder.Sql(
                "UPDATE ImagesInfo SET AITypes = 'AzureImageML;' WHERE AzureImageAPIInfo like '{%' and AITypes = '';");

            migrationBuilder.Sql(
                "UPDATE ImagesInfo SET AITypes = 'Florence2;' WHERE AzureImageAPIInfo != '' and AzureImageAPIInfo not like '{%' and AITypes = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
