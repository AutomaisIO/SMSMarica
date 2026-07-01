using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRegistroErro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registro_erro",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_referencia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    caminho = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    query_string = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    tipo_excecao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    mensagem = table.Column<string>(type: "text", nullable: false),
                    stack_trace = table.Column<string>(type: "text", nullable: true),
                    interna = table.Column<string>(type: "text", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registro_erro", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_registro_erro_codigo_referencia",
                schema: "smsmarica",
                table: "registro_erro",
                column: "codigo_referencia");

            migrationBuilder.CreateIndex(
                name: "ix_registro_erro_criado_em",
                schema: "smsmarica",
                table: "registro_erro",
                column: "criado_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registro_erro",
                schema: "smsmarica");
        }
    }
}
