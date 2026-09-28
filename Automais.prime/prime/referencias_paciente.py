"""As colunas que guardam id de paciente. ESTA LISTA E A INTEGRIDADE REFERENCIAL DESTE BANCO.

`fhir.patient` nao tem **nenhuma** FK apontando para ela — sao 33 colunas em `fhir.*` e
`smsmarica.*` guardando id de paciente sem constraint alguma. Uma fusao que esqueca uma coluna
deixa dado orfao e o banco nao reclama. Se esta lista envelhecer, o silencio continua.

Conferir com:
    select table_schema, table_name, column_name from information_schema.columns
    where column_name ~* '(patient|paciente)_?id$' and table_schema in ('fhir','smsmarica');

Em modulo proprio, sem importar nada, para os scripts que rodam NO SERVIDOR poderem usa-la sem
arrastar junto o helper de banco (que le credencial do ambiente de desenvolvimento).
"""

from __future__ import annotations

REFERENCIAS: list[tuple[str, str, str]] = [
    ("fhir", "condition", "patient_id"), ("fhir", "document_reference", "patient_id"),
    ("fhir", "encounter", "patient_id"), ("fhir", "medication_administration", "patient_id"),
    ("fhir", "medication_request", "patient_id"), ("fhir", "observation", "patient_id"),
    ("smsmarica", "anexo_upload_token", "patient_id"), ("smsmarica", "cidadao_acesso", "patient_id"),
    ("smsmarica", "cidadao_login_link", "patient_id"),
    ("smsmarica", "comunicacao_paciente", "paciente_id"),
    ("smsmarica", "contato_comprometido", "paciente_id"),
    ("smsmarica", "contato_registro", "paciente_id"), ("smsmarica", "conversa", "paciente_id"),
    ("smsmarica", "dispensa_verificacao_contato", "paciente_id"),
    ("smsmarica", "exame_associacao", "paciente_id"), ("smsmarica", "laudo", "paciente_id"),
    ("smsmarica", "ouvidoria_manifestacao", "manifestante_patient_id"),
    ("smsmarica", "ouvidoria_manifestacao", "referido_patient_id"),
    ("smsmarica", "pendencia_cadastro", "paciente_id"),
    ("smsmarica", "pesquisa_satisfacao", "patient_id"),
    ("smsmarica", "regulacao_solicitacao", "paciente_id"),
    ("smsmarica", "robo_tarefa", "paciente_id"), ("smsmarica", "ser_solicitacao", "paciente_id"),
    ("smsmarica", "sernit_solicitacao", "paciente_id"), ("smsmarica", "solicitacao", "paciente_id"),
    ("smsmarica", "tfd_registro_faturamento", "paciente_id"),
    ("smsmarica", "tratamento", "paciente_id"),
    ("smsmarica", "verificacao_cadastral_estado", "paciente_id"),
    ("smsmarica", "whatsapp_mensagem", "paciente_id"),
]
