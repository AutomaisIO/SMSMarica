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


class Store:
    def __init__(self, path: Path = DB_PATH) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        self._db = sqlite3.connect(str(path), check_same_thread=False)
        self._db.row_factory = sqlite3.Row
        self._db.execute("PRAGMA journal_mode=WAL")
        self._db.execute("PRAGMA synchronous=NORMAL")
        self._db.execute("PRAGMA foreign_keys=ON")
        self._db.executescript(_SCHEMA)
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
                       ticket_titulo: Optional[str]) -> None:
        now = time.time()
        self._write(
            "INSERT INTO sessions (id, title, ticket_numero, ticket_titulo, created_at, last_used_at)"
            " VALUES (?,?,?,?,?,?)",
            (sid, title, ticket_numero, ticket_titulo, now, now),
        )

    def touch_session(self, sid: str, title: Optional[str] = None) -> None:
        if title:
            self._write(
                "UPDATE sessions SET last_used_at=?, title=CASE WHEN title='' THEN ? ELSE title END"
                " WHERE id=?", (time.time(), title, sid))
        else:
            self._write("UPDATE sessions SET last_used_at=? WHERE id=?", (time.time(), sid))

    def archive_session(self, sid: str) -> bool:
        if not self._rows("SELECT id FROM sessions WHERE id=? AND archived_at IS NULL", (sid,)):
            return False
        self._write("UPDATE sessions SET archived_at=? WHERE id=?", (time.time(), sid))
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

    def list_sessions(self, include_archived: bool = False, limit: int = 50) -> list[dict]:
        sql = ("SELECT s.*, (SELECT COUNT(*) FROM turns t WHERE t.session_id=s.id) AS turn_count"
               " FROM sessions s")
        if not include_archived:
            sql += " WHERE s.archived_at IS NULL"
        sql += " ORDER BY s.last_used_at DESC LIMIT ?"
        return [dict(r) for r in self._rows(sql, (limit,))]

    def prune_sessions(self, older_than_days: int) -> int:
        cutoff = time.time() - older_than_days * 86400
        rows = self._rows("SELECT id FROM sessions WHERE last_used_at < ?", (cutoff,))
        for row in rows:
            self._write("DELETE FROM sessions WHERE id=?", (row["id"],))
        return len(rows)

    # ------------------------------------------------------------------ turnos

    def create_turn(self, tid: str, sid: str, prompt: str, started_at: float) -> None:
        self._write(
            "INSERT INTO turns (id, session_id, prompt, status, started_at) VALUES (?,?,?,?,?)",
            (tid, sid, prompt, "running", started_at))

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
