"""Hook PostToolUse: ferramenta de laboratório criada ou alterada → lembra de indexar.

Por que existe: em 08/09/2026 havia 125 scripts espalhados por quatro laboratórios e nenhum
lugar que dissesse que eles existem. O `INDICE.md` resolveu o retrabalho de redescobrir — mas
índice que depende de alguém lembrar de atualizar envelhece em uma semana. Este hook é o
lembrete que não depende de memória.

Lê o JSON do hook em stdin e, quando o arquivo tocado é um `.py` de laboratório, devolve
`additionalContext` para o modelo. Não bloqueia nada: PostToolUse já rodou, e travar a edição de
um script por causa de documentação seria pior que a doença.

Silencioso para qualquer outro arquivo — hook que fala sempre vira ruído e é desligado.
"""
import json
import pathlib
import sys

# Pastas cujos .py são ferramenta de laboratório. `Salux/scripts` é a única com nível a mais.
LABORATORIOS = (
    "/Automais.SISREG/",
    "/Automais.SER/",
    "/Automais.SERNIT/",
    "/Automais.SISCAN/",
    "/Salux/scripts/",
)

RECADO = (
    "Você criou ou alterou uma ferramenta de laboratório ({nome}). "
    "Acrescente a linha correspondente em \"Aprendizados e Scratchpads/INDICE.md\" — é o que "
    "impede a próxima sessão de redescobrir que ela existe. "
    "Confira com: python \"Aprendizados e Scratchpads/ferramentas/catalogar.py\" --faltando"
)


def main() -> int:
    try:
        evento = json.load(sys.stdin)
    except Exception:
        return 0  # stdin inesperado nunca pode atrapalhar a edição

    entrada = evento.get("tool_input") or {}
    resposta = evento.get("tool_response") or {}
    caminho = entrada.get("file_path") or resposta.get("filePath") or ""
    if not caminho:
        return 0

    # Normaliza a barra: no Windows o caminho vem com `\`.
    normalizado = "/" + caminho.replace("\\", "/").lstrip("/")

    if not normalizado.endswith(".py"):
        return 0
    if not any(pasta in normalizado for pasta in LABORATORIOS):
        return 0

    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PostToolUse",
            "additionalContext": RECADO.format(nome=pathlib.PurePosixPath(normalizado).name),
        }
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
