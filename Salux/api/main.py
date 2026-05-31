"""FastAPI backend do Salux Discovery — ponte HTTP→Oracle (read-only).

Reusa `scripts/conexao.py`. Sobe em http://localhost:8000.

Rodar:
    uvicorn api.main:app --reload --host 127.0.0.1 --port 8000
"""
from __future__ import annotations

import pathlib
import sys

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

# Permite importar scripts/conexao.py
_RAIZ = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_RAIZ / "scripts"))

from api.routers import pacientes, atendimentos  # noqa: E402

app = FastAPI(
    title="Salux Discovery API",
    description="Ponte HTTP→Oracle read-only para o React de descoberta do Salux HIS.",
    version="0.1.0",
)

# CORS pro Vite dev (porta 5173)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://127.0.0.1:5173"],
    allow_credentials=False,
    allow_methods=["GET"],
    allow_headers=["*"],
)

app.include_router(pacientes.router, prefix="/api/pacientes", tags=["pacientes"])
app.include_router(atendimentos.router, prefix="/api", tags=["atendimentos"])


@app.get("/api/health")
def health():
    return {"ok": True, "env": "salux-discovery"}
