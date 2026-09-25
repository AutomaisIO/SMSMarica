using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvisoCancelamentoLigadoEm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "aviso_cancelamento_ligado_em",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "timestamp with time zone",
                nullable: true);

            // Instância que já estivesse com o aviso ligado ganha o corte agora: sem ele, o envio
            // trataria todo aviso como retroativo e nada sairia. Em Maricá a chave está desligada e
            // esta linha não muda nada — o corte nasce quando alguém ligar pela tela.
            migrationBuilder.Sql(
                "UPDATE smsmarica.confirmacao_configuracao SET aviso_cancelamento_ligado_em = now() "
                + "WHERE aviso_cancelamento_habilitado;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aviso_cancelamento_ligado_em",
                schema: "smsmarica",
                table: "confirmacao_configuracao");
        }
    }
}
