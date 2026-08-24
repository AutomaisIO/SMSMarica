"""Entrypoint do CLI `equipamento`."""

from __future__ import annotations

import typer

from equipamento_sim.comandos import exec as exec_cmd
from equipamento_sim.comandos import mwl as mwl_cmd

app = typer.Typer(
    name="equipamento",
    help="Simulador de equipamento DICOM para o SMSMarica.",
    no_args_is_help=True,
    add_completion=False,
)

app.command(name="mwl", help="Lista a worklist do dcm4chee (C-FIND).")(mwl_cmd.comando)
app.command(name="exec", help="Simula execução de um exame (gera DICOM e envia via C-STORE).")(exec_cmd.comando)


if __name__ == "__main__":
    app()
