"""Desenha o relatorio de divergencias a partir do JSON do coletar_divergencias.py.

Denso de proposito: quem abre isto esta cacando problema, nao lendo um resumo. Uma linha por caso,
sem cartao nenhum -- a altura so cresce onde HA divergencia, e ai crescer e o ponto, porque e
exatamente o que se procura na tela.

Separado do coletor porque a varredura dos TXT leva minutos: ajustar layout nao pode custar isso.
"""
from __future__ import annotations

import json
import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

CAP = pathlib.Path(__file__).parent / "capturas"
DADOS = CAP / "_divergencias_dados.json"
SAIDA = CAP / "DIVERGENCIAS.html"

CSS = """
*{box-sizing:border-box}
body{margin:0;font:12px/1.35 ui-sans-serif,-apple-system,"Segoe UI",Roboto,sans-serif;
     color:#18181b;background:#fff}
.mono{font-family:ui-monospace,"Cascadia Mono",Consolas,monospace;font-size:11px}
header{position:sticky;top:0;z-index:20;background:#fff;border-bottom:1px solid #d4d4d8;
       padding:7px 12px}
h1{margin:0;font-size:13px;font-weight:650;display:inline}
.sub{color:#71717a;font-size:11px;margin-left:8px}
.aviso{background:#fffbeb;border:1px solid #fcd34d;border-left:3px solid #d97706;
       padding:5px 10px;margin:6px 12px;font-size:11.5px;line-height:1.45}
.aviso b{color:#92400e}
.barra{display:flex;gap:5px;align-items:center;flex-wrap:wrap;padding:5px 12px;
       border-bottom:1px solid #e4e4e7;position:sticky;top:31px;background:#fafafa;z-index:19}
input[type=search]{font:inherit;padding:3px 7px;border:1px solid #d4d4d8;border-radius:3px;
                   width:220px}
.chip{font-size:11px;padding:2px 8px;border:1px solid #d4d4d8;border-radius:10px;cursor:pointer;
      background:#fff;user-select:none;white-space:nowrap}
.chip.on{background:#18181b;color:#fff;border-color:#18181b}
.sep{width:1px;height:15px;background:#d4d4d8;margin:0 3px}
.cnt{margin-left:auto;color:#71717a;font-size:11px}
table{width:100%;border-collapse:collapse}
thead th{position:sticky;top:62px;background:#f4f4f5;border-bottom:1px solid #d4d4d8;
         text-align:left;font-size:10.5px;font-weight:600;color:#52525b;padding:4px 6px;
         text-transform:uppercase;letter-spacing:.03em;cursor:pointer;white-space:nowrap;z-index:18}
thead th.s{color:#18181b}
tbody td{border-bottom:1px solid #f4f4f5;padding:3px 6px;vertical-align:top}
tbody tr.l:hover{background:#fafafa;cursor:pointer}
tbody tr.l.ab{background:#f4f4f5}
.dif{display:block;color:#b91c1c;background:#fef2f2;padding:0 3px;border-radius:2px;
     width:fit-content;max-width:100%}
.eq{color:#a1a1aa}
.falta{color:#1d4ed8;background:#eff6ff;padding:0 3px;border-radius:2px;width:fit-content}
.tag{display:inline-block;font-size:9.5px;font-weight:600;padding:1px 5px;border-radius:2px;
     white-space:nowrap}
.t-cns{background:#eff6ff;color:#1d4ed8}
.t-cpf{background:#fef2f2;color:#b91c1c}
.t-conflito{background:#fdf4ff;color:#a21caf}
.t-baixa{background:#fffbeb;color:#b45309}
.t-orfao{background:#fef2f2;color:#7f1d1d}
.num{text-align:right;font-variant-numeric:tabular-nums}
.mot{color:#71717a;font-size:11px;max-width:270px;overflow:hidden;text-overflow:ellipsis;
     white-space:nowrap;display:block}
.det{background:#fafafa;border-bottom:2px solid #d4d4d8}
.det .cx{display:flex;gap:18px;padding:8px 10px;align-items:flex-start}
.det h3{margin:0 0 4px;font-size:10.5px;text-transform:uppercase;letter-spacing:.04em;color:#52525b}
.cmp{border-collapse:collapse}
.cmp td,.cmp th{padding:1px 10px 1px 0;font-size:11px;text-align:left;white-space:nowrap}
.cmp th{color:#71717a;font-weight:500}
.ped{border-collapse:collapse;width:100%}
.ped th{font-size:10px;color:#71717a;text-align:left;padding:1px 8px 2px 0;font-weight:600}
.ped td{padding:1px 8px 1px 0;font-size:11px;white-space:nowrap}
.ped tr.fora td{background:#fef2f2;color:#b91c1c}
.velho{color:#b45309;font-weight:600}
.vazio{padding:24px;text-align:center;color:#a1a1aa}
footer{padding:9px 12px;color:#a1a1aa;font-size:10.5px;border-top:1px solid #e4e4e7}
"""

JS = r"""
const D = JSON.parse(document.getElementById('dados').textContent);
const L = D.linhas, G = D.grupos;
const POR_I = new Map(L.map(l => [l.i, l]));
let fGrupo = new Set(), fCampo = new Set(), fSo = new Set(), ordem = 'dif', busca = '';

const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c =>
  ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const br = d => d && d.length === 10 ? d.slice(8,10)+'/'+d.slice(5,7)+'/'+d.slice(0,4) : (d || '');
const ANTIGO = '2023-01-01';

// Campo divergente mostra os DOIS lados: ver so o valor errado nao diz o que conferir.
function cel(l, campo) {
  const d = l.diff[campo];
  const meu = (l.hub && l.hub[campo]) || '', ser = (l.ser && l.ser[campo]) || '';
  if (d) return '<span class="mono">' + esc(d[0]) + '</span><span class="dif mono">' +
                esc(d[1]) + '</span>';
  if (meu) return '<span class="eq mono">' + esc(meu) + '</span>';
  // Vazio nosso com valor do SER nao e divergencia: e lacuna que o SER preenche. Mostrar como
  // traco escondia justamente o que da para consertar sem julgar nada.
  if (ser) return '<span class="falta mono" title="o hub nao tem; o SER tem">' + esc(ser) + '</span>';
  return '<span class="eq">-</span>';
}

function passa(l) {
  if (fGrupo.size && !fGrupo.has(l.grupo)) return false;
  if (fCampo.size && ![...fCampo].some(c => c in l.diff)) return false;
  if (fSo.has('antigo') && !(l.de && l.de < ANTIGO)) return false;
  if (fSo.has('fora') && !l.fora) return false;
  if (fSo.has('semficha') && l.fichas) return false;
  if (fSo.has('lacuna') && !(!(l.hub && l.hub.nascimento) && l.ser && l.ser.nascimento)) return false;
  if (busca) {
    const h = (l.nome + ' ' + l.chave + ' ' + l.motivo + ' ' +
               Object.values(l.diff).flat().join(' ')).toLowerCase();
    if (!h.includes(busca)) return false;
  }
  return true;
}

const ORD = {
  dif:    (a, b) => Object.keys(b.diff).length - Object.keys(a.diff).length || b.qtd - a.qtd,
  qtd:    (a, b) => b.qtd - a.qtd,
  antigo: (a, b) => (a.de || '9') < (b.de || '9') ? -1 : 1,
  nome:   (a, b) => a.nome.localeCompare(b.nome),
};

function detalhe(l) {
  const linha = (rot, campo) => {
    const d = l.diff[campo];
    const meu = (l.hub && l.hub[campo]) || '', ser = (l.ser && l.ser[campo]) || '';
    if (!meu && !ser) return '';
    return '<tr><th>' + rot + '</th><td class="mono">' + (esc(meu) || '<span class=eq>-</span>') +
           '</td><td class="mono' + (d ? ' dif' : '') + '">' +
           (esc(ser) || '<span class=eq>-</span>') + '</td></tr>';
  };
  const todos = (l.hub && l.hub.cns_todos) || [];
  const r = l.receita;
  const linhaRec = (rot, o) => o ? '<tr><th>' + rot + '</th><td>' +
    esc(o.erro ? o.erro : (o.nome || '') + (o.nascimento ? ' - ' + o.nascimento : '')) +
    '</td></tr>' : '';
  const rec = r ? '<h3 style="margin-top:8px">Receita Federal</h3><table class="cmp">' +
    linhaRec('CPF do hub', r.cpf_do_hub) + linhaRec('CPF do SER', r.cpf_do_ser) + '</table>' : '';
  const peds = l.sols.length
    ? '<table class="ped"><thead><tr><th>Agendado</th><th>Pedido em</th><th>Codigo</th>' +
      '<th>Procedimento</th><th>Unidade</th><th>CID</th></tr></thead><tbody>' +
      l.sols.map(s => '<tr class="' + (s.in ? '' : 'fora') + '"><td class="mono ' +
        (s.data && s.data < ANTIGO ? 'velho' : '') + '">' + br(s.data) + '</td><td class="mono">' +
        br(s.solic) + '</td><td class="mono">' + esc(s.cod) + '</td><td>' +
        esc(s.proc).slice(0, 54) + '</td><td>' + esc(s.uni).slice(0, 30) + '</td><td class="mono">' +
        esc(s.cid) + '</td></tr>').join('') + '</tbody></table>'
    : '<p class="eq">Nenhuma solicitação ligada a esta ficha.</p>';

  return '<td colspan="9"><div class="cx"><div style="flex:0 0 auto">' +
    '<h3>Campo a campo</h3><table class="cmp">' +
    '<tr><th></th><th>No hub</th><th>Devolvido pelo SER</th></tr>' +
    linha('Nome', 'nome') + linha('Nascimento', 'nascimento') +
    linha('CPF', 'cpf') + linha('CNS', 'cns') + '</table>' +
    '<table class="cmp" style="margin-top:6px">' +
    '<tr><th>Ficha</th><td class="mono">' + esc((l.hub && l.hub.id) || '-') + '</td></tr>' +
    '<tr><th>CNS no hub</th><td class="mono">' +
      (todos.length ? todos.map(esc).join(' · ') : '-') + '</td></tr>' +
    '<tr><th>Origem</th><td class="mono">' +
      esc(((l.hub && l.hub.origem) || '-').split('/source/').pop()) + '</td></tr></table>' +
    rec + '<p style="max-width:340px;color:#52525b;margin:6px 0 0">' + esc(l.motivo) + '</p></div>' +
    '<div style="flex:1 1 auto;min-width:0"><h3>' + l.qtd + ' solicitaç' +
    (l.qtd === 1 ? 'ão' : 'ões') +
    (l.fora ? ' · <span style="color:#b91c1c">' + l.fora + ' não importada' +
              (l.fora > 1 ? 's' : '') + '</span>' : '') +
    '</h3>' + peds + '</div></div></td>';
}

function pinta() {
  const vis = L.filter(passa).sort(ORD[ordem]);
  // Distintas: duas linhas podem apontar para a MESMA ficha, e somar por linha inflaria o numero.
  const u = new Set();
  vis.forEach(l => l.sols.forEach(s => u.add(s.cod + '|' + s.data)));
  document.getElementById('cnt').textContent = vis.length + ' de ' + L.length + ' casos · ' +
    u.size.toLocaleString('pt-BR') + ' solicitações';
  const tb = document.getElementById('tb');
  if (!vis.length) {
    tb.innerHTML = '<tr><td colspan=9 class=vazio>Nada com esse filtro.</td></tr>';
    return;
  }
  tb.innerHTML = vis.map(l =>
    '<tr class="l" data-i="' + l.i + '">' +
    '<td><span class="tag t-' + l.grupo + '">' + esc(G[l.grupo]) + '</span></td>' +
    '<td>' + esc(l.nome) +
      (l.diff.nome ? '<span class="dif">' + esc(l.diff.nome[1]) + '</span>' : '') + '</td>' +
    '<td>' + cel(l, 'nascimento') + '</td>' +
    '<td>' + cel(l, 'cpf') + '</td>' +
    '<td>' + cel(l, 'cns') + '</td>' +
    '<td class="num">' + (l.fichas === 1 ? '<span class=eq>1</span>'
      : '<b style="color:#b91c1c">' + l.fichas + '</b>') + '</td>' +
    '<td class="num">' + (l.qtd || '<span class=eq>0</span>') +
      (l.fora ? '<span style="color:#b91c1c"> +' + l.fora + '</span>' : '') + '</td>' +
    '<td class="mono ' + (l.de && l.de < ANTIGO ? 'velho' : '') + '">' + br(l.de) +
      (l.ate && l.ate !== l.de ? ' → ' + br(l.ate) : '') + '</td>' +
    '<td><span class="mot" title="' + esc(l.motivo) + '">' + esc(l.motivo) + '</span></td></tr>'
  ).join('');
}

document.addEventListener('click', e => {
  const chip = e.target.closest('.chip');
  if (chip) {
    const alvo = { g: fGrupo, c: fCampo, s: fSo }[chip.dataset.t];
    if (alvo) {
      alvo.has(chip.dataset.v) ? alvo.delete(chip.dataset.v) : alvo.add(chip.dataset.v);
      chip.classList.toggle('on');
      pinta();
    }
    return;
  }
  const tr = e.target.closest('tr.l');
  if (!tr) return;
  const prox = tr.nextElementSibling;
  if (prox && prox.classList.contains('det')) {
    prox.remove();
    tr.classList.remove('ab');
    return;
  }
  document.querySelectorAll('tr.det').forEach(d => d.remove());
  document.querySelectorAll('tr.l.ab').forEach(d => d.classList.remove('ab'));
  tr.classList.add('ab');
  const d = document.createElement('tr');
  d.className = 'det';
  d.innerHTML = detalhe(POR_I.get(+tr.dataset.i));
  tr.after(d);
});

document.querySelectorAll('thead th[data-o]').forEach(th => th.onclick = () => {
  ordem = th.dataset.o;
  document.querySelectorAll('thead th').forEach(x => x.classList.remove('s'));
  th.classList.add('s');
  pinta();
});
document.getElementById('q').oninput = e => {
  busca = e.target.value.trim().toLowerCase();
  pinta();
};
pinta();
"""

CURTO = {"cns": "CNS", "cpf": "CPF", "conflito": "FICHA 2x", "baixa": "BAIXA CONF",
         "orfao": "SEM FICHA"}
ROTULO = {"cns": "SER devolveu outro CNS", "cpf": "SER devolveu outro CPF",
          "conflito": "Ficha partida no hub", "baixa": "Baixa confiança",
          "orfao": "Sem ficha no hub"}


def esc(s):
    return (str(s or "").replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace('"', "&quot;"))


def n(v):
    return f"{v:,}".replace(",", ".")


def main() -> int:
    d = json.loads(DADOS.read_text(encoding="utf-8"))
    linhas = d["linhas"]

    # Duas linhas podem apontar para a MESMA ficha: somar por linha inflaria o total.
    tot_sol = len({(s["cod"], s["data"]) for l in linhas for s in l["sols"]})
    tot_fora = len({(s["cod"], s["data"]) for l in linhas for s in l["sols"] if not s["in"]})
    antigas = sum(1 for l in linhas if l["de"] and l["de"] < "2023-01-01")
    sem_ficha = sum(1 for l in linhas if not l["fichas"])
    por_grupo = {g: sum(1 for l in linhas if l["grupo"] == g) for g in ROTULO}

    # O achado que a tela existe para expor: o classificador disse "mesma pessoa" e o hub nao ficou
    # sabendo. Buscar por aquele CNS nao acha a ficha -- e o proximo atendimento cria a duplicata.
    # O achado que a tela existe para expor: o classificador disse "mesma pessoa" e o hub nao
    # ficou sabendo. Buscar por aquele CNS nao acha a ficha -- e o proximo atendimento cria a
    # duplicata.
    ausentes = [l for l in linhas if "cns" in l["diff"] and l["decisao"] == "mesma_pessoa"]
    fortes = [l for l in ausentes if "nascimento do hub confere" in l["motivo"]]
    sol_fortes = len({(s["cod"], s["data"]) for l in fortes for s in l["sols"]})

    # E a causa de quase todo o resto: caiu no criterio fraco porque NOS nao temos o nascimento.
    fracos = [l for l in ausentes if l not in fortes]
    lacuna = [l for l in fracos
              if not (l["hub"] or {}).get("nascimento") and (l["ser"] or {}).get("nascimento")]

    chips_g = "".join(
        f'<span class="chip" data-t="g" data-v="{g}">{esc(r)} <b>{por_grupo[g]}</b></span>'
        for g, r in ROTULO.items())
    chips_c = "".join(
        f'<span class="chip" data-t="c" data-v="{c}">{r}</span>'
        for c, r in (("nome", "nome"), ("nascimento", "nascimento"),
                     ("cpf", "CPF"), ("cns", "CNS")))

    dados = json.dumps({"grupos": CURTO, "linhas": linhas},
                       ensure_ascii=False).replace("</", "<\\/")

    doc = f"""<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Divergências de identidade</title>
<style>{CSS}</style>
<header><h1>Divergências de identidade</h1><span class="sub">{n(len(linhas))} casos ·
{n(tot_sol)} solicitações ligadas · {n(antigas)} com histórico anterior a 2023 ·
08/09/2026 · <b>contém dado pessoal</b></span></header>

<div class="aviso">
<b>{len(ausentes)} pessoas foram classificadas como “mesma pessoa” e o CNS que o SER devolveu não
está gravado no hub</b> — a busca por aquele número não acha a ficha, e o próximo atendimento por
ele cria uma duplicata.<br>
<b>{len(fortes)}</b> tiveram o nascimento conferido (o critério forte) e mesmo assim ficaram de
fora — dá para gravar sem julgar nada, e carregam {n(sol_fortes)} solicitações.
<b>{len(lacuna)} das outras {len(fracos)} caíram no critério fraco porque o NOSSO cadastro não tem
data de nascimento — e o SER tem, em todas</b>: não é caso de julgamento humano, é campo faltando
do nosso lado (célula azul).{f' · {sem_ficha} casos não têm ficha alguma no hub.' if sem_ficha else ''}
</div>

<div class="barra">
<input type="search" id="q" placeholder="nome, CNS, CPF, motivo…" autocomplete="off">
<span class="sep"></span>{chips_g}
<span class="sep"></span><span class="sub" style="margin:0">diverge:</span>{chips_c}
<span class="sep"></span>
<span class="chip" data-t="s" data-v="antigo">histórico &lt; 2023</span>
<span class="chip" data-t="s" data-v="fora">tem pedido não importado</span>
<span class="chip" data-t="s" data-v="semficha">sem ficha no hub</span>
<span class="chip" data-t="s" data-v="lacuna">SER tem o nascimento, nós não</span>
<span class="cnt" id="cnt"></span>
</div>

<table><thead><tr>
<th>Tipo</th><th data-o="nome">Nome</th><th>Nascimento</th><th>CPF</th><th>CNS</th>
<th class="num">Fichas</th><th class="num" data-o="qtd">Pedidos</th>
<th data-o="antigo">Período</th><th data-o="dif" class="s">Motivo</th>
</tr></thead><tbody id="tb"></tbody></table>

<footer>Vermelho = o SER devolveu valor diferente do nosso. Azul = o hub não tem o campo e o SER
tem. Âmbar = anterior a 2023.
“Pedidos +N” = N solicitações que não entraram na carga ({tot_fora} no total, ainda fora do banco).
Clique na linha para o detalhe; clique no cabeçalho para reordenar.</footer>

<script type="application/json" id="dados">{dados}</script>
<script>{JS}</script>
"""
    SAIDA.write_text(doc, encoding="utf-8")
    print(f"HTML: {SAIDA}  ({SAIDA.stat().st_size / 1024:.0f} KB)")
    print(f"  casos {n(len(linhas))} · solicitacoes {n(tot_sol)} · nao importadas {tot_fora}")
    print(f"  CNS do SER ausente no hub: {len(ausentes)} (dos quais {len(fortes)} com nascimento conferido)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
