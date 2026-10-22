"""Sonda 4: o COMPROVANTE de agendamento de exame do ESUS SG (a "guia" que o paciente leva).

Responde: como o front obtém o comprovante de um agendamento de exame, o que ele traz, e se o SG
conta vias / carimba "comprovante impresso" quando ele é gerado.

O que o front faz (bundle 2.8.4, tela Exame → Regulação → Agendados pela Fila, botão Imprimir —
`ExamSearchPatientSchedule.actScheduleVoucherHTML`), nesta ordem:

1. `exames2/controller-exame-agendamento-procedimento/cadastrar-comprovante-impressao-lote`
   `{"arrEap_id": [eap_id]}` — ESCRITA: carimba "comprovante impresso". Esta sonda NÃO chama.
2. `exames2/controller-exame-agendamento-procedimento/obter-comprovante-exame-agendamento-em-lote-com-preparo-subprocedimentos`
   `{"ids": [eap_id]}` — devolve JSON; o front junta `{...dados[0], ...linhaDaGrade}` e monta o
   HTML do cupom (7 cm, impressora térmica) no navegador. Não existe PDF do servidor.

Passos:
  (padrão)   lista os agendados de exame de Maricá com atendimento de amanhã em diante (ou a partir
             de --minimo), sorteia um
             (entre os ainda NÃO impressos, se houver — é o único jeito de medir se o passo 2 carimba
             o flag) e mostra o estado. Guarda o sorteio em <saida>/sorteio.json.
  --baixar   faz SÓ o passo 2 para o sorteado (porta única nomeada, fora da trava geral), monta o
             HTML como o front, imprime em PDF pelo Chrome headless e relê o flag.

A saída tem dado de paciente: vai para --saida, fora do repositório.
"""

from __future__ import annotations

import argparse
import datetime as dt
import html
import json
import pathlib
import random
import subprocess
import sys

from esus.client import ErroEsus, paginar, sessao
from esus.telas import CAMINHO, form_agendados

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Porta única, autorizada em 21/10/2026 para baixar UMA guia. Não entra na trava geral
# (`esus/client.py` segue recusando todo `*comprovante*`); só esta ação, só aqui.
OBTER_COMPROVANTE = ("/exames2/controller-exame-agendamento-procedimento/"
                     "obter-comprovante-exame-agendamento-em-lote-com-preparo-subprocedimentos")
CHROME = pathlib.Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe")


def agendados(s, de: str, ate: str) -> list[dict]:
    linhas, _ = paginar(s, CAMINHO["agendados_exame"],
                        form_agendados(s.unidade, exame=True, de=de, ate=ate),
                        chave=("fil_id", "eap_id", "data_hora_formatada"))
    return linhas


def data_exame(ln: dict) -> dt.date | None:
    v = (ln.get("eha_data_exame") or ln.get("data_agendada") or "")[:10]
    try:
        return dt.datetime.strptime(v, "%d/%m/%Y").date()
    except ValueError:
        return None


def resumo(ln: dict) -> str:
    return (f"eap_id={ln.get('eap_id')} fil_id={ln.get('fil_id')} | "
            f"{ln.get('stp_novo_nome_procedimento')} | {ln.get('data_hora_formatada')} | "
            f"{ln.get('unidadeAgendamento')} / {ln.get('set_nome')} / {ln.get('lca_nome')} | "
            f"impresso={ln.get('comprovanteImpresso')}")


def obter_comprovante(s, eap_id) -> list[dict]:
    r = s.leg.post(OBTER_COMPROVANTE, json={"ids": [eap_id]}, headers=s._cab(legado=True))
    if r.status_code >= 400:
        raise ErroEsus(f"obter comprovante -> HTTP {r.status_code}: {r.text[:300]}")
    d = r.json()
    if isinstance(d, dict) and d.get("status") is False:
        raise ErroEsus(f"obter comprovante: {json.dumps(d, ensure_ascii=False)[:500]}")
    return d.get("dados") if isinstance(d, dict) and "dados" in d else d


def cupom_html(e: dict) -> str:
    """Porta do template `X2a` do bundle (cupom de 7 cm). Escapa o texto; o front não escapa."""
    def v(k):
        x = e.get(k)
        return "" if x is None else html.escape(str(x))
    sub = "".join(
        f"<div><label>Preparo Subprocedimento: {html.escape(str(m.get('nome_suprocedimento') or ''))}"
        f"</label><span>{html.escape(str(m.get('preparo') or ''))}</span></div><hr />"
        for m in (e.get("recomendacaoPreparoSub") or []))
    senha = f"<p><b>Senha Efetivação: </b>{v('eap_senha_efetivacao')}</p>" if e.get("eap_senha_efetivacao") else ""
    fila = "block" if e.get("agendadoPelaFila") else "none"
    return f"""<!DOCTYPE html><html><head><meta charset="UTF-8"><style>
body{{width:7cm;margin:.1cm;font-size:8pt;font-family:Verdana,sans-serif}}
div,p{{width:6.5cm;margin:.2cm auto;word-wrap:break-word;line-height:120%}}
.uppercase{{text-transform:uppercase}} .label{{width:2.2cm;display:inline-block}}
.fila{{display:{fila}}} .client-name,.unit-name{{font-weight:bold}} @page{{margin:0}}
</style></head><body>
<div><h2 class="client-name">{v('cli_nome')}</h2></div>
<p class="unit-name">{v('uns_nome')}</p>
<p>{v('tlg_nome')} {v('end_logradouro')}, {v('end_numero')} {v('end_complemento')} - {v('bai_nome')} {v('mun_nome')}</p>
<p>Referência: {v('end_referencia')}</p>
<p><strong>Comparecer ao exame</strong></p>
<p>Data: <strong>{v('eha_data_exame')} às {v('eap_hora')}</strong></p>
<p><span class="label">Paciente:</span> {v('pes_nome')}</p>
<p><span class="label">Nome Social:</span> {v('pes_nome_social')}</p>
<p><span class="label">Prontuário:</span> {v('ppr_numero')}</p>
<p><span class="label">Pront. Único:</span> {v('pac_prontuario_unico')}</p>
<p><span class="label">Cartão Sus:</span> {v('crs_numero')}</p>
<p><span class="label">Setor:</span> {v('set_nome')}</p>
<p><span class="label">Local:</span> {v('lca_nome')}</p>
<p><span class="label">Procedimento:</span> {v('stp_novo_nome_procedimento')}</p>
<p><span class="label">SubProcedimentos:</span> {v('subprocedimentos')}</p>
<p>Profissional: <span class="uppercase">{v('atendente')}</span></p>
<p>Ocupação: <span class="uppercase">{v('ocp_nome')}</span></p>
<p><strong>{v('observacoes')}</strong></p>
<p>Marcação realizada no dia</p>
<p>{v('data_marcacao')} às {v('hora_marcacao')}</p>
<p>por: {v('usu_nome')}</p>
<div class="fila"><label style="width:200px">{v('agendadoPelaFila')}</label></div>
<div class="fila"><label>{v('lblNomeRegulador')}</label> <span>{v('nomeRegulador')}</span></div>
{senha}
{e.get('recomendacaoPreparo') or ''}
{sub}
<hr /></body></html>"""


def main() -> int:
    hoje = dt.date.today()
    ap = argparse.ArgumentParser()
    ap.add_argument("--saida", required=True, type=pathlib.Path)
    ap.add_argument("--de", default=(hoje + dt.timedelta(days=1)).strftime("%d/%m/%Y"))
    ap.add_argument("--ate", default=(hoje + dt.timedelta(days=120)).strftime("%d/%m/%Y"))
    ap.add_argument("--minimo", help="sorteia só atendimento a partir desta data (padrão: amanhã)")
    ap.add_argument("--baixar", action="store_true", help="obtém o comprovante do sorteado (1 chamada)")
    a = ap.parse_args()
    a.saida.mkdir(parents=True, exist_ok=True)
    arq_sorteio = a.saida / "sorteio.json"

    with sessao() as s:
        linhas = agendados(s, a.de, a.ate)
        minimo = (dt.datetime.strptime(a.minimo, "%d/%m/%Y").date() if a.minimo
                  else hoje + dt.timedelta(days=1))
        futuras = [ln for ln in linhas if (data_exame(ln) or dt.date.min) >= minimo]
        print(f"agendados de exame (Maricá) {a.de}..{a.ate}: {len(linhas)}; atendimento a partir de "
              f"{minimo:%d/%m/%Y}: {len(futuras)}")
        if not a.baixar:
            if not futuras:
                return 0
            print("comprovante impresso:", {k: sum(1 for ln in futuras if ln.get("comprovanteImpresso") == k)
                                             for k in {ln.get("comprovanteImpresso") for ln in futuras}})
            nao = [ln for ln in futuras if ln.get("comprovanteImpresso") not in ("SIM",)]
            esc = random.choice(nao or futuras)
            print(f"sorteado ({'entre os NÃO impressos' if nao else 'entre todos'}): {resumo(esc)}")
            arq_sorteio.write_text(json.dumps(esc, ensure_ascii=False, indent=1), encoding="utf-8")
            print("campos da linha:", ", ".join(sorted(esc)))
            return 0

        esc = json.loads(arq_sorteio.read_text(encoding="utf-8"))
        antes = next((ln for ln in linhas if str(ln.get("eap_id")) == str(esc["eap_id"])), None)
        print("antes :", resumo(antes) if antes else "(sumiu da lista)")

        dados = obter_comprovante(s, esc["eap_id"])
        (a.saida / f"comprovante_{esc['eap_id']}.json").write_text(
            json.dumps(dados, ensure_ascii=False, indent=1), encoding="utf-8")
        reg = dados[0] if isinstance(dados, list) and dados else {}
        print(f"obter-comprovante: {len(dados) if isinstance(dados, list) else type(dados).__name__} "
              f"registro(s); campos: {', '.join(sorted(reg))}")

        e = reg | (antes or esc)   # o front faz {...dados[0], ...linhaDaGrade}
        arq_html = a.saida / f"comprovante_{esc['eap_id']}.html"
        arq_html.write_text(cupom_html(e), encoding="utf-8")
        arq_pdf = arq_html.with_suffix(".pdf")
        subprocess.run([str(CHROME), "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
                        f"--print-to-pdf={arq_pdf}", arq_html.resolve().as_uri()],
                       check=False, capture_output=True, timeout=120)
        print(f"HTML: {arq_html}\nPDF : {arq_pdf if arq_pdf.exists() else '(Chrome não gerou)'}")

        depois = next((ln for ln in agendados(s, a.de, a.ate)
                       if str(ln.get("eap_id")) == str(esc["eap_id"])), None)
        print("depois:", resumo(depois) if depois else "(sumiu da lista)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
