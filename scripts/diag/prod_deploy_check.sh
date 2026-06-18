#!/bin/bash
echo "=== Data do DLL deployado ==="
ls -la --time-style=full-iso /opt/smsmarica/server/SMSMarica.Core.dll /opt/smsmarica/server/SMSMarica.Api.dll 2>/dev/null
echo
echo "=== Status do servico ==="
systemctl status smsmarica-server --no-pager 2>/dev/null | grep -E "Active|Main PID|since"
echo
echo "=== Health ==="
curl -s http://127.0.0.1:5080/health; echo
echo
echo "=== Ultimo log de envio de MWL (worklist) ==="
journalctl -u smsmarica-server --no-pager -n 2000 2>/dev/null | grep -iE "mwl|worklist|recus" | tail -10
