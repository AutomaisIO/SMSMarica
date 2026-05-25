"""Permite rodar o CLI via `python -m equipamento_sim …` — útil quando o
entrypoint `equipamento` não está no PATH (caso típico do Python da Windows
Store, que não adiciona Scripts/ ao PATH)."""

from equipamento_sim.main import app

if __name__ == "__main__":
    app()
