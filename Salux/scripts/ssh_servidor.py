"""SSH no servidor Oracle (10.50.0.18) como root.

Encapsula paramiko. Usar com cuidado — máquina de produção.
"""
from __future__ import annotations

import os
import pathlib

import paramiko
from dotenv import load_dotenv

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
load_dotenv(_RAIZ / ".env")


def conectar() -> paramiko.SSHClient:
    cli = paramiko.SSHClient()
    cli.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    cli.connect(
        hostname=os.environ["SALUX_SSH_HOST"],
        username=os.environ["SALUX_SSH_USER"],
        password=os.environ["SALUX_SSH_PASSWORD"],
        timeout=15,
        allow_agent=False,
        look_for_keys=False,
    )
    return cli


def executar(cli: paramiko.SSHClient, cmd: str, stdin_text: str | None = None, timeout: int = 60) -> tuple[int, str, str]:
    stdin, stdout, stderr = cli.exec_command(cmd, timeout=timeout)
    if stdin_text is not None:
        stdin.write(stdin_text)
        stdin.channel.shutdown_write()
    out = stdout.read().decode("utf-8", errors="replace")
    err = stderr.read().decode("utf-8", errors="replace")
    rc = stdout.channel.recv_exit_status()
    return rc, out, err
