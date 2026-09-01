# -*- coding: utf-8 -*-
"""MOLDE da réplica do classificador — roda LOCAL sobre o corpus gerado pelo dump_conversas.py.

ATENÇÃO: as condições abaixo são a fotografia de 01/09/2026. Antes de usar, ATUALIZE a lista
ASSUNTOS a partir da seção "ASSUNTOS" do dump_material.py (as condições mudam pela tela!) e
confira no RoboClassificador as regras de casamento vigentes. Em 01/09: PalavraChave = borda de
palavra sobre texto normalizado; Frase = substring; Regex = texto ORIGINAL com IgnoreCase;
Saudações usa UMA Regex de casamento integral; auto-resposta é curto-circuito ANTES do
classificador (RoboAtendimentoProcessador.PareceAutoResposta).

Uso:  PYTHONIOENCODING=utf-8 python replica_classificador.py conversas.txt
"""
import io, unicodedata, re, collections, sys


def norm(s):
    s = unicodedata.normalize("NFD", s)
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    return unicodedata.normalize("NFC", s).lower().strip()


RX_SAUD = re.compile(
    r"^\W*((oi+|ol[aá]|bom\s+dia+|boa\s+tarde+|boa\s+noite+|tudo\s+bem|obrigad[oa]s?|"
    r"muito\s+obrigad[oa]s?|de\s+nada|gratid[aã]o|am[eé]m|ok|blz|beleza|valeu|tá\s+bom|ta\s+bom)"
    r"[\s!.,?…]*)+\W*$", re.I)
AUTORESP = ["agradece seu contato", "responderemos assim que possivel",
            "nao estamos disponiveis no momento", "mensagem automatica"]

# >>> ATUALIZAR a partir do dump_material.py — (nome, [condições PalavraChave normalizadas]),
# na ordem da coluna `ordem`; Saudações entra com lista None (usa RX_SAUD). <<<
ASSUNTOS = [
    # ("Sintoma/urgência", ["passando mal", ...]),
    # ("Saudações", None),
]


def casa_palavra(alvo_norm, cond):
    return re.search(r"(?<![a-z0-9])" + re.escape(cond) + r"(?![a-z0-9])", alvo_norm) is not None


def classificar(texto):
    tn = norm(texto)
    if len(texto) >= 30 and (any(p in tn for p in AUTORESP)
                             or ("fora do horario de atendimento" in tn and "deixe sua mensagem" in tn)):
        return "AUTO-RESPOSTA (curto-circuito)", None
    for (nome, conds) in ASSUNTOS:
        if conds is None:
            if RX_SAUD.match(texto):
                return nome, "(regex integral)"
            continue
        for c in conds:
            if casa_palavra(tn, c):
                return nome, c
    return "(padrão)", None


corpus = []
lendo = False
for ln in io.open(sys.argv[1] if len(sys.argv) > 1 else "conversas.txt", encoding="utf-8"):
    if ln.startswith("CORPUS_30D"):
        lendo = True
        continue
    if not lendo or "\t" not in ln:
        continue
    ts, txt = ln.rstrip("\n").split("\t", 1)
    if txt.strip():
        corpus.append(txt.strip())

dist = collections.Counter()
gatilho = collections.Counter()
for t in corpus:
    nome, cond = classificar(t)
    dist[nome] += 1
    if cond:
        gatilho[(nome, cond)] += 1

print(f"corpus: {len(corpus)}")
for nome, n in dist.most_common():
    print(f"  {nome:30} {n:5} ({100 * n / len(corpus):.0f}%)")
print("\ngatilhos mais acionados:")
for ((nome, cond), n) in gatilho.most_common(20):
    print(f"  {n:4}  {nome:22} <- {cond}")
