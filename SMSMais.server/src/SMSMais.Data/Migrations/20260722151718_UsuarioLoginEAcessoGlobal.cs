using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class UsuarioLoginEAcessoGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "acesso_global",
                schema: "smsmarica",
                table: "usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "login",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Unicidade do login ignorando maiúsculas: "Bernardo" e "bernardo" são o mesmo
            // login. Índice por EXPRESSÃO — o EF não modela isso, então vive aqui no SQL (e
            // por isso não aparece no snapshot).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_usuario_login_lower ON smsmarica.usuario (lower(login)) "
                + "WHERE login IS NOT NULL;");

            // Preserva o comportamento vigente no instante do deploy: até aqui quem enxergava
            // todas as unidades era o usuário "Administrador" semeado, por comparação de GUID
            // no código. Sem este UPDATE haveria uma janela em que NINGUÉM teria acesso global
            // (a coluna nasce false) — e ninguém poderia conceder, porque conceder exige já ter.
            migrationBuilder.Sql(
                "UPDATE smsmarica.usuario SET acesso_global = true "
                + "WHERE id = '11111111-1111-1111-1111-111111111111';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS smsmarica.ix_usuario_login_lower;");

            migrationBuilder.DropColumn(
                name: "acesso_global",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "login",
                schema: "smsmarica",
                table: "usuario");
        }
    }
}
