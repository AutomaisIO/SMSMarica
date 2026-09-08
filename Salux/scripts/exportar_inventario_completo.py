#!/usr/bin/env python3
"""Exporta TODO o material do Salux (schema INFOSAUDE), de todas as camadas que guardam
item, num CSV unico com ORIGEM_TABELA + TIPO_REGISTRO + CONTA_NO_TOTAL.

Uso: python3 exportar_inventario_completo.py [saida.csv] [resumo.csv]

Somente leitura (proxy SQL interno, guard read-only). Abrange os 3 hospitais do Salux.
"""
import csv, json, sys, urllib.request

TOKEN = open('/etc/smsmarica-server/proxy-sql.env').read().split('=', 1)[1].strip()
PAGINA = 4000  # o proxy corta em 5000 linhas por consulta


def q(sql):
    corpo = json.dumps({'base': 'salux-hcml', 'consultas': [sql], 'maxLinhas': 200000}).encode()
    req = urllib.request.Request('http://127.0.0.1:5091/proxy-sql', data=corpo,
        headers={'Content-Type': 'application/json', 'X-Proxy-Token': TOKEN})
    try:
        with urllib.request.urlopen(req, timeout=900) as r:
            return json.load(r)['resultados'][0]
    except urllib.error.HTTPError as e:
        print('HTTP %d: %s' % (e.code, e.read().decode()[:800]))
        print('--- SQL ---')
        print(sql[:3000])
        raise


def paginado(sql):
    linhas, offset = [], 0
    while True:
        res = q('SELECT * FROM (' + sql + ') OFFSET %d ROWS FETCH NEXT %d ROWS ONLY' % (offset, PAGINA))
        linhas.extend(res['linhas'])
        if len(res['linhas']) < PAGINA:
            return linhas
        offset += PAGINA


# ------------------------------------------------------------------ blocos comuns
TIPO_ITEM = """CASE m.cd_gr_estocagem
        WHEN 14 THEN 'MEDICAMENTO' WHEN 20 THEN 'MEDICAMENTO_CONTROLADO'
        WHEN 21 THEN 'MEDICAMENTO_ANTIRRETROVIRAL'
        WHEN 10 THEN 'MATERIAL_MEDICO_HOSPITALAR' WHEN 16 THEN 'OPME'
        WHEN 19 THEN 'MATERIAL_ODONTOLOGICO' WHEN 9 THEN 'INSTRUMENTAL_CIRURGICO'
        WHEN 1 THEN 'DIETA_NUTRICAO' WHEN 7 THEN 'GENERO_ALIMENTICIO'
        WHEN 22 THEN 'SUPLEMENTO_ALIMENTAR' WHEN 6 THEN 'GAS_MEDICINAL'
        WHEN 2 THEN 'EPI' WHEN 4 THEN 'EQUIPAMENTO_MEDICO' WHEN 5 THEN 'EQUIPAMENTO_MANUTENCAO'
        WHEN 3 THEN 'EQUIPAMENTO_INFORMATICA' WHEN 15 THEN 'MOVEL_UTENSILIO'
        WHEN 11 THEN 'MATERIAL_EXPEDIENTE' WHEN 8 THEN 'IMPRESSO'
        WHEN 12 THEN 'HIGIENE_LIMPEZA' WHEN 13 THEN 'MATERIAL_MANUTENCAO'
        WHEN 17 THEN 'ROUPARIA' WHEN 18 THEN 'UNIFORME'
        ELSE DECODE(m.in_medicamento, 'S', 'MEDICAMENTO', 'OUTRO') END"""


def situacao(col):
    return ("CASE WHEN %s IS NULL THEN 'SEM_VALIDADE' "
            "WHEN %s < TRUNC(SYSDATE) THEN 'VENCIDO' "
            "WHEN %s < TRUNC(SYSDATE)+90 THEN 'VENCE_EM_ATE_90D' ELSE 'OK' END" % (col, col, col))


CAT = ("um.sigla AS unidade, " + TIPO_ITEM + " AS tipo_item, g.ds_gr_estocagem AS classificacao, "
       "sg.ds_sub_gr_estocagem AS subgrupo, gc.ds_gr_compra AS grupo_compra")

FLAGS = ("m.in_medicamento, m.in_controlado, m.in_antibiotico, m.in_patrimonio, "
         "m.in_consignado, m.in_kit, m.in_ativo AS in_ativo_item, "
         "TO_CHAR(m.nr_registro_ms) AS registro_ms, m.cd_dcb")

JOINS = """JOIN matmed m ON m.cd_material = x.cd_material
  LEFT JOIN gr_estocagem g ON g.cd_gr_estocagem = m.cd_gr_estocagem
  LEFT JOIN sub_gr_estocagem sg ON sg.cd_gr_estocagem = m.sub_cd_gr_estocagem
       AND sg.cd_sub_gr_estocagem = m.cd_sub_gr_estocagem
  LEFT JOIN gr_compra gc ON gc.cd_gr_compra = m.cd_gr_compra
  LEFT JOIN unidade_medida um ON um.cd_unidade_medida = m.cd_unidade_medida"""

CUSTO = 'NVL(mh.vl_custo_medio, m.vl_custo_medio)'
AQUIS = 'NVL(mh.vl_custo_aquisicao, m.vl_custo_aquisicao)'

TEM_LOTE = """EXISTS (SELECT 1 FROM matmed_lote l WHERE l.cd_hospital = x.cd_hospital
          AND l.cd_estoque = x.cd_estoque AND l.cd_material = x.cd_material AND l.qt_estoque <> 0)"""

# ------------------------------------------------------------------ camadas
C_SALDO_LOTE = """
SELECT 'MATMED_LOTE' AS origem_tabela, 'SALDO_LOTE' AS tipo_registro, 'S' AS conta_no_total,
       x.cd_hospital, h.ds_hospital, x.cd_estoque, e.ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       x.sc_lote AS lote, TO_CHAR(x.dt_validade,'YYYY-MM-DD') AS validade,
       """ + situacao('x.dt_validade') + """ AS situacao_validade,
       x.qt_estoque AS quantidade, """ + CUSTO + """ AS valor_unitario,
       ROUND(x.qt_estoque * """ + CUSTO + """, 2) AS valor_total,
       """ + AQUIS + """ AS valor_aquisicao_unit,
       ma.ds_marca AS marca, lb.ds_laboratorio AS laboratorio,
       TRIM(NVL(me.sc_sala,' ')||' '||NVL(me.sc_estante,' ')||' '||NVL(me.sc_prateleira,' ')) AS localizacao,
       """ + FLAGS + """, CAST(NULL AS VARCHAR2(200)) AS referencia,
       CAST(NULL AS VARCHAR2(20)) AS data_ref
  FROM matmed_lote x """ + JOINS + """
  LEFT JOIN hospital h ON h.cd_hospital = x.cd_hospital
  LEFT JOIN estoque e ON e.cd_hospital = x.cd_hospital AND e.cd_estoque = x.cd_estoque
  LEFT JOIN matmed_hospital mh ON mh.cd_hospital = x.cd_hospital AND mh.cd_material = x.cd_material
  LEFT JOIN matmed_estoque me ON me.cd_hospital = x.cd_hospital AND me.cd_estoque = x.cd_estoque
       AND me.cd_material = x.cd_material
  LEFT JOIN marca ma ON ma.cd_marca = x.cd_marca
  LEFT JOIN laboratorio lb ON lb.cd_laboratorio = x.cd_laboratorio
 WHERE x.qt_estoque <> 0
 ORDER BY x.cd_hospital, x.cd_estoque, x.cd_material, x.dt_validade, x.sc_lote"""

C_SALDO_ESTOQUE = """
SELECT 'MATMED_ESTOQUE' AS origem_tabela,
       CASE WHEN """ + TEM_LOTE + """ THEN 'SALDO_ESTOQUE_COM_LOTE' ELSE 'SALDO_SEM_LOTE' END AS tipo_registro,
       CASE WHEN """ + TEM_LOTE + """ THEN 'N' ELSE 'S' END AS conta_no_total,
       x.cd_hospital, h.ds_hospital, x.cd_estoque, e.ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       CAST(NULL AS VARCHAR2(20)) AS lote, CAST(NULL AS VARCHAR2(10)) AS validade,
       'SEM_VALIDADE' AS situacao_validade,
       x.qt_estoque AS quantidade, """ + CUSTO + """ AS valor_unitario,
       ROUND(x.qt_estoque * """ + CUSTO + """, 2) AS valor_total,
       """ + AQUIS + """ AS valor_aquisicao_unit,
       CAST(NULL AS VARCHAR2(60)) AS marca, CAST(NULL AS VARCHAR2(60)) AS laboratorio,
       TRIM(NVL(x.sc_sala,' ')||' '||NVL(x.sc_estante,' ')||' '||NVL(x.sc_prateleira,' ')) AS localizacao,
       """ + FLAGS + """, CAST(NULL AS VARCHAR2(200)) AS referencia,
       CAST(NULL AS VARCHAR2(20)) AS data_ref
  FROM matmed_estoque x """ + JOINS + """
  LEFT JOIN hospital h ON h.cd_hospital = x.cd_hospital
  LEFT JOIN estoque e ON e.cd_hospital = x.cd_hospital AND e.cd_estoque = x.cd_estoque
  LEFT JOIN matmed_hospital mh ON mh.cd_hospital = x.cd_hospital AND mh.cd_material = x.cd_material
 WHERE x.qt_estoque <> 0
 ORDER BY x.cd_hospital, x.cd_estoque, x.cd_material"""

C_CONSIGNADO = """
SELECT 'MATMED_ESTOQUE' AS origem_tabela, 'CONSIGNADO' AS tipo_registro, 'N' AS conta_no_total,
       x.cd_hospital, h.ds_hospital, x.cd_estoque, e.ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       CAST(NULL AS VARCHAR2(20)) AS lote, CAST(NULL AS VARCHAR2(10)) AS validade,
       'SEM_VALIDADE' AS situacao_validade,
       x.qt_consignado AS quantidade, """ + CUSTO + """ AS valor_unitario,
       ROUND(x.qt_consignado * """ + CUSTO + """, 2) AS valor_total,
       """ + AQUIS + """ AS valor_aquisicao_unit,
       CAST(NULL AS VARCHAR2(60)) AS marca, CAST(NULL AS VARCHAR2(60)) AS laboratorio,
       TRIM(NVL(x.sc_sala,' ')||' '||NVL(x.sc_estante,' ')||' '||NVL(x.sc_prateleira,' ')) AS localizacao,
       """ + FLAGS + """, 'material de terceiro em consignacao' AS referencia,
       CAST(NULL AS VARCHAR2(20)) AS data_ref
  FROM matmed_estoque x """ + JOINS + """
  LEFT JOIN hospital h ON h.cd_hospital = x.cd_hospital
  LEFT JOIN estoque e ON e.cd_hospital = x.cd_hospital AND e.cd_estoque = x.cd_estoque
  LEFT JOIN matmed_hospital mh ON mh.cd_hospital = x.cd_hospital AND mh.cd_material = x.cd_material
 WHERE NVL(x.qt_consignado,0) <> 0
 ORDER BY x.cd_hospital, x.cd_estoque, x.cd_material"""

C_CONSOLIDADO = """
SELECT 'MATMED_HOSPITAL' AS origem_tabela, 'CONSOLIDADO_HOSPITAL' AS tipo_registro,
       'N' AS conta_no_total,
       x.cd_hospital, h.ds_hospital, CAST(NULL AS NUMBER) AS cd_estoque,
       'TODOS OS ESTOQUES' AS ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       CAST(NULL AS VARCHAR2(20)) AS lote, CAST(NULL AS VARCHAR2(10)) AS validade,
       'SEM_VALIDADE' AS situacao_validade,
       x.qt_estoque AS quantidade, NVL(x.vl_custo_medio, m.vl_custo_medio) AS valor_unitario,
       ROUND(x.qt_estoque * NVL(x.vl_custo_medio, m.vl_custo_medio), 2) AS valor_total,
       NVL(x.vl_custo_aquisicao, m.vl_custo_aquisicao) AS valor_aquisicao_unit,
       CAST(NULL AS VARCHAR2(60)) AS marca, CAST(NULL AS VARCHAR2(60)) AS laboratorio,
       CAST(NULL AS VARCHAR2(60)) AS localizacao,
       """ + FLAGS + """,
       'totalizador do hospital (nao somar com as camadas de saldo)' AS referencia,
       TO_CHAR(x.dt_atualizacao,'YYYY-MM-DD') AS data_ref
  FROM matmed_hospital x """ + JOINS + """
  LEFT JOIN hospital h ON h.cd_hospital = x.cd_hospital
 WHERE NVL(x.qt_estoque,0) <> 0
 ORDER BY x.cd_hospital, x.cd_material"""

C_CATALOGO = """
SELECT 'MATMED' AS origem_tabela, 'CATALOGO' AS tipo_registro, 'N' AS conta_no_total,
       CAST(NULL AS NUMBER) AS cd_hospital, 'CADASTRO (todos os hospitais)' AS ds_hospital,
       CAST(NULL AS NUMBER) AS cd_estoque, CAST(NULL AS VARCHAR2(60)) AS ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       CAST(NULL AS VARCHAR2(20)) AS lote, CAST(NULL AS VARCHAR2(10)) AS validade,
       'SEM_VALIDADE' AS situacao_validade,
       m.qt_estoque AS quantidade, m.vl_custo_medio AS valor_unitario,
       ROUND(NVL(m.qt_estoque,0) * NVL(m.vl_custo_medio,0), 2) AS valor_total,
       m.vl_custo_aquisicao AS valor_aquisicao_unit,
       ma.ds_marca AS marca, lb.ds_laboratorio AS laboratorio,
       CAST(NULL AS VARCHAR2(60)) AS localizacao,
       """ + FLAGS + """, SUBSTR(m.ds_completa, 1, 200) AS referencia,
       TO_CHAR(m.dt_cadastro,'YYYY-MM-DD') AS data_ref
  FROM (SELECT cd_material FROM matmed) x """ + JOINS + """
  LEFT JOIN marca ma ON ma.cd_marca = m.cd_marca
  LEFT JOIN laboratorio lb ON lb.cd_laboratorio = m.cd_laboratorio
 ORDER BY x.cd_material"""

C_EMPRESTIMO = """
SELECT 'EMPRESTIMO_ITEM' AS origem_tabela, 'EMPRESTIMO_SALDO' AS tipo_registro,
       'N' AS conta_no_total,
       x.cd_hospital, h.ds_hospital, em.cd_estoque, e.ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       CAST(NULL AS VARCHAR2(20)) AS lote, CAST(NULL AS VARCHAR2(10)) AS validade,
       'SEM_VALIDADE' AS situacao_validade,
       x.qt_saldo AS quantidade, NVL(x.vl_custo_medio, m.vl_custo_medio) AS valor_unitario,
       ROUND(x.qt_saldo * NVL(x.vl_custo_medio, m.vl_custo_medio), 2) AS valor_total,
       m.vl_custo_aquisicao AS valor_aquisicao_unit,
       CAST(NULL AS VARCHAR2(60)) AS marca, CAST(NULL AS VARCHAR2(60)) AS laboratorio,
       CAST(NULL AS VARCHAR2(60)) AS localizacao,
       """ + FLAGS + """,
       'emprestimo ' || x.nr_emprestimo || '/' || x.ano_emprestimo || ' - ' ||
       NVL(t.nm_terceiro,'?') AS referencia,
       TO_CHAR(em.dt_movimento,'YYYY-MM-DD') AS data_ref
  FROM emprestimo_item x """ + JOINS + """
  JOIN emprestimo em ON em.cd_hospital = x.cd_hospital AND em.id_emprestimo = x.id_emprestimo
       AND em.nr_emprestimo = x.nr_emprestimo AND em.ano_emprestimo = x.ano_emprestimo
  LEFT JOIN hospital h ON h.cd_hospital = x.cd_hospital
  LEFT JOIN estoque e ON e.cd_hospital = em.est_cd_hospital AND e.cd_estoque = em.cd_estoque
  LEFT JOIN terceiro t ON t.cd_terceiro = em.cd_terceiro
 WHERE NVL(x.qt_saldo,0) <> 0
 ORDER BY em.dt_movimento DESC, x.cd_material"""

C_ENTRADA_FIFO = """
SELECT 'MATMED_LOTE_ENT' AS origem_tabela, 'ENTRADA_FIFO_SALDO' AS tipo_registro,
       'N' AS conta_no_total,
       CAST(NULL AS NUMBER) AS cd_hospital, 'SEM VINCULO DE HOSPITAL NA TABELA' AS ds_hospital,
       CAST(NULL AS NUMBER) AS cd_estoque, CAST(NULL AS VARCHAR2(60)) AS ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       x.sc_lote AS lote, TO_CHAR(x.dt_validade,'YYYY-MM-DD') AS validade,
       """ + situacao('x.dt_validade') + """ AS situacao_validade,
       x.qt_saldo AS quantidade, m.vl_custo_medio AS valor_unitario,
       ROUND(x.qt_saldo * m.vl_custo_medio, 2) AS valor_total,
       m.vl_custo_aquisicao AS valor_aquisicao_unit,
       ma.ds_marca AS marca, lb.ds_laboratorio AS laboratorio,
       CAST(NULL AS VARCHAR2(60)) AS localizacao,
       """ + FLAGS + """, 'saldo FIFO por entrada (rastreio, nao somar)' AS referencia,
       TO_CHAR(x.dt_ultima_entrada,'YYYY-MM-DD') AS data_ref
  FROM (SELECT cd_material, sc_lote, dt_validade, cd_marca, cd_laboratorio,
               SUM(qt_saldo) qt_saldo, MAX(dt_movimento_lote) dt_ultima_entrada
          FROM matmed_lote_ent WHERE NVL(qt_saldo,0) <> 0
         GROUP BY cd_material, sc_lote, dt_validade, cd_marca, cd_laboratorio) x
  """ + JOINS + """
  LEFT JOIN marca ma ON ma.cd_marca = x.cd_marca
  LEFT JOIN laboratorio lb ON lb.cd_laboratorio = x.cd_laboratorio
 ORDER BY x.cd_material, x.dt_validade, x.sc_lote"""

C_INVENTARIO = """
SELECT 'INVENTARIO_ITEM' AS origem_tabela, 'ULTIMA_CONTAGEM' AS tipo_registro,
       'N' AS conta_no_total,
       x.cd_hospital, h.ds_hospital, x.cd_estoque, e.ds_estoque,
       x.cd_material AS codigo, m.ds_material AS descricao, """ + CAT + """,
       CAST(NULL AS VARCHAR2(20)) AS lote, CAST(NULL AS VARCHAR2(10)) AS validade,
       'SEM_VALIDADE' AS situacao_validade,
       NVL(x.qt_recontagem, x.qt_contagem) AS quantidade, x.vl_custo_medio AS valor_unitario,
       ROUND(NVL(x.qt_recontagem, x.qt_contagem) * x.vl_custo_medio, 2) AS valor_total,
       m.vl_custo_aquisicao AS valor_aquisicao_unit,
       CAST(NULL AS VARCHAR2(60)) AS marca, CAST(NULL AS VARCHAR2(60)) AS laboratorio,
       CAST(NULL AS VARCHAR2(60)) AS localizacao,
       """ + FLAGS + """,
       'contagem fisica - saldo do sistema na data ' || TO_CHAR(x.qt_material) AS referencia,
       TO_CHAR(x.dt_inventario,'YYYY-MM-DD') AS data_ref
  FROM (SELECT i.cd_hospital, i.cd_estoque, i.cd_material, i.dt_inventario, i.qt_material,
               i.qt_contagem, i.qt_recontagem, i.vl_custo_medio,
               ROW_NUMBER() OVER (PARTITION BY i.cd_hospital, i.cd_estoque, i.cd_material
                                  ORDER BY i.dt_inventario DESC) rn
          FROM inventario_item i) x
  """ + JOINS + """
  LEFT JOIN hospital h ON h.cd_hospital = x.cd_hospital
  LEFT JOIN estoque e ON e.cd_hospital = x.cd_hospital AND e.cd_estoque = x.cd_estoque
 WHERE x.rn = 1
 ORDER BY x.cd_hospital, x.cd_estoque, x.cd_material"""

CAMADAS = [
    ('SALDO_LOTE', C_SALDO_LOTE),
    ('SALDO_ESTOQUE', C_SALDO_ESTOQUE),
    ('CONSIGNADO', C_CONSIGNADO),
    ('CONSOLIDADO_HOSPITAL', C_CONSOLIDADO),
    ('CATALOGO', C_CATALOGO),
    ('EMPRESTIMO', C_EMPRESTIMO),
    ('ENTRADA_FIFO', C_ENTRADA_FIFO),
    ('INVENTARIO', C_INVENTARIO),
]

CABECALHO = ['ORIGEM_TABELA', 'TIPO_REGISTRO', 'CONTA_NO_TOTAL', 'CD_HOSPITAL', 'DS_HOSPITAL',
    'CD_ESTOQUE', 'DS_ESTOQUE', 'CODIGO', 'DESCRICAO', 'UNIDADE', 'TIPO_ITEM', 'CLASSIFICACAO',
    'SUBGRUPO', 'GRUPO_COMPRA', 'LOTE', 'VALIDADE', 'SITUACAO_VALIDADE', 'QUANTIDADE',
    'VALOR_UNITARIO', 'VALOR_TOTAL', 'VALOR_AQUISICAO_UNIT', 'MARCA', 'LABORATORIO',
    'LOCALIZACAO', 'IN_MEDICAMENTO', 'IN_CONTROLADO', 'IN_ANTIBIOTICO', 'IN_PATRIMONIO',
    'IN_CONSIGNADO', 'IN_KIT', 'IN_ATIVO_ITEM', 'REGISTRO_MS', 'CD_DCB', 'REFERENCIA',
    'DATA_REF', 'EXTRAIDO_EM']

I_QTD, I_VLTOT, I_TIPO_ITEM, I_DS_HOSP = 17, 19, 10, 4


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def main():
    saida = sys.argv[1] if len(sys.argv) > 1 else '/root/inventario_salux_completo.csv'
    resumo = sys.argv[2] if len(sys.argv) > 2 else saida.replace('.csv', '_resumo.csv')
    carimbo = q("SELECT TO_CHAR(SYSDATE,'YYYY-MM-DD HH24:MI:SS') FROM dual")['linhas'][0][0]
    print('carimbo %s' % carimbo)
    todas = []
    for nome, sql in CAMADAS:
        linhas = paginado(sql)
        if len(linhas) != len(set(map(tuple, [tuple(l) for l in linhas]))):
            print('  (aviso: %s tem linhas repetidas)' % nome)
        for l in linhas:
            todas.append(list(l) + [carimbo])
        print('  %-22s %7d linhas' % (nome, len(linhas)))
    with open(saida, 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.writer(f, delimiter=';')
        w.writerow(CABECALHO)
        for l in todas:
            w.writerow(['' if v is None else v for v in l])
    agreg = {}
    for l in todas:
        chave = (l[0], l[1], l[2], l[I_DS_HOSP] or '', l[I_TIPO_ITEM] or '')
        acc = agreg.setdefault(chave, [0, 0.0, 0.0])
        acc[0] += 1
        acc[1] += num(l[I_QTD])
        acc[2] += num(l[I_VLTOT])
    with open(resumo, 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.writer(f, delimiter=';')
        w.writerow(['ORIGEM_TABELA', 'TIPO_REGISTRO', 'CONTA_NO_TOTAL', 'DS_HOSPITAL', 'TIPO_ITEM',
                    'LINHAS', 'QUANTIDADE_TOTAL', 'VALOR_TOTAL_REAIS'])
        for chave in sorted(agreg):
            n, qt, vl = agreg[chave]
            w.writerow(list(chave) + [n, round(qt, 4), round(vl, 2)])
    print('TOTAL %d linhas -> %s' % (len(todas), saida))
    print('resumo -> %s' % resumo)


main()
