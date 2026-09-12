"""
Ferramenta ÚNICA do modo `dados`: consultar a base de dados desta sessão.

O modo `dados` (perguntas às bases, no menu IA) sobe o Claude Code **sem nenhuma ferramenta
de host** — nada de Bash/Read/Write/Edit/git/rede. A ÚNICA capacidade é esta: executar SQL
somente-leitura contra a base da sessão, e mesmo assim mediado pelo `/proxy-sql` da API .NET
(loopback :5091), que:

- resolve a conexão pela `ia_fonte` (Oracle direto ou SQL Server via agente WSS) — o agente
  nunca vê credencial;
- garante leitura (SqlReadOnlyGuard dentro de cada IFonteDados, e de novo no agente WSS);
- capa o número de linhas.

A base é **presa no servidor** (slug fixo por sessão): o modelo não escolhe outra base nem
alcança o host por aqui. Ver ADR-0023.
"""
import asyncio
import json
import urllib.error
import urllib.request

from claude_agent_sdk import create_sdk_mcp_server, tool

import config

# Nome MCP resultante: mcp__dados__consultar_base — é ele que entra no allowed_tools.
SERVER_NAME = "dados"
TOOL_NAME = "consultar_base"
TOOL_FQN = f"mcp__{SERVER_NAME}__{TOOL_NAME}"

# Linhas devolvidas ao modelo por consulta. Teto pequeno de propósito: agregar > despejar
# linhas cruas, e respostas enormes só incham o contexto. O proxy tem o seu próprio teto.
_MAX_LINHAS = 200
# Quantas linhas de fato formatamos no texto que volta ao modelo.
_MAX_LINHAS_TEXTO = 100


def _formatar(resultado: dict) -> str:
    """Resultado do proxy -> tabela compacta em texto para o modelo ler."""
    resultados = resultado.get("resultados") or []
    if not resultados:
        return "(sem resultado)"
    r = resultados[0]
    colunas = r.get("colunas") or []
    linhas = r.get("linhas") or []
    if not colunas:
        return "(consulta sem colunas)"

    partes = [" | ".join(str(c) for c in colunas)]
    for linha in linhas[:_MAX_LINHAS_TEXTO]:
        partes.append(" | ".join("" if v is None else str(v) for v in linha))
    if len(linhas) > _MAX_LINHAS_TEXTO:
        partes.append(f"... (+{len(linhas) - _MAX_LINHAS_TEXTO} linhas não mostradas)")
    total = f"\n\n[{len(linhas)} linha(s) devolvida(s); duração {r.get('duracaoMs', '?')} ms]"
    return "\n".join(partes) + total


VIZ_TOOL_NAME = "visualizar"
VIZ_TOOL_FQN = f"mcp__{SERVER_NAME}__{VIZ_TOOL_NAME}"


def make_server(base_slug: str, auditoria=None):
    """Cria o MCP server in-process com as ferramentas presas a ESTA base (slug fixo).

    `auditoria`: função sem argumentos que devolve o dict do TURNO corrente
    ({usuarioId, pergunta, turnoId}). Vai junto de cada SQL para o /proxy-sql, que grava pergunta
    + SQL + operador em `ia_consulta` — e recusa as bases do próprio SMSMais (Regulação,
    Atendimento) quando não sabe quem perguntou. É lida a cada chamada, não na criação: o cliente
    vive por vários turnos.

    Duas ferramentas, ambas benignas (nenhuma toca host/código):
    - `consultar_base`: SELECT read-only via /proxy-sql (slug no closure — o modelo não troca
      de base). Uma sessão da UPA não consulta o Salux, nem por engano.
    - `visualizar`: NÃO executa nada — só DECLARA uma visualização (gráfico/mapa). O painel lê
      o `spec` do evento de uso da ferramenta e renderiza. É o canal estruturado de saída visual.
    """

    @tool(
        TOOL_NAME,
        "Executa um SELECT (somente leitura) na base de dados desta sessão e devolve as "
        "colunas e linhas. Use para responder perguntas sobre os dados. Passe apenas UM "
        "comando SELECT/WITH no dialeto correto da base.",
        {"sql": str},
    )
    async def consultar_base(args: dict) -> dict:
        sql = (args or {}).get("sql", "")
        if not isinstance(sql, str) or not sql.strip():
            return {"content": [{"type": "text", "text": "Erro: informe o parâmetro 'sql'."}],
                    "is_error": True}

        payload = {
            "base": base_slug,
            "consultas": [sql],
            "maxLinhas": _MAX_LINHAS,
        }
        if auditoria is not None:
            try:
                payload["auditoria"] = auditoria() or None
            except Exception:  # noqa: BLE001 — sem auditoria o proxy decide (recusa as sensíveis)
                payload["auditoria"] = None
        corpo = json.dumps(payload).encode("utf-8")

        def _chamar() -> tuple[int, str]:
            req = urllib.request.Request(
                config.PROXYSQL_URL, data=corpo, method="POST",
                headers={"Content-Type": "application/json",
                         "X-Proxy-Token": config.PROXYSQL_TOKEN},
            )
            try:
                with urllib.request.urlopen(req, timeout=config.PROXYSQL_TIMEOUT_SEC) as resp:
                    return resp.status, resp.read().decode("utf-8")
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode("utf-8", "replace")
            except Exception as e:  # noqa: BLE001
                return 0, str(e)

        status, texto = await asyncio.to_thread(_chamar)

        if status == 200:
            try:
                return {"content": [{"type": "text", "text": _formatar(json.loads(texto))}]}
            except Exception as e:  # noqa: BLE001
                return {"content": [{"type": "text", "text": f"Resposta ilegível do proxy: {e}"}],
                        "is_error": True}

        # Erro do proxy (SQL inválido, base fora, agente desconectado): devolve pro modelo
        # ajustar. A mensagem já vem em pt-BR do .NET.
        msg = texto
        try:
            msg = json.loads(texto).get("mensagem", texto)
        except Exception:  # noqa: BLE001
            pass
        prefixo = "A base não está acessível agora" if status in (502, 503) else "Consulta recusada"
        return {"content": [{"type": "text", "text": f"{prefixo}: {msg}"}], "is_error": True}

    @tool(
        VIZ_TOOL_NAME,
        "Declara uma visualização para o operador (o painel renderiza). NÃO executa nada — só "
        "descreve o gráfico/mapa a partir de dados que você JÁ obteve com consultar_base. "
        "Passe um objeto 'spec'. Formatos:\n"
        "- número: {tipo:'numero', titulo, valor, unidade?}\n"
        "- tabela: {tipo:'tabela', titulo, colunas:[...], linhas:[[...]]}\n"
        "- pizza:  {tipo:'pizza', titulo, dados:[{rotulo, valor}, ...]}\n"
        "- barra:  {tipo:'barra', titulo, eixoX?, eixoY?, dados:[{rotulo, valor}, ...]}\n"
        "- linha:  {tipo:'linha', titulo, eixoX?, eixoY?, dados:[{rotulo, valor}, ...]}\n"
        "- mapa de pontos:  {tipo:'mapa_pontos', titulo, pontos:[{lat, lng, rotulo?, valor?}, ...]}\n"
        "- mapa de calor:   {tipo:'mapa_calor', titulo, pontos:[{lat, lng, peso?}, ...]}\n"
        "- mapa de polígono:{tipo:'mapa_poligono', titulo, poligonos:[{coordenadas:[[lat,lng],...], "
        "rotulo?, valor?}, ...]}\n"
        "Ofereça visualização quando ajudar (comparações→barra/pizza, série temporal→linha, "
        "dados geográficos→mapa). Não invente coordenadas nem números fora do que consultou.",
        {"spec": dict},
    )
    async def visualizar(args: dict) -> dict:
        spec = (args or {}).get("spec")
        if not isinstance(spec, dict) or not spec.get("tipo"):
            return {"content": [{"type": "text", "text":
                    "Erro: passe 'spec' com ao menos 'tipo'."}], "is_error": True}
        # O trabalho de renderizar é do painel (lê o input deste tool_use). Aqui só confirmamos.
        return {"content": [{"type": "text", "text":
                f"Visualização '{spec.get('tipo')}' preparada para o operador."}]}

    return create_sdk_mcp_server(
        name=SERVER_NAME, version="1.0.0", tools=[consultar_base, visualizar])
