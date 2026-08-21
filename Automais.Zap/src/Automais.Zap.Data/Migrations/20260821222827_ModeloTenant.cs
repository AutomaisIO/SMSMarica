using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Zap.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModeloTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_numero_destino_destino_id",
                schema: "zap",
                table: "numero");

            migrationBuilder.DropForeignKey(
                name: "FK_waba_destino_destino_id",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropTable(
                name: "destino",
                schema: "zap");

            migrationBuilder.DropIndex(
                name: "IX_waba_destino_id",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropIndex(
                name: "IX_numero_destino_id",
                schema: "zap",
                table: "numero");

            migrationBuilder.DropColumn(
                name: "destino_id",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropColumn(
                name: "destino_id",
                schema: "zap",
                table: "numero");

            migrationBuilder.RenameColumn(
                name: "destino_id",
                schema: "zap",
                table: "entrega_log",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "ix_entrega_log_destino_recebido",
                schema: "zap",
                table: "entrega_log",
                newName: "ix_entrega_log_tenant_recebido");

            migrationBuilder.AddColumn<bool>(
                name: "roteamento_ativo",
                schema: "zap",
                table: "waba",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "zap",
                table: "waba",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "url_destino",
                schema: "zap",
                table: "waba",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "global",
                schema: "zap",
                table: "usuario_admin",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // numero.waba_id muda de SIGNIFICADO: era o id do WABA na Meta (texto), passa a ser
            // a FK para waba.id (uuid). Postgres nao casta varchar->uuid, entao a conversao e
            // feita a mao -- e preservando o vinculo, casando pelo id da Meta que ja estava la.
            //
            // Dropar e recriar a coluna seria mais curto e apagaria a rota de todo mundo: linha
            // sobrevivente ficaria com uuid zero e a FK estouraria no meio da migration.
            migrationBuilder.Sql("""
                ALTER TABLE zap.numero ADD COLUMN waba_ref uuid;

                UPDATE zap.numero n
                   SET waba_ref = w.id
                  FROM zap.waba w
                 WHERE w.waba_id = n.waba_id;

                -- Numero que nao casa com nenhum WABA cadastrado nao tem como ser roteado:
                -- some aqui e volta na proxima sincronizacao com a Meta, que e a fonte.
                DELETE FROM zap.numero WHERE waba_ref IS NULL;

                ALTER TABLE zap.numero DROP COLUMN waba_id;
                ALTER TABLE zap.numero RENAME COLUMN waba_ref TO waba_id;
                ALTER TABLE zap.numero ALTER COLUMN waba_id SET NOT NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "rotulo",
                schema: "zap",
                table: "numero",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "url_destino_override",
                schema: "zap",
                table: "numero",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    suspenso_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    suspenso_motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_tenant",
                schema: "zap",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_tenant", x => new { x.usuario_id, x.tenant_id });
                    table.ForeignKey(
                        name: "FK_usuario_tenant_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "zap",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuario_tenant_usuario_admin_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "zap",
                        principalTable: "usuario_admin",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                DO $$
                DECLARE r RECORD; novo uuid;
                BEGIN
                  FOR r IN SELECT id, waba_id, nome FROM zap.waba
                           WHERE tenant_id = '00000000-0000-0000-0000-000000000000' LOOP
                    novo := gen_random_uuid();
                    INSERT INTO zap.tenant (id, nome, ativo, criado_em)
                      VALUES (novo, COALESCE(NULLIF(r.nome, ''), 'WABA ' || r.waba_id), true, now());
                    UPDATE zap.waba SET tenant_id = novo WHERE id = r.id;
                  END LOOP;
                END $$;
                """);

            migrationBuilder.Sql("UPDATE zap.usuario_admin SET global = true;");

            migrationBuilder.CreateIndex(
                name: "IX_waba_tenant_id",
                schema: "zap",
                table: "waba",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_numero_waba_id",
                schema: "zap",
                table: "numero",
                column: "waba_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenant_nome",
                schema: "zap",
                table: "tenant",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_tenant_id",
                schema: "zap",
                table: "usuario_tenant",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_numero_waba_waba_id",
                schema: "zap",
                table: "numero",
                column: "waba_id",
                principalSchema: "zap",
                principalTable: "waba",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_waba_tenant_tenant_id",
                schema: "zap",
                table: "waba",
                column: "tenant_id",
                principalSchema: "zap",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_numero_waba_waba_id",
                schema: "zap",
                table: "numero");

            migrationBuilder.DropForeignKey(
                name: "FK_waba_tenant_tenant_id",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropTable(
                name: "usuario_tenant",
                schema: "zap");

            migrationBuilder.DropTable(
                name: "tenant",
                schema: "zap");

            migrationBuilder.DropIndex(
                name: "IX_waba_tenant_id",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropIndex(
                name: "IX_numero_waba_id",
                schema: "zap",
                table: "numero");

            migrationBuilder.DropColumn(
                name: "roteamento_ativo",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropColumn(
                name: "url_destino",
                schema: "zap",
                table: "waba");

            migrationBuilder.DropColumn(
                name: "global",
                schema: "zap",
                table: "usuario_admin");

            migrationBuilder.DropColumn(
                name: "url_destino_override",
                schema: "zap",
                table: "numero");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                schema: "zap",
                table: "entrega_log",
                newName: "destino_id");

            migrationBuilder.RenameIndex(
                name: "ix_entrega_log_tenant_recebido",
                schema: "zap",
                table: "entrega_log",
                newName: "ix_entrega_log_destino_recebido");

            migrationBuilder.AddColumn<Guid>(
                name: "destino_id",
                schema: "zap",
                table: "waba",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "waba_id",
                schema: "zap",
                table: "numero",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "rotulo",
                schema: "zap",
                table: "numero",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destino_id",
                schema: "zap",
                table: "numero",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "destino",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    url_webhook = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_destino", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_waba_destino_id",
                schema: "zap",
                table: "waba",
                column: "destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_numero_destino_id",
                schema: "zap",
                table: "numero",
                column: "destino_id");

            migrationBuilder.CreateIndex(
                name: "ux_destino_nome",
                schema: "zap",
                table: "destino",
                column: "nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_numero_destino_destino_id",
                schema: "zap",
                table: "numero",
                column: "destino_id",
                principalSchema: "zap",
                principalTable: "destino",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_waba_destino_destino_id",
                schema: "zap",
                table: "waba",
                column: "destino_id",
                principalSchema: "zap",
                principalTable: "destino",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
