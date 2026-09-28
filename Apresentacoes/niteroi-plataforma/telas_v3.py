# -*- coding: utf-8 -*-
"""Telas 4–9: agenda, estratégias de fila, consulta inteligente, painel, confirmações, telefonia."""
from telas_v2 import tela, slide_tela

# ─────────────────────────────────────────────── AGENDA · ANÁLISE DE VAGAS
tela('agenda_vagas', 'agenda', u'Análise de vagas', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 3v16a2 2 0 0 0 2 2h16"/><path d="M7 16.5 12 11l3 3 5-5.5"/></svg>
      Análise de vagas
      <span class="ajuda">?</span>
    </h1>
    <p class="sub">A oferta que foi aberta contra a fila que está esperando.</p>
  </div>
  <div class="filtros" style="margin:0">
    <div class="inp w40"><span>Setembro / 2026</span><svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></div>
    <div class="inp w48"><span>Todas as unidades</span><svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></div>
  </div>
</div>

<div class="kpi-linha mt14">
  <div class="kp"><span class="r">Vagas abertas</span><span class="v">2.300</span></div>
  <div class="kp"><span class="r">Usadas</span><span class="v ok">1.883</span><span class="d">81,9%</span></div>
  <div class="kp"><span class="r">Venceram vazias</span><span class="v al">417</span><span class="d">18,1%</span></div>
  <div class="kp"><span class="r">Na fila hoje</span><span class="v">344</span><span class="d">esperando</span></div>
</div>

<div class="cx mt10">
  <p class="cx-tit">Vagas abertas × usadas × fila, por especialidade</p>
  <div class="duplas">
    <div class="cat"><span class="rot">Cardiologia</span><span class="par"><span class="b oferta" style="width:58%"></span><span class="b uso" style="width:56%"></span></span><span class="val"><b>412 / 420</b>fila: 147</span></div>
    <div class="cat"><span class="rot">Ortopedia</span><span class="par"><span class="b oferta" style="width:74%"></span><span class="b uso" style="width:70%"></span></span><span class="val"><b>521 / 540</b>fila: 96</span></div>
    <div class="cat"><span class="rot">Oftalmologia</span><span class="par"><span class="b oferta" style="width:100%"></span><span class="b uso" style="width:61%"></span></span><span class="val"><b>446 / 730</b>fila: 12</span></div>
    <div class="cat"><span class="rot">Dermatologia</span><span class="par"><span class="b oferta" style="width:48%"></span><span class="b uso" style="width:47%"></span></span><span class="val"><b>344 / 350</b>fila: 81</span></div>
    <div class="cat"><span class="rot">Endocrinologia</span><span class="par"><span class="b oferta" style="width:36%"></span><span class="b uso" style="width:22%"></span></span><span class="val"><b>160 / 260</b>fila: 8</span></div>
  </div>
  <div class="legenda-mini"><span><i style="background:var(--p200)"></i>abertas</span><span><i style="background:var(--nit-laranja)"></i>usadas</span></div>
  <div class="aviso-tela mt10">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 9v4"/><path d="M12 17h.01"/><circle cx="12" cy="12" r="10"/></svg>
    <span><b>284 vagas de oftalmologia venceram vazias</b> enquanto 147 pessoas esperam cardiologia. Remanejar resolve mais rápido do que abrir agenda nova.</span>
  </div>
</div>
''')

slide_tela('c40-agenda.html', 'c-agenda', u'A tela · Agenda',
           u'Onde a vaga sobrou', u'e onde a fila espera',
           u'/app/agenda/analise', 'agenda_vagas', [
    u'<b>Aberta × usada</b> por especialidade, no mesmo gráfico.',
    u'A <b>fila de cada uma</b> ao lado do número da oferta.',
    u'<b>Vaga vencida vazia</b> aparece como perda, não some.',
    u'O sistema <b>aponta o remanejamento</b> antes de pedir mais agenda.',
])

# ─────────────────────────────────────────────── ESTRATÉGIAS DE FILA
tela('estrategia', 'agenda', u'Estratégias de fila', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/></svg>
      Estratégia · Cardiologia adulto
      <span class="ajuda">?</span>
    </h1>
    <p class="sub">Simulação contra a fila real — nada aqui escreve no sistema de regulação.</p>
  </div>
  <button class="btn btn-primary">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14"/><path d="m13 6 6 6-6 6"/></svg>
    Simular
  </button>
</div>

<div class="est-grid mt14">
  <div class="cx">
    <p class="cx-tit">O que posso mexer</p>
    <div class="ctrl"><span class="ck">Unidades executantes</span><span class="cv2">3 <i>→</i> <b>4</b></span></div>
    <div class="ctrl"><span class="ck">Profissionais na escala</span><span class="cv2">5 <i>→</i> <b>6</b></span></div>
    <div class="ctrl"><span class="ck">Dias de agenda por semana</span><span class="cv2">3 <i>→</i> <b>5</b></span></div>
    <div class="ctrl"><span class="ck">Vagas por turno</span><span class="cv2">12 <i>→</i> <b>12</b></span></div>
    <div class="ctrl travado"><span class="ck">Fila atual <span class="pill s-externo">trava</span></span><span class="cv2"><b>147</b></span></div>
    <div class="ctrl travado"><span class="ck">Entrada semanal <span class="pill s-externo">trava</span></span><span class="cv2"><b>22/sem</b></span></div>
    <p class="cx-pe">O que está <b>travado</b> é contrato: o simulador não inventa demanda nem reduz fila por decreto.</p>
  </div>

  <div class="cx">
    <p class="cx-tit">Projeção da fila</p>
    <div class="serie" style="height:150px">
      <svg viewBox="0 0 600 150" preserveAspectRatio="none">
        <g stroke="#e2e8f0" stroke-width="1"><line x1="0" y1="37" x2="600" y2="37"/><line x1="0" y1="74" x2="600" y2="74"/><line x1="0" y1="111" x2="600" y2="111"/></g>
        <polyline fill="none" stroke="#94a3b8" stroke-width="3" stroke-dasharray="6 5" points="0,18 100,31 200,45 300,61 400,79 500,99 600,121"/>
        <polyline fill="none" stroke="#ee7219" stroke-width="3.5" stroke-linejoin="round" points="0,18 100,40 200,70 300,106 420,144"/>
        <circle cx="420" cy="144" r="6" fill="#ee7219"/>
      </svg>
      <div class="eixo"><span>hoje</span><span>2 sem</span><span>4</span><span>6</span><span>8</span><span>10</span><span>12</span></div>
    </div>
    <div class="res-linha">
      <div class="res"><span class="r">Com a oferta de hoje</span><span class="v">12+ <i>semanas</i></span></div>
      <div class="res destaque"><span class="r">Com a simulação</span><span class="v">9 <i>semanas</i></span></div>
      <div class="res"><span class="r">Vagas a mais / semana</span><span class="v">+48</span></div>
    </div>
    <div class="aviso-tela mt10">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 9v4"/><path d="M12 17h.01"/><circle cx="12" cy="12" r="10"/></svg>
      <span>Estratégia salva fica no nosso banco para consulta. <b>“Aplicada” é anotação humana</b> — nada é escrito no SISREG.</span>
    </div>
  </div>
</div>
''')

slide_tela('c50-estrategia.html', 'c-estrategia', u'A tela · Estratégias de fila',
           u'“Se eu abrir mais dois dias,', u'a fila zera quando?”',
           u'/app/agenda/estrategias/cardiologia', 'estrategia', [
    u'Mexe-se em <b>oferta</b>: unidades, profissionais, dias, vagas.',
    u'O que é <b>contrato fica travado</b> — fila e entrada semanal.',
    u'A projeção responde <b>em semanas</b>, não em opinião.',
    u'<b>Nada escreve no SISREG.</b> É planejamento.',
])

# ─────────────────────────────────────────────── CONSULTA INTELIGENTE
tela('consulta_ia', 'ia', u'Consulta inteligente', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z"/></svg>
      Consulta inteligente
      <span class="ajuda">?</span>
    </h1>
    <p class="sub">Pergunte em português. A resposta vem com o número e o caminho.</p>
  </div>
  <div class="filtros" style="margin:0">
    <div class="inp w48"><span>Base: Regulação</span><svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></div>
  </div>
</div>

<div class="ia-grid mt14">
  <div class="cx">
    <p class="cx-tit">Perguntas de exemplo <span class="cx-nota">cada base libera as suas</span></p>
    <div class="sug"><span class="sug-b">Regulação</span>“Quantas pessoas esperam mais de 90 dias por cardiologia, e em quais unidades?”</div>
    <div class="sug"><span class="sug-b">Agenda</span>“Quais especialidades tiveram mais vaga vencida vazia no mês?”</div>
    <div class="sug"><span class="sug-b">Atendimento</span>“Sobre o que os cidadãos mais escreveram no WhatsApp esta semana?”</div>
    <div class="sug"><span class="sug-b">Telefonia</span>“Quantas ligações a recepção do Barreto atendeu ontem, e qual a média de duração?”</div>
    <div class="sug"><span class="sug-b">Prontuários</span>“Quantos hipertensos acima de 60 anos não passam em consulta há mais de um ano?”</div>
    <div class="sug"><span class="sug-b">Exames</span>“Qual o tempo médio entre o exame feito e o laudo assinado, por unidade?”</div>
  </div>

  <div class="cx">
    <div class="chat-sim">
      <div class="balao-eu">Quantas pessoas esperam mais de 90 dias por cardiologia, e em quais unidades?</div>
      <div class="balao-ia">
        <span class="passo-ia"><span class="giro"></span> consultando a fila…</span>
        <p style="margin:0 0 6px">Hoje há <b>412 solicitações de cardiologia</b> na fila. Dessas, <b>147 esperam mais de 90 dias</b>:</p>
        <div class="viz-numero" style="padding:8px 12px"><span class="v" style="font-size:26px">147<span class="u" style="font-size:12px">pessoas</span></span><span class="t">acima de 90 dias</span></div>
        <div class="grafico" style="margin-top:8px;padding:9px 10px">
          <div class="barras" style="height:74px">
            <div class="col"><span class="val">58</span><div class="bar" style="height:76%"></div><span class="rot">Policl.<br>Centro</span></div>
            <div class="col"><span class="val">41</span><div class="bar" style="height:54%"></div><span class="rot">Policl.<br>Icaraí</span></div>
            <div class="col"><span class="val">29</span><div class="bar" style="height:38%"></div><span class="rot">UBS<br>Barreto</span></div>
            <div class="col fraca"><span class="val">19</span><div class="bar" style="height:25%"></div><span class="rot">UBS<br>Engenhoca</span></div>
          </div>
        </div>
        <p style="margin:7px 0 0">Espera média do grupo: <b>127 dias</b>. A Policlínica do Centro responde por <b>39%</b>.</p>
      </div>
    </div>
  </div>
</div>
''')

slide_tela('c60-ia.html', 'c-ia', u'A tela · Consulta inteligente',
           u'Pergunte em português —', u'sobre qualquer base',
           u'/app/inteligencia/consulta', 'consulta_ia', [
    u'<b>Regulação, agenda, exames</b> — e também conversas e ligações.',
    u'Até <b>o conteúdo dos prontuários</b> importados.',
    u'A resposta traz <b>número, tabela ou gráfico</b>.',
    u'<b>Cada base é liberada por permissão</b> — e toda pergunta fica auditada.',
])

# ─────────────────────────────────────────────── PAINEL EM TEMPO REAL
tela('painel_sec', 'painel', u'Painel da secretaria', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/></svg>
      Painel da secretaria
      <span class="ajuda">?</span>
    </h1>
    <p class="sub"><span class="vivo"><i></i> ao vivo</span> · atualizado há 12 segundos · terça, 24/09 · 15h41</p>
  </div>
  <div class="filtros" style="margin:0">
    <div class="inp w40"><span>Rede inteira</span><svg class="cv" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></div>
  </div>
</div>

<div class="kpi-linha mt14">
  <div class="kp"><span class="r">Atendimentos hoje</span><span class="v">3.412</span><span class="d">+8% vs. terça passada</span></div>
  <div class="kp"><span class="r">Pessoas em espera agora</span><span class="v al">188</span><span class="d">em 14 unidades</span></div>
  <div class="kp"><span class="r">Exames aguardando laudo</span><span class="v">231</span><span class="d">média: 1,8 dia</span></div>
  <div class="kp"><span class="r">Conversas abertas</span><span class="v">64</span><span class="d">7 sem atendente</span></div>
</div>

<div class="painel-grid mt10">
  <div class="cx">
    <p class="cx-tit">Atendimentos por hora — hoje</p>
    <div class="serie" style="height:118px">
      <svg viewBox="0 0 600 118" preserveAspectRatio="none">
        <defs><linearGradient id="pg1" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ee7219" stop-opacity=".5"/><stop offset="1" stop-color="#ee7219" stop-opacity=".04"/></linearGradient></defs>
        <g stroke="#e2e8f0" stroke-width="1"><line x1="0" y1="30" x2="600" y2="30"/><line x1="0" y1="60" x2="600" y2="60"/><line x1="0" y1="90" x2="600" y2="90"/></g>
        <polygon fill="url(#pg1)" points="0,118 0,96 60,74 120,44 180,36 240,52 300,80 360,50 420,38 480,44 540,62 600,88 600,118"/>
        <polyline fill="none" stroke="#ee7219" stroke-width="3" stroke-linejoin="round" points="0,96 60,74 120,44 180,36 240,52 300,80 360,50 420,38 480,44 540,62 600,88"/>
      </svg>
      <div class="eixo"><span>7h</span><span>9h</span><span>11h</span><span>13h</span><span>15h</span><span>17h</span><span>19h</span></div>
    </div>
  </div>

  <div class="cx">
    <p class="cx-tit">Unidades com mais gente esperando <span class="cx-nota">agora</span></p>
    <div class="ranking">
      <div class="linha"><span class="rot">UPA Centro</span><span class="trilho"><span class="barra" style="width:100%"></span></span><span class="val">47</span></div>
      <div class="linha"><span class="rot">Policl. do Centro</span><span class="trilho"><span class="barra" style="width:68%"></span></span><span class="val">32</span></div>
      <div class="linha"><span class="rot">UPA Barreto</span><span class="trilho"><span class="barra" style="width:57%"></span></span><span class="val">27</span></div>
      <div class="linha"><span class="rot">Policl. de Icaraí</span><span class="trilho"><span class="barra fraca" style="width:40%"></span></span><span class="val">19</span></div>
      <div class="linha"><span class="rot">UBS Engenhoca</span><span class="trilho"><span class="barra fraca" style="width:26%"></span></span><span class="val">12</span></div>
    </div>
  </div>

  <div class="cx">
    <p class="cx-tit">Composição do atendimento</p>
    <div class="rosca-bloco" style="gap:12px">
      <div class="rosca" style="width:96px;height:96px;background:conic-gradient(var(--nit-laranja) 0 44%, var(--p400) 44% 71%, var(--p200) 71% 88%, var(--s300) 88% 100%)">
        <span class="centro"><b style="font-size:20px">44%</b><span style="font-size:7.5px">atenção básica</span></span>
      </div>
      <div class="legenda-rosca">
        <span class="li"><span class="pt" style="background:var(--nit-laranja)"></span> Atenção básica <b>44%</b></span>
        <span class="li"><span class="pt" style="background:var(--p400)"></span> Especializada <b>27%</b></span>
        <span class="li"><span class="pt" style="background:var(--p200)"></span> Urgência <b>17%</b></span>
        <span class="li"><span class="pt" style="background:var(--s300)"></span> Imagem <b>12%</b></span>
      </div>
    </div>
  </div>
</div>
''')

slide_tela('c70-painel.html', 'c-painel', u'A tela · Painel da secretaria',
           u'A rede inteira, agora —', u'na parede da sala do secretário',
           u'/app/paineis/secretaria', 'painel_sec', [
    u'<b>Ao vivo</b>: atualiza sozinho, sem ninguém montar planilha.',
    u'<b>Onde há gente esperando</b> neste momento, por unidade.',
    u'A curva do dia <b>enquanto o dia acontece</b>.',
    u'Modo TV: <b>fica aberto na parede</b> da sala da secretaria.',
])

# ─────────────────────────────────────────────── CONFIRMAÇÕES / WHATSAPP
tela('confirmacoes', 'atendimento', u'Confirmações', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M7.9 20A9 9 0 1 0 4 16.1L2 22z"/></svg>
      Confirmações
      <span class="ajuda">?</span>
    </h1>
    <p class="sub">O que o WhatsApp oficial já resolveu e o que precisa de gente.</p>
  </div>
</div>

<div class="abas mt14">
  <span class="aba">Não confirmados <span class="cont">148</span></span>
  <span class="aba ativa">Não respondeu <span class="cont">61</span></span>
  <span class="aba">Contato errado <span class="cont">24</span></span>
  <span class="aba">Pendentes <span class="cont">12</span></span>
  <span class="aba">Equipe</span>
</div>

<div class="tab-card mt10">
  <table class="tab compacta">
    <colgroup><col style="width:158px"><col style="width:168px"><col style="width:120px"><col style="width:132px"><col style="width:150px"><col style="width:120px"></colgroup>
    <thead><tr><th>Paciente</th><th>Agendamento</th><th>Quando</th><th>Mensagens</th><th>Situação</th><th>Quem assumiu</th></tr></thead>
    <tbody>
      <tr>
        <td>Terezinha Alves Pinto</td>
        <td class="t-ass"><div class="a1">Cardiologia · 12/10</div><div class="a2">Policlínica do Centro</div></td>
        <td class="mono">em 18 dias</td>
        <td><span class="msg-ic">✓✓</span> 2 enviadas · 1 resposta</td>
        <td><span class="pill s-respondida">confirmou</span></td>
        <td><span class="quem-av">LC</span> Luana C.</td>
      </tr>
      <tr>
        <td>José Carlos Ribeiro</td>
        <td class="t-ass"><div class="a1">US de abdome · 28/09</div><div class="a2">Policlínica de Icaraí</div></td>
        <td class="mono">em 4 dias</td>
        <td><span class="msg-ic">✓✓</span> 2 enviadas · sem resposta</td>
        <td><span class="pill s-triagem">não respondeu</span></td>
        <td class="sem">—</td>
      </tr>
      <tr>
        <td>Maria das Graças Souza</td>
        <td class="t-ass"><div class="a1">Mamografia · 29/09</div><div class="a2">Policlínica de Icaraí</div></td>
        <td class="mono">em 5 dias</td>
        <td><span class="msg-ic falha">✗</span> número inválido</td>
        <td><span class="pill s-complementacao">contato errado</span></td>
        <td class="sem">recepção</td>
      </tr>
      <tr>
        <td>Antônio Ferreira da Silva</td>
        <td class="t-ass"><div class="a1">Oftalmologia · 30/09</div><div class="a2">UBS Engenhoca</div></td>
        <td class="mono">em 6 dias</td>
        <td><span class="msg-ic">✓✓</span> 1 enviada · 1 resposta</td>
        <td><span class="pill s-recurso">não vai</span></td>
        <td><span class="quem-av">RM</span> Rita M.</td>
      </tr>
      <tr>
        <td>Sebastião Rocha Martins</td>
        <td class="t-ass"><div class="a1">Neurologia · 14/10</div><div class="a2">fora do município</div></td>
        <td class="mono">em 20 dias</td>
        <td><span class="msg-ic">✓</span> 1 enviada · entregue</td>
        <td><span class="pill s-triagem">aguardando</span></td>
        <td class="sem">—</td>
      </tr>
    </tbody>
  </table>
</div>

<div class="rodape-tela mt10">
  <span class="rt"><b>Regra:</b> humano entrou na conversa → o automático para de tentar.</span>
  <span class="rt"><b>Vaga liberada:</b> quem avisou que não vai devolve a vaga antes do dia.</span>
  <span class="rt"><b>Contato errado</b> vira pendência de cadastro — e nada mais é enviado.</span>
</div>
''')

slide_tela('c80-confirmacoes.html', 'c-confirmacoes', u'A tela · Confirmações',
           u'O WhatsApp confirma —', u'e a equipe só trabalha o que sobrou',
           u'/app/atendimento/confirmacoes', 'confirmacoes', [
    u'<b>Abas por situação</b>: quem não respondeu, quem não vai, número errado.',
    u'<b>Quem assumiu</b> aparece — e o robô para de tentar.',
    u'Quem avisa que não vai <b>devolve a vaga a tempo</b>.',
    u'Número inválido <b>vira pendência</b> para a recepção.',
])

# ─────────────────────────────────────────────── TELEFONIA NO NAVEGADOR
tela('telefonia', 'atendimento', u'Telefonia', u'''<div class="pg-cab">
  <div>
    <h1>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"/></svg>
      Telefonia
      <span class="ajuda">?</span>
    </h1>
    <p class="sub">Ramal 2041 · Luana C. · <span class="vivo"><i></i> em chamada</span></p>
  </div>
  <button class="btn btn-primary">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14"/><path d="M12 5v14"/></svg>
    Ligar
  </button>
</div>

<div class="tel-grid mt14">
  <div class="cx destaque">
    <p class="cx-tit">Chamada em andamento</p>
    <div class="tel-atual">
      <span class="tel-av">TA</span>
      <span class="tel-quem"><b>Terezinha Alves Pinto</b><span>68 anos · Barreto · cadastro aberto ao lado</span></span>
      <span class="tel-tempo">02:41</span>
    </div>
    <div class="onda" style="margin-top:10px">
      <i class="lido" style="height:42%"></i><i class="lido" style="height:70%"></i><i class="lido" style="height:54%"></i><i class="lido" style="height:88%"></i><i class="lido" style="height:36%"></i><i class="lido" style="height:62%"></i><i class="lido" style="height:78%"></i><i class="lido" style="height:44%"></i>
      <i style="height:66%"></i><i style="height:34%"></i><i style="height:82%"></i><i style="height:50%"></i><i style="height:72%"></i><i style="height:40%"></i><i style="height:58%"></i><i style="height:86%"></i><i style="height:46%"></i><i style="height:64%"></i><i style="height:30%"></i><i style="height:76%"></i><i style="height:52%"></i><i style="height:68%"></i><i style="height:38%"></i><i style="height:80%"></i>
      <span class="tempo">gravando</span>
    </div>
    <div class="transcricao" style="margin-top:9px">
      <p class="fala dela"><b>Terezinha:</b> Moça, é sobre a consulta do coração, dia doze…</p>
      <p class="fala"><b>Luana C.:</b> Cardiologia, 12/10 às 14h20, na Policlínica do Centro. A senhora confirmou pelo WhatsApp.</p>
      <p class="fala dela"><b>Terezinha:</b> Isso. Só queria saber da condução.</p>
    </div>
    <p class="cx-pe">Transcrição em tempo real. Ao desligar, <b>áudio e texto vão para o histórico da paciente</b>.</p>
  </div>

  <div class="cx">
    <p class="cx-tit">Ramais da secretaria <span class="cx-nota">quem está disponível agora</span></p>
    <div class="ramais">
      <div class="rm"><span class="st ocupado"></span><b>2041</b> Luana C. <span class="un">Policl. do Centro</span><span class="dir">em chamada · 02:41</span></div>
      <div class="rm"><span class="st livre"></span><b>2038</b> Rita M. <span class="un">Regulação</span><span class="dir">disponível</span></div>
      <div class="rm"><span class="st livre"></span><b>2044</b> Paulo R. <span class="un">Regulação</span><span class="dir">disponível</span></div>
      <div class="rm"><span class="st ocupado"></span><b>2102</b> Carla S. <span class="un">UBS Barreto</span><span class="dir">em chamada · 00:58</span></div>
      <div class="rm"><span class="st ausente"></span><b>2110</b> Denise L. <span class="un">UBS Engenhoca</span><span class="dir">ausente</span></div>
      <div class="rm"><span class="st livre"></span><b>2205</b> Marcos A. <span class="un">Policl. de Icaraí</span><span class="dir">disponível</span></div>
    </div>
    <div class="aviso-tela mt10">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 9v4"/><path d="M12 17h.01"/><circle cx="12" cy="12" r="10"/></svg>
      <span>O ramal segue o <b>login</b>, não o aparelho: quem entra no sistema atende de onde estiver, e a ligação fica no nome de quem falou.</span>
    </div>
  </div>
</div>
''')

slide_tela('c90-telefonia.html', 'c-telefonia', u'A tela · Telefonia',
           u'Cada login é um ramal —', u'e a ligação vira registro do paciente',
           u'/app/atendimento/telefonia', 'telefonia', [
    u'<b>Atende pelo navegador</b> ou pelo aparelho da mesa.',
    u'A ligação abre <b>o cadastro do paciente</b> junto.',
    u'<b>Voz e transcrição</b> vão para o histórico dele.',
    u'<b>Fica no nome de quem falou</b> — padrão em toda a secretaria.',
])
