#!/bin/bash
# Instala/atualiza o monitor do PACS no servidor do dcm4chee (docs/pacs.md §11.1).
# Rodar como root NO servidor do PACS, de dentro da pasta com estes arquivos:
#   bash instalar.sh
# Idempotente. Não mexe no dcm4chee: só troca o que o restart semanal executa (com backup).
set -euo pipefail
cd "$(dirname "$0")"

install -d -m 755 /opt/pacs-monitor /var/lib/pacs-monitor
install -m 755 pacs-monitor.py /opt/pacs-monitor/pacs-monitor.py

if [ ! -f /etc/pacs-monitor.env ]; then
  install -m 600 pacs-monitor.env.exemplo /etc/pacs-monitor.env
  echo ">> /etc/pacs-monitor.env criado SEM chave — preencha PACS_MONITOR_CHAVE antes de confiar nos avisos."
fi

# Backup do restart semanal antigo (só a primeira vez).
if [ -f /etc/systemd/system/dcm4chee-restart.service ] && [ ! -f /root/backup-dcm4chee-restart.service.antes-monitor ]; then
  cp /etc/systemd/system/dcm4chee-restart.service /root/backup-dcm4chee-restart.service.antes-monitor
fi

install -m 644 pacs-monitor.service pacs-monitor.timer dcm4chee-restart.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now pacs-monitor.timer
# O dcm4chee-restart.timer (domingo 00:00) não muda — só o service que ele dispara.

echo ">> instalado. Conferir:"
echo "   systemctl list-timers pacs-monitor.timer dcm4chee-restart.timer"
echo "   journalctl -u pacs-monitor -n 20"
echo "   set -a; . /etc/pacs-monitor.env; set +a; python3 /opt/pacs-monitor/pacs-monitor.py testar   # aviso de teste"
