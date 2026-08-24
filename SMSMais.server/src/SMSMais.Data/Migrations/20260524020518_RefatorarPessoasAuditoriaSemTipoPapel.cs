using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefatorarPessoasAuditoriaSemTipoPapel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Adiciona colunas novas de auditoria em todas as tabelas-pessoa.

            migrationBuilder.AddColumn<DateTime>(
                name: "atualizado_em", schema: "smsmarica", table: "usuario",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "atualizado_por", schema: "smsmarica", table: "usuario",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "criado_por", schema: "smsmarica", table: "usuario",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "excluido_em", schema: "smsmarica", table: "usuario",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "excluido_por", schema: "smsmarica", table: "usuario",
                type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "atualizado_por", schema: "smsmarica", table: "paciente",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "criado_por", schema: "smsmarica", table: "paciente",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "excluido_em", schema: "smsmarica", table: "paciente",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "excluido_por", schema: "smsmarica", table: "paciente",
                type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "atualizado_por", schema: "smsmarica", table: "motorista",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "criado_por", schema: "smsmarica", table: "motorista",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "excluido_em", schema: "smsmarica", table: "motorista",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "excluido_por", schema: "smsmarica", table: "motorista",
                type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "atualizado_por", schema: "smsmarica", table: "medico",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "criado_por", schema: "smsmarica", table: "medico",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "excluido_em", schema: "smsmarica", table: "medico",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "excluido_por", schema: "smsmarica", table: "medico",
                type: "uuid", nullable: true);

            // 2) Backfill — registros antes marcados como ativo=false viram excluídos.
            //    Usa atualizado_em (data da desativação) como melhor proxy do excluido_em;
            //    fallback para criado_em quando atualizado_em é null.
            migrationBuilder.Sql(@"
                UPDATE smsmarica.paciente
                SET excluido_em = COALESCE(atualizado_em, criado_em)
                WHERE ativo = false;");
            migrationBuilder.Sql(@"
                UPDATE smsmarica.motorista
                SET excluido_em = COALESCE(atualizado_em, criado_em)
                WHERE ativo = false;");
            migrationBuilder.Sql(@"
                UPDATE smsmarica.medico
                SET excluido_em = COALESCE(atualizado_em, criado_em)
                WHERE ativo = false;");

            // 3) Drop do CHECK constraint que referenciava tipo_papel.
            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_papel_exige_cpf",
                schema: "smsmarica",
                table: "usuario");

            // 4) Drop dos índices antigos que referenciam colunas que vamos remover.
            migrationBuilder.DropIndex(
                name: "IX_paciente_ativo",
                schema: "smsmarica",
                table: "paciente");

            // 5) Drop das colunas antigas (ativo nos papéis; tipo_papel em usuário).
            migrationBuilder.DropColumn(
                name: "ativo", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropColumn(
                name: "ativo", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropColumn(
                name: "ativo", schema: "smsmarica", table: "medico");
            migrationBuilder.DropColumn(
                name: "tipo_papel", schema: "smsmarica", table: "usuario");

            // 6) Índices novos (filtered) em excluido_em para acelerar a listagem
            //    'apenas vigentes' (queries adicionam WHERE excluido_em IS NULL).
            migrationBuilder.CreateIndex(
                name: "ix_usuario_excluido_em",
                schema: "smsmarica",
                table: "usuario",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_paciente_excluido_em",
                schema: "smsmarica",
                table: "paciente",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_motorista_excluido_em",
                schema: "smsmarica",
                table: "motorista",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_medico_excluido_em",
                schema: "smsmarica",
                table: "medico",
                column: "excluido_em",
                filter: "excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_usuario_excluido_em", schema: "smsmarica", table: "usuario");
            migrationBuilder.DropIndex(name: "ix_paciente_excluido_em", schema: "smsmarica", table: "paciente");
            migrationBuilder.DropIndex(name: "ix_motorista_excluido_em", schema: "smsmarica", table: "motorista");
            migrationBuilder.DropIndex(name: "ix_medico_excluido_em", schema: "smsmarica", table: "medico");

            migrationBuilder.AddColumn<int>(
                name: "tipo_papel", schema: "smsmarica", table: "usuario",
                type: "integer", nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ativo", schema: "smsmarica", table: "paciente",
                type: "boolean", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(
                name: "ativo", schema: "smsmarica", table: "motorista",
                type: "boolean", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(
                name: "ativo", schema: "smsmarica", table: "medico",
                type: "boolean", nullable: false, defaultValue: true);

            // Re-backfill ativo a partir de excluido_em (heurística no caminho inverso).
            migrationBuilder.Sql("UPDATE smsmarica.paciente SET ativo = (excluido_em IS NULL);");
            migrationBuilder.Sql("UPDATE smsmarica.motorista SET ativo = (excluido_em IS NULL);");
            migrationBuilder.Sql("UPDATE smsmarica.medico SET ativo = (excluido_em IS NULL);");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_papel_exige_cpf",
                schema: "smsmarica",
                table: "usuario",
                sql: "tipo_papel IS NULL OR cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_paciente_ativo",
                schema: "smsmarica",
                table: "paciente",
                column: "ativo");

            // Drop das colunas novas de auditoria.
            foreach (var tabela in new[] { "usuario", "paciente", "motorista", "medico" })
            {
                migrationBuilder.DropColumn(name: "excluido_por", schema: "smsmarica", table: tabela);
                migrationBuilder.DropColumn(name: "excluido_em", schema: "smsmarica", table: tabela);
                migrationBuilder.DropColumn(name: "criado_por", schema: "smsmarica", table: tabela);
                migrationBuilder.DropColumn(name: "atualizado_por", schema: "smsmarica", table: tabela);
            }
            // Em usuario, atualizado_em é nova (em motorista/medico/paciente já existia).
            migrationBuilder.DropColumn(name: "atualizado_em", schema: "smsmarica", table: "usuario");
        }
    }
}
