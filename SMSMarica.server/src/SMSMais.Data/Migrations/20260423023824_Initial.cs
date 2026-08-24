using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "smsmarica");

            migrationBuilder.CreateTable(
                name: "motorista",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_completo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    cnh = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_motorista", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "paciente",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_completo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    residencia_latitude = table.Column<double>(type: "double precision", nullable: false),
                    residencia_longitude = table.Column<double>(type: "double precision", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paciente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rastreamento_geofence",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    centro_latitude = table.Column<double>(type: "double precision", nullable: false),
                    centro_longitude = table.Column<double>(type: "double precision", nullable: false),
                    raio_metros = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rastreamento_geofence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidade", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_completo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    perfil = table.Column<int>(type: "integer", nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultimo_acesso_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "veiculo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    placa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rastreamento_ponto_gps",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    motorista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    capturado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rastreamento_ponto_gps", x => x.id);
                    table.ForeignKey(
                        name: "FK_rastreamento_ponto_gps_motorista_motorista_id",
                        column: x => x.motorista_id,
                        principalSchema: "smsmarica",
                        principalTable: "motorista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tratamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tratamento", x => x.id);
                    table.ForeignKey(
                        name: "FK_tratamento_paciente_paciente_id",
                        column: x => x.paciente_id,
                        principalSchema: "smsmarica",
                        principalTable: "paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tratamento_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "translado_rota_diaria",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motorista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    iniciada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    concluida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translado_rota_diaria", x => x.id);
                    table.ForeignKey(
                        name: "FK_translado_rota_diaria_motorista_motorista_id",
                        column: x => x.motorista_id,
                        principalSchema: "smsmarica",
                        principalTable: "motorista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translado_rota_diaria_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "veiculo_fileira",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    quantidade_assentos = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculo_fileira", x => x.id);
                    table.ForeignKey(
                        name: "FK_veiculo_fileira_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translado_sessao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tratamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_prevista = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translado_sessao", x => x.id);
                    table.ForeignKey(
                        name: "FK_translado_sessao_tratamento_tratamento_id",
                        column: x => x.tratamento_id,
                        principalSchema: "smsmarica",
                        principalTable: "tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tratamento_periodicidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tratamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    intervalo_dias = table.Column<int>(type: "integer", nullable: true),
                    dias_semana_mascara = table.Column<int>(type: "integer", nullable: true),
                    data_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    quantidade_sessoes = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tratamento_periodicidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_tratamento_periodicidade_tratamento_tratamento_id",
                        column: x => x.tratamento_id,
                        principalSchema: "smsmarica",
                        principalTable: "tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rastreamento_evento_chegada",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rota_diaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geofence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rastreamento_evento_chegada", x => x.id);
                    table.ForeignKey(
                        name: "FK_rastreamento_evento_chegada_rastreamento_geofence_geofence_~",
                        column: x => x.geofence_id,
                        principalSchema: "smsmarica",
                        principalTable: "rastreamento_geofence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rastreamento_evento_chegada_translado_rota_diaria_rota_diar~",
                        column: x => x.rota_diaria_id,
                        principalSchema: "smsmarica",
                        principalTable: "translado_rota_diaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "veiculo_assento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fileira_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculo_assento", x => x.id);
                    table.ForeignKey(
                        name: "FK_veiculo_assento_veiculo_fileira_fileira_id",
                        column: x => x.fileira_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo_fileira",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "avaliacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nota = table.Column<int>(type: "integer", nullable: false),
                    comentario = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avaliacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_avaliacao_translado_sessao_sessao_id",
                        column: x => x.sessao_id,
                        principalSchema: "smsmarica",
                        principalTable: "translado_sessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translado_alocacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rota_diaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translado_alocacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_translado_alocacao_translado_rota_diaria_rota_diaria_id",
                        column: x => x.rota_diaria_id,
                        principalSchema: "smsmarica",
                        principalTable: "translado_rota_diaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_translado_alocacao_translado_sessao_sessao_id",
                        column: x => x.sessao_id,
                        principalSchema: "smsmarica",
                        principalTable: "translado_sessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translado_alocacao_veiculo_assento_assento_id",
                        column: x => x.assento_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo_assento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_avaliacao_sessao_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "sessao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_motorista_cnh",
                schema: "smsmarica",
                table: "motorista",
                column: "cnh",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_motorista_cpf",
                schema: "smsmarica",
                table: "motorista",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paciente_cpf",
                schema: "smsmarica",
                table: "paciente",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_evento_chegada_geofence_id",
                schema: "smsmarica",
                table: "rastreamento_evento_chegada",
                column: "geofence_id");

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_evento_chegada_rota_diaria_id_ocorrido_em",
                schema: "smsmarica",
                table: "rastreamento_evento_chegada",
                columns: new[] { "rota_diaria_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_geofence_tipo_referencia_id",
                schema: "smsmarica",
                table: "rastreamento_geofence",
                columns: new[] { "tipo", "referencia_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_ponto_gps_motorista_id_capturado_em",
                schema: "smsmarica",
                table: "rastreamento_ponto_gps",
                columns: new[] { "motorista_id", "capturado_em" });

            migrationBuilder.CreateIndex(
                name: "IX_translado_alocacao_assento_id",
                schema: "smsmarica",
                table: "translado_alocacao",
                column: "assento_id");

            migrationBuilder.CreateIndex(
                name: "IX_translado_alocacao_rota_diaria_id_assento_id",
                schema: "smsmarica",
                table: "translado_alocacao",
                columns: new[] { "rota_diaria_id", "assento_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translado_alocacao_sessao_id",
                schema: "smsmarica",
                table: "translado_alocacao",
                column: "sessao_id");

            migrationBuilder.CreateIndex(
                name: "IX_translado_rota_diaria_data",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "IX_translado_rota_diaria_motorista_id",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                column: "motorista_id");

            migrationBuilder.CreateIndex(
                name: "IX_translado_rota_diaria_veiculo_id",
                schema: "smsmarica",
                table: "translado_rota_diaria",
                column: "veiculo_id");

            migrationBuilder.CreateIndex(
                name: "IX_translado_sessao_data_prevista",
                schema: "smsmarica",
                table: "translado_sessao",
                column: "data_prevista");

            migrationBuilder.CreateIndex(
                name: "IX_translado_sessao_tratamento_id_data_prevista",
                schema: "smsmarica",
                table: "translado_sessao",
                columns: new[] { "tratamento_id", "data_prevista" });

            migrationBuilder.CreateIndex(
                name: "IX_tratamento_paciente_id",
                schema: "smsmarica",
                table: "tratamento",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_tratamento_unidade_id",
                schema: "smsmarica",
                table: "tratamento",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_tratamento_periodicidade_tratamento_id",
                schema: "smsmarica",
                table: "tratamento_periodicidade",
                column: "tratamento_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_email",
                schema: "smsmarica",
                table: "usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_placa",
                schema: "smsmarica",
                table: "veiculo",
                column: "placa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_assento_fileira_id_numero",
                schema: "smsmarica",
                table: "veiculo_assento",
                columns: new[] { "fileira_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_fileira_veiculo_id_ordem",
                schema: "smsmarica",
                table: "veiculo_fileira",
                columns: new[] { "veiculo_id", "ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avaliacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "rastreamento_evento_chegada",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "rastreamento_ponto_gps",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "translado_alocacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tratamento_periodicidade",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "usuario",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "rastreamento_geofence",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "translado_rota_diaria",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "translado_sessao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "veiculo_assento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "motorista",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tratamento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "veiculo_fileira",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "paciente",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "unidade",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "veiculo",
                schema: "smsmarica");
        }
    }
}
