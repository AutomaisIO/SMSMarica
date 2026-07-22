# Pendência — upgrade do dcm4chee para habilitar o isolamento de worklist

**Status:** planejado, sem data. Decidido em 2026-07-22: fazer *depois*, com backup do
servidor antes e bateria de testes depois.

## Por que

O isolamento da worklist por unidade **no servidor** depende do atributo
`dcmMWLWorklistLabel` no Archive AE, que **não existe na 5.32.0** em produção —
conferido no `dcm4chee-archive.schema` da tag e confirmado na prática (o `PUT` do
device devolve 204 e descarta o atributo em silêncio). Ele entra na **5.33.0**.

Enquanto não houver upgrade, quem separa é o **filtro no próprio equipamento**
(`ScheduledStationAETitle`) — ver [`../pacs.md`](../pacs.md). Isso é suficiente hoje:
só existem um MG (CDT) e um US (CMI), que a modalidade já separa. **A necessidade
real aparece quando houver dois equipamentos da mesma modalidade em unidades
diferentes** — esse é o gatilho para priorizar isto.

## Já está pronto (não refazer)

| Peça | Onde |
|---|---|
| Item MWL carimbado com WorklistLabel (0074,1202) = AE do equipamento | `Core/Worklist/ConstrutorMwlItem.cs` |
| Itens legados nomeados (estavam com `*`, curinga padrão) | feito via REST em 2026-07-22 |
| Backend no AE administrativo `WORKLIST` (sem label, enxerga tudo) | `Pacs:Dcm4chee:WorklistBaseUrl` |
| AE `WORK-CMI` criado (clone do `WORK-CDT`) | dcm4chee — hoje **sem** isolamento |
| Backup do device config | `GET /dcm4chee-arc/devices/dcm4chee-arc` (refazer no dia) |

Depois do upgrade, habilitar é só setar `dcmMWLWorklistLabel`: `WORK-CDT` →
`FDR-MAMO`, `WORK-CMI` → `US_CMI`. **Não** colocar label no `WORKLIST`: é o AE por
onde o backend cria/confirma/remove itens de todas as unidades.

## Alvo e caminho

| | |
|---|---|
| Hoje | 5.32.0 (29/04/2024) |
| **Alvo sugerido** | **5.33.1** — um único salto de schema e já entrega o objetivo |
| Alternativa | 5.34.3 (última, 24/04/2026) — exige os scripts de 5.33 **e** 5.34 |

Upgrades são **sequenciais** por segundo componente da versão: não dá para pular do
5.32 direto ao 5.34. Três migrações, nesta ordem — banco (`sql/psql/update-5.3x-psql.sql`),
LDAP (LDIFs equivalentes via `ldapmodify`) e aplicação (trocar o EAR; os módulos do
WildFly precisam ser removidos e substituídos **à mão**, já que o deploy aqui é
WildFly nativo, sem Docker). Confirmar antes: versão de Java/WildFly instalada (5.34
pede Java 17+) e se o pacote `-unsecure` que usamos existe na versão alvo.

## Dimensionamento e risco

Acervo em 2026-07-22: **1.708 estudos, 7.337 instâncias, 2.994 pacientes** — base
pequena, migração de minutos. O risco não é volume: é o ambiente (instalação manual,
LDAP guardando **toda** a configuração do PACS, servidor único sem réplica).

Antes de começar: **snapshot do droplet** (reversão em um clique), `pg_dump` do
`dcmdb`, `slapcat` do LDAP e cópia do EAR atual.

Janela realista **1–2 h**, quase toda em verificação. Downtime efetivo é o restart
(~40 s) mais a migração.

## Testes obrigatórios depois

1. C-ECHO em `PACS-CDT`, `WORK-CDT`, `WORK-CMI`.
2. C-FIND MWL pelos AEs com label: `WORK-CDT` só devolve `FDR-MAMO`; `WORK-CMI` só
   `US_CMI` — **é o teste que prova o isolamento** (hoje ambos devolvem tudo).
3. REST `/aets/WORKLIST/rs/mwlitems` continua enxergando **todos** (backend depende).
4. Ciclo do backend: autorizar um exame → item criado → status `Recebida`.
5. C-STORE de teste → conciliação promove a Realizada → item some da worklist.
6. Abrir exame no visualizador do painel (QIDO/WADO) e um laudo antigo.
7. Verificar se o **compressor lossless do dcm4chee** voltou a funcionar (está
   quebrado na 5.32, retorna 500; por isso transcodificamos no nosso proxy — ver
   `pacs.md` §10). Se voltou, dá para simplificar aquele caminho.

## Quem executa

Exige **SSH root** em `pacs.marica.automais.cloud`, credencial que está no cofre da
equipe. A validação por DICOM/REST (itens 1–7) pode ser feita de fora, a cada etapa.
