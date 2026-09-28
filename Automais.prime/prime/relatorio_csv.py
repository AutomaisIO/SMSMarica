"""Leitor do CSV dos relatórios do Prime — que é MALFORMADO e quebra parser padrão.

O arquivo declara N colunas separadas por `;` mas **não usa aspas em lugar nenhum**, e os campos
`<procedimento>…</procedimento>` contêm QUEBRA DE LINHA crua. Resultado: um registro lógico se
espalha por várias linhas físicas e `csv.DictReader` desalinha as colunas EM SILÊNCIO — o valor da
duração aparece na coluna de diagnóstico, e a leitura parece boa mas está toda trocada.
(Medido 23/09/2026: cabeçalho com 22 colunas / 21 `;`, mas a maioria das linhas físicas com 11.)

A remontagem é determinística: acumula linhas físicas até o registro ter os 21 `;` do cabeçalho.
"""

from __future__ import annotations


def ler(texto: str, sep: str = ";") -> tuple[list[str], list[dict]]:
    """(colunas, registros). Remonta os registros quebrados por quebra de linha sem aspas."""
    linhas = texto.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    if not linhas:
        return [], []
    colunas = linhas[0].split(sep)
    alvo = len(colunas) - 1  # separadores esperados por registro

    registros: list[dict] = []
    buffer = ""
    for linha in linhas[1:]:
        buffer = linha if not buffer else buffer + "\n" + linha
        if buffer.count(sep) < alvo:
            continue  # registro ainda incompleto — a quebra era dentro de um campo
        partes = buffer.split(sep)
        if len(partes) > len(colunas):  # separador a mais dentro de um campo: junta a sobra na última
            partes = partes[: len(colunas) - 1] + [sep.join(partes[len(colunas) - 1 :])]
        registros.append({c: (v or "").strip() for c, v in zip(colunas, partes)})
        buffer = ""
    if buffer.strip():  # sobra (registro truncado no fim do arquivo)
        partes = (buffer.split(sep) + [""] * len(colunas))[: len(colunas)]
        registros.append({c: (v or "").strip() for c, v in zip(colunas, partes)})
    return colunas, registros


def conferir(colunas: list[str], registros: list[dict]) -> dict:
    """Sinais de que a remontagem deu certo — sempre conferir antes de confiar na importação."""
    if not registros:
        # Unidade sem movimento no dia e LEGITIMO (as USFs vivem no Klinikos e vem sempre vazias).
        # Vazio nao e falha de parser — marcar como falha aqui gerava 28 alertas falsos por noite.
        return {"registros": 0, "colunas": len(colunas), "vazio": True, "ok": True}
    # heurística barata: a coluna de duração deve conter duração, não CID, e vice-versa
    dur = sum(1 for r in registros if any(x in (r.get("Duracao") or "") for x in ("s", "min", "h")))
    diag = sum(1 for r in registros if (r.get("Diagnosticos") or "").strip())
    suspeito = sum(1 for r in registros
                   if any(x in (r.get("Diagnosticos") or "") for x in ("min ", "seg", "s")) and
                   len((r.get("Diagnosticos") or "")) < 12)
    return {
        "registros": len(registros),
        "colunas": len(colunas),
        "com_duracao": dur,
        "com_diagnostico": diag,
        "diagnostico_parecendo_duracao": suspeito,
        "ok": suspeito == 0,
    }
