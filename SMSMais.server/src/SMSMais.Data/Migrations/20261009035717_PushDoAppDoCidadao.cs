using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class PushDoAppDoCidadao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "push_plataforma",
                schema: "smsmarica",
                table: "cidadao_sessao",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "push_registrado_em",
                schema: "smsmarica",
                table: "cidadao_sessao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "push_token",
                schema: "smsmarica",
                table: "cidadao_sessao",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cidadao_notificacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(65)", maxLength: 65, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    rota = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    enviado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    aparelhos = table.Column<int>(type: "integer", nullable: false),
                    entregues = table.Column<int>(type: "integer", nullable: false),
                    falha = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cidadao_notificacao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cidadao_sessao_push_token",
                schema: "smsmarica",
                table: "cidadao_sessao",
                column: "push_token")
                .Annotation("Npgsql:IndexMethod", "hash");

            migrationBuilder.CreateIndex(
                name: "ix_cidadao_notificacao_paciente",
                schema: "smsmarica",
                table: "cidadao_notificacao",
                columns: new[] { "paciente_id", "criado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cidadao_notificacao",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ix_cidadao_sessao_push_token",
                schema: "smsmarica",
                table: "cidadao_sessao");

            migrationBuilder.DropColumn(
                name: "push_plataforma",
                schema: "smsmarica",
                table: "cidadao_sessao");

            migrationBuilder.DropColumn(
                name: "push_registrado_em",
                schema: "smsmarica",
                table: "cidadao_sessao");

            migrationBuilder.DropColumn(
                name: "push_token",
                schema: "smsmarica",
                table: "cidadao_sessao");
        }
    }
}
