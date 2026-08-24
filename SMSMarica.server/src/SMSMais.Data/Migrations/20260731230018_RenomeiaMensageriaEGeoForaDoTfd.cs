using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// ADR-0038 — mensageria WhatsApp e geocodificação saem de baixo do TFD.
    ///
    /// ATENÇÃO AO REVISOR: esta migration foi <b>reescrita à mão</b>. O scaffolder do EF
    /// gerou <c>DropTable</c> + <c>CreateTable</c>, porque ele não sabe inferir um rename —
    /// enxerga "tabela sumiu, tabela nova apareceu". Aplicar aquilo teria APAGADO 26 mil
    /// mensagens de WhatsApp, 696 geocódigos e as credenciais da Meta Cloud API.
    ///
    /// O que roda aqui é <c>ALTER TABLE ... RENAME TO</c>: operação de catálogo, instantânea,
    /// que não move um byte de dado. As FKs continuam válidas sozinhas (no PostgreSQL a
    /// constraint referencia a tabela por OID, não por nome) — só os NOMES de índices e
    /// constraints precisam ser acertados, senão sobra <c>PK_tfd_mensagem_whatsapp</c> numa
    /// tabela chamada <c>whatsapp_mensagem</c> e o próximo <c>migrations add</c> gera diff fantasma.
    ///
    /// Se algum dia for preciso regerar: NÃO aceitar o scaffold cru.
    /// </summary>
    public partial class RenomeiaMensageriaEGeoForaDoTfd : Migration
    {
        /// <summary>Pares (nome antigo, nome novo) usados nos laços de rename de constraint.</summary>
        private const string MapaSql = @"
            ARRAY[
                ['tfd_mensagem_whatsapp',   'whatsapp_mensagem'],
                ['tfd_config_whatsapp',     'whatsapp_configuracao'],
                ['tfd_geocodigo',           'geo_endereco'],
                ['tfd_config_google',       'geo_configuracao'],
                ['tfd_config_faturamento',  'tfd_configuracao']
            ]";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -----------------------------------------------------------------------------
            // 1. Cortar o cordão umbilical: a mensagem não conhece mais o TFD.
            //    sessao_id tinha 0 linhas preenchidas em 26.055 — nunca carregou um dado.
            //    Quem precisar amarrar mensagem a evento de domínio referencia a mensagem
            //    (como comunicacao_paciente faz), e não o contrário.
            // -----------------------------------------------------------------------------
            migrationBuilder.DropForeignKey(
                name: "FK_tfd_mensagem_whatsapp_sessao_de_tratamento_sessao_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_tfd_mensagem_whatsapp_sessao_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropColumn(
                name: "sessao_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            // -----------------------------------------------------------------------------
            // 2. Renomear as tabelas. Catálogo apenas — os dados ficam onde estão.
            // -----------------------------------------------------------------------------
            migrationBuilder.RenameTable(name: "tfd_mensagem_whatsapp", schema: "smsmarica",
                                         newName: "whatsapp_mensagem", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "tfd_config_whatsapp", schema: "smsmarica",
                                         newName: "whatsapp_configuracao", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "tfd_geocodigo", schema: "smsmarica",
                                         newName: "geo_endereco", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "tfd_config_google", schema: "smsmarica",
                                         newName: "geo_configuracao", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "tfd_config_faturamento", schema: "smsmarica",
                                         newName: "tfd_configuracao", newSchema: "smsmarica");

            // -----------------------------------------------------------------------------
            // 3. Índices secundários (os que não sustentam constraint).
            // -----------------------------------------------------------------------------
            migrationBuilder.RenameIndex(
                name: "IX_tfd_mensagem_whatsapp_wa_message_id", schema: "smsmarica",
                table: "whatsapp_mensagem", newName: "IX_whatsapp_mensagem_wa_message_id");
            migrationBuilder.RenameIndex(
                name: "IX_tfd_mensagem_whatsapp_paciente_id_ocorrido_em", schema: "smsmarica",
                table: "whatsapp_mensagem", newName: "IX_whatsapp_mensagem_paciente_id_ocorrido_em");
            migrationBuilder.RenameIndex(
                name: "IX_tfd_mensagem_whatsapp_conversa_id_ocorrido_em", schema: "smsmarica",
                table: "whatsapp_mensagem", newName: "IX_whatsapp_mensagem_conversa_id_ocorrido_em");
            migrationBuilder.RenameIndex(
                name: "IX_tfd_geocodigo_hash", schema: "smsmarica",
                table: "geo_endereco", newName: "IX_geo_endereco_hash");
            migrationBuilder.RenameIndex(
                name: "IX_tfd_geocodigo_revisao_pendente", schema: "smsmarica",
                table: "geo_endereco", newName: "IX_geo_endereco_revisao_pendente");

            // -----------------------------------------------------------------------------
            // 4. Constraints que sobraram com o nome antigo: PK, FK e os not-null nomeados
            //    pelo PostgreSQL. Em laço para não depender de listar ~40 nomes à mão e para
            //    tolerar a versão do PG, que mudou como nomeia not-null.
            // -----------------------------------------------------------------------------
            migrationBuilder.Sql($@"
                DO $$
                DECLARE
                    mapa text[][] := {MapaSql};
                    i int; velho text; novo text; r record;
                BEGIN
                    FOR i IN 1 .. array_length(mapa, 1) LOOP
                        velho := mapa[i][1];
                        novo  := mapa[i][2];

                        FOR r IN
                            SELECT c.conname
                            FROM pg_constraint c
                            JOIN pg_class t     ON t.oid = c.conrelid
                            JOIN pg_namespace n ON n.oid = t.relnamespace
                            WHERE n.nspname = 'smsmarica'
                              AND t.relname = novo
                              AND c.conname LIKE '%' || velho || '%'
                        LOOP
                            -- Renome de constraint é COSMÉTICO: se um caso específico não puder
                            -- ser renomeado, avisa e segue, em vez de abortar a migration inteira
                            -- e derrubar o rename das tabelas (que é o que realmente importa).
                            -- Motivo concreto: o PostgreSQL 18 cria constraints NOT NULL nomeadas
                            -- (~27 nestas tabelas) que o PG 17 não cria — não deu para exercitar
                            -- esse caminho na bancada de ensaio, que roda 17.
                            BEGIN
                                EXECUTE format('ALTER TABLE smsmarica.%I RENAME CONSTRAINT %I TO %I',
                                               novo, r.conname, replace(r.conname, velho, novo));
                            EXCEPTION WHEN others THEN
                                RAISE NOTICE 'ADR-0038: nao foi possivel renomear a constraint % (%). Nome antigo permanece — apenas cosmetico.',
                                             r.conname, SQLERRM;
                            END;
                        END LOOP;
                    END LOOP;
                END $$;");

            // A FK mora em comunicacao_paciente (lado dependente), então não entra no laço
            // acima, que varre apenas as tabelas renomeadas. O nome antigo vem truncado no
            // limite de 63 caracteres do PostgreSQL — por isso o LIKE em vez do nome literal.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT c.conname
                        FROM pg_constraint c
                        JOIN pg_class t     ON t.oid = c.conrelid
                        JOIN pg_namespace n ON n.oid = t.relnamespace
                        WHERE n.nspname = 'smsmarica'
                          AND t.relname = 'comunicacao_paciente'
                          AND c.conname LIKE 'FK_comunicacao_paciente_tfd_mensagem_whatsapp%'
                    LOOP
                        EXECUTE format('ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT %I TO %I',
                                       r.conname,
                                       'FK_comunicacao_paciente_whatsapp_mensagem_mensagem_whatsapp_id');
                    END LOOP;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Espelho do Up. O rollback é outro rename — não perde as mensagens que tiverem
            // chegado depois da ida (por isso ele, e não o restore do backup, é o caminho 1).
            migrationBuilder.Sql(@"
                DO $$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT c.conname
                        FROM pg_constraint c
                        JOIN pg_class t     ON t.oid = c.conrelid
                        JOIN pg_namespace n ON n.oid = t.relnamespace
                        WHERE n.nspname = 'smsmarica'
                          AND t.relname = 'comunicacao_paciente'
                          AND c.conname LIKE 'FK_comunicacao_paciente_whatsapp_mensagem%'
                    LOOP
                        EXECUTE format('ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT %I TO %I',
                                       r.conname,
                                       'FK_comunicacao_paciente_tfd_mensagem_whatsapp_mensagem_whatsap~');
                    END LOOP;
                END $$;");

            migrationBuilder.Sql($@"
                DO $$
                DECLARE
                    mapa text[][] := {MapaSql};
                    i int; antigo text; atual text; r record;
                BEGIN
                    FOR i IN 1 .. array_length(mapa, 1) LOOP
                        antigo := mapa[i][1];
                        atual  := mapa[i][2];

                        FOR r IN
                            SELECT c.conname
                            FROM pg_constraint c
                            JOIN pg_class t     ON t.oid = c.conrelid
                            JOIN pg_namespace n ON n.oid = t.relnamespace
                            WHERE n.nspname = 'smsmarica'
                              AND t.relname = atual
                              AND c.conname LIKE '%' || atual || '%'
                        LOOP
                            EXECUTE format('ALTER TABLE smsmarica.%I RENAME CONSTRAINT %I TO %I',
                                           atual, r.conname, replace(r.conname, atual, antigo));
                        END LOOP;
                    END LOOP;
                END $$;");

            migrationBuilder.RenameIndex(
                name: "IX_geo_endereco_revisao_pendente", schema: "smsmarica",
                table: "geo_endereco", newName: "IX_tfd_geocodigo_revisao_pendente");
            migrationBuilder.RenameIndex(
                name: "IX_geo_endereco_hash", schema: "smsmarica",
                table: "geo_endereco", newName: "IX_tfd_geocodigo_hash");
            migrationBuilder.RenameIndex(
                name: "IX_whatsapp_mensagem_conversa_id_ocorrido_em", schema: "smsmarica",
                table: "whatsapp_mensagem", newName: "IX_tfd_mensagem_whatsapp_conversa_id_ocorrido_em");
            migrationBuilder.RenameIndex(
                name: "IX_whatsapp_mensagem_paciente_id_ocorrido_em", schema: "smsmarica",
                table: "whatsapp_mensagem", newName: "IX_tfd_mensagem_whatsapp_paciente_id_ocorrido_em");
            migrationBuilder.RenameIndex(
                name: "IX_whatsapp_mensagem_wa_message_id", schema: "smsmarica",
                table: "whatsapp_mensagem", newName: "IX_tfd_mensagem_whatsapp_wa_message_id");

            migrationBuilder.RenameTable(name: "tfd_configuracao", schema: "smsmarica",
                                         newName: "tfd_config_faturamento", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "geo_configuracao", schema: "smsmarica",
                                         newName: "tfd_config_google", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "geo_endereco", schema: "smsmarica",
                                         newName: "tfd_geocodigo", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "whatsapp_configuracao", schema: "smsmarica",
                                         newName: "tfd_config_whatsapp", newSchema: "smsmarica");
            migrationBuilder.RenameTable(name: "whatsapp_mensagem", schema: "smsmarica",
                                         newName: "tfd_mensagem_whatsapp", newSchema: "smsmarica");

            migrationBuilder.AddColumn<Guid>(
                name: "sessao_id", schema: "smsmarica",
                table: "tfd_mensagem_whatsapp", type: "uuid", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tfd_mensagem_whatsapp_sessao_id", schema: "smsmarica",
                table: "tfd_mensagem_whatsapp", column: "sessao_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tfd_mensagem_whatsapp_sessao_de_tratamento_sessao_id",
                schema: "smsmarica", table: "tfd_mensagem_whatsapp", column: "sessao_id",
                principalSchema: "smsmarica", principalTable: "sessao_de_tratamento",
                principalColumn: "id", onDelete: ReferentialAction.SetNull);
        }
    }
}
