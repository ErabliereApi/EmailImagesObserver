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
    [Migration("20240811135612_addAIType")]
    partial class addAIType
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Alerte", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("CreateBy")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("ExternalOwnerId")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("ExternalOwnerSubId")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Keywords")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("RemoveKeywords")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("SendTo")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("TextTo")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<string>("UpdateBy")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("Alertes");
                });

            modelBuilder.Entity("BlazorApp.Data.EmailStates", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Email")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

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

                    b.Property<string>("AITypes")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

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

                    b.Property<Guid?>("ExternalSubOwner")
                        .HasColumnType("uniqueidentifier");

                    b.Property<byte[]>("Images")
                        .HasColumnType("varbinary(max)");

                    b.Property<string>("Name")
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<string>("Object")
                        .HasMaxLength(400)
                        .HasColumnType("nvarchar(400)");

                    b.Property<long>("UniqueId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("EmailStatesId");

                    b.HasIndex(new[] { "DateAjout" }, "Index_DateAjout");

                    b.HasIndex(new[] { "ExternalOwner" }, "Index_ExternalOwner");

                    b.HasIndex(new[] { "UniqueId" }, "Index_UniqueId")
                        .IsUnique();

                    b.ToTable("ImagesInfo");
                });

            modelBuilder.Entity("Mapping", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Filter")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("Key")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("Name")
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<string>("SubFilter")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<Guid?>("SubValue")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("Value")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.ToTable("Mappings");
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
