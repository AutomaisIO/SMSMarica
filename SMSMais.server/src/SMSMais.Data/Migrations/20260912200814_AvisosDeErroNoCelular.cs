using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvisosDeErroNoCelular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerta_destinatario",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefone = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerta_destinatario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alerta_envio",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origem_chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    detalhe = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    resultado = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerta_envio", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alerta_origem",
                schema: "smsmarica",
                columns: table => new
                {
                    chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grupo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    silenciada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultima_ocorrencia_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ocorrencias_sem_aviso = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_aviso_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    avisos_seguidos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ultimo_detalhe = table.Column<string>(type: "text", nullable: true),
                    atualizada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerta_origem", x => x.chave);
                });

            migrationBuilder.CreateIndex(
                name: "ux_alerta_destinatario_telefone",
                schema: "smsmarica",
                table: "alerta_destinatario",
                column: "telefone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alerta_envio_criado_em",
                schema: "smsmarica",
                table: "alerta_envio",
                column: "criado_em");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_envio_origem",
                schema: "smsmarica",
                table: "alerta_envio",
                columns: new[] { "origem_chave", "criado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerta_destinatario",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "alerta_envio",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "alerta_origem",
                schema: "smsmarica");
        }
    }
}
