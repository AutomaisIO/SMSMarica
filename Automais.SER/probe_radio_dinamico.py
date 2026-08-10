"""Captura o HTML CRU de um campo dinâmico do tipo radio/checkbox da aba Editar.

Motivo (10/08/2026): a cópia do catálogo gravou 56 campos `radio` e 4 `checkbox` SEM NENHUMA
opção, porque o extrator só lia <option> — que radio não tem. Resultado: a tela renderizava
caixa de texto livre onde o SER espera uma escolha entre valores fixos, e o rótulo vinha com as
opções grudadas ("Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB").

Para corrigir preciso ver como o RichFaces amarra input↔label. NÃO INFERIR: foi exatamente
inferir estrutura que estragou dado em produção antes.

SOMENTE LEITURA. Trocar combo apenas re-renderiza a view.

Uso:  python probe_radio_dinamico.py [CONSULTA] [995]
"""

from __future__ import annotations

import pathlib
import re
import sys

import httpx
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
sys.path.insert(0, str(RAIZ))

from probe_campos_dinamicos import (  # noqa: E402
    BASE, CAP, UA, Editar, login_e_modulo, sopa,
)

TIPO = sys.argv[1] if len(sys.argv) > 1 else "CONSULTA"
RECURSO = sys.argv[2] if len(sys.argv) > 2 else "995"


def evento_do_combo(html: str, campo: str) -> str | None:
    """Id do a4j:support lido do onchange — j_id é posicional e muda se a SES-RJ recompilar."""
    sel = re.search(r'<select[^>]*name="' + re.escape(campo) + r'"[^>]*>', html)
    if not sel:
        return None
    m = re.search(r"'similarityGroupingId'\s*:\s*'([^']+)'", sel.group(0))
    return m.group(1) if m else None


with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                  follow_redirects=True, verify=False) as c:
    login_e_modulo(c)
    ed = Editar(c)
    ed.abrir()

    evt_tipo = evento_do_combo(ed.full, "form0:comboTipoRecurso")
    ed.mudar("form0:comboTipoRecurso", TIPO, evt_tipo)
    evt_rec = evento_do_combo(ed.full, "form0:comboRecurso")

    # A RESPOSTA, não `ed.full`: a troca de combo devolve resposta A4J PARCIAL, que não traz o
    # <form id="form0"> — então `full` continua sendo a página anterior, sem os campos do recurso.
    html = ed.mudar("form0:comboRecurso", RECURSO, evt_rec)
    (CAP / f"criar_radio_{TIPO}_{RECURSO}.html").write_text(html, encoding="utf-8")

    d = sopa(html)
    achou = 0
    for cont in d.find_all(id=re.compile(r"^form0:container_dinamico_id_\d+$")):
        if not cont.find("input", attrs={"type": re.compile("radio|checkbox")}):
            continue
        achou += 1
        print("=" * 78)
        print(f"container {cont['id']}")
        print("=" * 78)
        print(cont.prettify()[:4000])
        if achou >= 2:
            break

    print(f"\n{achou} container(es) radio/checkbox impressos.")
    print(f"HTML salvo em capturas/criar_radio_{TIPO}_{RECURSO}.html")
