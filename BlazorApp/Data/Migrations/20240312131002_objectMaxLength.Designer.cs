﻿// <auto-generated />
using System;
using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(BlazorDbContext))]
    [Migration("20240312131002_objectMaxLength")]
    partial class objectMaxLength
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("BlazorApp.Data.EmailStates", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Email")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("MessagesCount")
                        .HasColumnType("int");

                    b.Property<long>("Size")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("EmailStates");
                });

            modelBuilder.Entity("BlazorApp.Data.ImageInfo", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

                    b.Property<string>("AzureImageAPIInfo")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("DateAjout")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("DateEmail")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("EmailStatesId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ExternalOwner")
                        .HasColumnType("uniqueidentifier");

                    b.Property<byte[]>("Images")
                        .HasColumnType("varbinary(max)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Object")
                        .HasMaxLength(400)
                        .HasColumnType("nvarchar(400)");

                    b.Property<long>("UniqueId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("EmailStatesId");

                    b.HasIndex(new[] { "DateAjout" }, "Index_DateAjout");

                    b.HasIndex(new[] { "UniqueId" }, "Index_UniqueId")
                        .IsUnique();

                    b.ToTable("ImagesInfo");
                });

            modelBuilder.Entity("BlazorApp.Data.ImageInfo", b =>
                {
                    b.HasOne("BlazorApp.Data.EmailStates", "EmailStates")
                        .WithMany()
                        .HasForeignKey("EmailStatesId");

                    b.Navigation("EmailStates");
                });
#pragma warning restore 612, 618
        }
    }
}
