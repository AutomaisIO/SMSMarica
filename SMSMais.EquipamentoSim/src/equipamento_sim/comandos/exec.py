"""Comando `equipamento exec` — simula execução de um exame (worklist → C-STORE)."""

from __future__ import annotations

from pathlib import Path

import typer
from rich.console import Console

from equipamento_sim.config import carregar
from equipamento_sim.dicom.cliente_mwl import consultar_worklist
from equipamento_sim.dicom.cliente_store import enviar
from equipamento_sim.dicom.gerador_dataset import (
    carregar_imagem_arquivo,
    gerar_imagem_fantasma,
    montar_dataset_a_partir_do_worklist,
)


def comando(
    accession: str = typer.Option(..., "--accession", "-a", help="AccessionNumber do exame a executar."),
    fantasma: bool = typer.Option(False, "--fantasma", help="Gera imagem sintética (gradiente)."),
    imagem: Path = typer.Option(None, "--imagem", help="Caminho para imagem JPEG/PNG real."),
    host: str = typer.Option(None, help="Host do dcm4chee."),
    port: int = typer.Option(None, help="Porta DICOM."),
    calling_ae: str = typer.Option(None, "--calling-ae"),
    called_ae_mwl: str = typer.Option(None, "--called-ae-mwl"),
    called_ae_store: str = typer.Option(None, "--called-ae-store"),
) -> None:
    """Busca o worklist item por AccessionNumber, gera o DICOM e envia via C-STORE."""

    if not fantasma and imagem is None:
        raise typer.BadParameter("Informe --fantasma ou --imagem <arquivo>.")
    if imagem is not None and not imagem.exists():
        raise typer.BadParameter(f"Arquivo não encontrado: {imagem}")

    cfg = carregar()
    console = Console()

    host_ = host or cfg.host
    port_ = port or cfg.port
    calling_ = calling_ae or cfg.calling_ae

    console.print(f"[cyan]Buscando worklist item AccessionNumber={accession}…[/cyan]")
    items = consultar_worklist(
        host=host_,
        port=port_,
        calling_ae=calling_,
        called_ae=called_ae_mwl or cfg.called_ae_mwl,
        accession=accession,
    )

    if not items:
        console.print(f"[red]Nenhum item encontrado para {accession}.[/red]")
        raise typer.Exit(1)

    item = items[0]
    console.print(
        f"[green]✓[/green] Encontrado: {item.patient_name.replace('^', ' ')} — "
        f"{item.descricao} ({item.modalidade})"
    )

    console.print("[cyan]Gerando DICOM…[/cyan]")
    if fantasma:
        pixels = gerar_imagem_fantasma()
    else:
        pixels = carregar_imagem_arquivo(imagem)
    ds = montar_dataset_a_partir_do_worklist(item, pixels)
    console.print(
        f"[green]✓[/green] Dataset montado. "
        f"StudyInstanceUID herdado: [dim]{ds.StudyInstanceUID}[/dim]"
    )

    console.print(
        f"[cyan]Enviando via C-STORE para {called_ae_store or cfg.called_ae_store}@{host_}:{port_}…[/cyan]"
    )
    ok, msg = enviar(
        host=host_,
        port=port_,
        calling_ae=calling_,
        called_ae=called_ae_store or cfg.called_ae_store,
        dataset=ds,
    )
    if ok:
        console.print(f"[green]✓[/green] {msg}")
        console.print(
            "[dim]O SMSMais detectará o study no próximo polling QIDO-RS (~30s) "
            "e marcará a solicitação como Realizada.[/dim]"
        )
    else:
        console.print(f"[red]✗ {msg}[/red]")
        raise typer.Exit(1)
