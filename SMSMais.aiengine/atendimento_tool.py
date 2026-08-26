"""
Ferramentas de COMANDO do kind `atendimento` (Fase 3).

Cada assunto habilita um subconjunto de comandos; a .NET manda a lista (nomes do enum
ComandoRobo) no payload, e aqui viram tools MCP. Cada tool faz um POST ao guichê interno
`/robo-comando` da API .NET (porta de loopback + X-Robo-Token), que valida a habilitação por
assunto, executa o handler tipado (com gate/minimização) e devolve uma mensagem ao modelo.

O robô NUNCA toca o banco: só estes comandos, e só os habilitados. Se um comando ainda não tem
handler na .NET, o guichê responde "indisponível" e o modelo segue sem ele.
"""
import asyncio
import json
import urllib.error
import urllib.request

from claude_agent_sdk import tool

import config

SERVER_NAME = "atendimento"

# Catálogo: nome do enum .NET -> (nome da tool, descrição, schema de args).
# Espelha ComandoRobo. Comandos sem handler na .NET ainda respondem "indisponível".
CATALOGO = {
    "RegistrarNumeroErrado": (
        "registrar_numero_errado",
        "Registra que o número não é do paciente (não sou essa pessoa). "
        "args: vinculo (Parente | Responsavel | SemVinculo | NaoInformado), observacao (opcional).",
        {"vinculo": str, "observacao": str},
    ),
    "EncaminharParaHumano": (
        "encaminhar_para_humano",
        "Encaminha a conversa para um atendente humano.",
        {},
    ),
    "InformarHorarioAtendimento": (
        "informar_horario_atendimento",
        "Informa o horário de atendimento.",
        {},
    ),
    "ConfirmarPresenca": (
        "confirmar_presenca",
        "Confirma a presença do paciente no agendamento. Só chame APÓS confirmar a identidade — "
        "4 primeiros dígitos do CPF + mês e ano de nascimento. Devolve qual agendamento foi confirmado. "
        "args: cpf (4 primeiros dígitos ou completo), mesNascimento (1-12), anoNascimento (ex.: 1985).",
        {"cpf": str, "mesNascimento": int, "anoNascimento": int},
    ),
    "IniciarCancelamento": (
        "iniciar_cancelamento",
        "Registra que o paciente NÃO vai comparecer. Ação sensível: só chame APÓS confirmar a "
        "identidade — 4 primeiros dígitos do CPF + mês e ano de nascimento. "
        "args: cpf (4 primeiros dígitos ou completo), mesNascimento (1-12), anoNascimento (ex.: 1985), "
        "motivo (opcional).",
        {"cpf": str, "mesNascimento": int, "anoNascimento": int, "motivo": str},
    ),
    "ConsultarStatusExameRecente": (
        "consultar_status_exame_recente",
        "Consulta a situação do exame/laudo recente APÓS confirmar identidade. "
        "args: nome (informado pela pessoa), cpf (4 primeiros dígitos ou completo).",
        {"nome": str, "cpf": str},
    ),
    "ConsultarPosicaoRegulacao": (
        "consultar_posicao_regulacao",
        "Consulta a posição do agendamento na regulação (SER/SISREG/SERNIT) — dado minimizado. "
        "Se o número não for verificado, exige identidade: 4 primeiros dígitos do CPF + mês e ano de "
        "nascimento (se o comando pedir, colete e chame de novo). "
        "args: cpf (opcional), mesNascimento (opcional), anoNascimento (opcional).",
        {"cpf": str, "mesNascimento": int, "anoNascimento": int},
    ),
    "VerificarCadastro": (
        "verificar_cadastro",
        "Valida os 4 primeiros dígitos do CPF (resposta ao pedido de verificação cadastral). "
        "Confere → libera o envio da confirmação do agendamento. args: cpf (4 primeiros dígitos ou completo).",
        {"cpf": str},
    ),
}


def _build(enum_name, tool_name, desc, schema, conversa_id, paciente_id, assunto_id):
    @tool(tool_name, desc, schema or {})
    async def _fn(args: dict) -> dict:
        payload = json.dumps({
            "conversaId": conversa_id,
            "pacienteId": paciente_id,
            "assuntoId": assunto_id,
            "comando": enum_name,
            "args": args or {},
        }).encode("utf-8")

        def _call():
            req = urllib.request.Request(
                config.ROBO_COMANDO_URL, data=payload, method="POST",
                headers={"Content-Type": "application/json", "X-Robo-Token": config.ROBO_COMANDO_TOKEN})
            try:
                with urllib.request.urlopen(req, timeout=config.ROBO_COMANDO_TIMEOUT_SEC) as r:
                    return r.status, r.read().decode("utf-8")
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode("utf-8", "replace")
            except Exception as e:  # noqa: BLE001
                return 0, str(e)

        status, body = await asyncio.to_thread(_call)
        msg = body
        try:
            msg = json.loads(body).get("mensagem") or body
        except Exception:  # noqa: BLE001
            pass
        if status == 200:
            return {"content": [{"type": "text", "text": msg}]}
        return {"content": [{"type": "text", "text": f"Comando indisponível: {msg}"}], "is_error": True}

    return _fn


def build_command_tools(conversa_id, paciente_id, assunto_id, comandos_habilitados):
    """Constrói as tools MCP dos comandos habilitados. Retorna (tools, fqns)."""
    tools, fqns = [], []
    for enum_name in comandos_habilitados or []:
        entry = CATALOGO.get(enum_name)
        if not entry:
            continue
        tool_name, desc, schema = entry
        tools.append(_build(enum_name, tool_name, desc, schema, conversa_id, paciente_id, assunto_id))
        fqns.append(f"mcp__{SERVER_NAME}__{tool_name}")
    return tools, fqns
