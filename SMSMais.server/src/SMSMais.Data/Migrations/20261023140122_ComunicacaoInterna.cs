using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ComunicacaoInterna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversa_interna",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    unidade_a_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_b_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_a_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_b_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ultima_mensagem_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversa_interna", x => x.id);
                    table.CheckConstraint("ck_conversa_interna_par", "(tipo = 1 AND unidade_a_id IS NOT NULL AND unidade_b_id IS NOT NULL AND unidade_a_id <= unidade_b_id AND usuario_a_id IS NULL AND usuario_b_id IS NULL) OR (tipo = 2 AND usuario_a_id IS NOT NULL AND usuario_b_id IS NOT NULL AND usuario_a_id < usuario_b_id AND unidade_a_id IS NULL AND unidade_b_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_conversa_interna_unidade_unidade_a_id",
                        column: x => x.unidade_a_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_conversa_interna_unidade_unidade_b_id",
                        column: x => x.unidade_b_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_conversa_interna_usuario_usuario_a_id",
                        column: x => x.usuario_a_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_conversa_interna_usuario_usuario_b_id",
                        column: x => x.usuario_b_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversa_interna_leitura",
                schema: "smsmarica",
                columns: table => new
                {
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lida_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    silenciada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversa_interna_leitura", x => new { x.conversa_id, x.usuario_id });
                    table.ForeignKey(
                        name: "FK_conversa_interna_leitura_conversa_interna_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa_interna",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_conversa_interna_leitura_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mensagem_interna",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    texto = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    anexo_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    anexo_mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    anexo_tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    anexo_chave = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    anexo_eh_audio = table.Column<bool>(type: "boolean", nullable: false),
                    anexo_duracao_segundos = table.Column<int>(type: "integer", nullable: true),
                    enviada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensagem_interna", x => x.id);
                    table.CheckConstraint("ck_mensagem_interna_conteudo", "texto IS NOT NULL OR anexo_chave IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_mensagem_interna_conversa_interna_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa_interna",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mensagem_interna_unidade_autor_unidade_id",
                        column: x => x.autor_unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mensagem_interna_usuario_autor_id",
                        column: x => x.autor_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversa_interna_unidade_b_id",
                schema: "smsmarica",
                table: "conversa_interna",
                column: "unidade_b_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversa_interna_usuario_b_id",
                schema: "smsmarica",
                table: "conversa_interna",
                column: "usuario_b_id");

            migrationBuilder.CreateIndex(
                name: "ux_conversa_interna_unidades",
                schema: "smsmarica",
                table: "conversa_interna",
                columns: new[] { "unidade_a_id", "unidade_b_id" },
                unique: true,
                filter: "tipo = 1");

            migrationBuilder.CreateIndex(
                name: "ux_conversa_interna_usuarios",
                schema: "smsmarica",
                table: "conversa_interna",
                columns: new[] { "usuario_a_id", "usuario_b_id" },
                unique: true,
                filter: "tipo = 2");

            migrationBuilder.CreateIndex(
                name: "IX_conversa_interna_leitura_usuario_id",
                schema: "smsmarica",
                table: "conversa_interna_leitura",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_mensagem_interna_autor_id",
                schema: "smsmarica",
                table: "mensagem_interna",
                column: "autor_id");

            migrationBuilder.CreateIndex(
                name: "IX_mensagem_interna_autor_unidade_id",
                schema: "smsmarica",
                table: "mensagem_interna",
                column: "autor_unidade_id");

            migrationBuilder.CreateIndex(
                name: "ix_mensagem_interna_conversa_enviada",
                schema: "smsmarica",
                table: "mensagem_interna",
                columns: new[] { "conversa_id", "enviada_em" });

            // Busca (ADR-0079 §7): f_unaccent(texto) ILIKE, atendida por GIN trigram — como a busca da
            // Central (ConversasBuscaTrgm, que criou a f_unaccent e a pg_trgm). gin_trgm_ops resolve
            // via search_path.
            migrationBuilder.Sql("SET LOCAL search_path = smsmarica, public;");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_mensagem_interna_texto_trgm
    ON smsmarica.mensagem_interna USING gin (smsmarica.f_unaccent(texto) gin_trgm_ops)
    WHERE texto IS NOT NULL;");

            // O módulo 86 nasce em TODO perfil com Consulta + Inclusão (= 3): o pedido é que as
            // unidades conversem, e um módulo desligado em todo lugar não é usado. O recorte é feito
            // na tela de Perfis. Não é seed institucional: vale igual em qualquer instância (o
            // Administrador ganha todos os módulos pelo seeder).
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.permissao_perfil (perfil_id, modulo, acoes)
                SELECT p.id, 86, 3
                FROM smsmarica.perfil p
                ON CONFLICT (perfil_id, modulo) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM smsmarica.permissao_perfil WHERE modulo = 86;");
            migrationBuilder.Sql("DELETE FROM smsmarica.permissao_usuario WHERE modulo = 86;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS smsmarica.ix_mensagem_interna_texto_trgm;");

            migrationBuilder.DropTable(
                name: "conversa_interna_leitura",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "mensagem_interna",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "conversa_interna",
                schema: "smsmarica");
        }
    }
}
