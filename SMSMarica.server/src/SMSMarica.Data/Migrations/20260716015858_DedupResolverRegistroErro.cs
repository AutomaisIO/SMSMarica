using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class DedupResolverRegistroErro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assinatura",
                schema: "smsmarica",
                table: "registro_erro",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ocorrencias",
                schema: "smsmarica",
                table: "registro_erro",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "resolucao_nota",
                schema: "smsmarica",
                table: "registro_erro",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolvido_em",
                schema: "smsmarica",
                table: "registro_erro",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolvido_por",
                schema: "smsmarica",
                table: "registro_erro",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ultima_ocorrencia_em",
                schema: "smsmarica",
                table: "registro_erro",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // 1) Backfill da assinatura (mesma fórmula do RegistroErroService.Assinar: SHA-256 hex
            //    MAIÚSCULO de metodo|caminho|status|tipo|mensagem) e ultima_ocorrencia_em = criado_em.
            migrationBuilder.Sql("""
                UPDATE smsmarica.registro_erro
                SET assinatura = upper(encode(sha256(convert_to(
                        metodo || '|' || caminho || '|' || status_code::text || '|'
                              || tipo_excecao || '|' || mensagem, 'UTF8')), 'hex')),
                    ultima_ocorrencia_em = criado_em;
            """);

            // 2) Limpa duplicados existentes: por assinatura, consolida no registro MAIS ANTIGO
            //    (mantém o código de referência já ditado ao usuário), soma as ocorrências e leva a
            //    última data; depois apaga os demais. Nenhum resolvido ainda (coluna recém-criada).
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id, assinatura,
                           row_number() OVER (PARTITION BY assinatura ORDER BY criado_em, id) AS rn
                    FROM smsmarica.registro_erro
                ),
                stats AS (
                    SELECT assinatura, count(*)::int AS total, max(criado_em) AS ultimo
                    FROM smsmarica.registro_erro
                    GROUP BY assinatura
                )
                UPDATE smsmarica.registro_erro r
                SET ocorrencias = stats.total,
                    ultima_ocorrencia_em = stats.ultimo
                FROM ranked, stats
                WHERE r.id = ranked.id AND ranked.rn = 1 AND stats.assinatura = r.assinatura;
            """);

            migrationBuilder.Sql("""
                DELETE FROM smsmarica.registro_erro r
                USING (
                    SELECT id, row_number() OVER (PARTITION BY assinatura ORDER BY criado_em, id) AS rn
                    FROM smsmarica.registro_erro
                ) ranked
                WHERE r.id = ranked.id AND ranked.rn > 1;
            """);

            migrationBuilder.CreateIndex(
                name: "ix_registro_erro_assinatura_aberto",
                schema: "smsmarica",
                table: "registro_erro",
                column: "assinatura",
                filter: "resolvido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_registro_erro_assinatura_aberto",
                schema: "smsmarica",
                table: "registro_erro");

            migrationBuilder.DropColumn(
                name: "assinatura",
                schema: "smsmarica",
                table: "registro_erro");

            migrationBuilder.DropColumn(
                name: "ocorrencias",
                schema: "smsmarica",
                table: "registro_erro");

            migrationBuilder.DropColumn(
                name: "resolucao_nota",
                schema: "smsmarica",
                table: "registro_erro");

            migrationBuilder.DropColumn(
                name: "resolvido_em",
                schema: "smsmarica",
                table: "registro_erro");

            migrationBuilder.DropColumn(
                name: "resolvido_por",
                schema: "smsmarica",
                table: "registro_erro");

            migrationBuilder.DropColumn(
                name: "ultima_ocorrencia_em",
                schema: "smsmarica",
                table: "registro_erro");
        }
    }
}
