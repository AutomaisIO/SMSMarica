using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class TfdModeloBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_ibge_cidade",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "externa",
                schema: "smsmarica",
                table: "unidade",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "distancia_total_metros",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duracao_estimada_segundos",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "gerada_em",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "origem",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "plano_rota_json",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "eta_previsto",
                schema: "smsmarica",
                table: "translado_alocacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "janela_fim",
                schema: "smsmarica",
                table: "translado_alocacao",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "janela_inicio",
                schema: "smsmarica",
                table: "translado_alocacao",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ordem_parada",
                schema: "smsmarica",
                table: "translado_alocacao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo_parada",
                schema: "smsmarica",
                table: "translado_alocacao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "acompanhante_canal",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "acompanhante_confirmado_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "acompanhante_esperado",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tfd_config_google",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    api_key_cifrada = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tfd_config_google", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tfd_config_whatsapp",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    token_cifrado = table.Column<string>(type: "text", nullable: true),
                    phone_number_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    waba_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    verify_token_cifrado = table.Column<string>(type: "text", nullable: true),
                    app_secret_cifrado = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tfd_config_whatsapp", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tfd_geocodigo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    endereco_normalizado = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    fonte = table.Column<int>(type: "integer", nullable: false),
                    precisao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    revisao_pendente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    geocodificado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tfd_geocodigo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tfd_mensagem_whatsapp",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    template = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    direcao = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    wa_message_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    contexto_wa_message_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tfd_mensagem_whatsapp", x => x.id);
                    table.ForeignKey(
                        name: "FK_tfd_mensagem_whatsapp_sessao_de_tratamento_sessao_id",
                        column: x => x.sessao_id,
                        principalSchema: "smsmarica",
                        principalTable: "sessao_de_tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translado_alocacao_rota_diaria_id_ordem_parada",
                schema: "smsmarica",
                table: "translado_alocacao",
                columns: new[] { "rota_diaria_id", "ordem_parada" });

            migrationBuilder.CreateIndex(
                name: "IX_tfd_geocodigo_hash",
                schema: "smsmarica",
                table: "tfd_geocodigo",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tfd_geocodigo_revisao_pendente",
                schema: "smsmarica",
                table: "tfd_geocodigo",
                column: "revisao_pendente");

            migrationBuilder.CreateIndex(
                name: "IX_tfd_mensagem_whatsapp_paciente_id_ocorrido_em",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                columns: new[] { "paciente_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "IX_tfd_mensagem_whatsapp_sessao_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                column: "sessao_id");

            migrationBuilder.CreateIndex(
                name: "IX_tfd_mensagem_whatsapp_wa_message_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                column: "wa_message_id",
                unique: true,
                filter: "wa_message_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tfd_config_google",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tfd_config_whatsapp",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tfd_geocodigo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tfd_mensagem_whatsapp",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_translado_alocacao_rota_diaria_id_ordem_parada",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropColumn(
                name: "codigo_ibge_cidade",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "externa",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "distancia_total_metros",
                schema: "smsmarica",
                table: "translado_rota_diaria");

            migrationBuilder.DropColumn(
                name: "duracao_estimada_segundos",
                schema: "smsmarica",
                table: "translado_rota_diaria");

            migrationBuilder.DropColumn(
                name: "gerada_em",
                schema: "smsmarica",
                table: "translado_rota_diaria");

            migrationBuilder.DropColumn(
                name: "origem",
                schema: "smsmarica",
                table: "translado_rota_diaria");

            migrationBuilder.DropColumn(
                name: "plano_rota_json",
                schema: "smsmarica",
                table: "translado_rota_diaria");

            migrationBuilder.DropColumn(
                name: "eta_previsto",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropColumn(
                name: "janela_fim",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropColumn(
                name: "janela_inicio",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropColumn(
                name: "ordem_parada",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropColumn(
                name: "tipo_parada",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropColumn(
                name: "acompanhante_canal",
                schema: "smsmarica",
                table: "sessao_de_tratamento");

            migrationBuilder.DropColumn(
                name: "acompanhante_confirmado_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento");

            migrationBuilder.DropColumn(
                name: "acompanhante_esperado",
                schema: "smsmarica",
                table: "sessao_de_tratamento");
        }
    }
}
