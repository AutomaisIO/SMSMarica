using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class MedicoLocalDaRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_medico_local",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    conselho = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero_conselho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    uf_conselho = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    grafias_json = table.Column<string>(type: "jsonb", nullable: true),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_medico_local", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_medico_local_cpf",
                schema: "smsmarica",
                table: "regulacao_medico_local",
                columns: new[] { "sistema", "cpf" },
                unique: true,
                filter: "cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_medico_local_nome",
                schema: "smsmarica",
                table: "regulacao_medico_local",
                columns: new[] { "sistema", "nome_normalizado" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulacao_medico_local",
                schema: "smsmarica");
        }
    }
}
