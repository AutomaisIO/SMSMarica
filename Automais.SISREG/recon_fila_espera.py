"""Recon das telas de FILA DE ESPERA (solicitacoes ainda NAO agendadas).

Motivo: o sistema so importa solicitacao que ja tem dia marcado. Medido em 05/09/2026,
22.081 de 22.101 solicitacoes tinham `data_agendada` — ou seja, a fila de espera nao
existe no nosso banco. Quem esta aguardando vaga e invisivel para a operacao.

SOMENTE LEITURA: 1 login (reaproveitado) + 1 GET por tela candidata. NENHUM POST.
O objetivo e descobrir o formulario de cada tela ANTES de gastar POST no orcamento
anti-robo, que e do OPERADOR (~500/h) e cujo estouro pausa a unidade por 24h.

Candidatas, do mapa de menus em docs/APRENDIZADOS.md:
  cons_pendente_fila_    "Solicitacoes Pendentes na Fila de Espera"  <- alvo principal
  gerenciador_solicitacao "Solicitacoes" (gerenciador)
  rellst.pl              "Agendamentos / Data Solicitacao"
  cons_fila_espera       "Agendados pela Fila de Espera" (ja agendados — contraste)
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg import SisregClient, SisregLoginError  # noqa: E402

BASE = pathlib.Path(__file__).parent
SESSAO = BASE / "capturas" / ".sessao_programador.json"

# Nomes REAIS, extraidos do frameset pos-login por recon_menu2.py. O mapa antigo de
# docs/APRENDIZADOS.md listava `cons_pendente_fila_`, que responde 404 — nao era
# truncamento, era outro nome.
TELAS = [
    ("rel_fila_espera_mun_pendentes.pl", "Pendentes Fila de Espera  <- ALVO"),
    ("rel_media_fila.pl", "Media de Espera em Fila"),
    ("gerenciador_solicitacao", "Solicitacoes (gerenciador)"),
    ("rel_fila_espera_mun.pl", "Agendamento fila de espera (contraste)"),
]

MARCAS_SESSAO_MORTA = (
    "logon em outra", "sess&#227;o foi finalizada", "sessao foi finalizada",
    "a sess&atilde;o expirou", "tempo de inatividade",
)

MARCAS_CAPTCHA = (
    "recaptcha?cod", "/cgi-bin/recaptcha", "6lemzwgsaaaaak85",
    'class="g-recaptcha"', "diferencia&ccedil;&atilde;o entre computadores",
)


def ler_env(caminho: pathlib.Path) -> dict[str, str]:
    d: dict[str, str] = {}
    if not caminho.exists():
        return d
    for linha in caminho.read_text(encoding="utf-8").splitlines():
        linha = linha.strip()
        if linha.startswith("#") or "=" not in linha:
            continue
        chave, valor = linha.split("=", 1)
        if chave.strip() in ("SISREG_USUARIO", "SISREG_SENHA", "SISREG_BASE_URL"):
            d[chave.strip()] = valor.strip()
    return d


def credencial() -> dict[str, str]:
    """Procura a credencial nos .env conhecidos do monorepo."""
    for p in (BASE / ".env", BASE.parent / "Automais.SISCAN" / ".env"):
        env = ler_env(p)
        if env.get("SISREG_USUARIO") and env.get("SISREG_SENHA"):
            print(f"[0] credencial de {p.name} em {p.parent.name}/")
            return env
    return {}


def texto(resp) -> str:
    try:
        return resp.content.decode("utf-8")
    except UnicodeDecodeError:
        return resp.content.decode("iso-8859-1", errors="replace")


def dissecar(html: str, rotulo: str) -> None:
    print(f"\n{'-' * 72}")
    print(f"  {rotulo}  ({len(html)} bytes)")
    print(f"{'-' * 72}")

    baixo = html.lower()
    if "n&atilde;o tem permiss" in baixo or "nao tem permiss" in baixo or "sem permiss" in baixo:
        print("  !! SEM PERMISSAO para este operador")
    if "logon em outra" in baixo or "sess&#227;o foi finalizada" in baixo:
        print("  !! SESSAO DERRUBADA")
        return

    for f in re.finditer(r"<form[^>]*>", html, re.I):
        print("  FORM:", re.sub(r"\s+", " ", f.group(0))[:160])

    campos = list(re.finditer(r"<(input|select|textarea)\b([^>]*)>", html, re.I))
    print(f"\n  campos ({len(campos)}):")
    for m in campos:
        tag, attrs = m.group(1).lower(), m.group(2)
        nome = re.search(r'name\s*=\s*["\']?([^"\'\s>]+)', attrs, re.I)
        tipo = re.search(r'type\s*=\s*["\']?([^"\'\s>]+)', attrs, re.I)
        val = re.search(r'value\s*=\s*["\']([^"\']*)', attrs, re.I)
        print(f"    {tag:8} name={(nome.group(1) if nome else '-'):24} "
              f"type={(tipo.group(1) if tipo else '-'):10} value={(val.group(1)[:38] if val else '-')}")

    etapas = sorted(set(re.findall(r"['\"]([A-Z_]{5,})['\"]", html)))
    if etapas:
        print(f"\n  etapas/constantes no JS: {etapas}")

    fns = sorted(set(re.findall(r"function\s+(\w+)\s*\(", html)))
    if fns:
        print(f"  funcoes JS: {fns}")

    print("\n  selects (tamanho):")
    for s in re.finditer(r'<select[^>]*name\s*=\s*["\']?([\w\[\]]+)[^>]*>(.*?)</select>', html, re.I | re.S):
        nome, corpo = s.group(1), s.group(2)
        opts = re.findall(r'<option[^>]*value\s*=\s*["\']?([^"\'>]*)["\']?[^>]*>([^<]*)', corpo, re.I)
        print(f"    {nome}: {len(opts)} options  ex: {[o[1].strip()[:28] for o in opts[1:4]]}")

    # Palavras que denunciam paginacao ou exportacao — e o que decide o custo em requisicoes.
    for chave in ("EXPORTAR", "exportar", "txt", "csv", "pagina", "proxima", "registros",
                  "limite", "700", "MAX", "total"):
        if chave in html:
            trecho = html[max(0, html.find(chave) - 60):html.find(chave) + 60]
            print(f"  ~ contem '{chave}': ...{re.sub(r'\\s+', ' ', trecho)}...")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True)
    args = ap.parse_args(argv)
    saida = pathlib.Path(args.saida)
    saida.mkdir(parents=True, exist_ok=True)

    env = credencial()
    if not env:
        print("!! credencial SISREG nao encontrada nos .env", file=sys.stderr)
        return 2

    with SisregClient(base_url=env.get("SISREG_BASE_URL") or "https://sisregiii.saude.gov.br") as cli:
        try:
            estado = "reaproveitada"
            if not (cli.carregar_sessao(SESSAO) and cli.esta_logado()):
                cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
                cli.salvar_sessao(SESSAO)
                estado = "novo_login"
        except SisregLoginError as e:
            print(f"!! LOGIN FALHOU: {e}", file=sys.stderr)
            return 1
        print(f"[1] login: operador={env['SISREG_USUARIO']} sessao={estado}")

        for i, (cgi, rotulo) in enumerate(TELAS, start=2):
            html = texto(cli.get(f"/cgi-bin/{cgi}"))

            # `esta_logado()` da falso positivo: o SISREG responde 200 com sisreg_erro.c
            # quando a sessao foi derrubada por outro logon. So o conteudo denuncia.
            if any(m in html.lower() for m in MARCAS_SESSAO_MORTA):
                print("  ! sessao derrubada — refazendo login uma vez")
                cli.login(env["SISREG_USUARIO"], env["SISREG_SENHA"])
                cli.salvar_sessao(SESSAO)
                html = texto(cli.get(f"/cgi-bin/{cgi}"))

            if any(m in html.lower() for m in MARCAS_CAPTCHA):
                print(f"!! CAPTCHA em {cgi} — PARANDO para nao pausar a unidade.", file=sys.stderr)
                return 4

            (saida / f"{cgi}.html").write_text(html, encoding="utf-8")
            print(f"[{i}] GET /cgi-bin/{cgi} -> {len(html)} bytes")
            dissecar(html, f"{cgi} — {rotulo}")

    print(f"\n== {len(TELAS)} GETs gastos. Nenhum POST. HTML cru em {saida} ==")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
