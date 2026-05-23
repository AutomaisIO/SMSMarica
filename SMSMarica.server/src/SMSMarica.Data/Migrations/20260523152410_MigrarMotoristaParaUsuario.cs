using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrarMotoristaParaUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Criar 1 usuario para cada motorista existente, copiando dados pessoais.
            //    tipo_papel = 3 (TipoPapel.Motorista). Senha placeholder 'PENDENTE_AUTH'
            //    força fluxo "definir senha" no primeiro login.
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.usuario (
                    id, nome_completo, cpf, email, telefone,
                    senha_hash, deve_trocar_senha, tipo_papel,
                    ativo, criado_em, foto_base64,
                    endereco_cep, endereco_logradouro, endereco_numero, endereco_complemento,
                    endereco_bairro, endereco_cidade, endereco_uf, endereco_ponto_referencia
                )
                SELECT
                    gen_random_uuid(),
                    m.nome_completo,
                    m.cpf,
                    'motorista-' || m.cpf || '@local.smsmarica',
                    m.telefone,
                    'PENDENTE_AUTH',
                    TRUE,
                    3,
                    m.ativo,
                    m.criado_em,
                    m.foto_base64,
                    m.endereco_cep, m.endereco_logradouro, m.endereco_numero, m.endereco_complemento,
                    m.endereco_bairro, m.endereco_cidade, m.endereco_uf, m.endereco_ponto_referencia
                FROM smsmarica.motorista m
                WHERE NOT EXISTS (
                    SELECT 1 FROM smsmarica.usuario u WHERE u.cpf = m.cpf
                );
                """);

            // 2) Adicionar usuario_id como nullable temporariamente.
            migrationBuilder.AddColumn<System.Guid>(
                name: "usuario_id",
                schema: "smsmarica",
                table: "motorista",
                type: "uuid",
                nullable: true);

            // 3) Linkar cada motorista ao usuario correspondente via CPF.
            migrationBuilder.Sql("""
                UPDATE smsmarica.motorista m
                SET usuario_id = u.id
                FROM smsmarica.usuario u
                WHERE u.cpf = m.cpf;
                """);

            // 4) Promover usuario_id a NOT NULL agora que está populado.
            migrationBuilder.AlterColumn<System.Guid>(
                name: "usuario_id",
                schema: "smsmarica",
                table: "motorista",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(System.Guid),
                oldType: "uuid",
                oldNullable: true);

            // 5) Index único + FK.
            migrationBuilder.CreateIndex(
                name: "IX_motorista_usuario_id",
                schema: "smsmarica",
                table: "motorista",
                column: "usuario_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_motorista_usuario_usuario_id",
                schema: "smsmarica",
                table: "motorista",
                column: "usuario_id",
                principalSchema: "smsmarica",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // 6) Agora pode dropar colunas duplicadas (dados já vivem em usuario).
            migrationBuilder.DropIndex(
                name: "IX_motorista_cpf",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(name: "cpf", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "nome_completo", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "telefone", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "foto_base64", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_bairro", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_cep", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_cidade", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_complemento", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_logradouro", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_numero", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_ponto_referencia", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "endereco_uf", schema: "smsmarica", table: "motorista");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) Recriar colunas dropadas como nullable temporariamente.
            migrationBuilder.AddColumn<string>(name: "cpf", schema: "smsmarica", table: "motorista", type: "character varying(11)", maxLength: 11, nullable: true);
            migrationBuilder.AddColumn<string>(name: "nome_completo", schema: "smsmarica", table: "motorista", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "telefone", schema: "smsmarica", table: "motorista", type: "character varying(30)", maxLength: 30, nullable: true);
            migrationBuilder.AddColumn<string>(name: "foto_base64", schema: "smsmarica", table: "motorista", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_bairro", schema: "smsmarica", table: "motorista", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_cep", schema: "smsmarica", table: "motorista", type: "character varying(8)", maxLength: 8, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_cidade", schema: "smsmarica", table: "motorista", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_complemento", schema: "smsmarica", table: "motorista", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_logradouro", schema: "smsmarica", table: "motorista", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_numero", schema: "smsmarica", table: "motorista", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_ponto_referencia", schema: "smsmarica", table: "motorista", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_uf", schema: "smsmarica", table: "motorista", type: "character varying(2)", maxLength: 2, nullable: true);

            // 2) Repopular colunas com dados do Usuario via JOIN.
            migrationBuilder.Sql("""
                UPDATE smsmarica.motorista m
                SET
                    cpf = u.cpf,
                    nome_completo = u.nome_completo,
                    telefone = u.telefone,
                    foto_base64 = u.foto_base64,
                    endereco_cep = u.endereco_cep,
                    endereco_logradouro = u.endereco_logradouro,
                    endereco_numero = u.endereco_numero,
                    endereco_complemento = u.endereco_complemento,
                    endereco_bairro = u.endereco_bairro,
                    endereco_cidade = u.endereco_cidade,
                    endereco_uf = u.endereco_uf,
                    endereco_ponto_referencia = u.endereco_ponto_referencia
                FROM smsmarica.usuario u
                WHERE m.usuario_id = u.id;
                """);

            // 3) Tornar cpf e nome_completo NOT NULL (estado original).
            migrationBuilder.AlterColumn<string>(name: "cpf", schema: "smsmarica", table: "motorista", type: "character varying(11)", maxLength: 11, nullable: false, defaultValue: "", oldClrType: typeof(string), oldType: "character varying(11)", oldMaxLength: 11, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "nome_completo", schema: "smsmarica", table: "motorista", type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "", oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200, oldNullable: true);

            // 4) Apagar usuarios criados exclusivamente pela migração (sem outros vínculos).
            migrationBuilder.Sql("""
                DELETE FROM smsmarica.usuario
                WHERE tipo_papel = 3
                  AND senha_hash = 'PENDENTE_AUTH'
                  AND email LIKE 'motorista-%@local.smsmarica';
                """);

            // 5) Remover FK/index/coluna usuario_id.
            migrationBuilder.DropForeignKey(name: "FK_motorista_usuario_usuario_id", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropIndex(name: "IX_motorista_usuario_id", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(name: "usuario_id", schema: "smsmarica", table: "motorista");

            migrationBuilder.CreateIndex(
                name: "IX_motorista_cpf",
                schema: "smsmarica",
                table: "motorista",
                column: "cpf",
                unique: true);
        }
    }
}
