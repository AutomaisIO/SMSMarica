"""Sonda em lote os relatórios de PARÂMETROS (`Relatorios/ParametroRelatorio.aspx?parrel=N`):
campos da tela, layout `parN` do `rptviewXls`, colunas do Excel e nº de linhas. SOMENTE LEITURA.

Para cada `parrel`:
1. GET da tela de parâmetros → lista os campos próprios (`ctlParam$…`) e as opções dos selects;
2. POST `btnImprimirExcel` com o período pedido (1 dia por padrão — o Crystal estoura com mês)
   → captura o `window.open('rptviewXls.aspx?parRel=…&parNum=…&par1=…')`;
3. GET desse `rptviewXls` → se vier XLS, acha a linha de cabeçalho (primeira com 3+ células de
   texto) e conta as linhas de dado. Só o CABEÇALHO é impresso; o XLS fica em capturas/.

Uso:  python sondar_relatorios.py 631 407 56 … [--modulo UPA] [--dia 2026-09-15]
Saída: tabela no stdout + capturas/sondagem_relatorios.json (sem PII).
"""

from __future__ import annotations

import io
import json
import re
import sys

from klinikos.client import CAP, LIBERADOS, KlinikosSession, action_do_form, campos_todos, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
PREF = "ctl00$ctl00$contentCenter$contentCenterChild$"


def arg(args, nome, padrao=None):
    return args[args.index(nome) + 1] if nome in args else padrao


def campos_proprios(html: str) -> list[dict]:
    d = sopa(html)
    out = []
    for e in d.find_all(["input", "select", "textarea"]):
        n = e.get("name") or ""
        if "ctlParam" not in n or n.startswith("__"):
            continue
        t = e.name if e.name != "input" else (e.get("type") or "text")
        if t == "hidden":
            continue
        item = {"nome": n.replace(PREF + "ctlParam$", ""), "tipo": t}
        if e.name == "select":
            item["opcoes"] = [(o.get("value"), o.get_text(strip=True)[:30]) for o in e.find_all("option")][:12]
            item["n_opcoes"] = len(e.find_all("option"))
        out.append(item)
    return out


RUIDO_CRYSTAL = re.compile(r"Per[íi]odo|Emitido em|Usu[áa]rio|P[áa]gina|\.rpt$|^SECRETARIA|^HOSPITAL|^UPA|^Unidade", re.I)


def cabecalho_xls(b: bytes) -> tuple[list[str], int]:
    """Cabeçalho = primeira linha com 3+ células de texto que NÃO seja o rodapé/cabeçalho do
    Crystal (Período, Emitido em, Usuário, Página, nome do .rpt, nome da unidade). Linhas de
    dado = linhas com 2+ células depois do cabeçalho, descontando repetições do cabeçalho (o
    407 repete o cabeçalho a cada registro)."""
    import xlrd
    sh = xlrd.open_workbook(file_contents=b).sheet_by_index(0)
    cab, linhas_dado = [], 0
    for i in range(sh.nrows):
        v = [str(c).strip() for c in sh.row_values(i) if str(c).strip()]
        if not cab:
            if len(v) >= 3 and all(not re.fullmatch(r"[\d\.\-/: ]+", x) for x in v) \
                    and not any(RUIDO_CRYSTAL.search(x) for x in v[:2]):
                cab = v
        elif len(v) >= 2 and v != cab and not RUIDO_CRYSTAL.search(v[0]):
            linhas_dado += 1
    return cab, linhas_dado


def campos_da_tela(html: str) -> list[dict]:
    """Campos do conteúdo (fora do master: HeaderLinks/RadMenu/StatusServico), qualquer tela."""
    d = sopa(html)
    out = []
    for e in d.find_all(["input", "select", "textarea"]):
        n = e.get("name") or ""
        if not n or n.startswith("__") or "HeaderLinks" in n or "RadMenu" in n or "StatusServico" in n \
                or n.endswith("_TSM") or n.endswith("_TSSM") or "_ClientState" in n or "_calendar_" in n:
            continue
        t = e.name if e.name != "input" else (e.get("type") or "text")
        if t == "hidden" and "hdf" in n:
            continue
        item = {"nome": n.replace(PREF, "").replace("ctl00$ctl00$", ""), "tipo": t}
        if e.name == "select":
            item["opcoes"] = [(o.get("value"), o.get_text(strip=True)[:30]) for o in e.find_all("option")][:12]
            item["n_opcoes"] = len(e.find_all("option"))
        if t in ("radio", "checkbox"):
            item["value"] = e.get("value")
        out.append(item)
    return out


def sondar(s: KlinikosSession, alvo: str, modulo: str, dia: str) -> dict:
    """`alvo` = parrel numérico (tela ParametroRelatorio) ou URL de tela própria de relatório."""
    if re.fullmatch(r"\d+", alvo):
        url = f"/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel={alvo}&Modulo={modulo}"
    else:
        url = alvo if alvo.startswith("/") else "/KlinikosNet/" + alvo
    r = s.get(url)
    html = r.text
    d = sopa(html)
    titulo = ""
    for cand in d.find_all(["span", "h1", "h2", "h3", "label"]):
        t = cand.get_text(" ", strip=True)
        if cand.get("id") and "lblTitulo" in cand.get("id", "") and t:
            titulo = t
            break
    info = {"alvo": alvo, "url": url, "titulo": titulo, "status": r.status_code,
            "campos": campos_da_tela(html)}
    if "GravaCookie" in str(r.url) or "sessaoexpirada" in str(r.url).lower():
        info["erro"] = "redirecionou para " + str(r.url)
        return info

    dados = campos_todos(html)
    dd, mm, aa = dia[8:10], dia[5:7], dia[0:4]
    # Datas: qualquer RadDatePicker cujo nome termine em Inicial/Final (com ou sem ctlParam).
    for chave in list(dados):
        # `rdpData*` (RadDatePicker) e `rdtpData*` (RadDateTimePicker, usado no Cadastro via
        # `UCPesquisaData1$rdtpDataInicio/Fim`).
        m = re.search(r"\$(rdt?pData\w*?(Inicial|Final|Inicio|Fim))$", chave)
        if m:
            base = chave
            dados[base] = dia
            dados[base + "$dateInput"] = f"{dia}-00-00-00"
            dados[base.replace("$", "_") + "_dateInput_text"] = f"{dd}/{mm}/{aa}"
    botoes = [i.get("name") for i in d.find_all("input", attrs={"type": "image"}) if i.get("name")]
    excel = next((b for b in botoes if re.search(r"ImprimirExcel$|Excel$", b)), None)
    imprimir = next((b for b in botoes if re.search(r"(btn|imb)Imprimir$|Gerar$|Visualizar$", b)), None)
    botao = excel or imprimir
    info["botao"] = botao
    if not botao:
        info["erro"] = f"sem botão de Excel/Imprimir; botões-imagem: {botoes}"
        return info
    dados[botao + ".x"] = "10"
    dados[botao + ".y"] = "10"
    LIBERADOS.update({botao + ".x", botao + ".y"})
    r2 = s.post(action_do_form(html, str(r.url)), dados)
    scripts = " ".join(sc.get_text() for sc in sopa(r2.text).find_all("script") if not sc.get("src"))
    m = re.search(r"""window\.open\(["']([^"']*rptview[^"']*)["']""", scripts, re.I)
    if not m:
        alerta = re.search(r"""(?:Swal\.fire|alert)\(([^)]{0,200})""", scripts)
        info["erro"] = "sem window.open após Excel" + (f" — alerta: {alerta.group(1)[:150]}" if alerta else "")
        (CAP / f"sond_{re.sub(r'[^A-Za-z0-9]+', '_', alvo)[:60]}_semopen.html").write_text(r2.text, encoding="utf-8")
        return info
    rpt = m.group(1)
    info["rptview"] = rpt
    from urllib.parse import urljoin
    r3 = s.get(urljoin(str(r2.url), rpt))
    ct = r3.headers.get("content-type", "")
    info["content_type"] = ct
    info["bytes"] = len(r3.content)
    nome = re.sub(r"[^A-Za-z0-9]+", "_", alvo)[:60]
    if r3.content[:8] == b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1":
        (CAP / f"sond_{nome}.xls").write_bytes(r3.content)
        cab, n = cabecalho_xls(r3.content)
        info["colunas"], info["linhas"] = cab, n
    elif r3.content[:4] == b"%PDF":
        (CAP / f"sond_{nome}.pdf").write_bytes(r3.content)
        info["resposta"] = "PDF"
    else:
        txt = r3.text
        (CAP / f"sond_{nome}.html").write_text(txt, encoding="utf-8")
        info["resposta"] = " ".join(sopa(txt).get_text(" ", strip=True).split())[:200]
    return info


def main(args: list[str]) -> int:
    modulo = arg(args, "--modulo", "UPA")
    dia = arg(args, "--dia", "2026-09-15")
    ids = [a for a in args if re.fullmatch(r"\d+", a) or ".aspx" in a.lower()]
    if not ids:
        print(__doc__)
        return 2
    s = KlinikosSession()
    s.entrar()
    saida = []
    for p in ids:
        try:
            info = sondar(s, p, modulo, dia)
        except Exception as e:  # noqa: BLE001
            info = {"alvo": p, "erro": f"{type(e).__name__}: {e}"[:200]}
        saida.append(info)
        campos = ", ".join(f"{c['nome']}({c.get('n_opcoes', c['tipo'])})" for c in info.get("campos", [])
                           if "dateInput" not in c["nome"])
        print(f"== {p} {info.get('titulo', '')}  [botão={info.get('botao')}]")
        print(f"   campos: {campos}")
        if info.get("rptview"):
            print(f"   rptview: {info['rptview']}")
        if info.get("colunas") is not None:
            print(f"   xls: {info['bytes']}B, {info['linhas']} linhas; colunas={info['colunas'][:16]}")
        if info.get("resposta"):
            print(f"   resposta: {info['resposta'][:160]}")
        if info.get("erro"):
            print(f"   ERRO: {info['erro']}")
        sys.stdout.flush()
    (CAP / "sondagem_relatorios.json").write_text(json.dumps(saida, ensure_ascii=False, indent=1), encoding="utf-8")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
