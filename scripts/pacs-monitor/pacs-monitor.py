#!/usr/bin/env python3
"""
Monitor que roda DENTRO do servidor do PACS (dcm4chee) e avisa a plataforma SMSMais, que manda
para o celular pelo caminho de sempre (POST /alertas-plataforma/externo → freio → WhatsApp →
tela Sistema → Avisos no celular). Ver docs/pacs.md §11.1.

Dois modos:
  verificar   chamado a cada minuto pelo pacs-monitor.timer
  reiniciar   chamado pelo dcm4chee-restart.timer (domingo 00:00): avisa que vai reiniciar,
              reinicia, espera o PACS responder e avisa que voltou (ou que NÃO voltou)

Avisa por TRANSIÇÃO, não por estado: um problema que continua não gera mensagem a cada minuto.
Quando ele some, sai um "voltou ao normal". Só biblioteca padrão (o host é Ubuntu 22.04, Python
3.10), sem pip.

Nada aqui escreve no dcm4chee além do restart programado — o resto é leitura.
"""
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime

# ---------------------------------------------------------------- configuração

URL = os.environ.get("PACS_MONITOR_URL", "")  # https://api.smsmarica.online/alertas-plataforma/externo
CHAVE = os.environ.get("PACS_MONITOR_CHAVE", "")

QIDO = os.environ.get(
    "PACS_MONITOR_QIDO", "http://localhost:8080/dcm4chee-arc/aets/PACS-CDT/rs/studies?limit=1")
STORAGE = os.environ.get("PACS_MONITOR_STORAGE", "/mnt/s3images")
SERVER_LOG = os.environ.get("PACS_MONITOR_SERVER_LOG", "/opt/wildfly/standalone/log/server.log")
JBOSS_CLI = os.environ.get("PACS_MONITOR_JBOSS_CLI", "/opt/wildfly/bin/jboss-cli.sh")

# Falha contínua por este tempo antes de avisar (o restart semanal leva ~1 min).
TOLERANCIA_S = int(os.environ.get("PACS_MONITOR_TOLERANCIA_S", "120"))
# Memória direta do Java: % do limite que já é alerta (o OOM de 03/10 foi em 100%).
DIRETA_ALERTA_PCT = int(os.environ.get("PACS_MONITOR_DIRETA_ALERTA_PCT", "80"))
# A leitura da memória do Java custa ~5 s de CPU (sobe um jboss-cli): não a cada minuto.
DIRETA_INTERVALO_S = int(os.environ.get("PACS_MONITOR_DIRETA_INTERVALO_S", "600"))
# Memória livre do host (MemAvailable) abaixo disto por TOLERANCIA_S = alerta. Host de 2 GB.
HOST_LIVRE_MIN_MB = int(os.environ.get("PACS_MONITOR_HOST_LIVRE_MIN_MB", "120"))
# Quanto o restart programado espera o PACS voltar antes de dizer que não voltou.
REINICIO_ESPERA_S = int(os.environ.get("PACS_MONITOR_REINICIO_ESPERA_S", "300"))

DIR_ESTADO = os.environ.get("PACS_MONITOR_DIR_ESTADO", "/var/lib/pacs-monitor")
ARQ_ESTADO = os.path.join(DIR_ESTADO, "estado.json")
ARQ_PENDENTES = os.path.join(DIR_ESTADO, "pendentes.jsonl")
FLAG_REINICIANDO = "/run/pacs-monitor-reiniciando"  # some no boot; vence sozinha (ver abaixo)
FLAG_VALIDADE_S = REINICIO_ESPERA_S + 120

# Fontes aceitas pela plataforma (AlertaCatalogo.AceitaDeMonitorExterno).
MEMORIA, SERVICO, RECUPERADO = "pacs.memoria", "pacs.servico", "pacs.recuperado"
REINICIO, REINICIO_CONCLUIDO = "pacs.reinicio", "pacs.reinicio_concluido"


def log(msg):
    print(f"{datetime.now():%Y-%m-%d %H:%M:%S} {msg}", flush=True)


# ---------------------------------------------------------------- envio à plataforma

def _post(evento):
    corpo = json.dumps(evento).encode("utf-8")
    req = urllib.request.Request(
        URL, data=corpo, method="POST",
        headers={"Content-Type": "application/json", "X-Monitor-Chave": CHAVE})
    with urllib.request.urlopen(req, timeout=15) as r:
        return r.status


def avisar(origem, titulo, detalhe=""):
    """Manda à plataforma. Se ela não responder, guarda e tenta de novo na próxima passagem."""
    evento = {"origem": origem, "titulo": titulo, "detalhe": detalhe,
              "registradoEm": datetime.now().isoformat(timespec="seconds")}
    log(f"AVISO {origem}: {titulo} — {detalhe}")
    if not URL or not CHAVE:
        log("PACS_MONITOR_URL/PACS_MONITOR_CHAVE não configurados — aviso só no journal.")
        return
    try:
        _post(evento)
    except (urllib.error.URLError, OSError) as e:
        log(f"plataforma não recebeu ({e}) — guardado para reenviar")
        with open(ARQ_PENDENTES, "a", encoding="utf-8") as f:
            f.write(json.dumps(evento, ensure_ascii=False) + "\n")


def reenviar_pendentes():
    if not os.path.exists(ARQ_PENDENTES) or not URL or not CHAVE:
        return
    with open(ARQ_PENDENTES, encoding="utf-8") as f:
        linhas = [l for l in f.read().splitlines() if l.strip()]
    sobra = []
    for linha in linhas[-20:]:  # se acumulou muito, os mais antigos perderam o sentido
        try:
            ev = json.loads(linha)
        except ValueError:
            continue
        ev["detalhe"] = f"{ev.get('detalhe', '')} (registrado em {ev.get('registradoEm')}, entregue atrasado)".strip()
        try:
            _post(ev)
        except (urllib.error.URLError, OSError):
            sobra.append(linha)
    if sobra:
        with open(ARQ_PENDENTES, "w", encoding="utf-8") as f:
            f.write("\n".join(sobra) + "\n")
    else:
        os.remove(ARQ_PENDENTES)


# ---------------------------------------------------------------- estado

def carregar_estado():
    try:
        with open(ARQ_ESTADO, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def salvar_estado(estado):
    tmp = ARQ_ESTADO + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(estado, f, ensure_ascii=False, indent=1)
    os.replace(tmp, ARQ_ESTADO)


def reiniciando():
    try:
        return time.time() - os.path.getmtime(FLAG_REINICIANDO) < FLAG_VALIDADE_S
    except OSError:
        return False


# ---------------------------------------------------------------- sondas (só leitura)

def sonda_http(timeout=15):
    """(ok, detalhe) da consulta leve de studies no dcm4chee."""
    try:
        with urllib.request.urlopen(QIDO, timeout=timeout) as r:
            return r.status == 200, f"HTTP {r.status}"
    except urllib.error.HTTPError as e:
        return False, f"HTTP {e.code}"
    except (urllib.error.URLError, OSError) as e:
        return False, str(getattr(e, "reason", e))


def sonda_storage():
    """O s3fs morto trava stat para sempre — por isso via subprocess com timeout."""
    alvo = os.path.join(STORAGE, str(datetime.now().year))
    try:
        r = subprocess.run(["stat", "-c", "%n", alvo], capture_output=True, text=True, timeout=20)
        if r.returncode == 0:
            return True, "ok"
        return False, (r.stderr.strip() or f"stat saiu {r.returncode}")[:200]
    except subprocess.TimeoutExpired:
        return False, "stat travou por 20 s (mount do s3fs morto?)"


def sonda_servico_ativo():
    r = subprocess.run(["systemctl", "is-active", "dcm4chee"], capture_output=True, text=True)
    return r.stdout.strip()


def ler_memoria_host_mb():
    info = {}
    with open("/proc/meminfo") as f:
        for linha in f:
            k, v = linha.split(":", 1)
            info[k] = int(v.split()[0]) // 1024
    swap_usado = info.get("SwapTotal", 0) - info.get("SwapFree", 0)
    return info["MemAvailable"], info["MemTotal"], swap_usado, info.get("SwapTotal", 0)


def ler_memoria_java():
    """(direta_usada, limite) em bytes, pelo jboss-cli. Limite = heap máx (é o default do
    MaxDirectMemorySize quando ele não é passado — e não é, ver docs/pacs.md §10.7)."""
    cmds = ("/core-service=platform-mbean/type=buffer-pool/name=direct:read-attribute(name=memory-used),"
            "/core-service=platform-mbean/type=memory:read-attribute(name=heap-memory-usage)")
    try:
        r = subprocess.run([JBOSS_CLI, "-c", f"--commands={cmds}"],
                           capture_output=True, text=True, timeout=60, cwd="/tmp")
    except subprocess.TimeoutExpired:
        return None
    usado = re.search(r'"result" => (\d+)L', r.stdout)
    limite = re.search(r'"max" => (\d+)L', r.stdout)
    if not usado or not limite:
        return None
    return int(usado.group(1)), int(limite.group(1))


def uptime_servico():
    r = subprocess.run(["systemctl", "show", "dcm4chee", "-p", "ActiveEnterTimestamp", "--value"],
                       capture_output=True, text=True)
    try:
        inicio = datetime.strptime(" ".join(r.stdout.split()[1:3]), "%Y-%m-%d %H:%M:%S")
        return datetime.now() - inicio
    except ValueError:
        return None


def mb(b):
    return f"{b / 1024 / 1024:.0f} MB"


def fmt_duracao(s):
    s = int(s)
    if s < 90:
        return f"{s} s"
    if s < 5400:
        return f"{s // 60} min"
    if s < 172800:
        return f"{s / 3600:.1f} h"
    return f"{s / 86400:.1f} dias"


# ---------------------------------------------------------------- verificações de estado

def checar(estado, nome, ok, origem, titulo, detalhe, agora, tolerancia=TOLERANCIA_S):
    """Transição ok→falha (após a tolerância) avisa uma vez; falha→ok avisa a volta."""
    st = estado.setdefault(nome, {})
    if ok:
        if st.get("avisado"):
            dur = agora - st.get("desde", agora)
            avisar(RECUPERADO, f"{titulo}: voltou ao normal", f"Ficou {fmt_duracao(dur)} com problema.")
        estado[nome] = {}
        return
    st.setdefault("desde", agora)
    st["detalhe"] = detalhe
    if not st.get("avisado") and agora - st["desde"] >= tolerancia:
        avisar(origem, titulo, f"{detalhe} (há {fmt_duracao(agora - st['desde'])})")
        st["avisado"] = True


def verificar_oom_no_log(estado):
    """OutOfMemoryError novos no server.log desde a última passagem (acompanha a rotação)."""
    try:
        s = os.stat(SERVER_LOG)
    except OSError:
        return
    pos = estado.get("log_pos", {})
    inicio = pos.get("offset", 0) if pos.get("inode") == s.st_ino and pos.get("offset", 0) <= s.st_size else 0
    if not pos:  # primeira execução: não relê o histórico inteiro
        inicio = s.st_size
    tipos = {}
    with open(SERVER_LOG, "rb") as f:
        f.seek(inicio)
        for bruta in f:
            linha = bruta.decode("utf-8", "replace")
            m = re.search(r"OutOfMemoryError: ([A-Za-z ]+)", linha)
            if m:
                tipo = "memória direta" if "direct buffer" in linha or "Cannot reserve" in m.group(1) else m.group(1).strip()
                tipos[tipo] = tipos.get(tipo, 0) + 1
        fim = f.tell()
    estado["log_pos"] = {"inode": s.st_ino, "offset": fim}
    if tipos:
        resumo = ", ".join(f"{n}× {t}" for t, n in tipos.items())
        if "memória direta" in tipos:
            dica = "É o vazamento de 03/10: sem memória direta o banco do PACS para — reiniciar o dcm4chee."
        else:
            dica = "Heap de 512 MB pequeno para alguma imagem grande: aquela imagem falha, o PACS segue no ar."
        avisar(MEMORIA, "Java do PACS ficou sem memória", f"{resumo} no último minuto. {dica}")


def verificar_kernel(estado, agora):
    """segfault (o s3fs de 03/10) e OOM-killer do kernel desde a última passagem."""
    desde = estado.get("kernel_desde") or (agora - 120)
    estado["kernel_desde"] = agora
    r = subprocess.run(
        ["journalctl", "-k", "--since", f"@{int(desde)}", "--no-pager", "-q", "-o", "cat"],
        capture_output=True, text=True, timeout=30)
    segfault = [l for l in r.stdout.splitlines() if "segfault" in l]
    morto = [l for l in r.stdout.splitlines() if "Killed process" in l or "Out of memory" in l]
    if segfault:
        procs = sorted({l.split("[")[0].strip() for l in segfault})
        avisar(SERVICO, "Processo caiu no servidor do PACS (segfault)",
               f"{', '.join(procs)[:200]}. Se for o s3fs, as imagens param de abrir — remontar /mnt/s3images.")
    if morto:
        avisar(MEMORIA, "Servidor do PACS sem memória: o sistema matou processo", morto[-1][:300])


def verificar():
    os.makedirs(DIR_ESTADO, exist_ok=True)
    estado = carregar_estado()
    agora = time.time()

    reenviar_pendentes()

    if reiniciando():
        # O restart programado cuida de avisar; aqui só não confunde a queda dele com falha.
        log("restart programado em andamento — verificação pulada")
        salvar_estado(estado)
        return

    ok_http, det_http = sonda_http()
    ativo = sonda_servico_ativo()
    checar(estado, "servico", ok_http, SERVICO, "PACS não responde",
           f"Consulta de exames: {det_http}; serviço dcm4chee: {ativo}.", agora)

    ok_st, det_st = sonda_storage()
    checar(estado, "storage", ok_st, SERVICO, "Armazenamento das imagens do PACS caiu",
           f"{STORAGE}: {det_st}. As imagens não abrem até remontar (docs/pacs.md §11).", agora)

    livre, total, swap_usado, swap_total = ler_memoria_host_mb()
    checar(estado, "host", livre >= HOST_LIVRE_MIN_MB, MEMORIA, "Servidor do PACS com pouca memória livre",
           f"{livre} MB livres de {total} MB; swap {swap_usado} de {swap_total} MB.", agora)

    if ok_http and agora - estado.get("direta_lida_em", 0) >= DIRETA_INTERVALO_S:
        java = ler_memoria_java()
        estado["direta_lida_em"] = agora
        if java:
            usado, limite = java
            pct = 100 * usado / limite
            estado["direta"] = {"usado": usado, "limite": limite, "em": agora}
            checar(estado, "direta", pct < DIRETA_ALERTA_PCT, MEMORIA, "Memória direta do Java do PACS alta",
                   f"{mb(usado)} de {mb(limite)} ({pct:.0f}%). Em 100% o PACS para (03/10) — "
                   "reiniciar o dcm4chee antes disso.", agora, tolerancia=0)

    verificar_oom_no_log(estado)
    verificar_kernel(estado, agora)
    salvar_estado(estado)


# ---------------------------------------------------------------- restart programado

def reiniciar():
    os.makedirs(DIR_ESTADO, exist_ok=True)
    reenviar_pendentes()

    up = uptime_servico()
    java = ler_memoria_java()
    antes = f"no ar há {fmt_duracao(up.total_seconds())}" if up else "uptime desconhecido"
    if java:
        usado, limite = java
        antes += f"; memória direta {mb(usado)} de {mb(limite)} ({100 * usado / limite:.0f}%)"
    avisar(REINICIO, "Reinício programado do PACS começando",
           f"O PACS fica fora por cerca de 1 minuto. Antes do reinício: {antes}.")

    with open(FLAG_REINICIANDO, "w") as f:
        f.write(str(time.time()))
    t0 = time.time()
    try:
        r = subprocess.run(["systemctl", "restart", "dcm4chee"], capture_output=True, text=True, timeout=180)
        if r.returncode != 0:
            avisar(SERVICO, "Reinício programado do PACS falhou",
                   f"systemctl restart saiu {r.returncode}: {(r.stderr or r.stdout).strip()[:300]}")
            return 1

        ok_http = ok_st = False
        det_http = det_st = ""
        while time.time() - t0 < REINICIO_ESPERA_S:
            time.sleep(5)
            ok_http, det_http = sonda_http(timeout=10)
            if ok_http:
                ok_st, det_st = sonda_storage()
                if ok_st:
                    break
        dur = time.time() - t0
        if ok_http and ok_st:
            avisar(REINICIO_CONCLUIDO, "PACS reiniciado e respondendo",
                   f"Voltou em {fmt_duracao(dur)}; consulta de exames e armazenamento das imagens ok.")
            return 0
        avisar(SERVICO, "PACS NÃO voltou do reinício programado",
               f"Após {fmt_duracao(dur)}: consulta de exames {det_http or 'sem resposta'}; "
               f"armazenamento {det_st or 'não testado'}; serviço dcm4chee: {sonda_servico_ativo()}.")
        return 1
    finally:
        try:
            os.remove(FLAG_REINICIANDO)
        except OSError:
            pass
        # A verificação por minuto recomeça do zero: a queda do restart não conta como falha.
        estado = carregar_estado()
        for k in ("servico", "storage"):
            estado.pop(k, None)
        salvar_estado(estado)


def testar():
    """Manda um aviso de 'voltou ao normal' de teste — confere URL, chave e o caminho todo."""
    avisar(RECUPERADO, "Teste do monitor do PACS", "Mensagem de teste: o monitor consegue avisar a plataforma.")


if __name__ == "__main__":
    modo = sys.argv[1] if len(sys.argv) > 1 else "verificar"
    if modo == "verificar":
        verificar()
    elif modo == "reiniciar":
        sys.exit(reiniciar())
    elif modo == "testar":
        testar()
    else:
        print(__doc__)
        sys.exit(2)
