"""Prepara os atendimentos do Prime como recursos FHIR, EM ARQUIVO LOCAL. Nao grava em lugar nenhum.

Separado de proposito da carga: aqui da para ler, conferir e discutir exatamente o que iria para o
hub, antes de qualquer escrita em producao. A carga e outro ato, com OK explicito.

O que entra (decidido em 23/09/2026 - "importar quem temos 100% de certeza em relacao ao CNS"):
- **so atendimento com CNS de DV valido.** Nos 243.330 medidos: 234.485 (96,3%) tem CNS e
  **zero** tem CNS invalido. Os 8.845 sem CNS (2.102 pessoas) ficam de fora desta carga.
- **so unidade com CNES resolvido** (`prime/unidades.py`). CEREST e Odontomovel nao existem no
  nosso cadastro; os dois CEO estao marcados `inferido` e so entram com `--aceitar-inferido`.
- **so paciente que JA existe no hub.** Criar paciente e outro ato, com outra regua (ADR-0041).

Sobre CNS provisorio: 96,2% dos CNS do Prime sao serie 7/8/9. Isso NAO desqualifica - o nosso
proprio hub e 99,3% provisorio. A regua e **DV valido**, nao serie. Exigir CNS definitivo deixaria
362 de 243.330 atendimentos, que e o mesmo que nao importar.

**Fora do alcance:** procedimentos SIGTAP e exames solicitados. Os recursos `Procedure` e
`ServiceRequest` NAO existem no schema `fhir` hoje (so `encounter` e `condition`). Levar o SIGTAP
- que esta em 100% dos registros e e a parte mais rica - exige criar o recurso no Automais.Fhir.

Entrada:  os NDJSON de JANELA LARGA de `capturas/backfill/` (`*_AAAAMMDD-AAAAMMDD.ndjson`)
          + `capturas/hub/pacientes.json` (mapa CNS -> Patient.id).
          Os arquivos de UM DIA (`*_AAAAMMDD.ndjson`), que a conferencia diaria gera, ficam de
          fora de proposito: o historico ja os contem, e junta-los contava 22/09 duas vezes.
Saida:    capturas/hub/encounter.ndjson, capturas/hub/condition.ndjson, capturas/hub/_relatorio.txt

Uso:
  python preparar_hub.py                          # todas as unidades resolvidas
  python preparar_hub.py --aceitar-inferido       # inclui os dois CEO deduzidos
  python preparar_hub.py --unidade <GUID>
"""

from __future__ import annotations

import collections
import glob
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from prime.cns import classificar  # noqa: E402
from prime.unidades import resolver  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = pathlib.Path("capturas/hub")
SYS_CNES = "https://fhir.saude.gov.br/sid/cnes"
SYS_ATEND = "urn:prime:atendimento"
META_SOURCE = "https://smsmarica.saude.marica/source/prime"
SYS_CID10 = "http://hl7.org/fhir/sid/icd-10"
CLASSE_AMB = {"system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
              "code": "AMB", "display": "ambulatory"}


# Brasilia = UTC-3 FIXO (sem horario de verao desde 2019).
OFFSET_BR = "-03:00"


def instante(local_iso: str | None) -> str | None:
    """Wall-clock de Marica -> instante FHIR COM offset explicito.

    Sem isto a carga entraria 3 horas errada e ninguem veria: o `EncounterService.ParseInstant`
    usa `DateTimeStyles.AssumeUniversal`, ou seja, datetime SEM offset e lido como UTC. Um
    atendimento fechado 07:53 em Marica viraria 07:53Z = 04:53 local.

    E o mesmo bug que ja aconteceu na importacao do SISREG (agendamento de 8h aparecendo 5h,
    corrigido no commit 1744eb5) - a regra unica de fuso do repositorio existe por causa dele:
    coluna `timestamptz` guarda INSTANTE, entao a origem tem de declarar o fuso.
    """
    if not local_iso:
        return None
    return local_iso if local_iso.endswith("Z") or local_iso[-6] in "+-" else local_iso + OFFSET_BR


def sem_nulos(d: dict) -> dict:
    return {k: v for k, v in d.items() if v is not None}


def encounter_fhir(a: dict, patient_id: str, cnes: str) -> dict:
    return sem_nulos({
        "resourceType": "Encounter",
        "meta": {"source": META_SOURCE},
        "identifier": [{"system": SYS_ATEND, "value": a["chave"]}],
        "status": "finished",  # o relatorio so lista atendimento ja fechado
        "class": CLASSE_AMB,
        "type": [{"text": a["funcao"]}] if a.get("funcao") else None,
        "subject": {"reference": f"Patient/{patient_id}"},
        # `period.end` = DataRegistro (fechamento do registro, hora real em 100%); `start` = end -
        # Duracao. NAO usar DataInicio/DataFim do relatorio: vem `00:00:00` em 100% dos registros.
        "period": {"start": instante(a["inicio"]), "end": instante(a["fim"])},
        # Referencia logica por CNES: as unidades do Prime ainda nao existem como Organization no
        # hub (so 3 existem, todas do Salux). O FHIR aceita referencia por identifier, e quando a
        # Organization for criada o `serviceProvider` passa a resolver sem reescrever o Encounter.
        "serviceProvider": {"identifier": {"system": SYS_CNES, "value": cnes}},
    })


def condition_fhir(a: dict, cid: dict, patient_id: str, i: int) -> dict:
    codificado = ({"coding": [{"system": SYS_CID10, "code": cid["codigo"],
                               "display": cid["descricao"]}], "text": cid["descricao"]}
                  if cid.get("codigo") else {"text": cid["descricao"]})
    return sem_nulos({
        "resourceType": "Condition",
        "meta": {"source": META_SOURCE},
        # `#i` porque um atendimento pode ter mais de um CID, e cada Condition precisa da sua
        # identidade estavel - senao o conditional update sobrescreve sempre o mesmo registro.
        "identifier": [{"system": SYS_ATEND, "value": f"{a['chave']}#{i}"}],
        "subject": {"reference": f"Patient/{patient_id}"},
        # O Encounter e referenciado pelo identifier do atendimento; quem carregar resolve o id
        # real depois do PUT condicional do Encounter (que devolve o id).
        "encounter": {"identifier": {"system": SYS_ATEND, "value": a["chave"]}},
        "code": codificado,
    })


def main(argv: list[str]) -> int:
    unidade = argv[argv.index("--unidade") + 1] if "--unidade" in argv else None
    aceitar_inferido = "--aceitar-inferido" in argv

    mapa_path = SAIDA / "pacientes.json"
    if not mapa_path.exists():
        raise SystemExit(f"falta {mapa_path} (mapa CNS -> Patient.id). Rode a resolucao primeiro.")
    mapa: dict[str, str] = json.loads(mapa_path.read_text(encoding="utf-8"))

    ats = []
    padrao = (argv[argv.index("--padrao") + 1] if "--padrao" in argv
              else "capturas/backfill/*_????????-????????.ndjson")
    vistos: set[str] = set()
    duplicados = 0
    for f in sorted(glob.glob(padrao)):
        if unidade and unidade not in f:
            continue
        with open(f, encoding="utf-8") as fh:
            for l in fh:
                if not l.strip():
                    continue
                a = json.loads(l)
                # Rede de seguranca: se dois arquivos cobrirem o mesmo periodo, o mesmo atendimento
                # apareceria duas vezes. A chave e estavel entre importacoes, entao basta ela.
                if a["chave"] in vistos:
                    duplicados += 1
                    continue
                vistos.add(a["chave"])
                ats.append(a)
    if duplicados:
        print(f"(descartados {duplicados:,} atendimentos repetidos entre arquivos)")

    motivo = collections.Counter()
    encs, conds = [], []
    por_unidade = collections.Counter()
    for a in ats:
        classe = classificar(a["paciente"]["cns"])
        if classe in ("ausente", "invalido"):
            motivo[f"CNS {classe}"] += 1
            continue
        cnes, conf = resolver(a["unidade_id"])
        if cnes is None:
            motivo[f"unidade fora do cadastro: {a['unidade'][:28]}"] += 1
            continue
        if conf == "inferido" and not aceitar_inferido:
            motivo[f"unidade inferida (use --aceitar-inferido): {a['unidade'][:22]}"] += 1
            continue
        pid = mapa.get((a["paciente"]["cns"] or "").strip())
        if not pid:
            motivo["paciente nao existe no hub (criar e outro ato)"] += 1
            continue
        encs.append(encounter_fhir(a, pid, cnes))
        for i, cid in enumerate(a["cids"]):
            conds.append(condition_fhir(a, cid, pid, i))
        por_unidade[a["unidade"]] += 1

    SAIDA.mkdir(parents=True, exist_ok=True)
    with (SAIDA / "encounter.ndjson").open("w", encoding="utf-8") as f:
        for e in encs:
            f.write(json.dumps(e, ensure_ascii=False) + "\n")
    with (SAIDA / "condition.ndjson").open("w", encoding="utf-8") as f:
        for c in conds:
            f.write(json.dumps(c, ensure_ascii=False) + "\n")

    linhas = [f"lidos:              {len(ats):>9,} atendimentos",
              f"Encounter prontos:  {len(encs):>9,}",
              f"Condition prontos:  {len(conds):>9,}",
              f"pacientes distintos:{len({e['subject']['reference'] for e in encs}):>9,}", "",
              "ficaram de fora:"]
    linhas += [f"   {n:>9,}  {m}" for m, n in motivo.most_common()]
    linhas += ["", "por unidade:"]
    linhas += [f"   {n:>9,}  {u}" for u, n in por_unidade.most_common()]
    texto = "\n".join(linhas)
    print(texto)
    (SAIDA / "_relatorio.txt").write_text(texto, encoding="utf-8")
    print(f"\narquivos em {SAIDA.resolve()} — NADA foi gravado no hub.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
