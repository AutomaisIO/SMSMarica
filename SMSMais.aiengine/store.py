"""
Persistência do motor de Agente IA (SQLite).

O operador precisa fechar a aba, perder internet ou pegar um deploy no meio do caminho e, ao
reabrir o painel, encontrar tudo lá. Enquanto sessões e eventos vivem só na memória do processo,
qualquer restart — deploy, reboot — apaga a conversa inteira em silêncio.

SQLite e não Postgres de propósito: isto é estado local do serviço, não dado de negócio. Não
pertence ao banco da Prefeitura (que é compartilhado com outros produtos), não precisa de
migration EF, e sobrevive a restart sem depender da rede.

Fica em /var/lib, FORA de /opt: todo deploy roda `find $APP_DIR -delete`.
"""
import json
import os
import sqlite3
import threading
import time
from pathlib import Path
from typing import Any, Optional

DB_PATH = Path(os.getenv("AIENGINE_DB_PATH", "/var/lib/smsmarica-aiengine/aiengine.db"))

_SCHEMA = """
CREATE TABLE IF NOT EXISTS sessions (
    id            TEXT PRIMARY KEY,
    title         TEXT NOT NULL DEFAULT '',
    ticket_numero INTEGER,
    ticket_titulo TEXT,
    created_at    REAL NOT NULL,
    last_used_at  REAL NOT NULL,
    archived_at   REAL
);
CREATE TABLE IF NOT EXISTS turns (
    id          TEXT PRIMARY KEY,
    session_id  TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    prompt      TEXT NOT NULL,
    status      TEXT NOT NULL,
    error       TEXT,
    started_at  REAL NOT NULL,
    finished_at REAL
);
CREATE TABLE IF NOT EXISTS events (
    turn_id TEXT NOT NULL REFERENCES turns(id) ON DELETE CASCADE,
    seq     INTEGER NOT NULL,
    payload TEXT NOT NULL,
    PRIMARY KEY (turn_id, seq)
);
CREATE INDEX IF NOT EXISTS idx_turns_session ON turns(session_id, started_at);
CREATE INDEX IF NOT EXISTS idx_sessions_ticket ON sessions(ticket_numero);
"""

# Colunas acrescentadas depois do schema inicial. SQLite não tem "ADD COLUMN IF NOT EXISTS",
# então tentamos e ignoramos o erro de duplicata — mais simples que versionar migrations
# para um banco que é estado local de um único serviço.
_COLUNAS_EXTRA = {
    "sessions": [
        # Diretório em que a sessão do Claude Code nasceu. O transcript é guardado POR PROJETO
        # (~/.claude/projects/<cwd-slug>/<id>.jsonl); retomar com cwd diferente do original faz o
        # CLI não achar a sessão e sair com código 1. Aconteceu quando o clone do repositório
        # passou a existir e sessões criadas em modo degradado (/tmp) viraram 500 (2026-07-19).
        ("cwd", "TEXT"),
        # Id da sessão DO CLAUDE. Normalmente igual ao nosso id, mas se o resume ficar
        # impossível (cwd mudou, transcript corrompido) trocamos só este e preservamos o
        # histórico que o painel mostra.
        ("claude_session_id", "TEXT"),
        # Quem abriu a sessão. A lista é global (todo operador do agente vê todas), então sem
        # isto não dá para saber de quem é cada conversa.
        ("usuario_id", "TEXT"),
        ("usuario_nome", "TEXT"),
        # Tipo da sessão: 'agente' (Agente IA, trabalha o repo) ou 'dados' (menu IA, só
        # consulta banco, sandbox restrito). Default 'agente' backfilla as sessões antigas.
        ("kind", "TEXT NOT NULL DEFAULT 'agente'"),
        # Slug da base (ia_fonte) desta sessão de dados. Fixo por sessão: o tool consulta
        # SEMPRE esta base, o modelo não troca. Nulo nas sessões 'agente'.
        ("base_slug", "TEXT"),
    ],
    # O autor da sessão não é a história toda: um colega pode assumir a investigação no meio.
    # Guardando quem mandou CADA turno, a sessão mostra todos os que participaram.
    "turns": [
        ("usuario_id", "TEXT"),
        ("usuario_nome", "TEXT"),
    ],
}


class Store:
    def __init__(self, path: Path = DB_PATH) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        self._db = sqlite3.connect(str(path), check_same_thread=False)
        self._db.row_factory = sqlite3.Row
        self._db.execute("PRAGMA journal_mode=WAL")
        self._db.execute("PRAGMA synchronous=NORMAL")
        self._db.execute("PRAGMA foreign_keys=ON")
        self._db.executescript(_SCHEMA)
        for tabela, colunas in _COLUNAS_EXTRA.items():
            for coluna, tipo in colunas:
                try:
                    self._db.execute(f"ALTER TABLE {tabela} ADD COLUMN {coluna} {tipo}")
                except sqlite3.OperationalError:
                    pass  # já existe
        self._db.commit()
        self._lock = threading.Lock()

    def _write(self, sql: str, args: tuple = ()) -> None:
        with self._lock:
            self._db.execute(sql, args)
            self._db.commit()

    def _rows(self, sql: str, args: tuple = ()) -> list[sqlite3.Row]:
        with self._lock:
            return self._db.execute(sql, args).fetchall()

    # ------------------------------------------------------------------ sessões

    def create_session(self, sid: str, title: str, ticket_numero: Optional[int],
                       ticket_titulo: Optional[str], cwd: str,
                       usuario_id: Optional[str] = None,
                       usuario_nome: Optional[str] = None,
                       kind: str = "agente", base_slug: Optional[str] = None) -> None:
        now = time.time()
        self._write(
            "INSERT INTO sessions (id, title, ticket_numero, ticket_titulo, created_at,"
            " last_used_at, cwd, claude_session_id, usuario_id, usuario_nome, kind, base_slug)"
            " VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
            (sid, title, ticket_numero, ticket_titulo, now, now, cwd, sid,
             usuario_id, usuario_nome, kind, base_slug),
        )

    def reset_claude_session(self, sid: str, novo_claude_id: str, cwd: str) -> None:
        """Troca o id da sessão do Claude preservando o histórico que o painel mostra.

        Usado quando retomar é impossível — a memória do modelo recomeça, mas a conversa
        na tela continua inteira.
        """
        self._write("UPDATE sessions SET claude_session_id=?, cwd=? WHERE id=?",
                    (novo_claude_id, cwd, sid))

    def touch_session(self, sid: str, title: Optional[str] = None) -> None:
        if title:
            self._write(
                "UPDATE sessions SET last_used_at=?, title=CASE WHEN title='' THEN ? ELSE title END"
                " WHERE id=?", (time.time(), title, sid))
        else:
            self._write("UPDATE sessions SET last_used_at=? WHERE id=?", (time.time(), sid))

    def rename_session(self, sid: str, title: str) -> bool:
        if not self._rows("SELECT id FROM sessions WHERE id=?", (sid,)):
            return False
        self._write("UPDATE sessions SET title=? WHERE id=?", (title, sid))
        return True

    def archive_session(self, sid: str) -> bool:
        if not self._rows("SELECT id FROM sessions WHERE id=? AND archived_at IS NULL", (sid,)):
            return False
        self._write("UPDATE sessions SET archived_at=? WHERE id=?", (time.time(), sid))
        return True

    def unarchive_session(self, sid: str) -> bool:
        if not self._rows("SELECT id FROM sessions WHERE id=? AND archived_at IS NOT NULL", (sid,)):
            return False
        # `last_used_at` volta a ser agora: restaurar uma conversa é para trabalhar nela, e a
        # lista é ordenada por uso — senão ela reaparece enterrada no fim.
        self._write("UPDATE sessions SET archived_at=NULL, last_used_at=? WHERE id=?",
                    (time.time(), sid))
        return True

    def get_session(self, sid: str) -> Optional[dict]:
        rows = self._rows("SELECT * FROM sessions WHERE id=?", (sid,))
        return dict(rows[0]) if rows else None

    def find_open_by_ticket(self, numero: int) -> Optional[dict]:
        """Reabrir o mesmo ticket deve cair na conversa que já existe, não abrir outra."""
        rows = self._rows(
            "SELECT * FROM sessions WHERE ticket_numero=? AND archived_at IS NULL"
            " ORDER BY last_used_at DESC LIMIT 1", (numero,))
        return dict(rows[0]) if rows else None

    def list_sessions(self, include_archived: bool = False, limit: int = 50,
                      kind: str = "agente", usuario_id: Optional[str] = None) -> list[dict]:
        # Segmentação: 'agente' é global (todo operador vê todas); 'dados' é POR USUÁRIO
        # (cada um vê só as suas conversas com o banco). Quem decide o kind é o chamador.
        sql = ("SELECT s.*, (SELECT COUNT(*) FROM turns t WHERE t.session_id=s.id) AS turn_count"
               " FROM sessions s WHERE s.kind = ?")
        args: list = [kind]
        if not include_archived:
            sql += " AND s.archived_at IS NULL"
        if usuario_id is not None:
            sql += " AND s.usuario_id = ?"
            args.append(usuario_id)
        sql += " ORDER BY s.last_used_at DESC LIMIT ?"
        args.append(limit)
        sessoes = [dict(r) for r in self._rows(sql, tuple(args))]
        participantes = self.participantes([s["id"] for s in sessoes])
        for s in sessoes:
            s["participantes"] = participantes.get(s["id"], [])
        return sessoes

    def participantes(self, sids: list[str]) -> dict[str, list[str]]:
        """Quem interagiu em cada sessão, na ordem em que entrou.

        O autor abre a conversa, mas quem toca nela depois também aparece — é comum um
        colega assumir a investigação de um ticket no meio.
        """
        if not sids:
            return {}
        marcas = ",".join("?" * len(sids))
        rows = self._rows(
            f"SELECT session_id, usuario_nome, MIN(started_at) AS primeiro FROM turns"
            f" WHERE session_id IN ({marcas}) AND usuario_nome IS NOT NULL AND usuario_nome <> ''"
            f" GROUP BY session_id, usuario_nome ORDER BY primeiro", tuple(sids))
        out: dict[str, list[str]] = {}
        for r in rows:
            out.setdefault(r["session_id"], []).append(r["usuario_nome"])
        return out

    def prune_sessions(self, older_than_days: int) -> int:
        cutoff = time.time() - older_than_days * 86400
        rows = self._rows("SELECT id FROM sessions WHERE last_used_at < ?", (cutoff,))
        for row in rows:
            self._write("DELETE FROM sessions WHERE id=?", (row["id"],))
        return len(rows)

    # ------------------------------------------------------------------ turnos

    def create_turn(self, tid: str, sid: str, prompt: str, started_at: float,
                    usuario_id: Optional[str] = None,
                    usuario_nome: Optional[str] = None) -> None:
        self._write(
            "INSERT INTO turns (id, session_id, prompt, status, started_at, usuario_id,"
            " usuario_nome) VALUES (?,?,?,?,?,?,?)",
            (tid, sid, prompt, "running", started_at, usuario_id, usuario_nome))

    def finish_turn(self, tid: str, status: str, error: Optional[str]) -> None:
        self._write("UPDATE turns SET status=?, error=?, finished_at=? WHERE id=?",
                    (status, error, time.time(), tid))

    def get_turn(self, tid: str) -> Optional[dict]:
        rows = self._rows("SELECT * FROM turns WHERE id=?", (tid,))
        return dict(rows[0]) if rows else None

    def append_event(self, tid: str, seq: int, payload: dict[str, Any]) -> None:
        self._write("INSERT OR REPLACE INTO events (turn_id, seq, payload) VALUES (?,?,?)",
                    (tid, seq, json.dumps(payload, ensure_ascii=False)))

    def events(self, tid: str, cursor: int = 0) -> list[dict]:
        rows = self._rows(
            "SELECT payload FROM events WHERE turn_id=? AND seq>=? ORDER BY seq", (tid, cursor))
        return [json.loads(r["payload"]) for r in rows]

    def event_count(self, tid: str) -> int:
        rows = self._rows("SELECT COUNT(*) AS c FROM events WHERE turn_id=?", (tid,))
        return int(rows[0]["c"]) if rows else 0

    def session_history(self, sid: str) -> list[dict]:
        turns = self._rows("SELECT * FROM turns WHERE session_id=? ORDER BY started_at", (sid,))
        return [{**dict(t), "events": self.events(t["id"])} for t in turns]

    # ------------------------------------------------------------------ recuperação

    def recover_orphan_turns(self) -> int:
        """Turno 'running' com o processo morto nunca termina sozinho.

        Sem isto o painel reataria e ficaria em "Trabalhando…" para sempre, esperando um
        turno cuja task morreu junto com o processo anterior.
        """
        rows = self._rows("SELECT id FROM turns WHERE status='running'")
        for row in rows:
            self.finish_turn(row["id"], "interrupted",
                             "O serviço reiniciou durante este turno (deploy ou reboot).")
        return len(rows)


store = Store()
