using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SerProfissionaisEspelho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ser_profissional",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    documento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    tipo_documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    presente_no_ser = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    primeira_leitura_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultima_leitura_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: true),
                    medico_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ligado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ligado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_profissional", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ser_profissional_cpf",
                schema: "smsmarica",
                table: "ser_profissional",
                column: "cpf");

            migrationBuilder.CreateIndex(
                name: "ix_ser_profissional_medico",
                schema: "smsmarica",
                table: "ser_profissional",
                column: "medico_id");

            migrationBuilder.CreateIndex(
                name: "ux_ser_profissional_chave",
                schema: "smsmarica",
                table: "ser_profissional",
                column: "chave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ser_profissional",
                schema: "smsmarica");

        }
    }
}
