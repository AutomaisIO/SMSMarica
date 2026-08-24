using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPesquisaSatisfacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pesquisa_satisfacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    atendimento_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    instrumento_versao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    respostas = table.Column<string>(type: "jsonb", nullable: true),
                    respondida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    respondida_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    enviada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    enviada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pesquisa_satisfacao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pesquisa_satisfacao_encounter_id",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                column: "encounter_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pesquisa_satisfacao_patient_id",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_pesquisa_satisfacao_respondida_em",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                column: "respondida_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pesquisa_satisfacao",
                schema: "smsmarica");
        }
    }
}
