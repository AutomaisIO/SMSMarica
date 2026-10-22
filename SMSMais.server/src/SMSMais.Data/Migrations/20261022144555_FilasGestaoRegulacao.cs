using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilasGestaoRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_solicitacao_compartilhamento",
                schema: "smsmarica",
                columns: table => new
                {
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fila = table.Column<int>(type: "integer", nullable: false),
                    compartilhado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    compartilhado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_solicitacao_compartilhamento", x => new { x.solicitacao_id, x.fila });
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_compartilhamento_regulacao_solicitaca~",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_compartilhamento_fila",
                schema: "smsmarica",
                table: "regulacao_solicitacao_compartilhamento",
                column: "fila");

            // As filas da Gestão de fila (83 NAR, 84 Internas, 85 Intermunicipais) passam a decidir o
            // que o técnico vê. Quem já tem a Gestão de fila (48) recebe as três, só Consulta, para
            // ninguém abrir a tela vazia depois do deploy; o recorte por perfil é feito na tela de
            // Perfis. Não é seed institucional: vale igual em qualquer instância (sem perfil com o
            // 48, não faz nada; o Administrador ganha todos os módulos pelo seeder).
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.permissao_perfil (perfil_id, modulo, acoes)
                SELECT pp.perfil_id, f.modulo, 1
                FROM smsmarica.permissao_perfil pp
                CROSS JOIN (VALUES (83), (84), (85)) AS f(modulo)
                WHERE pp.modulo = 48 AND pp.acoes <> 0
                ON CONFLICT (perfil_id, modulo) DO NOTHING;

                INSERT INTO smsmarica.permissao_usuario (usuario_id, modulo, acoes)
                SELECT pu.usuario_id, f.modulo, 1
                FROM smsmarica.permissao_usuario pu
                CROSS JOIN (VALUES (83), (84), (85)) AS f(modulo)
                WHERE pu.modulo = 48 AND pu.acoes <> 0
                ON CONFLICT (usuario_id, modulo) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM smsmarica.permissao_perfil WHERE modulo IN (83, 84, 85);
                DELETE FROM smsmarica.permissao_usuario WHERE modulo IN (83, 84, 85);
                """);

            migrationBuilder.DropTable(
                name: "regulacao_solicitacao_compartilhamento",
                schema: "smsmarica");
        }
    }
}
