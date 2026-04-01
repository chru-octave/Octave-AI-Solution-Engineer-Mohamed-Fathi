using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceExtraction.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brokers",
                columns: table => new
                {
                    BrokerId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BrokerName = table.Column<string>(type: "TEXT", nullable: false),
                    AgencyName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    ZipCode = table.Column<string>(type: "TEXT", nullable: true),
                    LicenseNumber = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brokers", x => x.BrokerId);
                });

            migrationBuilder.CreateTable(
                name: "Insureds",
                columns: table => new
                {
                    InsuredId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    ZipCode = table.Column<string>(type: "TEXT", nullable: true),
                    Industry = table.Column<string>(type: "TEXT", nullable: true),
                    YearsInBusiness = table.Column<int>(type: "INTEGER", nullable: true),
                    DotNumber = table.Column<string>(type: "TEXT", nullable: true),
                    McNumber = table.Column<string>(type: "TEXT", nullable: true),
                    AnnualRevenue = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insureds", x => x.InsuredId);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmailFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    EmailSubject = table.Column<string>(type: "TEXT", nullable: true),
                    EmailFrom = table.Column<string>(type: "TEXT", nullable: true),
                    AttachmentList = table.Column<string>(type: "TEXT", nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtractionConfidence = table.Column<double>(type: "REAL", nullable: false),
                    InsuredId = table.Column<int>(type: "INTEGER", nullable: true),
                    BrokerId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK_Submissions_Brokers_BrokerId",
                        column: x => x.BrokerId,
                        principalTable: "Brokers",
                        principalColumn: "BrokerId");
                    table.ForeignKey(
                        name: "FK_Submissions_Insureds_InsuredId",
                        column: x => x.InsuredId,
                        principalTable: "Insureds",
                        principalColumn: "InsuredId");
                });

            migrationBuilder.CreateTable(
                name: "CoverageLines",
                columns: table => new
                {
                    CoverageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineOfBusiness = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedLimit = table.Column<string>(type: "TEXT", nullable: true),
                    TargetPremium = table.Column<decimal>(type: "TEXT", nullable: true),
                    CurrentPremium = table.Column<decimal>(type: "TEXT", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageLines", x => x.CoverageId);
                    table.ForeignKey(
                        name: "FK_CoverageLines_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Exposures",
                columns: table => new
                {
                    ExposureId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExposureType = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exposures", x => x.ExposureId);
                    table.ForeignKey(
                        name: "FK_Exposures_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LossHistory",
                columns: table => new
                {
                    LossId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: false),
                    LossDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LossAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    LossType = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PolicyYear = table.Column<string>(type: "TEXT", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    ReserveAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    Claimant = table.Column<string>(type: "TEXT", nullable: true),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LossHistory", x => x.LossId);
                    table.ForeignKey(
                        name: "FK_LossHistory_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageLines_SubmissionId",
                table: "CoverageLines",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Exposures_SubmissionId",
                table: "Exposures",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_LossHistory_SubmissionId",
                table: "LossHistory",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_BrokerId",
                table: "Submissions",
                column: "BrokerId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_InsuredId",
                table: "Submissions",
                column: "InsuredId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverageLines");

            migrationBuilder.DropTable(
                name: "Exposures");

            migrationBuilder.DropTable(
                name: "LossHistory");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "Brokers");

            migrationBuilder.DropTable(
                name: "Insureds");
        }
    }
}
