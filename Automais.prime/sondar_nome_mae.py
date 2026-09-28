"""Busca paciente no Prime e traz NOME DA MAE, CPF e o CODIGO (pacienteId). SOMENTE LEITURA.

Serve a duas perguntas de uma vez:

1. **Desempatar homonimo.** O relatorio *Pacientes Atendidos* nao traz nome de mae, mas 99% dos
   pacientes do nosso hub tem (`contact` + `relationship`). A grade de busca do Prime tem a coluna
   "Nome da Mae" — entao o desempate existe, so precisa ser buscado.

2. **A chave de origem.** Medido em 23/09/2026: Salux e Klinikos tem chave da origem em 100% dos
   pacientes; **SISREG/implantacao tem em 0%**. Um Patient vindo do Prime pelo relatorio ficaria
   como o do SISREG — sem chave, dependente de adivinhar por CPF/CNS a cada reencontro. A grade
   de busca devolve o `Codigo`, que e o `pacienteId` GUID do Prime: e essa a chave
   (`urn:prime:paciente`).

De quebra a grade traz **CPF**, que o relatorio nao tem em coluna nenhuma.

**Armadilha do CadWeb:** quando a pessoa so existe no CadWeb e nao no Prime, a grade devolve a
linha com `Codigo = 00000000-0000-0000-0000-000000000000` e o rotulo "Paciente CadWeb". Isso NAO e
chave — e o eco do CadWeb. Quem gravar esse GUID zerado como identifier funde todo mundo que ainda
nao foi cadastrado no Prime num paciente so.

Uso:
  python sondar_nome_mae.py --ambiguos          # os homonimos de verdade (lista derivada local)
  python sondar_nome_mae.py --cns 7001234...    # um caso avulso
  python sondar_nome_mae.py --arquivo lista.txt # um CNS por linha
"""

from __future__ import annotations

import json
import pathlib
import re
import sys
import time
import unicodedata

from prime.client import APP, LIBERADOS, PrimeSession, action_do_form, campos_todos, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

URL = f"{APP}/Agendamento/AgendaRecepcao.aspx"
SAIDA = pathlib.Path("capturas/hub")
GUID_ZERO = "00000000-0000-0000-0000-000000000000"


def norm(s: str | None) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", (s or "").upper())
                   if unicodedata.category(c) != "Mn").strip()


def curto(n: str) -> str:
    return n.rsplit("$", 1)[-1]


def nome_longo(html: str, sufixo: str) -> str | None:
    for m in re.finditer(r'name="([^"]+)"', html):
        if curto(m.group(1)) == sufixo:
            return m.group(1)
    return None


def ler_grade(html: str) -> list[dict]:
    """Linhas da `gridBuscaPaciente` mapeadas pelo CABECALHO (nao por posicao fixa: a grade tem
    colunas escondidas e mexer nelas quebraria um parser por indice)."""
    d = sopa(html)
    grade = d.find(id=re.compile(r"gridBuscaPaciente$"))
    if not grade:
        return []
    cabecalho = [th.get_text(strip=True) for th in grade.find_all("th")]
    saida = []
    for tr in grade.find_all("tr"):
        tds = tr.find_all("td")
        if len(tds) < len(cabecalho) - 2:
            continue
        valores = [td.get_text(" ", strip=True).replace("\xa0", "").strip() for td in tds]
        linha = dict(zip(cabecalho, valores))
        if not (linha.get("Nome") or "").strip():
            continue
        saida.append(linha)
    return saida


def buscar(s: PrimeSession, termo: str) -> list[dict]:
    """Uma pesquisa pela lupa da recepcao. `termo` = CPF (11) ou CNS (15).

    **Cada busca refaz o GET da tela, de proposito.** Tentei reaproveitar o ViewState da busca
    anterior para economizar o GET de 335 KB: o POST volta 200, sem excecao, e com a grade
    VAZIA — 80 pacientes "pesquisados" e 3 gravados antes de eu perceber. Postback de WebForms
    nao e idempotente entre telas: a resposta de uma busca nao e um formulario de busca novo.
    O custo do GET e o preco de a varredura nao mentir.
    """
    html = s.get(URL).text
    originais = campos_todos(html)
    dados = dict(originais)
    n_txt = nome_longo(html, "txtPaciente")
    n_rbl = nome_longo(html, "rblPesquisarPor")
    if not n_txt:
        raise RuntimeError("campo de busca nao encontrado (tela mudou?)")
    dados[n_txt] = termo
    if n_rbl:
        dados[n_rbl] = "CPF" if len(termo) == 11 else "CNS"
    dados["__EVENTTARGET"] = ""
    dados["__EVENTARGUMENT"] = ""
    lupa = nome_longo(html, "imbConsulta") or "imbConsulta"
    dados[f"{lupa}.x"] = "10"
    dados[f"{lupa}.y"] = "10"
    # Libera SO o que devolvemos identico ao que a tela entregou (mesma regra do probe_agenda_busca).
    LIBERADOS.update(k for k, v in dados.items() if k in originais and originais[k] == v)
    r = s.post(action_do_form(html, URL), dados)
    return ler_grade(r.text)


def alvos_ambiguos() -> list[dict]:
    """Os homonimos de verdade: mesmo nome+nascimento no hub, com nomes de mae DIFERENTES."""
    import collections
    import glob

    def mae(d):
        for ct in (d.get("contact") or []):
            for r in (ct.get("relationship") or []):
                if "MAE" in norm(r.get("text")) or "MTH" in {c.get("code") for c in (r.get("coding") or [])}:
                    return norm((ct.get("name") or {}).get("text"))
        return None

    mapa = json.loads((SAIDA / "pacientes.json").read_text(encoding="utf-8"))
    novos = {}
    for f in glob.glob("capturas/backfill/*_????????-????????.ndjson"):
        with open(f, encoding="utf-8") as fh:
            for l in fh:
                p = json.loads(l)["paciente"]
                c = (p["cns"] or "").strip()
                if c and c not in mapa:
                    novos.setdefault(c, p)
    hub = [json.loads(l) for l in (SAIDA / "candidatos_hub.ndjson").open(encoding="utf-8")]
    por = collections.defaultdict(list)
    for h in hub:
        por[(norm(h["nome"]), h["nascimento"])].append(h)

    alvos = []
    for cns, p in novos.items():
        cands = por.get((norm(p["nome"]), p["nascimento"]), [])
        if len(cands) < 2:
            continue
        maes = [mae(h["content"]) for h in cands]
        if not all(maes) or len(set(maes)) == 1:
            continue  # mae igual = duplicata NOSSA, nao e homonimo: fora deste lote
        alvos.append({"cns": cns, "nome": p["nome"], "nascimento": p["nascimento"],
                      "candidatos": [{"id": h["id"], "cpf": h["cpf"], "cns": h["cns"],
                                      "mae": m} for h, m in zip(cands, maes)]})
    return alvos


def main(argv: list[str]) -> int:
    SAIDA.mkdir(parents=True, exist_ok=True)
    if "--cns" in argv:
        alvos = [{"cns": argv[argv.index("--cns") + 1], "nome": "?", "nascimento": "?", "candidatos": []}]
    elif "--arquivo" in argv:
        linhas = pathlib.Path(argv[argv.index("--arquivo") + 1]).read_text(encoding="utf-8").split()
        alvos = [{"cns": x, "nome": "?", "nascimento": "?", "candidatos": []} for x in linhas if x.strip()]
    else:
        alvos = alvos_ambiguos()
    print(f"alvos: {len(alvos)}")

    # Retomada: uma varredura de milhares de buscas cai no meio (sessao unica, rede) e nao pode
    # recomecar do zero. O que ja foi gravado e relido e pulado.
    saida_ndjson = SAIDA / "prime_busca.ndjson"
    ja_feitos: set[str] = set()
    if "--recomecar" not in argv and saida_ndjson.exists():
        with saida_ndjson.open(encoding="utf-8") as f:
            for l in f:
                try:
                    ja_feitos.add(json.loads(l)["cns_prime"])
                except Exception:  # noqa: BLE001 — linha truncada de uma queda anterior
                    pass
        if ja_feitos:
            print(f"retomando: {len(ja_feitos):,} ja gravados serao pulados")

    achados, resolvidos, sem_linha, cadweb, indefinidos = [], 0, 0, 0, 0
    t0 = time.perf_counter()
    # SEMPRE "a": abrir em "w" ja apagou 5 registros ja obtidos. So `--recomecar` zera, e ai
    # explicitamente, apagando o arquivo antes.
    if "--recomecar" in argv and saida_ndjson.exists():
        saida_ndjson.unlink()
    arq = saida_ndjson.open("a", encoding="utf-8")
    with PrimeSession() as s:
        s.entrar()
        for i, a in enumerate(alvos, 1):
            if a["cns"] in ja_feitos:
                continue
            try:
                linhas = buscar(s, a["cns"])
            except Exception as e:  # noqa: BLE001 — um alvo ruim nao derruba a varredura
                print(f"   FALHA {a['cns'][:6]}...: {str(e)[:70]}")
                continue
            if not linhas:
                sem_linha += 1
                # Disjuntor: "nenhuma linha" e um resultado legitimo para UM paciente, mas 20
                # seguidos significam que a busca parou de funcionar (sessao, tela, parser) —
                # e ai a varredura esta produzindo vazio com cara de resposta.
                if sem_linha >= 20 and not achados:
                    print("\nABORTADO: 20 buscas seguidas sem nenhuma linha — a busca nao esta "
                          "funcionando. Conferir sessao/tela antes de insistir.")
                    break
                continue
            # A grade devolve o que casou com o termo, mas conferir e barato e evita gravar a
            # pessoa errada se a tela algum dia devolver resultado residual da busca anterior.
            r = next((x for x in linhas
                      if re.sub(r"\D", "", x.get("CNS") or "") == a["cns"]), None)
            if r is None:
                sem_linha += 1
                continue
            codigo = (r.get("Código") or r.get("Codigo") or "").strip()
            registro = {
                "cns_prime": a["cns"], "nome": r.get("Nome"),
                "mae_prime": r.get("Nome da Mãe") or r.get("Nome da Mae"),
                "sexo": r.get("Sexo"), "nascimento": r.get("Nascimento"),
                "cpf_prime": re.sub(r"\D", "", r.get("CPF") or ""),
                "cns_grade": re.sub(r"\D", "", r.get("CNS") or ""),
                # `Codigo` zerado = eco do CadWeb, NAO e chave do Prime (ver docstring)
                "paciente_id_prime": None if codigo in ("", GUID_ZERO) else codigo,
                "so_no_cadweb": codigo == GUID_ZERO,
            }
            if registro["so_no_cadweb"]:
                cadweb += 1
            # desempate pelo nome da mae
            if a["candidatos"]:
                mp = norm(registro["mae_prime"])
                bate = [c for c in a["candidatos"] if c["mae"] and mp and c["mae"] == mp]
                registro["resolvido_para"] = bate[0]["id"] if len(bate) == 1 else None
                if len(bate) == 1:
                    resolvidos += 1
                else:
                    indefinidos += 1
            achados.append(registro)
            # Grava JA, linha a linha, com flush: uma varredura de milhares de buscas cai no meio,
            # e o que estiver so em memoria se perde. Nao ha reescrita do arquivo no fim — havia,
            # e era ela que apagava o resultado das rodadas anteriores a cada execucao.
            arq.write(json.dumps(registro, ensure_ascii=False) + "\n")
            arq.flush()
            if i % 50 == 0:
                resto = (len(alvos) - i) * (time.perf_counter() - t0) / max(len(achados), 1)
                print(f"   {i}/{len(alvos)}  ({time.perf_counter()-t0:.0f}s, "
                      f"faltam ~{resto/60:.0f} min)")
            time.sleep(0.2)
    arq.close()

    com_id = sum(1 for r in achados if r["paciente_id_prime"])
    com_cpf = sum(1 for r in achados if r["cpf_prime"])
    com_mae = sum(1 for r in achados if (r["mae_prime"] or "").strip())
    print(f"\nlinhas obtidas:        {len(achados)} de {len(alvos)}   (sem resultado: {sem_linha})")
    print(f"com pacienteId Prime:  {com_id}   so no CadWeb (GUID zerado): {cadweb}")
    print(f"com CPF na grade:      {com_cpf}   com nome da mae: {com_mae}")
    if alvos and alvos[0]["candidatos"]:
        print(f"\nDESEMPATE: resolvidos {resolvidos}   indefinidos {indefinidos}")
    print(f"salvo em {SAIDA / 'prime_busca.ndjson'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
