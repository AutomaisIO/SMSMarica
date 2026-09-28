# -*- coding: utf-8 -*-
"""Telas do painel (conteúdo + meta) e os slides que as exibem.

Formato do deck de Ouvidoria: moldura de navegador + tela + quatro legendas numeradas.
As telas reproduzem o produto — abas, colunas e rótulos vêm do código do front.
"""
import io, os

AQUI = os.path.dirname(os.path.abspath(__file__))
CAD = ('<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round">'
       '<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>')


def tela(nome, shell, ativo, conteudo, usuario='Rita Menezes|rita.menezes@saude.niteroi.rj.gov.br|RM'):
    io.open(os.path.join(AQUI, 'telas', 'conteudo', nome + '.html'), 'w', encoding='utf-8').write(conteudo)
    io.open(os.path.join(AQUI, 'telas', 'conteudo', nome + '.meta'), 'w', encoding='utf-8').write(
        u'shell=%s\nativo=%s\nusuario=%s\n' % (shell, ativo, usuario))


def slide_tela(arq, ident, olho, titulo, destaque, url, nome_tela, legendas):
    h = u'<section class="slide tela" id="%s">\n' % ident
    h += u'  <div class="fita-marca"><i></i><i></i><i></i><i></i><i></i></div>\n'
    h += u'  <div class="cabeca">\n    <div>\n      <p class="olho">%s</p>\n' % olho
    h += u'      <h1 class="titulo">%s <em>%s</em></h1>\n    </div>\n' % (titulo, destaque)
    h += u'    <div class="marca-canto"><img src="assets/niteroi-saude.png" alt=""></div>\n  </div>\n'
    h += u'  <div class="moldura">\n    <div class="barra-nav">\n      <div class="bolas"><i></i><i></i><i></i></div>\n'
    h += u'      <div class="url">%ssmsmais.saude.niteroi.rj.gov.br%s</div>\n    </div>\n' % (CAD, url)
    h += u'    <div class="palco">{{TELA:%s}}</div>\n  </div>\n' % nome_tela
    h += u'  <div class="legenda-fila">\n'
    for i, t in enumerate(legendas, 1):
        h += u'    <div class="item"><span class="n">%d</span><span class="t">%s</span></div>\n' % (i, t)
    h += u'  </div>\n  <div class="rodape"><span class="esq"></span><span class="num">{{N}}</span></div>\n</section>\n'
    io.open(os.path.join(AQUI, 'slides', arq), 'w', encoding='utf-8').write(h)
    print(arq)


# ─────────────────────────────────────────────── AGENDAMENTOS (multi-origem)
tela('agendamentos', 'regulacao', u'Agendamentos', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="18" x="3" y="4" rx="2"/><path d="M3 10h18"/><path d="M8 2v4"/><path d="M16 2v4"/></svg>
      Agendamentos
      <span class="ajuda">?</span>
    </h1>
    <p class="sub">Tudo o que está marcado para o cidadão — venha de onde vier.</p>
  </div>
</div>

<div class="filtros mt14">
  <div class="inp w56 placeholder">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
    <span>Nome, CPF ou cartão do SUS</span>
  </div>
  <div class="inp w40"><span>Todas as origens</span><svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></div>
  <div class="inp w48"><span>Todas as unidades</span><svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></div>
  <div class="inp w36"><span>Próximos 30 dias</span><svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></div>
</div>

<div class="abas mt14">
  <span class="aba ativa">Agendados <span class="cont">1.284</span></span>
  <span class="aba">Confirmados <span class="cont">796</span></span>
  <span class="aba">Remarcados <span class="cont">61</span></span>
  <span class="aba">Fora do município <span class="cont">88</span></span>
</div>

<div class="tab-card mt10">
  <table class="tab compacta">
    <colgroup><col style="width:150px"><col style="width:96px"><col style="width:176px"><col style="width:186px"><col style="width:112px"><col style="width:96px"><col style="width:96px"></colgroup>
    <thead><tr><th>Paciente</th><th>Origem</th><th>Procedimento</th><th>Onde</th><th>Quando</th><th>Situação</th><th>No app</th></tr></thead>
    <tbody>
      <tr>
        <td>Terezinha Alves Pinto</td><td><span class="pill or-sernit">SERNIT</span></td>
        <td class="t-ass"><div class="a1">Consulta em cardiologia</div></td>
        <td>Policlínica do Centro</td><td class="mono">12/10 · 14h20</td>
        <td><span class="pill s-respondida">confirmado</span></td><td><span class="pill s-concluida">visível</span></td>
      </tr>
      <tr>
        <td>José Carlos Ribeiro</td><td><span class="pill or-sisreg">SISREG</span></td>
        <td class="t-ass"><div class="a1">US de abdome total</div></td>
        <td>Policlínica de Icaraí</td><td class="mono">28/09 · 08h40</td>
        <td><span class="pill s-triagem">aguardando</span></td><td><span class="pill s-concluida">visível</span></td>
      </tr>
      <tr>
        <td>Luciana Barbosa Nunes</td><td><span class="pill or-ser">SER</span></td>
        <td class="t-ass"><div class="a1">Tomografia de crânio</div><div class="a2">fora do município</div></td>
        <td>Hospital Estadual · Niterói</td><td class="mono">03/10 · 07h00</td>
        <td><span class="pill s-encaminhada">encaminhada</span></td><td><span class="pill s-concluida">visível</span></td>
      </tr>
      <tr>
        <td>Antônio Ferreira da Silva</td><td><span class="pill or-local">Agenda local</span></td>
        <td class="t-ass"><div class="a1">Retorno em oftalmologia</div></td>
        <td>UBS Engenhoca</td><td class="mono">30/09 · 10h00</td>
        <td><span class="pill s-respondida">confirmado</span></td><td><span class="pill s-concluida">visível</span></td>
      </tr>
      <tr>
        <td>Maria das Graças Souza</td><td><span class="pill or-sisreg">SISREG</span></td>
        <td class="t-ass"><div class="a1">Mamografia bilateral</div></td>
        <td>Policlínica de Icaraí</td><td class="mono">29/09 · 13h30</td>
        <td><span class="pill s-complementacao">remarcado</span></td><td><span class="pill s-concluida">avisado</span></td>
      </tr>
      <tr>
        <td>Sebastião Rocha Martins</td><td><span class="pill or-ser">SER</span></td>
        <td class="t-ass"><div class="a1">Consulta em neurologia</div><div class="a2">fora do município</div></td>
        <td>Hospital Universitário · RJ</td><td class="mono">14/10 · 09h30</td>
        <td><span class="pill s-encaminhada">encaminhada</span></td><td><span class="pill s-concluida">visível</span></td>
      </tr>
    </tbody>
  </table>
</div>

<div class="paginacao">
  <div class="mostrar"><span>Mostrar</span><span class="inp" style="width:68px">25 <svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></span><span>1.284 agendamentos</span></div>
  <div class="pgs"><span class="pg">‹</span><span class="pg ativa">1</span><span class="pg">2</span><span class="pg">3</span><span class="pg">›</span></div>
</div>
''')

slide_tela('c20-agendamentos.html', 'c-agendamentos', u'A tela · Agendamentos',
           u'De qualquer sistema de regulação —', u'e o paciente vê tudo no app',
           u'/app/regulacao/agendamentos', 'agendamentos', [
    u'<b>Quatro origens</b>: SISREG, SERNIT, SER e agenda própria.',
    u'<b>Dentro e fora do município</b>, na mesma lista.',
    u'<b>“No app”</b>: o paciente já enxerga no celular.',
    u'Mudou a data? <b>Ele é avisado</b> pelo mesmo canal.',
])

# ─────────────────────────────────────────────── SOLICITAÇÃO → EQUIPAMENTO
tela('solicitacao', 'regulacao', u'Solicitações de exame', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 22h14a2 2 0 0 0 2-2V7l-5-5H6a2 2 0 0 0-2 2v4"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M3 15h6"/><path d="M6 12v6"/></svg>
      Solicitação 2026-018791
      <span class="ajuda">?</span>
    </h1>
    <p class="sub">José Carlos Ribeiro · 61 anos · US de abdome total · SIGTAP 0205020046</p>
  </div>
  <button class="btn btn-primary">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14"/><path d="m13 6 6 6-6 6"/></svg>
    Abrir exame
  </button>
</div>

<div class="sol-grid mt14">
  <div class="cx">
    <p class="cx-tit">O pedido</p>
    <div class="linhas">
      <div class="lin"><span class="k">Origem</span><span class="v"><span class="pill or-sisreg">SISREG</span> importado em 14/09</span></div>
      <div class="lin"><span class="k">Unidade solicitante</span><span class="v">UBS Barreto</span></div>
      <div class="lin"><span class="k">Unidade executante</span><span class="v">Policlínica de Icaraí</span></div>
      <div class="lin"><span class="k">Agendado para</span><span class="v">28/09 · 08h40</span></div>
      <div class="lin"><span class="k">Categoria</span><span class="v">Imagem <span class="cx-nota">cria satélite de exame</span></span></div>
    </div>
  </div>

  <div class="cx destaque">
    <p class="cx-tit">Equipamento de destino</p>
    <div class="equip">
      <div class="eq-sel">
        <span class="eq-ic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="14" x="3" y="3" rx="2"/><path d="M7 21h10"/><path d="M12 17v4"/></svg></span>
        <span class="eq-txt"><b>US-02 · Policlínica de Icaraí</b><span>Ultrassom · Mindray</span></span>
        <span class="pill s-concluida">na worklist</span>
      </div>
      <p class="eq-msg"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="m9 12 2 2 4-4"/></svg> Equipamento definido — exame enviado à worklist.</p>
      <p class="eq-nota">O aparelho vê este paciente na lista de trabalho dele, e só dele. A técnica escolhe da lista: o exame já volta vinculado a este pedido.</p>
    </div>
  </div>
</div>

<div class="cx mt10">
  <p class="cx-tit">Linha do tempo do pedido</p>
  <div class="passos-h">
    <div class="ph feito"><span class="pb"></span><b>Importado do SISREG</b><span>14/09 · 06h12</span></div>
    <div class="ph feito"><span class="pb"></span><b>Paciente resolvido por CPF</b><span>14/09 · 06h12</span></div>
    <div class="ph feito"><span class="pb"></span><b>Equipamento definido</b><span>15/09 · 09h40 · Rita M.</span></div>
    <div class="ph feito"><span class="pb"></span><b>Enviado à worklist</b><span>15/09 · 09h40</span></div>
    <div class="ph"><span class="pb"></span><b>Aguardando execução</b><span>28/09 · 08h40</span></div>
    <div class="ph"><span class="pb"></span><b>Laudo e entrega</b><span>—</span></div>
  </div>
</div>
''')

slide_tela('c30-solicitacao.html', 'c-solicitacao', u'A tela · Solicitação de exame',
           u'Do pedido ao aparelho,', u'sem ninguém digitar o nome',
           u'/app/solicitacoes-exame/018791', 'solicitacao', [
    u'O pedido <b>importado da regulação</b>, com paciente já resolvido.',
    u'<b>Um clique define o aparelho</b> — e o exame entra na worklist.',
    u'A mensagem é a do sistema: <b>“enviado à worklist”</b>.',
    u'Cada passo fica <b>com autor e hora</b>.',
])
