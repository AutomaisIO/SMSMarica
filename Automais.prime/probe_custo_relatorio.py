"""Mede o CUSTO de puxar o clínico do Prime por relatório. SOMENTE LEITURA.

Duas vias, medidas separadamente:
  A) RelatorioPacientesAtendidosRPT — LOTE por dia+unidade. Traz o estruturado
     (CID, procedimentos SIGTAP, exames solicitados, medicamentos). 1 requisição por dia/unidade.
  B) RelatorioImpressaoSOAP — POR ATENDIMENTO (`?qId=&unidadeId=`). É o único que traz a NARRATIVA
     do SOAP (S/O/A/P). Custa 1 requisição por encontro.

Uso:  python probe_custo_relatorio.py [DD/MM/AAAA] [--qid GUID]
"""

from __future__ import annotations

import sys
import time

from prime.client import APP, CAP, PrimeSession

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

UNIDADE = "2b89b351-f048-492e-b28b-75c498fd04bc"  # CDT DR ALBERTO LUIS MACHADO BORGES


def medir(s: PrimeSession, rotulo: str, url: str):
    t0 = time.perf_counter()
    r = s.get(url)
    dt = time.perf_counter() - t0
    n = len(r.content or b"")
    ct = (r.headers.get("content-type") or "")[:40]
    cd = (r.headers.get("content-disposition") or "")[:60]
    print(f"  {rotulo:<34} {dt*1000:7.0f} ms  {n:>10,} bytes  {r.status_code}  {ct}")
    if cd:
        print(f"      {cd}")
    return r, dt, n


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    dia = args[0] if args else "22/09/2026"
    qid = None
    if "--qid" in sys.argv:
        qid = sys.argv[sys.argv.index("--qid") + 1]

    with PrimeSession() as s:
        s.entrar()
        print(f"\n=== A) LOTE do dia {dia} (um pedido cobre a unidade inteira) ===")
        base = (f"{APP}/Relatorios/RelatorioPacientesAtendidosRPT.aspx"
                f"?dataInicio={dia}&dataFim={dia}&funcao=&unidades={UNIDADE}")
        for ext in ("CSV", "PDF"):
            r, dt, n = medir(s, f"PacientesAtendidos {ext}", f"{base}&extensao={ext}")
            (CAP / f"custo_atendidos_{ext.lower()}").write_bytes(r.content or b"")
            if ext == "CSV" and r.content:
                txt = r.content.decode("utf-8-sig", errors="replace")
                linhas = [l for l in txt.splitlines() if l.strip()]
                print(f"      linhas: {len(linhas)}   (1a col do cabeçalho: {linhas[0][:60] if linhas else '?'})")

        if qid:
            print("\n=== B) NARRATIVA do SOAP (por atendimento) ===")
            r, dt, n = medir(s, "ImpressaoSOAP (qId capturado)",
                             f"{APP}/Relatorios/RelatorioImpressaoSOAP.aspx?qId={qid}&unidadeId={UNIDADE}")
            (CAP / "custo_soap_print.bin").write_bytes(r.content or b"")
            corpo = (r.content or b"").decode("utf-8", errors="replace")
            marca = [t for t in ("Subjetivo", "Objetivo", "Avalia", "Plano", "SOAP") if t.lower() in corpo.lower()]
            print(f"      traz seções do SOAP? {marca or 'NAO — provavelmente o qId expirou'}")


if __name__ == "__main__":
    main()
