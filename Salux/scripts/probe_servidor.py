"""Probe inicial: descobre identidade, oratab, ORACLE_HOME do servidor.

Não roda nada de DB. Só shell read-only.
"""
from __future__ import annotations

from ssh_servidor import conectar, executar


def main() -> int:
    cli = conectar()
    try:
        for titulo, cmd in [
            ("whoami", "whoami && id && uname -a"),
            ("/etc/oratab", "cat /etc/oratab 2>/dev/null || echo '(não existe)'"),
            ("oracle user existe", "id oracle 2>/dev/null || echo '(não existe)'"),
            ("ORACLE env no shell de root", "env | grep -iE 'ORACLE|TNS' || echo '(sem oracle env)'"),
            ("processos oracle", "ps -ef | grep -E 'ora_|tnslsnr' | grep -v grep | head -10"),
            ("ORACLE_HOME (do processo pmon)", "ls -l /proc/$(pgrep -f 'ora_pmon_ORASX01' | head -1)/cwd 2>/dev/null || echo '(pmon não encontrado)'"),
            ("oraenv disponível", "which oraenv 2>/dev/null || ls /usr/local/bin/oraenv 2>/dev/null || echo '(não)'"),
        ]:
            print(f"\n=== {titulo} ===")
            rc, out, err = executar(cli, cmd)
            print(out.rstrip())
            if err.strip():
                print(f"[stderr] {err.rstrip()}")
            print(f"[exit {rc}]")
    finally:
        cli.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
