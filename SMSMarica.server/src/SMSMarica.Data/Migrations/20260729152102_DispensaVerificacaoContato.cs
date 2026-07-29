using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class DispensaVerificacaoContato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispensa_verificacao_contato",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo = table.Column<int>(type: "integer", nullable: false),
                    motivo_descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    paciente_ciente = table.Column<bool>(type: "boolean", nullable: false),
                    telefone_na_epoca = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revogado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revogado_motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispensa_verificacao_contato", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispensa_verificacao_contato_paciente_id_criado_em",
                schema: "smsmarica",
                table: "dispensa_verificacao_contato",
                columns: new[] { "paciente_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_dispensa_verificacao_contato_paciente_ativa",
                schema: "smsmarica",
                table: "dispensa_verificacao_contato",
                column: "paciente_id",
                unique: true,
                filter: "revogado_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispensa_verificacao_contato",
                schema: "smsmarica");
        }
    }
}
