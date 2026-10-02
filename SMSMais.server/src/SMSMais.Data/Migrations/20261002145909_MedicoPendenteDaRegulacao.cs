using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class MedicoPendenteDaRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_medico_pendente",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    numero_documento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    especialidade = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    nome_no_sistema = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_medico_pendente", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_medico_pendente_sistema_situacao",
                schema: "smsmarica",
                table: "regulacao_medico_pendente",
                columns: new[] { "sistema", "situacao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulacao_medico_pendente",
                schema: "smsmarica");
        }
    }
}
