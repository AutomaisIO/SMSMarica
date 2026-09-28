"""Normaliza o relatório *Pacientes Atendidos* do Prime num registro por ATENDIMENTO.

É a via de ingestão do clínico do Prime que NÃO depende da extensão estar instalada no PC:
uma requisição GET por dia × unidade (medido 23/09/2026: ~248 ms, ~135 KB, 227 atendimentos).

O que o relatório dá e o que NÃO dá (medido 23/09/2026, Ambulatório, 22/09):
- DÁ: paciente (nome/sexo/nascimento/CNS/endereço+telefone), profissional, função, demanda,
  CID, procedimentos SIGTAP com quantidade, exames solicitados, medicamentos, e o TEMPO.
- NÃO DÁ: a narrativa do SOAP (só o `RelatorioImpressaoSOAP.aspx?qId=`, 1 requisição por
  atendimento), nem o `atendimentoId`/GUID do paciente — por isso a chave aqui é SINTÉTICA.

O TEMPO, que é a parte contra-intuitiva:
- `DataInicio` e `DataFim` trazem só a DATA — a hora vem zerada (`00:00:00`) nos 227. Não servem.
- `DataRegistro` traz hora de verdade em 100% e é o FECHAMENTO do registro. Conferido em
  23/09/2026 pela linha do tempo de cada profissional: o intervalo entre registros consecutivos
  comporta a duração do seguinte em 129 dos 170 pares; os 41 restantes se espalham por TODAS as
  funções na proporção do volume — é o profissional fechando registros em fila, não erro.
- Então: `fim = DataRegistro` e `inicio = fim - Duracao`.

Preenchimento, lido pela função do atendimento (misturar as duas subestima o clínico):
- linhas `Procedimento` (129/227) são acolhimento/escuta inicial: sem CID, exame ou medicamento
  POR NATUREZA — não é dado faltando.
- linhas de CONSULTA (98/227): CID em 98/98 (100%), exame solicitado em 29, medicamento em 0.
"""

from __future__ import annotations

import hashlib
import re
from datetime import datetime, timedelta

from .relatorio_csv import conferir, ler

APP_REL = "/Prime/Relatorios/RelatorioPacientesAtendidosRPT.aspx"


def duracao_segundos(v: str | None) -> int:
    """'1min 2s' / '50s' / '1h 5min' -> segundos."""
    v = (v or "").strip()
    t = 0
    for padrao, mult in ((r"(\d+)\s*h", 3600), (r"(\d+)\s*min", 60), (r"(\d+)\s*s(?!\w)", 1)):
        m = re.search(padrao, v)
        if m:
            t += int(m.group(1)) * mult
    return t


def _dt(v: str | None) -> datetime | None:
    try:
        return datetime.strptime((v or "").strip(), "%d/%m/%Y %H:%M:%S")
    except ValueError:
        return None


def _procedimentos(codificado: str | None) -> list[dict]:
    """`<procedimento>  0301040079 - NOME - Quantidade: 1\n</procedimento>` (repetido) -> lista."""
    itens = []
    for bruto in re.findall(r"<procedimento>(.*?)</procedimento>", codificado or "", re.S):
        txt = " ".join(bruto.split())
        m = re.match(r"^(\d{6,12})\s*-\s*(.*?)(?:\s*-\s*Quantidade:\s*(\d+))?$", txt)
        if m:
            itens.append({"codigo": m.group(1), "nome": m.group(2).strip(),
                          "quantidade": int(m.group(3) or 1)})
        elif txt:
            itens.append({"codigo": None, "nome": txt.lstrip(", ").strip(), "quantidade": 1})
    return itens


def _cids(v: str | None) -> list[dict]:
    saida = []
    for linha in (v or "").replace("\r", "\n").split("\n"):
        linha = linha.strip(" ,")
        if not linha:
            continue
        m = re.match(r"^([A-Z]\d{2,4}|\d{2,4})\s*-\s*(.+)$", linha)
        saida.append({"codigo": m.group(1), "descricao": m.group(2).strip()} if m
                     else {"codigo": None, "descricao": linha})
    return saida


def _lista(v: str | None) -> list[str]:
    return [x.strip() for x in (v or "").replace("\r", "\n").split("\n") if x.strip()]


def chave_natural(unidade_id: str, reg: dict) -> str:
    """(unidade, fechamento, profissional, paciente) — a identidade "óbvia" do atendimento, já que
    o relatório NÃO traz o `atendimentoId`.

    **Ela sozinha NÃO basta**, e isso foi medido: em 120.832 atendimentos históricos, 15 pares
    caem na mesma chave natural, e 14 deles são atendimentos DIFERENTES de verdade (mesmo paciente,
    mesmo profissional, mesmo segundo, procedimento SIGTAP diferente — ex. `0301100039` × `0301100250`).
    Usar só isto fundiria atendimento real. Por isso `atribuir_chaves()` acrescenta um ordinal."""
    crua = "|".join([unidade_id, reg.get("DataRegistro", ""), reg.get("prof_nome", ""),
                     reg.get("CNS") or reg.get("pac_nome", ""), reg.get("Data_Nasc", "")])
    return hashlib.sha1(crua.encode("utf-8")).hexdigest()


def _digest_conteudo(a: dict) -> str:
    """Só o que distingue dois atendimentos do mesmo segundo — NÃO entra na chave final, serve
    apenas para ordenar o grupo de forma determinística entre execuções."""
    partes = [a["funcao"], str(a["duracao_s"])]
    partes += sorted(f"{p['codigo']}x{p['quantidade']}" for p in a["procedimentos"])
    partes += sorted(c["codigo"] or c["descricao"] for c in a["cids"])
    return "|".join(partes)


def atribuir_chaves(atendimentos: list[dict]) -> list[dict]:
    """Fecha a `chave` = natural + ordinal dentro do grupo que compartilha a natural.

    Por que ordinal e não hash do conteúdo: se o profissional EDITAR o atendimento depois (juntar
    um CID, corrigir procedimento), um hash de conteúdo mudaria a chave e o hub ganharia um
    atendimento fantasma em vez de atualizar o que já tem. O ordinal mantém a identidade estável
    através da edição. O preço: se dois atendimentos do MESMO segundo trocarem de ordem entre duas
    importações (só pode acontecer se o conteúdo de um deles mudar), eles trocam de chave — 15
    grupos em 120.832 atendimentos, e a consequência é uma atualização cruzada, não perda."""
    grupos: dict[str, list[dict]] = {}
    for a in atendimentos:
        grupos.setdefault(a["chave_natural"], []).append(a)
    for natural, itens in grupos.items():
        if len(itens) == 1:
            itens[0]["chave"] = hashlib.sha1(f"{natural}#0".encode()).hexdigest()
            continue
        for i, a in enumerate(sorted(itens, key=_digest_conteudo)):
            a["chave"] = hashlib.sha1(f"{natural}#{i}".encode()).hexdigest()
            a["mesmo_segundo"] = len(itens)  # bandeira para quem for conferir no hub
    return atendimentos


def normalizar(reg: dict, unidade_id: str, unidade_nome: str = "") -> dict:
    fim = _dt(reg.get("DataRegistro"))
    dur = duracao_segundos(reg.get("Duracao"))
    nasc = _dt(reg.get("Data_Nasc"))
    funcao = (reg.get("FuncaoAtendimento") or "").strip()
    return {
        "chave": None,  # fechada por `atribuir_chaves()`, que precisa ver o lote inteiro
        "chave_natural": chave_natural(unidade_id, reg),
        "unidade_id": unidade_id,
        "unidade": unidade_nome,
        "funcao": funcao,
        "acolhimento": funcao == "Procedimento",  # sem clínico por natureza
        "demanda": (reg.get("Demanda") or "").strip() or None,
        "profissional": (reg.get("prof_nome") or "").strip() or None,
        "paciente": {
            "nome": (reg.get("pac_nome") or "").strip() or None,
            "nome_social": (reg.get("pac_nomeSocial") or "").strip() or None,
            "sexo": (reg.get("pac_sexo") or "").strip() or None,
            "nascimento": nasc.date().isoformat() if nasc else None,
            "cns": (reg.get("CNS") or "").strip() or None,
            "endereco_telefone": (reg.get("EnderecoTelefone") or "").strip() or None,
        },
        "inicio": (fim - timedelta(seconds=dur)).isoformat() if fim else None,
        "fim": fim.isoformat() if fim else None,
        "duracao_s": dur,
        "cids": _cids(reg.get("Diagnosticos")),
        "procedimentos": _procedimentos(reg.get("Cod_Procedimentos")),
        "exames_solicitados": _lista(reg.get("ExamesSolicitados")),
        "medicamentos": _lista(reg.get("MedicamentosPrescritos")),
    }


def do_csv(texto: str, unidade_id: str, unidade_nome: str = "") -> tuple[list[dict], dict]:
    """(atendimentos normalizados, conferência do parser). SEMPRE olhar a conferência: o CSV do
    Prime é malformado e um parser ingênuo desalinha as colunas EM SILÊNCIO."""
    colunas, registros = ler(texto)
    aferido = conferir(colunas, registros)
    ats = atribuir_chaves([normalizar(r, unidade_id, unidade_nome) for r in registros])
    return ats, aferido


def baixar(sessao, unidade_id: str, dia_br: str, ate_br: str | None = None) -> str:
    """GET do relatório em CSV para uma unidade. SOMENTE LEITURA.

    `dia_br` sozinho = um dia. Com `ate_br`, o relatório aceita uma JANELA LARGA numa requisição
    só — medido 23/09/2026: o ano de 2026 inteiro do CDT (3.942 atendimentos) veio em 3,2 s / 2,2 MB.
    É o que torna o backfill histórico barato: 1 requisição por unidade em vez de 1 por dia.
    """
    r = sessao.get(APP_REL, params={"dataInicio": dia_br, "dataFim": ate_br or dia_br, "funcao": "",
                                    "prof": "", "idGrupoPrioritario": "", "extensao": "CSV",
                                    "unidades": unidade_id})
    if "csv" not in (r.headers.get("content-type") or ""):
        raise RuntimeError(f"resposta não é CSV ({r.status_code} "
                           f"{r.headers.get('content-type')}) — sessão pode ter expirado")
    return (r.content or b"").decode("utf-8-sig", errors="replace")
