"""Materializa TODA a agenda de uma unidade executante no SISREG (cons_agendas).

O SISREG exige 3 filtros obrigatorios (unidade -> profissional -> procedimento),
entao "toda a agenda" = varredura do produto cartesiano:

    para cada profissional da unidade (AJAX PROFISSIONAIS_POR_UPS)
      para cada procedimento do profissional (AJAX PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS)
        para cada pagina do resultado (etapa=ListaConsulta)

Dedup por codigo de solicitacao (procedimentos "GRUPO - X" repetem os individuais).

SOMENTE LEITURA: usa apenas etapa=ListaConsulta. Nunca Confirma/Falta.

Uso:
    python extrair_agenda_unidade.py DD/MM/AAAA DD/MM/AAAA [CNES] [--limite-prof N]

Saida (em capturas/, gitignored — contem PII):
    agenda_<cnes>_<ini>_<fim>.jsonl   (1 agendamento por linha, incremental)
    agenda_<cnes>_<ini>_<fim>.csv     (consolidado)
"""
from __future__ import annotations

import csv
import json
import pathlib
import re
import sys
import time
import xml.etree.ElementTree as ET

from bs4 import BeautifulSoup

# console do Windows e cp1252 nao imprimem 'ç'/'ª' vindos das entidades HTML
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except AttributeError:  # pragma: no cover
    pass

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
CAP = BASE / "capturas"
SESSAO = CAP / ".sessao.json"
CNES_PADRAO = "3132358"  # CDT DR ALBERTO LUIS MACHADO BORGES

MARCAS_SESSAO_MORTA = (
    "sisreg_erro",
    "erro ao carregar a sessao",
    "logon em outra",
    "foi finalizada",
)

# Anti-bot: apos ~700 requisicoes o SISREG passa a redirecionar TODAS as telas
# para /cgi-bin/recaptcha. Nao e sessao caida — relogar NAO resolve; so um
# humano resolvendo o captcha no navegador (mesma origem) libera de novo.
MARCAS_CAPTCHA = ("recaptcha", "g-recaptcha", "diferencia&ccedil;&atilde;o entre computadores")

# Pausa entre requisicoes, para nao disparar o anti-bot tao cedo.
PAUSA_SEGUNDOS = 0.35


class CaptchaExigido(RuntimeError):
    """O SISREG passou a exigir captcha — varredura precisa parar."""

CAMPOS = [
    "co_solicitacao", "cns", "paciente", "nascimento", "idade", "origem", "telefones",
    "unidade_solicitante", "cnes_solicitante", "vaga_solicitada", "vaga_consumida",
    "cid10", "data", "dia_semana", "hora", "situacao", "procedimentos",
    "prof_cpf", "prof_nome", "pa_codigo", "pa_nome",
]


# ---------------------------------------------------------------- credencial
def credencial(indice: int = 0) -> tuple[str, str]:
    """Le o .env manualmente: ele tem MAIS DE UM par usuario/senha (dotenv pegaria o ultimo)."""
    users: list[str] = []
    senhas: list[str] = []
    for linha in (BASE / ".env").read_text(encoding="utf-8").splitlines():
        linha = linha.strip()
        if linha.startswith("#") or "=" not in linha:
            continue
        chave, valor = linha.split("=", 1)
        if chave.strip() == "SISREG_USUARIO":
            users.append(valor.strip())
        elif chave.strip() == "SISREG_SENHA":
            senhas.append(valor.strip())
    if len(users) <= indice or len(senhas) <= indice:
        raise SystemExit("credencial nao encontrada no .env")
    return users[indice], senhas[indice]


def sessao_morta(html: str) -> bool:
    baixo = html.lower()
    return any(m in baixo for m in MARCAS_SESSAO_MORTA)


def tem_captcha(html: str) -> bool:
    baixo = html.lower()
    return any(m in baixo for m in MARCAS_CAPTCHA)


def checar_captcha(html: str, onde: str) -> None:
    if tem_captcha(html):
        raise CaptchaExigido(
            f"SISREG exigiu CAPTCHA em {onde}. Relogar nao resolve — um humano precisa "
            f"abrir o SISREG no navegador com esse operador e resolver o reCAPTCHA. "
            f"Depois disso, rode de novo (e considere aumentar PAUSA_SEGUNDOS)."
        )


def conectar(cli: SisregClient, usuario: str, senha: str) -> str:
    """Reaproveita a sessao salva; so reloga se ela estiver morta.

    Nao confia so no esta_logado() do client: a pagina de sessao derrubada volta
    HTTP 200 com um redirect JS para sisreg_erro (falso positivo).
    """
    if cli.carregar_sessao(SESSAO):
        teste = cli.get("/cgi-bin/cons_agendas").text
        if not sessao_morta(teste) and "cons_agendas" in teste:
            return "reaproveitada"
    cli.login(usuario, senha)
    cli.salvar_sessao(SESSAO)
    return "novo_login"


def texto(resp) -> str:
    """Decodifica a resposta do SISREG.

    O XML do sisreg_ajax e UTF-8 de verdade (acentos nos nomes de procedimento);
    o HTML das telas e ASCII puro com entidades (&ccedil;), entao UTF-8 tambem
    serve. Latin-1 fica como rede de seguranca (nunca falha).
    """
    try:
        return resp.content.decode("utf-8")
    except UnicodeDecodeError:
        return resp.content.decode("iso-8859-1", errors="replace")


# ------------------------------------------------------------------- AJAX
def _rows(xml: str) -> list[tuple[str, str]]:
    try:
        root = ET.fromstring(xml.strip() or "<ROOT/>")
    except ET.ParseError:
        return []
    saida = []
    for row in root.iter("ROW"):
        codigo = (row.get("codigo") or "").strip()
        nome = (row.text or "").strip()
        if codigo:
            saida.append((codigo, nome))
    return saida


def profissionais(cli, cnes: str) -> list[tuple[str, str]]:
    time.sleep(PAUSA_SEGUNDOS)
    r = cli.get("/cgi-bin/sisreg_ajax", params={"BUSCA": "PROFISSIONAIS_POR_UPS", "AJAX_UPS": cnes})
    html = texto(r)
    checar_captcha(html, "AJAX profissionais")
    return _rows(html)


def procedimentos(cli, cnes: str, cpf: str) -> list[tuple[str, str]]:
    time.sleep(PAUSA_SEGUNDOS)
    r = cli.get(
        "/cgi-bin/sisreg_ajax",
        params={
            "BUSCA": "PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS",
            "AJAX_CPF": cpf,
            "AJAX_UPS": cnes,
        },
    )
    html = texto(r)
    checar_captcha(html, "AJAX procedimentos")
    return _rows(html)


# --------------------------------------------------------------- listagem
def consultar(cli, cnes, dt_ini, dt_fim, cpf, pa, pagina=0):  # noqa: PLR0913
    dados = {
        "co_solicitacao": "", "cns_paciente": "",
        "dataInicial": dt_ini, "dataFinal": dt_fim,
        "ups": cnes, "cpf": cpf, "pa": pa,
        "cmbTipoOperacao": "Consulta",
        "chkboxExibirProcedimentos": "on", "chkboxExibirTelefones": "on",
        "cmbOrdenacao": "1", "cmbMaxResults": "50",
        "etapa": "ListaConsulta",  # LEITURA
        "pagina": str(pagina), "linhas": "0",
    }
    time.sleep(PAUSA_SEGUNDOS)
    html = texto(cli.post("/cgi-bin/cons_agendas", data=dados))
    checar_captcha(html, "cons_agendas")
    return html


def total_paginas(html: str) -> int:
    """'Mostrando Pagina [input] de N' -> N."""
    plano = re.sub(r"\s+", " ", BeautifulSoup(html, "html.parser").get_text(" ", strip=True))
    m = re.search(r"Mostrando\s+P.gina\s*(?:de)?\s*(\d+)", plano, re.I)
    if m:
        return int(m.group(1))
    m = re.search(r"de\s+(\d+)\s*Estat", plano, re.I)
    return int(m.group(1)) if m else 1


def _valor(celula: str) -> str:
    return celula.split(":", 1)[1].strip() if ":" in celula else celula.strip()


def parse_registros(html: str, prof: tuple[str, str], pa: tuple[str, str]) -> list[dict]:
    soup = BeautifulSoup(html, "html.parser")
    saida = []
    for tab in soup.find_all("table", id=re.compile(r"^tblConsulta\d+")):
        linhas = tab.find_all("tr")
        if not linhas:
            continue
        c0 = [c.get_text(" ", strip=True) for c in linhas[0].find_all(["td", "th"])]
        c1 = [c.get_text(" ", strip=True) for c in linhas[1].find_all(["td", "th"])] if len(linhas) > 1 else []
        c2 = [c.get_text(" ", strip=True) for c in linhas[2].find_all(["td", "th"])] if len(linhas) > 2 else []

        reg = {k: "" for k in CAMPOS}
        reg["co_solicitacao"] = (tab.get("id") or "").replace("tblConsulta", "")
        if c0:
            reg["cns"] = _valor(next((c for c in c0 if c.startswith("CNS")), ""))
            reg["paciente"] = _valor(next((c for c in c0 if c.startswith("Paciente")), ""))
            reg["nascimento"] = _valor(next((c for c in c0 if c.startswith("Nascimento")), ""))
            reg["idade"] = _valor(next((c for c in c0 if c.startswith("Idade")), ""))
            reg["origem"] = _valor(next((c for c in c0 if c.startswith("Origem")), ""))
            reg["telefones"] = _valor(next((c for c in c0 if c.startswith("Telefone")), ""))
        for celula in c1:
            if celula.startswith("Unidade Solicitante"):
                bruto = _valor(celula)
                m = re.search(r"\((\d+)\)\s*$", bruto)
                reg["cnes_solicitante"] = m.group(1) if m else ""
                reg["unidade_solicitante"] = re.sub(r"\s*\(\d+\)\s*$", "", bruto)
            elif celula.startswith("Vaga Solicitada"):
                reg["vaga_solicitada"] = _valor(celula)
            elif celula.startswith("Vaga Consumida"):
                reg["vaga_consumida"] = _valor(celula)
            elif celula.startswith("CID-10"):
                reg["cid10"] = _valor(celula)
            elif celula.startswith("Data/Hora"):
                bruto = _valor(celula)
                partes = [p.strip() for p in bruto.split("-")]
                reg["data"] = partes[0] if partes else bruto
                reg["dia_semana"] = partes[1] if len(partes) > 1 else ""
                reg["hora"] = partes[2] if len(partes) > 2 else ""
            elif celula.startswith("Situa"):
                reg["situacao"] = _valor(celula)
        if c2:
            reg["procedimentos"] = " | ".join(p for p in c2 if not p.startswith("Procedimento"))

        reg["prof_cpf"], reg["prof_nome"] = prof
        reg["pa_codigo"], reg["pa_nome"] = pa
        saida.append(reg)
    return saida


# ------------------------------------------------------------------ main
def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    dt_ini, dt_fim = argv[0], argv[1]
    posicionais = [a for a in argv[2:] if not a.startswith("--")]
    cnes = posicionais[0] if posicionais else CNES_PADRAO
    limite = None
    if "--limite-prof" in argv:
        limite = int(argv[argv.index("--limite-prof") + 1])

    usuario, senha = credencial(0)
    CAP.mkdir(exist_ok=True)
    sufixo = f"{cnes}_{dt_ini.replace('/', '')}_{dt_fim.replace('/', '')}"
    caminho_jsonl = CAP / f"agenda_{sufixo}.jsonl"
    caminho_csv = CAP / f"agenda_{sufixo}.csv"

    inicio = time.time()
    vistos: set[str] = set()
    total_linhas = 0
    combinacoes = 0
    requisicoes = 0

    with SisregClient() as cli, caminho_jsonl.open("w", encoding="utf-8") as fj:
        try:
            estado = conectar(cli, usuario, senha)
        except SisregLoginError as e:
            print(f"LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        except CaptchaExigido as e:
            print(f"!! {e}", file=sys.stderr)
            return 3
        print(f"operador={usuario} sessao={estado} unidade={cnes} periodo={dt_ini}..{dt_fim}")

        # O AJAX de profissionais e o teste de vida mais confiavel: com sessao
        # derrubada ele devolve <ROOT/> vazio, sem erro nenhum.
        profs = [p for p in profissionais(cli, cnes) if p[0]]
        requisicoes += 1
        if not profs:
            # <ROOT/> vazio nao diz o motivo. Quem revela e a tela HTML: pode ser
            # sessao derrubada (relogin resolve) ou CAPTCHA anti-bot (nao resolve).
            diag = texto(cli.get("/cgi-bin/cons_agendas"))
            requisicoes += 1
            checar_captcha(diag, "cons_agendas (diagnostico inicial)")
            print("AJAX vazio — sessao derrubada; forcando login novo…")
            cli.login(usuario, senha)
            cli.salvar_sessao(SESSAO)
            profs = [p for p in profissionais(cli, cnes) if p[0]]
            requisicoes += 2
            if not profs:
                checar_captcha(texto(cli.get("/cgi-bin/cons_agendas")), "cons_agendas (pos-relogin)")
        if not profs:
            print(f"!! Unidade {cnes} nao devolveu profissionais nem apos relogin. Abortando.", file=sys.stderr)
            return 1
        if limite:
            profs = profs[:limite]
        print(f"profissionais: {len(profs)}\n")

        sem_pa: list[str] = []
        relogins = 0
        parou_em: str | None = None

        try:
            for i, prof in enumerate(profs, 1):
                cpf, nome = prof
                pas = [p for p in procedimentos(cli, cnes, cpf) if p[0]]
                requisicoes += 1
                if not pas:
                    # Vazio aqui e NORMAL: boa parte dos profissionais da unidade
                    # nao tem procedimento com agenda. So investiga se a sessao caiu.
                    if sessao_morta(texto(cli.get("/cgi-bin/cons_agendas"))):
                        print("   !! sessao derrubada — relogando e repetindo o profissional")
                        cli.login(usuario, senha)
                        cli.salvar_sessao(SESSAO)
                        relogins += 1
                        pas = [p for p in procedimentos(cli, cnes, cpf) if p[0]]
                        requisicoes += 2
                    if not pas:
                        sem_pa.append(nome)

                achados_prof = 0
                for pa in pas:
                    combinacoes += 1
                    html = consultar(cli, cnes, dt_ini, dt_fim, cpf, pa[0], 0)
                    requisicoes += 1
                    if sessao_morta(html):
                        print("!! SESSAO MORTA no meio da varredura — relogando…")
                        cli.login(usuario, senha)
                        cli.salvar_sessao(SESSAO)
                        relogins += 1
                        html = consultar(cli, cnes, dt_ini, dt_fim, cpf, pa[0], 0)
                        requisicoes += 1
                    npag = total_paginas(html)
                    for pagina in range(npag):
                        if pagina:
                            html = consultar(cli, cnes, dt_ini, dt_fim, cpf, pa[0], pagina)
                            requisicoes += 1
                        for reg in parse_registros(html, prof, pa):
                            chave = reg["co_solicitacao"]
                            if chave in vistos:
                                continue
                            vistos.add(chave)
                            fj.write(json.dumps(reg, ensure_ascii=False) + "\n")
                            total_linhas += 1
                            achados_prof += 1
                print(
                    f"[{i:>3}/{len(profs)}] {nome[:44]:<44} pas={len(pas):>2} "
                    f"novos={achados_prof:>4} total={total_linhas:>5} ({time.time()-inicio:.0f}s)"
                )
                fj.flush()
        except CaptchaExigido as e:
            # Nao perde o que ja foi extraido: o jsonl e incremental.
            parou_em = f"profissional {i}/{len(profs)} ({nome})"
            print(f"\n!! {e}\n!! Varredura INTERROMPIDA em {parou_em}. Parcial preservado.")

    registros = [json.loads(l) for l in caminho_jsonl.read_text(encoding="utf-8").splitlines() if l.strip()]
    with caminho_csv.open("w", encoding="utf-8-sig", newline="") as fc:
        w = csv.DictWriter(fc, fieldnames=CAMPOS, delimiter=";")
        w.writeheader()
        w.writerows(registros)

    print(f"\n{'='*70}")
    print(f"AGENDAMENTOS UNICOS: {len(registros)}  |  combinacoes prof x proc: {combinacoes}")
    print(f"requisicoes HTTP: {requisicoes}  |  tempo: {time.time()-inicio:.0f}s")
    print(f"relogins no meio: {relogins}  |  profissionais sem procedimento: {len(sem_pa)}/{len(profs)} (normal)")
    if parou_em:
        print(f"!! COBERTURA PARCIAL — interrompido por CAPTCHA em {parou_em}.")
    else:
        print("cobertura: COMPLETA (todos os profissionais da unidade varridos)")
    print(f"jsonl: {caminho_jsonl}\ncsv:   {caminho_csv}")

    def agrupar(campo: str, topo: int = 12):
        cont: dict[str, int] = {}
        for r in registros:
            cont[r.get(campo) or "(vazio)"] = cont.get(r.get(campo) or "(vazio)", 0) + 1
        print(f"\n-- por {campo} --")
        for k, v in sorted(cont.items(), key=lambda kv: -kv[1])[:topo]:
            print(f"   {v:>5}  {k[:64]}")

    for campo in ("data", "pa_nome", "prof_nome", "situacao", "unidade_solicitante", "vaga_consumida"):
        agrupar(campo)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except CaptchaExigido as erro:
        print(f"\n!! {erro}", file=sys.stderr)
        raise SystemExit(3) from None
