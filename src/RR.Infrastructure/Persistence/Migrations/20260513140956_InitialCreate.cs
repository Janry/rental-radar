using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    timezone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    areas = table.Column<string>(type: "TEXT", nullable: false),
                    search_keywords = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_discovery_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scrape_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    location_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    member_count = table.Column<int>(type: "INTEGER", nullable: true),
                    relevance_score = table.Column<double>(type: "REAL", nullable: false),
                    is_auto_discovered = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    added_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_scraped_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_success_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    consecutive_failures = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scrape_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_scrape_sources_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_filters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    telegram_chat_id = table.Column<long>(type: "INTEGER", nullable: false),
                    location_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    max_price = table.Column<decimal>(type: "TEXT", nullable: true),
                    min_price = table.Column<decimal>(type: "TEXT", nullable: true),
                    areas = table.Column<string>(type: "TEXT", nullable: false),
                    property_types = table.Column<string>(type: "TEXT", nullable: false),
                    min_bedrooms = table.Column<int>(type: "INTEGER", nullable: true),
                    require_pets_allowed = table.Column<bool>(type: "INTEGER", nullable: true),
                    require_pool = table.Column<bool>(type: "INTEGER", nullable: true),
                    require_hot_water = table.Column<bool>(type: "INTEGER", nullable: true),
                    semantic_query = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_matched_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_filters", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_filters_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    location_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    source_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    raw_text = table.Column<string>(type: "TEXT", nullable: false),
                    author_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    author_profile_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    price_per_month = table.Column<decimal>(type: "TEXT", nullable: true),
                    area = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    bedrooms = table.Column<int>(type: "INTEGER", nullable: true),
                    property_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    pets_allowed = table.Column<bool>(type: "INTEGER", nullable: true),
                    has_pool = table.Column<bool>(type: "INTEGER", nullable: true),
                    has_hot_water = table.Column<bool>(type: "INTEGER", nullable: true),
                    has_wifi = table.Column<bool>(type: "INTEGER", nullable: true),
                    available_from = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    available_until = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    image_urls = table.Column<string>(type: "TEXT", nullable: false),
                    contact_info = table.Column<string>(type: "TEXT", nullable: false),
                    posted_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    scraped_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    processed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    confidence_score = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listings", x => x.id);
                    table.ForeignKey(
                        name: "fk_listings_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_listings_scrape_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "scrape_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listings_location_id_posted_at",
                table: "listings",
                columns: new[] { "location_id", "posted_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_listings_scraped_at",
                table: "listings",
                column: "scraped_at");

            migrationBuilder.CreateIndex(
                name: "ix_listings_source_id_external_id",
                table: "listings",
                columns: new[] { "source_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locations_slug",
                table: "locations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scrape_sources_location_id",
                table: "scrape_sources",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_scrape_sources_location_id_url",
                table: "scrape_sources",
                columns: new[] { "location_id", "url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_filters_location_id_is_active",
                table: "user_filters",
                columns: new[] { "location_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_user_filters_telegram_chat_id",
                table: "user_filters",
                column: "telegram_chat_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listings");

            migrationBuilder.DropTable(
                name: "user_filters");

            migrationBuilder.DropTable(
                name: "scrape_sources");

            migrationBuilder.DropTable(
                name: "locations");
        }
    }
}
