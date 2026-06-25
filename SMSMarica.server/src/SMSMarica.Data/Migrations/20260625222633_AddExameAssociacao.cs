using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExameAssociacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exame_associacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    solicitacao_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accession_number_dicom_original = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exame_associacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_exame_associacao_solicitacao_exame_solicitacao_exame_id",
                        column: x => x.solicitacao_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exame_associacao_paciente",
                schema: "smsmarica",
                table: "exame_associacao",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "ix_exame_associacao_solicitacao",
                schema: "smsmarica",
                table: "exame_associacao",
                column: "solicitacao_exame_id");

            migrationBuilder.CreateIndex(
                name: "ix_exame_associacao_study_uid_ativa",
                schema: "smsmarica",
                table: "exame_associacao",
                column: "study_instance_uid",
                unique: true,
                filter: "excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exame_associacao",
                schema: "smsmarica");
        }
    }
}
