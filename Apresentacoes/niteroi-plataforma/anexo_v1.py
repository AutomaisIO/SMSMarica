# -*- coding: utf-8 -*-
"""Anexo visual, parte 1: exames/laudos e regulação/agenda."""
from gramatica import *

# =============================================================== EXAMES
# b10 — de onde vem o pedido: duas origens convergem num pedido só
grava('b10-solicitacao.html', slide('b-solicitacao', 'corpo centro',
    cab(u'Exames · o pedido', u'De onde vem', u'o pedido'),
    converge([
        (u'Do sistema de regulação', u'importado, sem redigitar'),
        (u'Da própria unidade', u'aberto na tela, com permissão'),
        (u'Chegou duas vezes', u'reconhecido, não duplica'),
    ], u'Um pedido só', u'Já classificado e vinculado',
       u'Paciente, procedimento e unidade executante desde o nascimento. O procedimento diz se é imagem, laboratório, consulta ou método gráfico — e o sistema já sabe o que fazer com cada um.')
    + faixa(u'A maior parte do trabalho perdido em serviço de imagem está em corrigir vínculo errado depois. Exame que nasce certo chega ao laudo sem retrabalho.', u'Por que isso importa')))

# b11 — cada aparelho vê a sua lista: mini-tela da lista de trabalho + boneco
grava('b11-equipamentos.html', slide('b-equipamentos', 'corpo centro',
    cab(u'Exames · equipamentos', u'Cada aparelho vê', u'só a lista dele'),
    u'''    <div class="com-boneco" style="flex:none;height:340px">
      <div class="lado-texto">
        <div class="mini-tela" style="flex:1">
          <div class="mt-barra"><span class="bolas"><i></i><i></i><i></i></span><b>US-02 · Policlínica de Icaraí</b> · lista de trabalho de hoje</div>
          <div class="mt-corpo">
            <div class="mt-lin sel"><span class="av">JC</span><b>José Carlos Ribeiro</b> · US de abdome total<span class="dir">08h40 · <span class="pill s-triagem">aguardando</span></span></div>
            <div class="mt-lin"><span class="av">LB</span><b>Luciana Barbosa Nunes</b> · US de tireoide<span class="dir">09h10 · <span class="pill s-triagem">aguardando</span></span></div>
            <div class="mt-lin"><span class="av">AF</span><b>Antônio Ferreira da Silva</b> · US de vias urinárias<span class="dir">09h40 · <span class="pill s-concluida">feito</span></span></div>
            <div class="mt-lin"><span class="av">SR</span><b>Sebastião Rocha Martins</b> · US de abdome total<span class="dir">10h20 · <span class="pill s-triagem">aguardando</span></span></div>
            <div class="mt-lin"><span class="av">MG</span><b>Maria das Graças Souza</b> · US pélvico<span class="dir">11h00 · <span class="pill s-triagem">aguardando</span></span></div>
            <div class="mt-lin"><span class="av">RN</span><b>Raimunda Nonata Freire</b> · US de mama<span class="dir">11h30 · <span class="pill s-triagem">aguardando</span></span></div>
            <p style="font-size:10.5px;color:var(--s500);margin:10px 0 0">6 pacientes. Nada de outra unidade aparece aqui — nem por engano.</p>
          </div>
        </div>
      </div>
      <div class="boneco m" style="position:relative;height:262px;align-self:flex-end">
        <img src="assets/bonecos/atendente.jpg" alt="">
        <div class="balao-fala" style="position:absolute;right:-6px;top:-92px;width:230px;font-size:12px">“Eu não digito nome. <b>Escolho da lista</b> — e o exame já sai vinculado.”</div>
      </div>
    </div>
    <div class="grade g3" style="margin-top:14px;flex:none">
      <div class="bloco" style="padding:11px 14px"><p class="b-olho">Cadastro</p><p class="b-texto" style="margin:0">Aparelho instalado de manhã aparece na lista de trabalho à tarde.</p></div>
      <div class="bloco" style="padding:11px 14px"><p class="b-olho">Qualquer marca</p><p class="b-texto" style="margin:0">Raio-X, ultrassom, mamógrafo, tomógrafo — o padrão é o mesmo há décadas.</p></div>
      <div class="bloco realce" style="padding:11px 14px"><p class="b-olho">Sem cadastro</p><p class="b-texto" style="margin:0"><b>O envio falha com aviso</b> — não em silêncio, que é o pior dos mundos.</p></div>
    </div>
'''))

# b12 — um acervo só: número hero + rosca de modalidades
grava('b12-acervo.html', slide('b-acervo', 'corpo centro',
    cab(u'Exames · acervo', u'Um acervo, todas as unidades,', u'todos os aparelhos'),
    u'''    <div class="grade g-1-2" style="flex:none;align-items:center">
      <div>
        <div class="numerao"><span class="n">1</span><span class="u">acervo</span></div>
        <p class="numerao-legenda" style="font-size:13px">Exames de toda a rede no mesmo lugar, com a mesma busca. O médico vê o de hoje e os anteriores lado a lado, sem abrir outro sistema.</p>
        <div class="checks" style="margin-top:12px">
          <li><span class="ck">✓</span><span>Abre <b>no navegador</b>, de qualquer computador da rede</span></li>
          <li><span class="ck">✓</span><span><b>O acervo antigo entra junto</b> — o histórico não recomeça do zero</span></li>
          <li><span class="ck">✓</span><span>Cada unidade vê o que pediu e o que executou</span></li>
        </div>
      </div>
      <div class="grafico" style="display:flex;flex-direction:column;justify-content:center">
        <p class="g-tit">O que há no acervo, por tipo de exame</p>
        <div class="rosca-bloco">
          <div class="rosca" style="background:conic-gradient(var(--nit-laranja) 0 46%, var(--p400) 46% 77%, var(--p200) 77% 91%, var(--s300) 91% 100%)">
            <span class="centro"><b>4</b><span>modalidades</span></span>
          </div>
          <div class="legenda-rosca">
            <span class="li"><span class="pt" style="background:var(--nit-laranja)"></span> Raio-X <b>46%</b></span>
            <span class="li"><span class="pt" style="background:var(--p400)"></span> Ultrassom <b>31%</b></span>
            <span class="li"><span class="pt" style="background:var(--p200)"></span> Mamografia <b>14%</b></span>
            <span class="li"><span class="pt" style="background:var(--s300)"></span> Tomografia <b>9%</b></span>
          </div>
        </div>
        <p class="g-nota" style="margin-top:10px">Cenário ilustrativo. A composição real é a da rede — e aparece nesta mesma tela.</p>
      </div>
    </div>
''' + faixa(u'Ele deixa de carregar envelope de radiografia entre unidades — e deixa de repetir exame porque o anterior se perdeu.', u'O que o paciente ganha')))

# b13 — modelos de laudo: antes/depois + número
grava('b13-modelos-laudo.html', slide('b-modelos-laudo', 'corpo centro',
    cab(u'Exames · laudo', u'Modelos que o serviço', u'ajusta sozinho'),
    u'''    <div class="antes-depois" style="flex:none">
      <div class="col antes">
        <p class="rot-ad">Laudo do zero</p>
        <p class="frase">O médico digita técnica, achados normais e conclusão em todo exame — inclusive nos 80% que não têm nada.</p>
        <p class="apoio">Cada laudo é um documento novo. O que se repete se digita de novo.</p>
      </div>
      <div class="meio">''' + SETA_D + u'''</div>
      <div class="col depois">
        <p class="rot-ad">Com modelo</p>
        <p class="frase">O padrão já vem pronto. O médico escreve só o que é daquele caso.</p>
        <p class="apoio">Raio-X de tórax tem um modelo; mamografia tem outro, com a classificação que o rastreamento exige.</p>
      </div>
    </div>
    <div class="grade g3" style="margin-top:18px;flex:none">
      <div class="bloco"><div class="numerao"><span class="n">−4</span><span class="u">min por laudo</span></div><p class="numerao-legenda">é o que um modelo tira de um exame sem alteração — a maioria deles.</p></div>
      <div class="bloco"><p class="b-olho">Quem manda</p><p class="b-titulo">O serviço de imagem</p><p class="b-texto">Cria e ajusta os modelos na própria tela. Não depende de pedido a fornecedor.</p></div>
      <div class="bloco realce"><p class="b-olho">Cabeçalho</p><p class="b-titulo">A marca da secretaria</p><p class="b-texto">Cabeçalho e rodapé valem para todos os laudos e mudam num lugar só.</p></div>
    </div>
''' + faixa(u'O gargalo do serviço de imagem raramente é o aparelho — é o tempo até o laudo sair. Minutos por exame viram dias de fila.', u'Laudo mais rápido é fila menor')))

# b14 — entrega: trilho com a cidadã no fim
grava('b14-entrega-laudo.html', slide('b-entrega-laudo', 'corpo centro',
    cab(u'Exames · entrega', u'O laudo chega', u'e pode ser conferido'),
    trilho([
        ('', 'medico', u'Sai assinado', u'Com o certificado do próprio médico. Vale como documento.'),
        ('', None, u'Chega ao paciente', u'App ou WhatsApp oficial, com aviso na hora em que fica pronto.'),
        ('chave', 'cidada', u'Ela abre no celular', u'Laudo e imagens. Sem voltar à unidade para buscar papel.'),
        ('', None, u'Quem recebe confere', u'Página pública: o PDF é autêntico e não foi alterado.'),
        ('fim', None, u'Se precisar refazer', u'Versão nova, registrada. A anterior não some — some a dúvida.'),
    ])
    + faixa(u'Some a fila de balcão para retirar resultado — que em muitas unidades é a maior fila do dia e não atende ninguém: só entrega papel.', u'O que muda na rotina')))

# b15 — correção de identidade: linha do tempo do incidente
grava('b15-correcao.html', slide('b-correcao', 'corpo',
    cab(u'Exames · correção', u'Quando o exame vai', u'para o paciente errado'),
    u'''    <div class="com-boneco">
      <div class="boneco m"><img src="assets/bonecos/medico.jpg" alt=""></div>
      <div class="lado-texto">
        <div class="linha-tempo">
          <div class="passo"><div class="trilha"><span class="bola"></span><span class="fio"></span></div><div class="conteudo-passo"><p class="quando-tp">Dia 1 · 09h12</p><p class="tit-tp">A técnica seleciona o item errado da lista</p><p class="txt-tp">O paciente anterior. As imagens são arquivadas sob a pessoa errada. Acontece em todo serviço de imagem.</p></div></div>
          <div class="passo"><div class="trilha"><span class="bola"></span><span class="fio"></span></div><div class="conteudo-passo"><p class="quando-tp">Dia 1 · 14h</p><p class="tit-tp">O laudo é assinado — no prontuário de quem não fez o exame</p><p class="txt-tp">É o tipo de erro que só aparece meses depois, quando alguém compara com a história clínica.</p></div></div>
          <div class="passo"><div class="trilha"><span class="bola"></span><span class="fio"></span></div><div class="conteudo-passo"><p class="quando-tp">Dia 9 · a recepção percebe</p><p class="tit-tp">Uma tela corrige de quem é o estudo</p><p class="txt-tp">A imagem é reescrita no acervo, os rascunhos de laudo são descartados e <b>o link já enviado ao paciente é revogado</b>.</p></div></div>
          <div class="passo pendente"><div class="trilha"><span class="bola"></span></div><div class="conteudo-passo"><p class="quando-tp">Registro</p><p class="tit-tp">Autor, data e motivo ficam gravados</p><p class="txt-tp">É permissão à parte, dada a um punhado de nomes — não a todo mundo que mexe com exame.</p></div></div>
        </div>
      </div>
    </div>
'''))

# =============================================================== REGULAÇÃO
# b21 — três filas convergem
grava('b21-sistemas.html', slide('b-sistemas', 'corpo centro',
    cab(u'Regulação · sistemas', u'Três filas', u'uma visão'),
    converge([
        (u'SERNIT', u'o sistema da própria casa'),
        (u'SISREG', u'nacional, com a agenda importada'),
        (u'SER', u'estadual — o encaminhado não some'),
    ], u'O paciente é um só', u'A repetição aparece sozinha',
       u'Hoje, saber se alguém já foi encaminhado por outro caminho exige abrir três sistemas e comparar na mão. Reunidos, a vaga duplicada aparece — e vaga duplicada é vaga perdida.')
    + faixa(u'Cada sistema continua sendo operado onde é. A plataforma lê a situação de cada pedido e o histórico de quem mexeu — não substitui nenhum deles.', u'O que não muda')))

# b22 — oferta × ocupação por especialidade: barras duplas
grava('b22-vagas.html', slide('b-vagas', 'corpo centro',
    cab(u'Agenda · vagas', u'A oferta contra', u'a fila'),
    u'''    <div class="grade g-3-2" style="flex:none;height:400px">
      <div class="grafico" style="display:flex;flex-direction:column">
        <p class="g-tit">Vagas abertas × usadas no mês, por especialidade</p>
        <div class="duplas" style="flex:1;justify-content:center">
          <div class="cat"><span class="rot">Cardiologia</span><span class="par"><span class="b oferta" style="width:58%"></span><span class="b uso" style="width:56%"></span></span><span class="val"><b>412 / 420</b>fila: 147</span></div>
          <div class="cat"><span class="rot">Ortopedia</span><span class="par"><span class="b oferta" style="width:74%"></span><span class="b uso" style="width:70%"></span></span><span class="val"><b>521 / 540</b>fila: 96</span></div>
          <div class="cat"><span class="rot">Oftalmologia</span><span class="par"><span class="b oferta" style="width:100%"></span><span class="b uso" style="width:61%"></span></span><span class="val"><b>446 / 730</b>fila: 12</span></div>
          <div class="cat"><span class="rot">Dermatologia</span><span class="par"><span class="b oferta" style="width:48%"></span><span class="b uso" style="width:47%"></span></span><span class="val"><b>344 / 350</b>fila: 81</span></div>
          <div class="cat"><span class="rot">Endocrinologia</span><span class="par"><span class="b oferta" style="width:36%"></span><span class="b uso" style="width:22%"></span></span><span class="val"><b>160 / 260</b>fila: 8</span></div>
        </div>
        <div class="legenda-mini"><span><i style="background:var(--p200)"></i>abertas</span><span><i style="background:var(--nit-laranja)"></i>usadas</span><span>· números de cenário</span></div>
      </div>
      <div style="display:flex;flex-direction:column;gap:11px">
        <div class="bloco realce" style="flex:1"><p class="b-olho">Lê-se assim</p><p class="b-titulo">Oftalmologia sobra, cardiologia falta</p><p class="b-texto">284 vagas de oftalmo venceram vazias no mês; cardiologia usou 98% e ainda tem 147 na fila. Não é mais oferta que resolve — é <b>remanejar</b>.</p></div>
        <div class="bloco escuro"><p class="b-olho" style="color:var(--nit-amarelo)">O que isso destrava</p><p class="b-texto">A notícia de que a agenda estourou deixa de chegar pela reclamação de quem não conseguiu marcar.</p></div>
      </div>
    </div>
'''))

# b23 — simulação: série comparando dois cenários
grava('b23-estrategias.html', slide('b-estrategias', 'corpo',
    cab(u'Agenda · simulação', u'“Se eu abrir mais dois dias,', u'a fila zera quando?”'),
    u'''    <div class="grade g-3-2" style="flex:1;min-height:0">
      <div class="grafico serie">
        <p class="g-tit">Fila de cardiologia, semanas à frente — dois cenários</p>
        <svg viewBox="0 0 600 170" preserveAspectRatio="none">
          <g stroke="#e2e8f0" stroke-width="1"><line x1="0" y1="42" x2="600" y2="42"/><line x1="0" y1="84" x2="600" y2="84"/><line x1="0" y1="126" x2="600" y2="126"/></g>
          <polyline fill="none" stroke="#94a3b8" stroke-width="3" stroke-dasharray="6 5" points="0,20 100,34 200,50 300,68 400,88 500,110 600,134"/>
          <polyline fill="none" stroke="#ee7219" stroke-width="3.5" stroke-linejoin="round" points="0,20 100,44 200,78 300,118 400,150 460,168"/>
          <circle cx="460" cy="168" r="6" fill="#ee7219"/>
        </svg>
        <div class="eixo"><span>hoje</span><span>2 sem</span><span>4</span><span>6</span><span>8</span><span>10</span><span>12</span></div>
        <p class="g-nota"><span style="display:inline-block;width:18px;height:0;border-top:3px dashed #94a3b8;vertical-align:middle;margin-right:5px"></span>oferta atual: 12+ semanas &nbsp; <span style="display:inline-block;width:18px;height:3px;background:#ee7219;vertical-align:middle;margin-right:5px"></span>+2 dias de agenda: <b>zera na 9ª semana</b></p>
      </div>
      <div style="display:flex;flex-direction:column;gap:11px">
        <div class="bloco"><div class="numerao"><span class="n">9</span><span class="u">semanas</span></div><p class="numerao-legenda">em vez de “não sei”. A pergunta que hoje se responde no chute passa a ter conta.</p></div>
        <div class="bloco realce" style="flex:1"><p class="b-olho">Só planejamento</p><p class="b-texto">A simulação <b>não marca nada e não altera agenda nenhuma</b>. Ela existe para decidir antes de mexer — e a decisão fica guardada com quem tomou.</p></div>
      </div>
    </div>
''' + faixa(u'A simulação vale pela oferta que se informa. Ela não prevê falta de profissional nem aparelho quebrado — mostra o teto do que a oferta planejada alcança.', u'Uma ressalva', claro=True)))

# b24 — alterações de agenda: decisão + atendente
grava('b24-alteracoes.html', slide('b-alteracoes', 'corpo centro',
    cab(u'Agenda · mudanças', u'Quando a agenda muda', u'depois de marcada'),
    u'''    <div class="com-boneco" style="flex:none;align-items:center;gap:26px">
      <div class="lado-texto">
        <div class="decisao">
          <div class="caixa" style="grid-row:1;grid-column:1"><b>A importação percebe</b>remarcação, troca de profissional ou de procedimento</div>
          <div class="liga h" style="grid-row:1;grid-column:2">''' + SETA_D + u'''</div>
          <div class="losango-wrap" style="grid-row:1;grid-column:3"><div class="losango"><span>Alguém já foi avisado?</span></div></div>
          <div class="liga h" style="grid-row:1;grid-column:4"><span class="rotulo">NÃO</span>''' + SETA_D + u'''</div>
          <div class="caixa robo" style="grid-row:1;grid-column:5"><b>Entra na fila de tratamento</b>em vez de passar despercebida</div>
          <div class="liga baixo" style="grid-row:2;grid-column:3"><span class="rotulo">SIM</span>''' + SETA_B + u'''</div>
          <div class="liga baixo" style="grid-row:2;grid-column:5"><span class="rotulo">A EQUIPE CONFIRMA</span>''' + SETA_B + u'''</div>
          <div class="caixa" style="grid-row:3;grid-column:3"><b>Fica registrado</b>sem repetir o aviso</div>
          <div class="caixa humano" style="grid-row:3;grid-column:5"><b>O paciente é avisado</b>pelo mesmo canal da confirmação</div>
        </div>
      </div>
      <div class="boneco m" style="height:290px;position:relative;align-self:flex-end">
        <img src="assets/bonecos/atendente.jpg" alt="">
        <div class="balao-fala" style="position:absolute;right:-6px;top:-92px;width:236px;font-size:12px">“Antes ele descobria <b>na porta</b> que o médico não vinha.”</div>
      </div>
    </div>
''' + faixa(u'Paciente que viaja para uma consulta que mudou de dia perde diária, transporte e confiança. O terceiro é o mais caro de recuperar.', u'Por que é mais que cortesia')))

# b25 — produção por operador: KPI + ranking
grava('b25-operadores.html', slide('b-operadores', 'corpo',
    cab(u'Regulação · equipe', u'A produção de', u'quem opera'),
    u'''    <div class="painel-kpi" style="grid-template-columns:repeat(4,1fr)">
      <div class="kpi"><span class="rot">Autorizadas no mês</span><span class="num">2.140</span><span class="meta">SISREG + SERNIT + SER</span></div>
      <div class="kpi"><span class="rot">Devolvidas à ponta</span><span class="num">318</span><span class="meta">com pendência nomeada</span></div>
      <div class="kpi"><span class="rot">Tempo médio na mão</span><span class="num">1,8<i>dia</i></span><span class="meta">de assumir a decidir</span></div>
      <div class="kpi"><span class="rot">Paradas > 5 dias</span><span class="num alerta">41</span><span class="meta">é aqui que se olha primeiro</span></div>
    </div>
    <div class="grade g-3-2" style="margin-top:14px;flex:1;min-height:0">
      <div class="grafico" style="display:flex;flex-direction:column">
        <p class="g-tit">Solicitações decididas por operador — SISREG</p>
        <div class="ranking" style="flex:1;justify-content:center">
          <div class="linha"><span class="rot">Rita M.</span><span class="trilho"><span class="barra" style="width:100%"></span></span><span class="val">486</span></div>
          <div class="linha"><span class="rot">Paulo R.</span><span class="trilho"><span class="barra" style="width:84%"></span></span><span class="val">409</span></div>
          <div class="linha"><span class="rot">Carla S.</span><span class="trilho"><span class="barra" style="width:77%"></span></span><span class="val">372</span></div>
          <div class="linha"><span class="rot">Marcos A.</span><span class="trilho"><span class="barra fraca" style="width:39%"></span></span><span class="val">188</span></div>
          <div class="linha"><span class="rot">Denise L.</span><span class="trilho"><span class="barra fraca" style="width:21%"></span></span><span class="val">102</span></div>
        </div>
        <p class="g-nota">Denise entrou na equipe há três semanas. A leitura é <b>onde reforçar</b>, não quem cobrar.</p>
      </div>
      <div style="display:flex;flex-direction:column;gap:11px">
        <div class="bloco" style="flex:1"><p class="b-olho">Por sistema</p><p class="b-titulo">Equipes diferentes, leituras separadas</p><p class="b-texto">Quem cuida do SISREG não é quem cuida do SERNIT. Cada sistema tem a sua tela e a sua permissão.</p></div>
        <div class="bloco escuro"><p class="b-olho" style="color:var(--nit-amarelo)">Quem enxerga</p><p class="b-texto">Permissão própria, desligada por padrão. Quem atende não precisa ver o ranking das colegas; quem coordena, precisa.</p></div>
      </div>
    </div>
'''))
