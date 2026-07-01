FASE A — plano concreto arquivo-a-arquivo (pronto para implementar após aprovação). Ordem = P0 (hotfix/pré-requisitos, prod primeiro) → refactor do mapper → backfill → testes.

=== P0.1 — UPSERT de identifiers (mata o Identifier=[]; hotfix isolado) ===
Arquivo: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\PacienteFhirMapper.cs
- Em AplicarPayload (linha ~301): remover `patient.Identifier = [];` + os 3 `Add`. Trocar por chamadas a um novo helper `UpsertIdentifier(patient, system, value)` que:
  - garante `patient.Identifier ??= []`;
  - encontra por `System==system`; se existe e value não-vazio, atualiza `.Value`; se não existe e value não-vazio, faz Add; nunca remove os demais.
  - preserva Assigner existente (RG do importado) quando só o value muda.
- Efeito: preserva PIS/passaporte/RNE/certidão/SGH/CEM/urn:salux:cd_paciente. Este commit sozinho já fecha o bug ativo de re-dedup/duplicação clínica.

=== P0.2 — Concorrência otimista (If-Match/ETag) ===
Hub:
- C:\Projetos GIT\SMSMarica\Automais.Fhir\src\Automais.Fhir.Core\Patients\IPatientService.cs
  - `AtualizarAsync(Guid id, Patient patient, int? versaoEsperada, CancellationToken ct)`.
- C:\Projetos GIT\SMSMarica\Automais.Fhir\src\Automais.Fhir.Core\Patients\PatientService.cs (AtualizarAsync, linha ~42)
  - se `versaoEsperada is int v && v != row.VersionId` → lançar nova `ConflitoVersaoException(id, v, row.VersionId)`.
- C:\Projetos GIT\SMSMarica\Automais.Fhir\src\Automais.Fhir.Core\Common\Excecoes\FhirException.cs
  - adicionar `ConflitoVersaoException` (mapeada para 409 no OperationOutcomeMiddleware).
- C:\Projetos GIT\SMSMarica\Automais.Fhir\src\Automais.Fhir.Api\Infra\OperationOutcomeMiddleware.cs
  - mapear `ConflitoVersaoException` → 409.
- C:\Projetos GIT\SMSMarica\Automais.Fhir\src\Automais.Fhir.Api\Controllers\PatientController.cs
  - Atualizar: ler header `If-Match` (`W/"<n>"`), extrair int, passar a `versaoEsperada`.
  - Ler/Criar/Atualizar: setar `Response.Headers.ETag = W/"{Meta.VersionId}"`.
Server (cliente):
- C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\Fhir\IPacienteFhirClient.cs e PacienteFhirClient.cs
  - AtualizarAsync: enviar `If-Match: W/"{patient.Meta?.VersionId}"`; em 409/412 lançar `ConflitoVersaoHubException`.
- C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Integracoes\Pep\Fhir\HubFhirEscritor.cs
  - AtualizarAsync: idem If-Match; expor conflito para retry do chamador.
- C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\PacientesService.cs
  - AtualizarAsync/AtualizarNomeAsync/AdicionarTelefoneAsync/AtualizarFotoAsync/AtualizarContatoAsync: envolver ObterAsync→mutar→AtualizarAsync num retry (ex.: 3x) que re-lê e re-aplica em `ConflitoVersaoHubException`.

=== P0.3 — Importação vira merge/preserve (nunca replace-all) ===
Arquivo: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Integracoes\Pep\Estrategias\Salux\SaluxImportacaoStrategy.cs (UpsertCanonicoAsync, linha ~316)
- Quando `atual` existe e é Patient: em vez de `novo.Id = atual.Id; PUT novo`, fazer MERGE sobre `atual`:
  - copiar a extension `urn:smsmarica:paciente-payload` de `atual` para o recurso a gravar (preserva blob → clínico não é destruído);
  - aplicar os campos Oracle via o motor de merge compartilhado (ver R1) respeitando a regra de dono: Oracle escreve identidade/filiação/oficiais; telecom/email/endereço/foto/contato geridos pelo smsmarica são preservados quando já presentes (ver Decisão aberta abaixo);
  - unir identifiers (UnirIdentifiers já faz);
  - gravar com If-Match (VersionId de `atual`) + retry.
- Extrair a lógica de merge para um utilitário compartilhado (ver R1) para edição e import usarem a MESMA semântica.
Decisão aberta a confirmar antes do merge: no reimport COMPLETO, quem vence o contato (Oracle vs edição smsmarica). Recomendado: marcador de proveniência `urn:smsmarica:campos-editados` (lista de chaves escritas pelo form) que o import não sobrescreve; fallback simples: proteção só no incremental (dt_alteracao) + full-refresh documentado como overwrite.

=== R1 — Refator do PacienteFhirMapper para native-first + motor de merge ===
Arquivo: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\PacienteFhirMapper.cs
- Novo `AplicarNativo(Patient existente, <campos do request>)` (substitui AplicarPayload como caminho de escrita demográfica), com helpers de UPSERT idempotente:
  - UpsertNomeOficial(text) — preserva se vazio; nunca Text vazio.
  - UpsertNomeSocial(text) — HumanName use=Nickname.
  - UpsertIdentifier(system, value) — de P0.1.
  - SetBirthDate/SetGender — só se fornecido; senão preserva.
  - UpsertEndereco(EnderecoDto) — address[0]: Line[0]=logradouro; número → extension iso21090-ADXP-houseNumber no elemento Line[0] (round-trip do campo Numero); Line[1]=complemento; District/City/State/PostalCode/Text.
  - UpsertTelecom(system, use, value, rank?) — por (system,use); principal com Rank=1; dedupe por dígitos; preserva não gerenciados.
  - UpsertContato(relCode, nome, fone?) — MTH/FTH/GUARD/SPS (v3-RoleCode) e emergência (v2-0131|C, parentesco em coding secundário/text).
  - SetMaritalStatus(EstadoCivil) — coding v3-MaritalStatus (S/M/T/D/W/L/UNK) + text.
  - SetFoto(base64) — Patient.photo.
- ConstruirNovo: montar Patient nativo via AplicarNativo (espelha BuildPatient), NÃO via Payload. Em Fase A, ainda gravar o blob COMPLETO como dual-write (reversibilidade). Também: chamar geocoder no PacientesService (ver R2) para lat/long.
- AplicarAtualizacao: operar direto sobre o nativo via AplicarNativo; remover as gambiarras de fallback de imutáveis (linhas ~96-102 e 132-133), pois com escrita nativa correta o name.text vazio e o apagamento de CPF/CNS/nascimento deixam de existir. Manter dual-write do blob completo.
- AplicarNome (linha ~149): já é native-correto; manter (o ramo de blob some só na Fase C).
- ParaDto: virar native-first para TODOS os campos com casa nativa, com novos leitores:
  - NomeSocialNativo (name nickname), TelecomPorUso (principal=rank1||primeiro; celular=use=mobile≠principal; residencial=use=home≠principal), EmailNativo (já existe telecom email), EstadoCivilNativo (coding v3 → enum; senão normaliza .text pt-BR do Salux), ContatoEmergenciaNativo (contact[v2-0131|C] → Nome/Parentesco/Telefone), FotoNativa (Patient.photo), EnderecoNativo estendido para ler o Numero da extension houseNumber, Lat/Long da extension geolocation.
  - Manter `?? pl.X` como fallback de transição (removido na Fase C).
- Extrair os helpers UpsertIdentifier/UpsertTelecom/UpsertContato/UpsertEndereco/SetMaritalStatus para uma classe estática compartilhada (ex.: PatientMergeFhir) reutilizada por P0.3 (import) — fonte única da semântica de merge.
- Decisão §3 (bloqueante): antes de codar o normalizador de EstadoCivil, rodar `SELECT DISTINCT ds_est_civil FROM estado_civil` (read-only) e congelar o mapa.

=== R2 — Geocoder-on-save (lat/long reais) ===
Arquivo: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\PacientesService.cs
- Injetar Geo.IGeocodificadorService (já registrado em Core/DependencyInjection.cs).
- Em CadastrarAsync e AtualizarAsync: se há endereço e lat/long ausente/zero (ou endereço mudou), geocodificar e gravar via novo helper que seta a extension geolocation em address[0] (no PacienteFhirMapper/PatientMergeFhir).
- ParaDto lê lat/long da geolocation (blob fallback). Remove o 0,0 universal.

=== R3 — Paginação keyset no hub (habilita backfill) ===
- C:\Projetos GIT\SMSMarica\Automais.Fhir\src\Automais.Fhir.Core\Patients\PatientService.cs + IPatientService.cs
  - novo `ListarParaManutencaoAsync(Guid? cursorId, int count, ct)` (OrderBy Id, Where Id>cursor && !IsDeleted, Take count) devolvendo Bundle com Link[rel=next] carregando o cursor (último Id da página).
- C:\Projetos GIT\SMSMarica\Automais.Fhir\src\Automais.Fhir.Api\Controllers\PatientController.cs
  - aceitar `_count` e `_cursor` no GET (ou endpoint dedicado /fhir/Patient/$manutencao) e emitir o next link.
- C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\Fhir\IPacienteFhirClient.cs + PacienteFhirClient.cs
  - método que itera páginas seguindo o next link.

=== R4 — Backfill de promoção blob→nativo (smsmarica-side) ===
- Novo: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\Promocao\PromocaoSnapshotService.cs
  - roda com o mapper ANTIGO (pré-deploy) e exporta o golden ParaDto por paciente (tabela paciente_promocao_snapshot ou arquivo). Executado ANTES do deploy do mapper novo.
- Novo: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Pacientes\Promocao\PromocaoBlobService.cs
  - pagina todos os Patients (R3); para cada com extension urn:smsmarica:paciente-payload: aplica o merge (PatientMergeFhir) promovendo demografia do blob para nativo (idempotente: só grava ausente/divergente), grava com If-Match+retry; NÃO remove o blob.
  - compara o ParaDto NOVO ao golden; divergências → trilha de qualidade.
  - recusa iniciar se PepSincronizacaoEstadoVivo indicar import ativo (gate §6).
- Reuso da trilha: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Core\Integracoes\Pep\Falhas\RegistradorFalhasPep.cs (ou nova tabela paciente_promocao_divergencia + migration em SMSMarica.Data/Migrations).
- Fila/runner: espelhar C:\...\Integracoes\Pep\Background\ (PepSincronizacaoFila/PepSincronizacaoRunner) para um PromocaoRunner enfileirável.
- Endpoint disparo: C:\Projetos GIT\SMSMarica\SMSMarica.server\src\SMSMarica.Api\Controllers\ — novo endpoint de manutenção (ex.: em PacientesController POST /pacientes/promocao-blob ou controller admin), gate por ModuloPermissao (reusar SincronizacaoPep=27 ou novo módulo) + confirmação; enfileira o job.
- Registrar serviços/hosted em C:\...\SMSMarica.Core\DependencyInjection.cs.

=== T — Testes (xUnit; server usa Testcontainers p/ integração, unit para o mapper) ===
- Reescrever C:\Projetos GIT\SMSMarica\SMSMarica.server\tests\SMSMarica.Tests\Pacientes\PacientesServiceTests.cs sobre um fake IPacienteFhirClient.
- Novo C:\...\tests\SMSMarica.Tests\Pacientes\PacienteFhirMapperTests.cs:
  1. SHADOWING: Patient importado (email/endereço/telefone nativos, sem blob) → AplicarAtualizacao muda email → ParaDto reflete o NOVO (falha hoje).
  2. PERDA DE IDENTIFICADORES: Patient com PIS/passaporte/RNE/certidão/SGH/CEM/urn:salux:cd_paciente → editar qualquer campo → TODOS sobrevivem (falha hoje).
  3. NAME VAZIO: request com nome vazio / paciente sem blob → nunca HumanName com Text vazio.
  4. MERGE unit: UpsertIdentifier por system (não dup/não apaga); telecom por (system,use)+rank; contact por relationship; maritalStatus enum↔v3; photo.
  5. TELECOM: Salux 1 fone use=home → ParaDto principal=esse fone, residencial null (paridade de shape); multi-fone round-trip.
  6. ESTADO CIVIL: "CASADO(A)" → EstadoCivil.Casado; enum→v3→enum round-trip.
  7. ENDEREÇO: Numero round-trip via extension houseNumber.
  8. IDEMPOTÊNCIA: promover 2x = no-op na 2ª.
- Novo C:\...\tests\SMSMarica.Tests\Integracoes\Pep\SaluxImportPreservaTests.cs: UpsertCanonicoAsync (merge) preserva blob + contato smsmarica; identifiers unidos.
- Hub: C:\Projetos GIT\SMSMarica\Automais.Fhir\tests\Automais.Fhir.Tests\ — If-Match versão stale → 409; retry re-lê e sucede; paginação next-link.
- Busca: paciente criado pelo smsmarica é encontrado por telefone (integração contra hub de teste).

=== Ordem de deploy sugerida ===
1) P0.1 (hotfix identifiers) → prod.
2) P0.2 (If-Match) hub+server → prod.
3) P0.3 (import merge) → prod, com import PARADO.
4) Discovery ds_est_civil; congelar mapa.
5) R1+R2 (mapper native-first + geocoder) → deploy com dual-write; PromocaoSnapshotService (golden) roda ANTES do deploy do mapper novo.
6) R3+R4 (paginação + backfill) → rodar em treinamento/cópia; depois prod com gate (sem import ativo) + backup do fhir.patient.
7) Soak + relatório de divergências (pré-condição da Fase B/C).