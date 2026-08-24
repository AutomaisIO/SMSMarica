using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrarPacienteParaUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Criar 1 usuario para cada paciente existente, copiando dados pessoais base.
            //    tipo_papel = 5 (TipoPapel.Paciente). Email sintético sempre na migração
            //    para evitar colisões com UNIQUE; admin pode corrigir depois via /pacientes/{id}.
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.usuario (
                    id, nome_completo, cpf, rg, data_nascimento, sexo,
                    email, telefone,
                    senha_hash, deve_trocar_senha, tipo_papel,
                    ativo, criado_em, foto_base64,
                    endereco_cep, endereco_logradouro, endereco_numero, endereco_complemento,
                    endereco_bairro, endereco_cidade, endereco_uf, endereco_ponto_referencia
                )
                SELECT
                    gen_random_uuid(),
                    p.nome_completo,
                    p.cpf,
                    p.rg,
                    p.data_nascimento,
                    p.sexo,
                    'paciente-' || p.cpf || '@local.smsmarica',
                    p.telefone_principal,
                    'PENDENTE_AUTH',
                    TRUE,
                    5,
                    p.ativo,
                    p.criado_em,
                    p.foto_base64,
                    p.endereco_cep, p.endereco_logradouro, p.endereco_numero, p.endereco_complemento,
                    p.endereco_bairro, p.endereco_cidade, p.endereco_uf, p.endereco_ponto_referencia
                FROM smsmarica.paciente p
                WHERE NOT EXISTS (
                    SELECT 1 FROM smsmarica.usuario u WHERE u.cpf = p.cpf
                );
                """);

            // 2) Adicionar usuario_id em paciente como nullable temporariamente.
            migrationBuilder.AddColumn<System.Guid>(
                name: "usuario_id",
                schema: "smsmarica",
                table: "paciente",
                type: "uuid",
                nullable: true);

            // 3) Linkar cada paciente ao usuario correspondente via CPF.
            migrationBuilder.Sql("""
                UPDATE smsmarica.paciente p
                SET usuario_id = u.id
                FROM smsmarica.usuario u
                WHERE u.cpf = p.cpf;
                """);

            // 4) Promover usuario_id a NOT NULL.
            migrationBuilder.AlterColumn<System.Guid>(
                name: "usuario_id",
                schema: "smsmarica",
                table: "paciente",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(System.Guid),
                oldType: "uuid",
                oldNullable: true);

            // 5) Index único + FK.
            migrationBuilder.CreateIndex(
                name: "IX_paciente_usuario_id",
                schema: "smsmarica",
                table: "paciente",
                column: "usuario_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_paciente_usuario_usuario_id",
                schema: "smsmarica",
                table: "paciente",
                column: "usuario_id",
                principalSchema: "smsmarica",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // 6) Agora pode dropar colunas duplicadas (dados já vivem em usuario).
            migrationBuilder.DropIndex(
                name: "IX_paciente_cpf",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropIndex(
                name: "IX_paciente_nome_completo",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(name: "cpf", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "nome_completo", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "rg", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "data_nascimento", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "sexo", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "email", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "telefone_principal", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "foto_base64", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_bairro", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_cep", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_cidade", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_complemento", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_logradouro", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_numero", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_ponto_referencia", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "endereco_uf", schema: "smsmarica", table: "paciente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) Recriar colunas dropadas como nullable temporariamente.
            migrationBuilder.AddColumn<string>(name: "cpf", schema: "smsmarica", table: "paciente", type: "character varying(11)", maxLength: 11, nullable: true);
            migrationBuilder.AddColumn<string>(name: "nome_completo", schema: "smsmarica", table: "paciente", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "rg", schema: "smsmarica", table: "paciente", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<System.DateOnly>(name: "data_nascimento", schema: "smsmarica", table: "paciente", type: "date", nullable: true);
            migrationBuilder.AddColumn<int>(name: "sexo", schema: "smsmarica", table: "paciente", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "email", schema: "smsmarica", table: "paciente", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "telefone_principal", schema: "smsmarica", table: "paciente", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "foto_base64", schema: "smsmarica", table: "paciente", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_bairro", schema: "smsmarica", table: "paciente", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_cep", schema: "smsmarica", table: "paciente", type: "character varying(8)", maxLength: 8, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_cidade", schema: "smsmarica", table: "paciente", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_complemento", schema: "smsmarica", table: "paciente", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_logradouro", schema: "smsmarica", table: "paciente", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_numero", schema: "smsmarica", table: "paciente", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_ponto_referencia", schema: "smsmarica", table: "paciente", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "endereco_uf", schema: "smsmarica", table: "paciente", type: "character varying(2)", maxLength: 2, nullable: true);

            // 2) Repopular colunas com dados do Usuario via JOIN.
            migrationBuilder.Sql("""
                UPDATE smsmarica.paciente p
                SET
                    cpf = u.cpf,
                    nome_completo = u.nome_completo,
                    rg = u.rg,
                    data_nascimento = u.data_nascimento,
                    sexo = COALESCE(u.sexo, 0),
                    telefone_principal = u.telefone,
                    foto_base64 = u.foto_base64,
                    endereco_cep = u.endereco_cep,
                    endereco_logradouro = u.endereco_logradouro,
                    endereco_numero = u.endereco_numero,
                    endereco_complemento = u.endereco_complemento,
                    endereco_bairro = u.endereco_bairro,
                    endereco_cidade = u.endereco_cidade,
                    endereco_uf = u.endereco_uf,
                    endereco_ponto_referencia = u.endereco_ponto_referencia,
                    email = CASE WHEN u.email LIKE 'paciente-%@local.smsmarica' THEN NULL ELSE u.email END
                FROM smsmarica.usuario u
                WHERE p.usuario_id = u.id;
                """);

            // 3) Tornar cpf e nome_completo NOT NULL (estado original).
            migrationBuilder.AlterColumn<string>(name: "cpf", schema: "smsmarica", table: "paciente", type: "character varying(11)", maxLength: 11, nullable: false, defaultValue: "", oldClrType: typeof(string), oldType: "character varying(11)", oldMaxLength: 11, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "nome_completo", schema: "smsmarica", table: "paciente", type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "", oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200, oldNullable: true);

            // 4) Apagar usuarios criados exclusivamente pela migração (sem outros vínculos).
            migrationBuilder.Sql("""
                DELETE FROM smsmarica.usuario
                WHERE tipo_papel = 5
                  AND senha_hash = 'PENDENTE_AUTH'
                  AND email LIKE 'paciente-%@local.smsmarica';
                """);

            // 5) Remover FK/index/coluna usuario_id.
            migrationBuilder.DropForeignKey(name: "FK_paciente_usuario_usuario_id", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropIndex(name: "IX_paciente_usuario_id", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(name: "usuario_id", schema: "smsmarica", table: "paciente");

            // 6) Recriar indexes originais.
            migrationBuilder.CreateIndex(
                name: "IX_paciente_cpf",
                schema: "smsmarica",
                table: "paciente",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paciente_nome_completo",
                schema: "smsmarica",
                table: "paciente",
                column: "nome_completo");
        }
    }
}
