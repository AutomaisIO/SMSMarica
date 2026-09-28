"""Confere, na Receita, quem e o dono do CPF de cada grupo duplicado. SOMENTE LEITURA.

Usa o proxy que o SMSMais ja tem (`GET /integracoes/cpf?cpf=&dataNascimento=`, motor Hub do
Desenvolvedor), que valida **CPF contra data de nascimento** e devolve o **nome oficial**, a
situacao cadastral e o sexo.

### Por que isto resolve o que nenhuma heuristica resolveu
Duas perguntas que travaram a fusao, e que a Receita responde direto:

1. **Quem e o dono do CPF.** No grupo `23290075745` ha duas criancas nascidas em 23/02/2023, mesma
   mae, nomes `WANDERLEY DA SILVA` e `LAVINIA DOS SANTOS PIMENTEL` — gemeos, com o CPF de um na
   ficha do outro. Nenhum criterio meu separou isso; o nome que a Receita devolver separa.
2. **Qual grafia e a certa.** Nos pares com erro de digitacao (`ANTONIO`/`ANTTONIO`,
   `PEREIRA`/`PERREIRA`), o nome da Receita diz qual ficha esta correta — e portanto qual deveria
   sobreviver, o que e melhor criterio que a minha pontuacao de completude.

### Custo
Cada consulta e **paga** (creditos do Hub do Desenvolvedor) e manda CPF + data de nascimento a um
terceiro. Por isso o padrao e o conjunto pequeno (`--nivel1`, 6 CPF) e ampliar exige pedir: os 510
grupos T1 seriam 510 consultas.

### Autenticacao
O token do SMSMais vem de `SMSMAIS_TOKEN` (variavel de ambiente) ou `--token`. Nunca e impresso.

### Dois caminhos
- **pelo proxy do SMSMais** (preferido): precisa do JWT do painel em `SMSMAIS_TOKEN`. Passa pela
  contabilizacao, pelo fallback entre motores e pela trilha.
- **`--direto`**: fala com o Hub do Desenvolvedor sem intermediario, com o token do Hub em
  `HUB_TOKEN`. Serve quando nao se tem o JWT a mao. Perde contabilizacao/fallback/trilha — tudo
  bem para uma conferencia pontual, nao para rotina.

O token **nunca** vai em argumento de linha de comando no modo direto: so por variavel de
ambiente, para nao ficar no historico do shell.

Uso:
  HUB_TOKEN="..." python conferir_cpf_receita.py --nivel1 --direto
  SMSMAIS_TOKEN="..." python conferir_cpf_receita.py --nivel1
  python conferir_cpf_receita.py --cpf 23290075745 --direto
"""

from __future__ import annotations

import collections
import csv
import difflib
import json
import os
import pathlib
import sys
import time
import unicodedata

import httpx

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = pathlib.Path("capturas/hub")


def norm(s: str | None) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", (s or "").upper())
                   if unicodedata.category(c) != "Mn").strip()


def prox(a: str, b: str) -> float:
    x, y = norm(a), norm(b)
    if not x or not y:
        return 0.0
    return difflib.SequenceMatcher(None, x, y).ratio()


def dv_cpf_ok(cpf: str | None) -> bool:
    c = "".join(ch for ch in (cpf or "") if ch.isdigit())
    if len(c) != 11 or c == c[0] * 11:
        return False
    for corte in (9, 10):
        soma = sum(int(c[i]) * (corte + 1 - i) for i in range(corte))
        d = (soma * 10) % 11
        if d == 10:
            d = 0
        if d != int(c[corte]):
            return False
    return True


def gravar_resultados(resultados: list[dict]) -> None:
    """Grava o acumulado a CADA resposta, FUNDINDO com o que ja existia no arquivo.

    Duas razoes, e as duas custam dinheiro se ignoradas:
    - gravar so no fim significa que uma queda na consulta 400 joga 400 consultas PAGAS no lixo;
    - sobrescrever sem fundir apagaria os vereditos de rodadas anteriores — foi o defeito que
      truncou o `prime_busca.ndjson` duas vezes (APRENDIZADOS §27).
    Escreve em arquivo temporario e troca, para uma queda no meio da escrita nao deixar JSON
    partido — que na proxima execucao seria lido como "nada conferido".
    """
    SAIDA.mkdir(parents=True, exist_ok=True)
    destino = SAIDA / "conferencia_receita.json"
    acumulado = {r["cpf"]: r for r in (veredito_receita().values())}
    for r in resultados:
        acumulado[r["cpf"]] = r
    tmp = destino.with_suffix(".json.tmp")
    tmp.write_text(json.dumps(list(acumulado.values()), ensure_ascii=False, indent=1),
                   encoding="utf-8")
    tmp.replace(destino)


def veredito_receita() -> dict[str, dict]:
    """CPF -> conferencia ja feita. Existe para NAO reconsultar: cada chamada ao Hub e paga, e
    uma varredura interrompida no meio seria refeita do zero sem isto."""
    arq = SAIDA / "conferencia_receita.json"
    if not arq.exists():
        return {}
    try:
        return {r["cpf"]: r for r in json.loads(arq.read_text(encoding="utf-8")) if r.get("receita")}
    except Exception:  # noqa: BLE001 — arquivo truncado por queda anterior nao pode travar tudo
        return {}


def grupos(argv: list[str]) -> list[tuple[str, list[dict]]]:
    g: dict[tuple, list[dict]] = collections.defaultdict(list)
    with (SAIDA / "duplicatas.csv").open(encoding="utf-8-sig") as f:
        for l in csv.DictReader(f):
            g[(l["tier"], l["chave"])].append(l)

    if "--cpf" in argv:
        alvo = argv[argv.index("--cpf") + 1]
        return [(k[1], ms) for k, ms in g.items() if k[1] == alvo]

    # Ja conferidos nao se consultam de novo: cada chamada e PAGA.
    feitos = {r["cpf"] for r in veredito_receita().values()} if "--refazer" not in argv else set()

    saida = []
    for (tier, chave), ms in g.items():
        if tier != "T1-CPF" or not dv_cpf_ok(chave) or chave in feitos:
            continue
        if "--nivel1" in argv:
            # o mesmo recorte do `fundir_seguros.py`, mas SEM o filtro de nome: aqui o objetivo
            # e justamente olhar os que o nome reprovou (o par de gemeos entra).
            if len(ms) != 2:
                continue
            absorvidos = [m for m in ms if m["sobrevivente"] != "True"]
            if len(absorvidos) != 1 or int(absorvidos[0]["referencias"]) != 0:
                continue
        elif "--pendentes" in argv:
            # Exatamente o que o `fundir_com_repontamento.py` aceitaria se tivesse o veredito:
            # par simples, mae nao divergente, absorvido dentro do teto de linhas.
            # O filtro de NOME fica de fora de proposito — e sobre esses que a Receita decide,
            # e foi um deles (gemeos) que so ela barrou.
            teto = int(argv[argv.index("--max-linhas") + 1]) if "--max-linhas" in argv else 10
            if len(ms) != 2 or any(m["maes_divergem"] == "True" for m in ms):
                continue
            absorvidos = [m for m in ms if m["sobrevivente"] != "True"]
            if len(absorvidos) != 1 or int(absorvidos[0]["referencias"]) > teto:
                continue
        saida.append((chave, ms))
    return saida


HUB_URL = "https://ws.hubdodesenvolvedor.com.br/v2/"


def consultar_direto(http: httpx.Client, cpf: str, nasc_iso: str, token: str) -> tuple[int, dict]:
    """Chama o Hub do Desenvolvedor direto, do mesmo jeito que o `HubDoDesenvolvedorMotorCpf`.

    **Preferir o proxy do SMSMais quando houver JWT.** O direto pula a contabilizacao, o fallback
    entre motores e a trilha — aceitavel para uma conferencia pontual de 11 CPF, nao para rotina.

    O `status:false` do Hub NAO e sempre negativa: sem saldo, instabilidade e token invalido chegam
    do mesmo jeito. So e negativa de verdade quando a mensagem diz que os DADOS nao conferem.
    """
    dia, mes, ano = nasc_iso[8:10], nasc_iso[5:7], nasc_iso[0:4]
    r = http.get(f"{HUB_URL}cpf/", params={"cpf": cpf, "data": f"{dia}/{mes}/{ano}", "token": token})
    if r.status_code != 200:
        return r.status_code, {}
    p = r.json()
    if not p.get("status") or not p.get("result"):
        motivo = f"{p.get('return') or ''} {p.get('message') or ''}".strip()
        return 409, {"_motivo": motivo or "status=false sem mensagem"}
    res = p["result"]
    return 200, {
        "nome": res.get("nome_da_pf") or "",
        "sexo": res.get("genero") or res.get("sexo"),
        "situacaoCadastral": res.get("situacao_cadastral"),
    }


def main(argv: list[str]) -> int:
    direto = "--direto" in argv
    api = argv[argv.index("--api") + 1] if "--api" in argv else "https://api.smsmarica.online"
    if direto:
        token = os.environ.get("HUB_TOKEN", "")
        if not token:
            raise SystemExit(
                "falta o token do Hub: defina HUB_TOKEN no ambiente.\n"
                "  bash:       HUB_TOKEN='...' python conferir_cpf_receita.py --nivel1 --direto\n"
                "  powershell: $env:HUB_TOKEN='...'; python conferir_cpf_receita.py --nivel1 --direto")
    else:
        token = (argv[argv.index("--token") + 1] if "--token" in argv
                 else os.environ.get("SMSMAIS_TOKEN", ""))
        if not token:
            raise SystemExit("falta o token: export SMSMAIS_TOKEN=... (ou --token ...)")

    alvos = grupos(argv)
    # Teto por execucao: o condutor da madrugada chama em blocos para que, se o Hub limitar ou a
    # maquina matar o processo, se perca pouco — e o que ja veio esta gravado e nao sera reconsultado.
    if "--max-consultas" in argv:
        teto = int(argv[argv.index("--max-consultas") + 1])
        if len(alvos) > teto:
            print(f"(limitando a {teto} consultas nesta execucao; restam {len(alvos) - teto})")
            alvos = alvos[:teto]
    print(f"grupos a conferir na Receita: {len(alvos)}   (1 consulta paga cada)\n")

    resultados = []
    cliente = (httpx.Client(timeout=60.0) if direto
               else httpx.Client(base_url=api, timeout=60.0,
                                 headers={"Authorization": f"Bearer {token}"}))
    # Fila em vez de `for` direto: um alvo recuado por limite de taxa volta para o fim e e
    # tentado de novo, em vez de ser perdido.
    fila = collections.deque(alvos)
    pendentes: list = []
    tentativas_403 = 0

    with cliente as http:
        while fila or pendentes:
            if not fila and pendentes:
                fila.extend(pendentes)
                pendentes = []
            cpf, ms = fila.popleft()
            nasc = ms[0]["nascimento"]  # as fichas do grupo compartilham a data
            try:
                if direto:
                    codigo, corpo = consultar_direto(http, cpf, nasc, token)
                    r = httpx.Response(codigo, json=corpo)
                else:
                    r = http.get("/integracoes/cpf", params={"cpf": cpf, "dataNascimento": nasc})
            except Exception as e:  # noqa: BLE001
                print(f"   {cpf}: falha de rede ({type(e).__name__})")
                continue
            if r.status_code in (401, 403):
                # 401/403 tem DOIS significados aqui, e tratá-los igual custou uma varredura:
                #  - na PRIMEIRA chamada = credencial ruim, e insistir gastaria N chamadas para
                #    colher N vezes a mesma recusa (aconteceu: o placeholder "<seu token>" foi
                #    colado literalmente e o script tentou 11 vezes);
                #  - DEPOIS de sucessos = limite de taxa/cota do Hub, e aí a resposta certa é
                #    recuar e tentar de novo (aconteceu: 403 apareceu na consulta 274, depois de
                #    273 respostas boas, e o abort jogou fora a varredura inteira — ver §37).
                if not resultados:
                    raise SystemExit(
                        f"HTTP {r.status_code} na PRIMEIRA chamada: credencial invalida ou ausente "
                        f"({'HUB_TOKEN' if direto else 'SMSMAIS_TOKEN'}).")
                espera = min(60, 5 * (2 ** tentativas_403))
                tentativas_403 += 1
                if tentativas_403 > 5:
                    print(f"   {cpf}: HTTP {r.status_code} apos {len(resultados)} respostas e "
                          f"5 recuos — parando. O acumulado esta salvo; retomar depois nao "
                          f"reconsulta o que ja veio.")
                    break
                print(f"   HTTP {r.status_code} (limite do Hub?) — recuando {espera}s "
                      f"[tentativa {tentativas_403}/5, {len(resultados)} ja conferidos]")
                time.sleep(espera)
                pendentes.append((cpf, ms))  # nao perde o alvo: volta para o fim da fila
                continue
            if r.status_code != 200:
                # 409/400 costuma ser "CPF nao validado pela Receita" — o par de datas nao bate
                print(f"   {cpf} ({nasc}): HTTP {r.status_code} — {r.text[:120]}")
                resultados.append({"cpf": cpf, "nascimento": nasc, "erro": r.status_code})
                continue
            d = r.json()
            oficial = d.get("nome") or d.get("Nome") or ""
            sexo = d.get("sexo") or d.get("Sexo")
            situacao = d.get("situacaoCadastral") or d.get("SituacaoCadastral")

            print(f"   CPF {cpf}  ({nasc})")
            print(f"      RECEITA: {oficial[:46]:<46} sexo={sexo or '-'}  situacao={situacao or '-'}")
            melhor, placar = None, 0.0
            for m in ms:
                s = prox(oficial, m["nome"])
                marca = ""
                if s >= 0.85:
                    marca = "  <== E ESTA"
                elif s < 0.60:
                    marca = "  <== NAO e esta pessoa"
                print(f"      ficha:   {m['nome'][:46]:<46} sim={s:.2f}{marca}")
                if s > placar:
                    melhor, placar = m, s
            veredito = ("fundir, sobrevivente = a que casa" if placar >= 0.85
                        else "NAO FUNDIR — nenhuma ficha casa com o nome da Receita")
            if placar >= 0.85 and sum(1 for m in ms if prox(oficial, m["nome"]) >= 0.85) == 1 \
                    and len(ms) > 1 and any(prox(oficial, m["nome"]) < 0.60 for m in ms):
                veredito = ("NAO FUNDIR — so UMA ficha e o dono do CPF; a outra tem CPF de "
                            "terceiro (possivel gemeo)")
            print(f"      -> {veredito}\n")
            resultados.append({"cpf": cpf, "nascimento": nasc, "receita": oficial,
                               "sexo": sexo, "situacao": situacao,
                               "melhor_id": melhor["id"] if melhor else None,
                               "similaridade": round(placar, 2), "veredito": veredito})
            gravar_resultados(resultados)  # a cada resposta: consulta paga nao se perde por queda
            tentativas_403 = 0  # resposta boa zera o recuo: o limite do Hub e transitorio
            time.sleep(0.3)

    gravar_resultados(resultados)
    print(f"salvo em {SAIDA / 'conferencia_receita.json'} — nada foi alterado.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
