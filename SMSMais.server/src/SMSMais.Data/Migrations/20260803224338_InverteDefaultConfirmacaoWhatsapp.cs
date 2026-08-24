using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class InverteDefaultConfirmacaoWhatsapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_procedimento_profissional",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            // Realinha as linhas existentes com o novo padrão. Elas só têm `true` porque foi o
            // default que a migração anterior (do MESMO dia) carimbou — não houve decisão de
            // operador nenhuma: no momento desta migração, todas as 261 linhas estavam no valor
            // default e a única escolha explícita feita na tela já era `false`.
            //
            // Sem este UPDATE o sistema ficaria incoerente: padrão desligado para o que vier, e
            // ligado para tudo que já existe — ou seja, continuaria avisando pacientes.
            migrationBuilder.Sql(
                "UPDATE smsmarica.sisreg_procedimento_profissional SET enviar_confirmacao = false;");
            migrationBuilder.Sql(
                "UPDATE smsmarica.sisreg_varredura_agenda SET enviar_confirmacao = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_procedimento_profissional",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
