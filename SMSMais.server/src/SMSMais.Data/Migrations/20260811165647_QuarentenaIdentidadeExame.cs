using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuarentenaIdentidadeExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exame_incidente_identidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    exame_imagem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paciente_suspeito_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    automatico = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    resolucao_nota = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exame_incidente_identidade", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exame_incidente_identidade_status",
                schema: "smsmarica",
                table: "exame_incidente_identidade",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_exame_incidente_identidade_study_aberto",
                schema: "smsmarica",
                table: "exame_incidente_identidade",
                column: "study_instance_uid",
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exame_incidente_identidade",
                schema: "smsmarica");
        }
    }
}
