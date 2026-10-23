using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ComunicacaoInternaTranscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "transcricao",
                schema: "smsmarica",
                table: "mensagem_interna",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "transcrita_em",
                schema: "smsmarica",
                table: "mensagem_interna",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transcrita_por",
                schema: "smsmarica",
                table: "mensagem_interna",
                type: "uuid",
                nullable: true);

            // O áudio transcrito entra na busca como o texto (f_unaccent + GIN trigram, ver ComunicacaoInterna).
            migrationBuilder.Sql("SET LOCAL search_path = smsmarica, public;");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_mensagem_interna_transcricao_trgm
    ON smsmarica.mensagem_interna USING gin (smsmarica.f_unaccent(transcricao) gin_trgm_ops)
    WHERE transcricao IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS smsmarica.ix_mensagem_interna_transcricao_trgm;");

            migrationBuilder.DropColumn(
                name: "transcricao",
                schema: "smsmarica",
                table: "mensagem_interna");

            migrationBuilder.DropColumn(
                name: "transcrita_em",
                schema: "smsmarica",
                table: "mensagem_interna");

            migrationBuilder.DropColumn(
                name: "transcrita_por",
                schema: "smsmarica",
                table: "mensagem_interna");
        }
    }
}
