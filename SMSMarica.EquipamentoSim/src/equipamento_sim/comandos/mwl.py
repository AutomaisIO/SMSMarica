"""Comando `equipamento mwl` — lista o worklist DICOM do dcm4chee."""

from __future__ import annotations

import typer
from rich.console import Console
from rich.table import Table

from equipamento_sim.config import carregar
from equipamento_sim.dicom.cliente_mwl import consultar_worklist


def comando(
    host: str = typer.Option(None, help="Host do dcm4chee (default: env EQSIM_HOST)."),
    port: int = typer.Option(None, help="Porta DICOM (default: 11112)."),
    calling_ae: str = typer.Option(None, "--calling-ae", help="AE Title deste simulador."),
    called_ae: str = typer.Option(None, "--called-ae", help="AE Title do worklist (default WORKLIST)."),
    modalidade: str = typer.Option(None, "--modalidade", help="Filtra por modalidade (MG, CR, US, etc)."),
    accession: str = typer.Option(None, "--accession", help="Filtra por AccessionNumber específico."),
) -> None:
    """Lista os exames agendados (workitems) atualmente no dcm4chee."""

    cfg = carregar()
    console = Console()

    items = consultar_worklist(
        host=host or cfg.host,
        port=port or cfg.port,
        calling_ae=calling_ae or cfg.calling_ae,
        called_ae=called_ae or cfg.called_ae_mwl,
        modalidade=modalidade,
        accession=accession,
    )

    if not items:
        console.print("[yellow]Nenhum item encontrado na worklist.[/yellow]")
        raise typer.Exit(0)

    tabela = Table(show_header=True, header_style="bold cyan")
    tabela.add_column("Accession", style="bold")
    tabela.add_column("Paciente")
    tabela.add_column("Agendado")
    tabela.add_column("Modal")
    tabela.add_column("Descrição", no_wrap=False)

    for it in items:
        tabela.add_row(
            it.accession_number or "—",
            it.patient_name.replace("^", " ").strip() or "—",
            it.scheduled_datetime or "—",
            it.modalidade or "—",
            it.descricao or "—",
        )

    console.print(tabela)
    console.print(f"\n[dim]{len(items)} item(ns) encontrado(s).[/dim]")
