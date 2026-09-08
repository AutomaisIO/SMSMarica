"""Teste de validacao: export CSV da agenda (expo_solicitacoes) para
CMI x Renato Roque x Ultrassom Gestante, com a credencial PROGRAMADOR-BERNARDO
(acesso a todas as unidades), lida do .env do Automais.SISCAN.

SOMENTE LEITURA: login + GETs de form + AJAX + POST etapa=exportar (leitura).
Nunca Confirma/Falta/escrita. Para limpo se detectar captcha/sessao morta.

Uso:
    python teste_export_cmi.py            # data1=hoje, data2=hoje+29
    python teste_export_cmi.py 26/08/2026 24/09/2026
"""
from __future__ import annotations

import datetime as dt
import pathlib
import re
import sys
import time
import xml.etree.ElementTree as ET

from bs4 import BeautifulSoup

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except AttributeError:
    pass

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
CAP = BASE / "capturas"
SESSAO = CAP / ".sessao_programador.json"
ENV_SISCAN = BASE.parent / "Automais.SISCAN" / ".env"

PAUSA = 0.4
# NAO usar "recaptcha" cru: a tela cons_agendas SAUDAVEL carrega
# https://www.google.com/recaptcha/api.js no <head> (falso positivo).
# A PAREDE real redireciona com window.location="./recaptcha?cod=0" e/ou traz
# o widget g-recaptcha + a sitekey.
MARCAS_CAPTCHA = (
    'recaptcha?cod',
    '/cgi-bin/recaptcha',
    '6lemzwgsaaaaak85',                # sitekey da parede
    'class="g-recaptcha"',
    'class=\'g-recaptcha\'',
    'diferencia&ccedil;&atilde;o entre computadores',
)
MARCAS_SESSAO_MORTA = ("sisreg_erro", "erro ao carregar a sessao", "logon em outra", "foi finalizada")

ALVO_UNIDADE = "CMI"            # nome (fantasia) que buscamos no seletor
ALVO_PROF = "RENATO ROQUE"     # nome do profissional
ALVO_PROC = "GESTA"           # substring do procedimento (Ultrassom Gestante / Obstetrica)


class CaptchaExigido(RuntimeError):
    pass


def ler_env(caminho: pathlib.Path) -> dict[str, str]:
    d: dict[str, str] = {}
    for linha in caminho.read_text(encoding="utf-8").splitlines():
        linha = linha.strip()
        if linha.startswith("#") or "=" not in linha:
            continue
        chave, valor = linha.split("=", 1)
        chave = chave.strip()
        if chave in ("SISREG_USUARIO", "SISREG_SENHA", "SISREG_BASE_URL"):
            d[chave] = valor.strip()
    return d


def texto(resp) -> str:
    try:
        return resp.content.decode("utf-8")
    except UnicodeDecodeError:
        return resp.content.decode("iso-8859-1", errors="replace")


def checar(html: str, onde: str) -> None:
    baixo = html.lower()
    if any(m in baixo for m in MARCAS_CAPTCHA):
        raise CaptchaExigido(f"CAPTCHA em {onde} — parar. Um humano precisa resolver no navegador.")


def sessao_morta(html: str) -> bool:
    baixo = html.lower()
    return any(m in baixo for m in MARCAS_SESSAO_MORTA)


def rows_xml(xml: str) -> list[tuple[str, str]]:
    try:
        root = ET.fromstring(xml.strip() or "<ROOT/>")
    except ET.ParseError:
        return []
    out = []
    for row in root.iter("ROW"):
        cod = (row.get("codigo") or "").strip()
        nome = (row.text or "").strip()
        if cod:
            out.append((cod, nome))
    return out


def selects_de_unidade(html: str) -> list[tuple[str, str]]:
    """Extrai (CNES, nome) do <select name='ups'> do cons_agendas."""
    soup = BeautifulSoup(html, "html.parser")
    sel = soup.find("select", attrs={"name": "ups"})
    if not sel:
        return []
    out = []
    for op in sel.find_all("option"):
        val = (op.get("value") or "").strip()
        nome = op.get_text(strip=True)
        if val:
            out.append((val, nome))
    return out


def main(argv: list[str]) -> int:
    hoje = dt.date(2026, 8, 26)  # data fixa (o ambiente bloqueia Date.now em alguns contextos)
    if len(argv) >= 2:
        data1, data2 = argv[0], argv[1]
    else:
        data1 = hoje.strftime("%d/%m/%Y")
        data2 = (hoje + dt.timedelta(days=29)).strftime("%d/%m/%Y")

    env = ler_env(ENV_SISCAN)
    usuario = env.get("SISREG_USUARIO", "")
    senha = env.get("SISREG_SENHA", "")
    if not usuario or not senha:
        print("!! credencial SISREG nao encontrada no .env do SISCAN", file=sys.stderr)
        return 2
    CAP.mkdir(exist_ok=True)

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br") as cli:
        # 1) login (sessao propria do PROGRAMADOR — nao reusa a do operador CDT)
        try:
            estado = "reaproveitada"
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                cli.login(usuario, senha)
                cli.salvar_sessao(SESSAO)
                estado = "novo_login"
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"[1] login: operador={usuario} sessao={estado}")

        # 2) unidades visiveis (cons_agendas -> select ups) -> achar CMI
        time.sleep(PAUSA)
        html = texto(cli.get("/cgi-bin/cons_agendas"))
        (CAP / "_cons_agendas_programador.html").write_text(html, encoding="utf-8")
        checar(html, "cons_agendas form")
        if sessao_morta(html):
            print("   sessao morta no GET cons_agendas — relogando…")
            cli.login(usuario, senha); cli.salvar_sessao(SESSAO)
            time.sleep(PAUSA)
            html = texto(cli.get("/cgi-bin/cons_agendas"))
            checar(html, "cons_agendas form (pos-relogin)")
        unidades = selects_de_unidade(html)
        print(f"[2] unidades visiveis: {len(unidades)}")
        for cnes, nome in unidades:
            marca = "  <== ALVO" if ALVO_UNIDADE in nome.upper() else ""
            print(f"      {cnes}  {nome}{marca}")
        cand = [u for u in unidades if ALVO_UNIDADE in u[1].upper()
                or "MATERNO" in u[1].upper() or "MATERNO INFANTIL" in u[1].upper()]
        if not cand:
            print(f"!! nenhuma unidade com '{ALVO_UNIDADE}'/'MATERNO' no nome. "
                  f"Veja a lista acima e me diga o CNES certo.", file=sys.stderr)
            return 1
        cnes_cmi, nome_cmi = cand[0]
        print(f"    -> CMI = {cnes_cmi} ({nome_cmi})")

        # 3) profissionais do CMI -> Renato Roque
        time.sleep(PAUSA)
        xmlp = texto(cli.get("/cgi-bin/sisreg_ajax",
                             params={"BUSCA": "PROFISSIONAIS_POR_UPS", "AJAX_UPS": cnes_cmi}))
        checar(xmlp, "AJAX profissionais CMI")
        profs = rows_xml(xmlp)
        print(f"[3] profissionais no CMI: {len(profs)}")
        alvo = [p for p in profs if ALVO_PROF in p[1].upper()]
        for cpf, nome in alvo:
            print(f"      {cpf}  {nome}  <== ALVO")
        if not alvo:
            print(f"!! '{ALVO_PROF}' nao esta entre os profissionais do CML. "
                  f"Profissionais (primeiros 40):", file=sys.stderr)
            for cpf, nome in profs[:40]:
                print(f"      {cpf}  {nome}")
            return 1
        cpf_renato, nome_renato = alvo[0]

        # 4) procedimentos do Renato no CMI -> Ultrassom Gestante
        time.sleep(PAUSA)
        xmlpr = texto(cli.get("/cgi-bin/sisreg_ajax",
                              params={"BUSCA": "PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS",
                                      "AJAX_CPF": cpf_renato, "AJAX_UPS": cnes_cmi}))
        checar(xmlpr, "AJAX procedimentos Renato")
        procs = rows_xml(xmlpr)
        print(f"[4] procedimentos do Renato no CMI: {len(procs)}")
        for pa, nome in procs:
            marca = "  <== ALVO" if ALVO_PROC in nome.upper() else ""
            print(f"      {pa}  {nome}{marca}")
        alvo_proc = [p for p in procs if ALVO_PROC in p[1].upper()
                     or "OBSTETRICA" in p[1].upper() or "GESTANTE" in p[1].upper()]
        if not alvo_proc:
            print("!! nenhum procedimento 'GESTA/OBSTETRICA/GESTANTE'. Veja a lista acima.", file=sys.stderr)
            return 1
        pa_code, pa_nome = alvo_proc[0]
        print(f"    -> procedimento = {pa_code} ({pa_nome})")

        # 5) inspecionar o form de exportacao (como a unidade e fixada)
        time.sleep(PAUSA)
        formhtml = texto(cli.get("/cgi-bin/expo_solicitacoes"))
        checar(formhtml, "expo_solicitacoes form")
        (CAP / "_expo_form_programador.html").write_text(formhtml, encoding="utf-8")
        m_unidade = re.search(r"name=[\"']unidade[\"'][^>]*value=[\"'](\d+)[\"']", formhtml)
        print(f"[5] expo form salvo. unidade(hidden)={m_unidade.group(1) if m_unidade else '(select/ausente)'} "
              f"bloqueio_horario={'SIM' if 'bloquead' in formhtml.lower() else 'nao'}")

        # 6) POST export CSV
        dados = {
            "data1": data1, "data2": data2,
            "cpf": cpf_renato, "procedimento": pa_code,
            "tp_arquivo": "1",           # 1 = CSV
            "etapa": "exportar",
            "unidade": cnes_cmi,
        }
        time.sleep(PAUSA)
        resp = cli.post("/cgi-bin/expo_solicitacoes", data=dados)
        corpo = texto(resp)
        checar(corpo, "expo export POST")
        destino = CAP / f"_export_cmi_{cnes_cmi}_{data1.replace('/','')}_{data2.replace('/','')}.csv"
        destino.write_text(corpo, encoding="utf-8")
        linhas = [l for l in corpo.splitlines() if l.strip()]
        print(f"[6] export CSV: {len(linhas)} linha(s). salvo em {destino.name}")
        print("--- cabecalho + ate 3 linhas ---")
        for l in linhas[:4]:
            print("   " + l[:200])
        if len(linhas) <= 1:
            print("   (0 agendamentos no periodo — talvez alargar a janela, ou o proc/prof nao tem agenda)")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except CaptchaExigido as e:
        print(f"\n!! {e}", file=sys.stderr)
        raise SystemExit(3) from None
