# -*- coding: utf-8 -*-
"""Ferramentas do robô no formato OpenAI (espelho fiel das descrições do RoboFerramentaCatalogo,
via molde da skill analisar-robo-cenarios-reais de 01/09/2026). Só as 6 usadas pelas 12 cenas."""

def _t(nome, desc, props, req):
    return {"type": "function", "function": {"name": nome, "description": desc,
            "parameters": {"type": "object", "properties": props, "required": req}}}

RESPONDER = _t("responder_cidadao",
    "Entrega a resposta final ao cidadão. É o ÚNICO canal de saída: nada que você escrever fora "
    "desta ferramenta chega à pessoa. Chame-a exatamente uma vez, por último.",
    {"texto": {"type": "string", "description": "a mensagem que o cidadão vai ler — nunca raciocínio/planos/ferramentas"},
     "handoff": {"type": "boolean", "description": "true quando deve ir para atendente humano"},
     "motivoHandoff": {"type": "string", "description": "interno"},
     "confianca": {"type": "number", "description": "0..1"}},
    ["texto", "handoff", "confianca"])

CONSULTAR_CADASTRO = _t("consultar_cadastro",
    "Confere a identidade da pessoa contra o cadastro e devolve o NOME dela. Colete ANTES de chamar: "
    "os QUATRO primeiros dígitos do CPF (todos de uma vez, nunca dígito por dígito, nunca o CPF "
    "completo) e o mês e o ano de nascimento — do PACIENTE do agendamento, não de quem escreve. "
    "NUNCA chame esta ferramenta com campo vazio ou inventado: se faltar algum dado, PERGUNTE "
    "primeiro. Use SEMPRE esta ferramenta para confirmar identidade — nunca diga por conta própria "
    "que os dados conferem ou não conferem.",
    {"cpf": {"type": "string", "description": "OBRIGATORIO — 4 primeiros dígitos"},
     "mesNascimento": {"type": "integer", "description": "OBRIGATORIO (1-12)"},
     "anoNascimento": {"type": "integer", "description": "OBRIGATORIO (4 dígitos)"}},
    ["cpf", "mesNascimento", "anoNascimento"])

CONSULTAR_AGENDAMENTOS = _t("consultar_agendamentos",
    "Lista os agendamentos FUTUROS do paciente (procedimento, data e unidade). Exige identidade: "
    "colete os QUATRO primeiros dígitos do CPF (de uma vez) e o mês/ano de nascimento ANTES de "
    "chamar. Use SEMPRE esta ferramenta antes de falar qualquer coisa sobre agendamento — inclusive "
    "para dizer que NÃO há: nunca afirme ausência sem ter consultado.",
    {"cpf": {"type": "string"}, "mesNascimento": {"type": "integer"}, "anoNascimento": {"type": "integer"}},
    ["cpf", "mesNascimento", "anoNascimento"])

CONSULTAR_UNIDADES = _t("consultar_unidades",
    "Lista unidades de saúde da rede com nome e endereço — use para dizer ONDE fica um posto, NUNCA "
    "invente endereço. Aceita 'termo' (nome da unidade ou bairro). ATENÇÃO: isto NÃO é oferta de "
    "atendimento. Não marque consulta, não diga que a unidade vai atender, não prometa horário nem "
    "encaixe — oriente sempre a procurar o posto de saúde onde a pessoa já é atendida.",
    {"termo": {"type": "string", "description": "opcional (nome ou bairro; vazio devolve amostra)"}}, [])

ENCAMINHAR = _t("encaminhar_para_humano",
    "Devolve a conversa para um atendente humano. Use APENAS quando a pessoa pedir um atendente ou "
    "quando o pedido estiver claramente fora do que você pode tratar.", {}, [])

CONFIRMAR_PRESENCA = _t("confirmar_presenca",
    "Confirma a presença do paciente no agendamento. FLUXO EM DUAS CHAMADAS: primeiro chame com cpf "
    "(os 4 PRIMEIROS dígitos — NUNCA peça o CPF completo) + mesNascimento + anoNascimento; o comando "
    "valida e devolve o NOME para você confirmar com a pessoa. Só depois que ela confirmar o nome, "
    "chame de novo com confirmado=true. Nunca revele procedimento, data, hora ou local antes de a "
    "identidade conferir.",
    {"cpf": {"type": "string"}, "mesNascimento": {"type": "integer"}, "anoNascimento": {"type": "integer"},
     "confirmado": {"type": "boolean", "description": "só true na 2ª chamada, após confirmar o NOME"}},
    ["cpf", "mesNascimento", "anoNascimento"])

BASE = [RESPONDER, CONSULTAR_CADASTRO, CONSULTAR_AGENDAMENTOS, CONSULTAR_UNIDADES, ENCAMINHAR]
POR_ASSUNTO = {"confirmacao": [CONFIRMAR_PRESENCA]}

def ferramentas_da_cena(chave_assunto):
    return BASE + POR_ASSUNTO.get(chave_assunto, [])
