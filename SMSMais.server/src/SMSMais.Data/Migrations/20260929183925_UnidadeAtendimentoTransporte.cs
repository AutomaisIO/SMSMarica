using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnidadeAtendimentoTransporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tratamento_unidade_unidade_id",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.RenameColumn(
                name: "unidade_id",
                schema: "smsmarica",
                table: "tratamento",
                newName: "unidade_atendimento_id");

            migrationBuilder.RenameIndex(
                name: "IX_tratamento_unidade_id",
                schema: "smsmarica",
                table: "tratamento",
                newName: "IX_tratamento_unidade_atendimento_id");

            migrationBuilder.RenameColumn(
                name: "unidade_id",
                schema: "smsmarica",
                table: "tfd_registro_faturamento",
                newName: "unidade_atendimento_id");

            migrationBuilder.AddColumn<int>(
                name: "tempo_medio_minutos",
                schema: "smsmarica",
                table: "tratamento",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "unidade_atendimento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    endereco_bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    endereco_cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    endereco_ponto_referencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    externa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidade_atendimento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidade_atendimento_ativo",
                schema: "smsmarica",
                table: "unidade_atendimento",
                column: "ativo");

            // O destino do tratamento deixa de ser a unidade de saúde (SISREG/CNES) e passa a ser a
            // unidade de atendimento. As unidades que tratamentos/faturamento já referenciam são
            // copiadas com o MESMO id — a coluna renomeada continua apontando para algo válido e
            // nada se perde. A cópia nasce ativa só se houver tratamento ativo indo para ela; as
            // demais ficam inativas, fora do seletor, à espera do cadastro manual. Nome em maiúsculas,
            // como o cadastro exige. Numa instância
            // nova as duas tabelas estão vazias e o INSERT não faz nada.
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.unidade_atendimento
                    (id, nome, endereco_cep, endereco_logradouro, endereco_numero, endereco_complemento,
                     endereco_bairro, endereco_cidade, endereco_uf, endereco_ponto_referencia,
                     latitude, longitude, telefone, externa, ativo, criado_em)
                SELECT u.id, upper(u.nome), u.endereco_cep, u.endereco_logradouro, u.endereco_numero, u.endereco_complemento,
                       u.endereco_bairro, u.endereco_cidade, u.endereco_uf, u.endereco_ponto_referencia,
                       u.latitude, u.longitude, u.telefone, u.externa,
                       EXISTS (SELECT 1 FROM smsmarica.tratamento t
                               WHERE t.unidade_atendimento_id = u.id AND t.ativo),
                       now()
                FROM smsmarica.unidade u
                WHERE u.id IN (SELECT unidade_atendimento_id FROM smsmarica.tratamento
                               UNION
                               SELECT unidade_atendimento_id FROM smsmarica.tfd_registro_faturamento);
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_tratamento_unidade_atendimento_unidade_atendimento_id",
                schema: "smsmarica",
                table: "tratamento",
                column: "unidade_atendimento_id",
                principalSchema: "smsmarica",
                principalTable: "unidade_atendimento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Só volta limpo enquanto todo tratamento apontar para uma cópia (id = unidade de saúde).
            // Tratamento criado depois, com destino cadastrado à mão, quebra a FK recriada abaixo —
            // de propósito: desfazer aqui descartaria o destino dele em silêncio.
            migrationBuilder.DropForeignKey(
                name: "FK_tratamento_unidade_atendimento_unidade_atendimento_id",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropTable(
                name: "unidade_atendimento",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "tempo_medio_minutos",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.RenameColumn(
                name: "unidade_atendimento_id",
                schema: "smsmarica",
                table: "tratamento",
                newName: "unidade_id");

            migrationBuilder.RenameIndex(
                name: "IX_tratamento_unidade_atendimento_id",
                schema: "smsmarica",
                table: "tratamento",
                newName: "IX_tratamento_unidade_id");

            migrationBuilder.RenameColumn(
                name: "unidade_atendimento_id",
                schema: "smsmarica",
                table: "tfd_registro_faturamento",
                newName: "unidade_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tratamento_unidade_unidade_id",
                schema: "smsmarica",
                table: "tratamento",
                column: "unidade_id",
                principalSchema: "smsmarica",
                principalTable: "unidade",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
