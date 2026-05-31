"""Sweep das pendências da seção 13: várias investigações em um script só.

Cada bloco dump em arquivo próprio em `capturas/`. Tudo read-only.
"""
from __future__ import annotations

import pathlib
import sys

from conexao import executar_select

_SAIDA = pathlib.Path(__file__).resolve().parent.parent / "capturas"


def dump(arq: str, header: str, sql: str, binds: dict | None = None, modo: str = "obs", timeout: int = 60) -> None:
    print(f"\n=== {header} ===")
    try:
        linhas, cols = executar_select(sql, binds, modo=modo, timeout=timeout)
    except Exception as e:
        print(f"FALHOU: {e}")
        with (_SAIDA / arq).open("a", encoding="utf-8") as f:
            f.write(f"\n# {header}\nFALHOU: {e}\n")
        return
    with (_SAIDA / arq).open("a", encoding="utf-8") as f:
        f.write(f"\n# {header}\n")
        f.write(" | ".join(cols) + "\n")
        f.write("-" * 80 + "\n")
        for l in linhas:
            f.write(" | ".join(l) + "\n")
        f.write(f"({len(linhas)} linhas)\n")
    print(f"OK {len(linhas)} linhas -> {arq}")


def edoc_regra():
    arq = "investig_edoc_regra.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    dump(arq, "EDOC_REGRA colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='EDOC_REGRA' ORDER BY column_id")
    dump(arq, "EDOC_REGRA_CONDICAO colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='EDOC_REGRA_CONDICAO' ORDER BY column_id")
    dump(arq, "EDOC_REGRA_FORMULA colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='EDOC_REGRA_FORMULA' ORDER BY column_id")
    dump(arq, "EDOC_REGRA sample 10",
         "SELECT * FROM edoc_regra WHERE ROWNUM <= 10", modo="supervisor")
    dump(arq, "EDOC_REGRA_CONDICAO sample 10",
         "SELECT * FROM edoc_regra_condicao WHERE ROWNUM <= 10", modo="supervisor")
    dump(arq, "EDOC_REGRA_FORMULA todas (10 linhas)",
         "SELECT * FROM edoc_regra_formula", modo="supervisor")


def edoc_script():
    arq = "investig_edoc_script.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    dump(arq, "EDOC_SCRIPT colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='EDOC_SCRIPT' ORDER BY column_id")
    dump(arq, "EDOC_SCRIPT 20 mais recentes (sem body LONG)",
         "SELECT cd_script, ds_script, in_ativo, dt_inclusao, cd_funcionario_inc "
         "FROM edoc_script ORDER BY dt_inclusao DESC FETCH FIRST 20 ROWS ONLY",
         modo="supervisor")


def dominios():
    arq = "investig_dominios.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    dump(arq, "PRESCRICAO_FIA: distinct ID_TIPO_PRESCRICAO + counts",
         "SELECT id_tipo_prescricao, COUNT(*) qtd "
         "FROM prescricao_fia GROUP BY id_tipo_prescricao ORDER BY qtd DESC",
         modo="supervisor", timeout=120)
    dump(arq, "PRESCRICAO_BAA: distinct ID_TIPO_PRESCRICAO",
         "SELECT id_tipo_prescricao, COUNT(*) qtd "
         "FROM prescricao_baa GROUP BY id_tipo_prescricao ORDER BY qtd DESC",
         modo="supervisor", timeout=120)
    dump(arq, "EDOC_MOVIMENTO: distinct IN_STATUS",
         "SELECT in_status, COUNT(*) qtd "
         "FROM edoc_movimento GROUP BY in_status ORDER BY qtd DESC",
         modo="supervisor", timeout=180)
    dump(arq, "EDOC_MOVIMENTO: distinct IN_ATIVO",
         "SELECT in_ativo, COUNT(*) qtd FROM edoc_movimento GROUP BY in_ativo",
         modo="supervisor", timeout=180)
    dump(arq, "BAA: distinct IN_BAA_ATENDIDO",
         "SELECT in_baa_atendido, COUNT(*) FROM baa GROUP BY in_baa_atendido",
         modo="supervisor", timeout=180)
    dump(arq, "BAA: distinct ID_DESTINO",
         "SELECT id_destino, COUNT(*) FROM baa GROUP BY id_destino ORDER BY 2 DESC",
         modo="supervisor", timeout=180)
    dump(arq, "FIA: distinct ID_INTERNACAO",
         "SELECT id_internacao, COUNT(*) FROM fia GROUP BY id_internacao",
         modo="supervisor", timeout=180)
    dump(arq, "FIA: distinct ID_TP_PACIENTE",
         "SELECT id_tp_paciente, COUNT(*) FROM fia GROUP BY id_tp_paciente",
         modo="supervisor", timeout=180)


def leito_quarto():
    arq = "investig_leito_quarto.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    dump(arq, "Tabelas LEITO/QUARTO em INFOSAUDE",
         "SELECT table_name, num_rows FROM dba_tables "
         "WHERE owner='INFOSAUDE' AND (table_name LIKE '%LEITO%' OR table_name LIKE '%QUARTO%') "
         "ORDER BY table_name")
    dump(arq, "LEITO colunas (se existir)",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='LEITO' ORDER BY column_id")
    dump(arq, "QUARTO colunas (se existir)",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='QUARTO' ORDER BY column_id")
    dump(arq, "FIA_LEITO colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='FIA_LEITO' ORDER BY column_id")


def faturamento_sus():
    arq = "investig_faturamento_sus.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    for nome, padrao in [("SUS_", "SUS_%"), ("_SUS", "%\\_SUS"), ("BPA", "%BPA%"),
                         ("APAC", "%APAC%"), ("AIH", "%AIH%"), ("TISS", "TISS%"),
                         ("SIA", "%SIA%"), ("CNES", "%CNES%")]:
        dump(arq, f"Tabelas LIKE '{padrao}'",
             "SELECT table_name, num_rows FROM dba_tables "
             "WHERE owner='INFOSAUDE' AND table_name LIKE :p ESCAPE '\\' "
             "AND num_rows > 0 ORDER BY num_rows DESC",
             {"p": padrao})


def pacs_dicom():
    arq = "investig_pacs.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    dump(arq, "Tabelas LIKE PACS/DICOM/IMAGEM",
         "SELECT table_name, num_rows FROM dba_tables "
         "WHERE owner='INFOSAUDE' AND (table_name LIKE '%PACS%' OR table_name LIKE '%DICOM%' "
         "OR table_name LIKE '%IMAGEM%' OR table_name LIKE '%IMAGE%') ORDER BY table_name")
    dump(arq, "Colunas com 'url' no nome em INFOSAUDE",
         "SELECT table_name, column_name, data_type FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND column_name LIKE '%URL%' ORDER BY table_name, column_name")
    dump(arq, "LAUDO_CONSOLIDADO colunas",
         "SELECT column_id, column_name, data_type FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='LAUDO_CONSOLIDADO' ORDER BY column_id")
    dump(arq, "LAUDO_CONSOLIDADO sample",
         "SELECT * FROM laudo_consolidado", modo="supervisor")


def funcoes_f():
    arq = "investig_funcoes.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    dump(arq, "Funções/procedures F_* em INFOSAUDE (resumo)",
         "SELECT DISTINCT name, type FROM dba_source "
         "WHERE owner='INFOSAUDE' AND name LIKE 'F\\_%' ESCAPE '\\' "
         "ORDER BY name FETCH FIRST 100 ROWS ONLY", timeout=120)


def chamada_paciente():
    arq = "investig_chamada_paciente.txt"
    (_SAIDA / arq).write_text("", encoding="utf-8")
    dump(arq, "CHAMADA_PACIENTE colunas",
         "SELECT column_id, column_name, data_type, data_length FROM dba_tab_columns "
         "WHERE owner='INFOSAUDE' AND table_name='CHAMADA_PACIENTE' ORDER BY column_id")
    dump(arq, "CHAMADA_PACIENTE 5 mais recentes",
         "SELECT id_chamada_paciente, cd_hospital, dt_ano_baa, nr_baa, "
         "nm_host_emissor, nm_host_receptor, cd_sala_painel, ds_sala_painel, "
         "dt_hr_chamada, dt_hr_comparecimento, dt_hr_cancelada, "
         "cd_funcionario, in_status, cd_paciente, primeiro_nm_paciente, senha "
         "FROM chamada_paciente "
         "ORDER BY dt_hr_chamada DESC FETCH FIRST 5 ROWS ONLY",
         modo="supervisor", timeout=120)
    dump(arq, "CHAMADA_PACIENTE distinct IN_STATUS",
         "SELECT in_status, COUNT(*) FROM chamada_paciente "
         "GROUP BY in_status ORDER BY 2 DESC",
         modo="supervisor", timeout=120)


if __name__ == "__main__":
    bloco = sys.argv[1] if len(sys.argv) > 1 else "tudo"
    mapa = {
        "edoc_regra": edoc_regra,
        "edoc_script": edoc_script,
        "dominios": dominios,
        "leito": leito_quarto,
        "sus": faturamento_sus,
        "pacs": pacs_dicom,
        "funcoes": funcoes_f,
        "chamada": chamada_paciente,
    }
    if bloco == "tudo":
        for n, fn in mapa.items():
            print(f"\n\n>>> {n.upper()}")
            fn()
    elif bloco in mapa:
        mapa[bloco]()
    else:
        print(f"bloco desconhecido: {bloco}. Opções: {list(mapa)}")
