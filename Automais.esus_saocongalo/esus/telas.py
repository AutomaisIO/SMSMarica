"""Payloads das telas de leitura do e-SUS SG, copiados do que o front manda (medido 30/09/2026).

Cada função devolve o `arrFormData` exato que a tela envia com os filtros em branco — os
probes só trocam o que precisam. Não inventar chave: o legado PHP ignora chave desconhecida
em silêncio e devolve 0 linhas, que parece "não há dado" (foi assim que 61 agendados sumiram).

Datas são SEMPRE `dd/mm/aaaa` (é o que a máscara da tela manda).
"""

from __future__ import annotations

# Permissões de tela (smo_id) que a busca de agendados manda junto.
SMO_AGENDADOS_CONSULTA = 447   # consulta.buscaPacientesAgendadosFilaConsulta.Exibir
SMO_AGENDADOS_EXAME = 449      # exame2.buscaPacientesAgendadosFilaExames.exibir

CAMINHO = {
    "fila_consulta": "/consultas/controller-fila-consulta/buscar",
    "fila_exame": "/exames2/controller-fila-exame/buscar",
    "agendados_consulta": "/consultas/controller-paciente-agendado-fila-consulta/buscar",
    "agendados_exame": "/exames2/controller-paciente-agendado-fila-exames/buscar",
}


def form_fila(unidade: int, *, exame: bool) -> dict:
    """Fila de Regulação (consulta ou exame), vista pela unidade atual.

    Na fila de EXAME a tela manda `uns_id=<unidade>` + `permissaoUnidadeSolicitante=0`;
    na de CONSULTA manda `uns_id=null` + `permissaoUnidadeSolicitante=1`. Copiado como veio.
    """
    f = {
        "uns_id": unidade if exame else None,
        "uns_atual": unidade,
        "pfi_id": None, "situacao": None, "pendencia": None, "requestingProfessionalId": None,
        "est_id": None, "mun_id": None, "bai_nome": "",
        "periodoInicial": "", "periodoInicialFila": "", "periodoFinal": "", "periodoFinalFila": "",
        "permissaoRegular": 0,
        "permissaoUnidadeSolicitante": 0 if exame else 1,
        "somenteRegulados": False,
        "pes_id": None, "pes_nome": None,
        "minAge": 0, "maxAge": 0, "minAgeType": None, "maxAgeType": None,
        "orderbyAge": None, "orderbyDate": None,
        "limiteInicio": 0, "limiteFim": 16,
    }
    if exame:
        f = {"fle_nome_procedimento": "TODOS", "esu_nome_exames_procedimentos_filho": ""} | f | {"rgb_agendado": None}
    return f


def form_agendados(unidade_solicitante: int, *, exame: bool, de: str, ate: str) -> dict:
    """Pacientes Agendados pela Fila — `de`/`ate` filtram a DATA DO AGENDAMENTO (dd/mm/aaaa).

    Sem período a tela devolve 0 — não é "não há agendado", é filtro obrigatório de fato.
    """
    return {
        "pfi_id": None, "set_id": None, "lca_id": None, "ocp_id": None,
        "usu_id_agendamento": 0, "pes_id": None, "pes_base": None,
        "periodoFinal": ate, "periodoInicial": de,
        "periodoFilaFinal": "", "periodoFilaInicial": "",
        "periodoCadastroFilaFinal": "", "periodoCadastroFilaInicial": "",
        "fun_id_solicitante": 0, "fun_id": 0,
        "uns_id": None, "uns_id_destino": None, "uns_solicitante": unidade_solicitante,
        "fil_id_agendado_por": 0,
        "fil_comprovante_impresso": 2,          # 2 = impresso e não impresso
        "smo_id": SMO_AGENDADOS_EXAME if exame else SMO_AGENDADOS_CONSULTA,
        "ilt_id": None, "not_resposta": "", "not_resposta_extensao": "",
        "limiteInicio": 0, "limiteFim": 21,
    }
