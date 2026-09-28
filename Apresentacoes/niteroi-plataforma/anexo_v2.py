# -*- coding: utf-8 -*-
"""Anexo visual, parte 2: cidadão, dado por baixo, gestão e "feito para a sua casa"."""
from gramatica import *

# =============================================================== CIDADÃO
# b31 — central de atendimento: mini-tela das conversas com posse + atendente
grava('b31-atendimento.html', slide('b-atendimento', 'corpo centro',
    cab(u'Cidadão · atendimento', u'Quem atende', u'sabe com quem fala'),
    u'''    <div class="com-boneco" style="flex:none;height:340px">
      <div class="lado-texto">
        <div class="mini-tela" style="flex:1">
          <div class="mt-barra"><span class="bolas"><i></i><i></i><i></i></span><b>Central de Atendimento</b> · Policlínica do Centro · 11 conversas abertas</div>
          <div class="mt-corpo">
            <div class="mt-lin sel"><span class="av">TA</span><b>Terezinha Alves Pinto</b> · quer saber do transporte<span class="dir"><span class="pill s-encaminhada">com Luana C.</span> · 2 min</span></div>
            <div class="mt-lin"><span class="av">JC</span><b>José Carlos Ribeiro</b> · resultado do US saiu?<span class="dir"><span class="pill s-triagem">robô respondeu</span> · 5 min</span></div>
            <div class="mt-lin"><span class="av">MG</span><b>Maria das Graças Souza</b> · remarcar mamografia<span class="dir"><span class="pill s-complementacao">sem dono</span> · 14 min</span></div>
            <div class="mt-lin"><span class="av">AF</span><b>Antônio Ferreira da Silva</b> · endereço da UBS<span class="dir"><span class="pill s-concluida">resolvida</span> · 22 min</span></div>
            <div class="mt-lin"><span class="av">LB</span><b>Luciana Barbosa Nunes</b> · transferida de Icaraí<span class="dir"><span class="pill s-encaminhada">com Rita M.</span> · 31 min</span></div>
            <p style="font-size:10.5px;color:var(--s500);margin:10px 0 0">“Sem dono” há 14 minutos é o que a coordenação vê primeiro.</p>
          </div>
        </div>
      </div>
      <div class="boneco m" style="position:relative;height:262px;align-self:flex-end">
        <img src="assets/bonecos/atendente.jpg" alt="">
        <div class="balao-fala" style="position:absolute;right:-6px;top:-92px;width:236px;font-size:12px">“Abro a conversa e o <b>histórico dela já está do lado</b>. Não pergunto de novo.”</div>
      </div>
    </div>
    <div class="grade g3" style="margin-top:14px;flex:none">
      <div class="bloco" style="padding:11px 14px"><p class="b-olho">Fila por unidade</p><p class="b-texto" style="margin:0">Cada casa atende a sua — não uma caixa única onde ninguém se responsabiliza.</p></div>
      <div class="bloco" style="padding:11px 14px"><p class="b-olho">Posse</p><p class="b-texto" style="margin:0">A conversa tem dono enquanto está sendo atendida. Ninguém responde por cima.</p></div>
      <div class="bloco realce" style="padding:11px 14px"><p class="b-olho">Supervisão</p><p class="b-texto" style="margin:0">Quem coordena vê todas e redistribui o que parou — permissão à parte.</p></div>
    </div>
'''))

# b33 — mensageria: os dois gráficos que a tela real tem
grava('b33-mensageria.html', slide('b-mensageria', 'corpo',
    cab(u'Cidadão · envios', u'O que saiu, o que chegou,', u'o que falhou'),
    u'''    <div class="grade g2" style="flex:1;min-height:0">
      <div class="grafico" style="display:flex;flex-direction:column;justify-content:center">
        <p class="g-tit">Status de entrega — mês</p>
        <div class="rosca-bloco">
          <div class="rosca" style="background:conic-gradient(var(--nit-verde) 0 71%, var(--p400) 71% 96%, var(--nit-vermelho) 96% 100%)">
            <span class="centro"><b>96%</b><span>entregues</span></span>
          </div>
          <div class="legenda-rosca">
            <span class="li"><span class="pt" style="background:var(--nit-verde)"></span> Lida <b>71%</b></span>
            <span class="li"><span class="pt" style="background:var(--p400)"></span> Entregue, não lida <b>25%</b></span>
            <span class="li"><span class="pt" style="background:var(--nit-vermelho)"></span> Falhou <b>4%</b></span>
          </div>
        </div>
        <p class="g-nota" style="margin-top:12px">Dos 4% que falham, quase tudo é número inválido — e cada um vira pendência de cadastro com nome.</p>
      </div>
      <div class="grafico" style="display:flex;flex-direction:column">
        <p class="g-tit">Templates mais enviados</p>
        <div class="ranking" style="flex:1;justify-content:center">
          <div class="linha"><span class="rot">Confirmação de consulta</span><span class="trilho"><span class="barra" style="width:100%"></span></span><span class="val">7.210</span></div>
          <div class="linha"><span class="rot">Exame pronto</span><span class="trilho"><span class="barra" style="width:52%"></span></span><span class="val">3.740</span></div>
          <div class="linha"><span class="rot">Lembrete de véspera</span><span class="trilho"><span class="barra" style="width:44%"></span></span><span class="val">3.180</span></div>
          <div class="linha"><span class="rot">Agenda mudou</span><span class="trilho"><span class="barra fraca" style="width:17%"></span></span><span class="val">1.226</span></div>
          <div class="linha"><span class="rot">Transporte confirmado</span><span class="trilho"><span class="barra fraca" style="width:12%"></span></span><span class="val">864</span></div>
        </div>
        <p class="g-nota">Reenviar com o número corrigido não dispara de novo para quem já respondeu.</p>
      </div>
    </div>
'''))

# b34 — satisfação: distribuição de notas + cidadã
grava('b34-satisfacao.html', slide('b-satisfacao', 'corpo centro',
    cab(u'Cidadão · satisfação', u'A avaliação que', u'vira indicador'),
    u'''    <div class="com-boneco" style="flex:none;align-items:flex-end;gap:24px">
      <div class="boneco m" style="height:270px;position:relative">
        <img src="assets/bonecos/cidada.jpg" alt="">
        <div class="balao-fala" style="position:absolute;left:78%;top:-4px;width:200px;font-size:12px">“Deram cinco estrelinhas pra eu apertar. Apertei.”</div>
      </div>
      <div class="lado-texto">
        <div class="grade g-1-2" style="align-items:center">
          <div>
            <div class="numerao"><span class="n">4,6</span><span class="u">de 5</span></div>
            <p class="numerao-legenda">média do mês, <b>1.912 respostas</b>. Enviada pelo mesmo canal, logo após o atendimento — enquanto a pessoa lembra.</p>
          </div>
          <div class="grafico">
            <p class="g-tit">Distribuição das notas</p>
            <div class="ranking">
              <div class="linha"><span class="rot">★★★★★</span><span class="trilho"><span class="barra" style="width:100%"></span></span><span class="val">68%</span></div>
              <div class="linha"><span class="rot">★★★★</span><span class="trilho"><span class="barra" style="width:31%"></span></span><span class="val">21%</span></div>
              <div class="linha"><span class="rot">★★★</span><span class="trilho"><span class="barra fraca" style="width:9%"></span></span><span class="val">6%</span></div>
              <div class="linha"><span class="rot">★★</span><span class="trilho"><span class="barra fraca" style="width:4%"></span></span><span class="val">3%</span></div>
              <div class="linha"><span class="rot">★</span><span class="trilho"><span class="barra fraca" style="width:3%"></span></span><span class="val">2%</span></div>
            </div>
          </div>
        </div>
      </div>
    </div>
''' + faixa(u'A resposta nasce amarrada à unidade e ao profissional avaliados — vira indicador por unidade e por período, inclusive o de satisfação que a Lei 13.460 exige da ouvidoria. Não é uma nota solta sobre “a saúde do município”.', u'Ligada a quê')))

# =============================================================== O DADO POR BAIXO
# b40 — cidadão único: cinco registros convergem
grava('b40-cidadao-unico.html', slide('b-cidadao-unico', 'corpo centro',
    cab(u'Cadastros · cidadão', u'Cinco registros,', u'uma pessoa'),
    converge([
        (u'Prontuário da UBS', u'nome com abreviação'),
        (u'Prontuário do hospital', u'data de nascimento digitada errada'),
        (u'Sistema de regulação', u'só o CNS'),
        (u'Base nacional', u'CPF e endereço antigo'),
        (u'Recepção, ontem', u'telefone novo'),
    ], u'Um cadastro só', u'O documento manda',
       u'CPF e cartão do SUS identificam a pessoa. Cada dado carrega de onde veio. Telefone novo entra sem apagar o antigo. Quem não tem CPF entra marcado, para a recepção resolver depois — sem travar o atendimento de hoje.')
    + faixa(u'Só existe histórico do paciente se houver um paciente só. Toda a leitura de “o que já foi feito por esta pessoa” depende deste ponto — é por isso que ele vem antes dos outros.', u'O que isso destrava')))

# b41 — prontuários: trilho de leitura
grava('b41-prontuarios.html', slide('b-prontuarios', 'corpo centro',
    cab(u'Cadastros · prontuários', u'O histórico dos sistemas', u'que a rede já usa'),
    trilho([
        ('', 'medico', u'O prontuário continua', u'Quem atende não muda de tela. A plataforma só lê.'),
        ('', None, u'Lê do jeito de cada um', u'Cada produto guarda as coisas à sua maneira; o entendimento se escreve uma vez.'),
        ('chave', None, u'Reúne sob o mesmo cidadão', u'Atendimentos, exames e medicações de sistemas diferentes, na mesma linha do tempo.'),
        ('', None, u'Roda sozinho', u'Leitura contínua, acompanhada: o que entrou, o que faltou, o que voltou a tentar.'),
        ('fim', 'gestora', u'O legado também', u'Sistema aposentado ainda guarda muita história. É lido uma vez e incorporado.'),
    ])
    + faixa(u'Nenhum prontuário sai de operação para a plataforma entrar. Ela lê o que ele registrou — não escreve nele e não altera a rotina de quem atende.', u'O que não muda')))

# b42 — qualidade: de 100 registros importados
grava('b42-qualidade.html', slide('b-qualidade', 'corpo centro',
    cab(u'Cadastros · qualidade', u'De cada 100 registros antigos,', u'o que entra'),
    u'''    <div class="proporcao" style="flex:none">
      <div class="segmentos">
        <div class="seg" style="flex:91;background:var(--nit-verde)"><b>91</b><span>entram limpos</span></div>
        <div class="seg" style="flex:6;background:var(--p400)"><b>6</b><span>divergência</span></div>
        <div class="seg" style="flex:3;background:var(--s500)"><b>3</b><span>rejeitados</span></div>
      </div>
      <div class="consequencias">
        <div class="cons" style="flex:91"><div class="flecha">''' + SETA_B + u'''</div><p class="txt"><b>Viram histórico do cidadão</b> — com a origem de cada dado registrada.</p></div>
        <div class="cons" style="flex:6"><div class="flecha">''' + SETA_B + u'''</div><p class="txt"><b>Pessoa decide</b>, com as duas versões à vista. O sistema não escolhe sozinho qual nome é o verdadeiro.</p></div>
        <div class="cons" style="flex:3"><div class="flecha">''' + SETA_B + u'''</div><p class="txt"><b>Ficam nomeados</b> numa lista: documento inválido, data impossível, registro sem pessoa.</p></div>
      </div>
    </div>
    <div class="grade g2" style="margin-top:22px;flex:none">
      <div class="bloco"><p class="b-olho">Divergência</p><p class="b-titulo">Quando as fontes discordam</p><p class="b-texto">Dois sistemas com nomes ou datas diferentes para o mesmo CPF: vira pendência para alguém arbitrar — não silêncio, não sobrescrita.</p></div>
      <div class="bloco realce"><p class="b-olho">A ressalva honesta</p><p class="b-titulo">Nunca é instantâneo nem 100%</p><p class="b-texto">O que se promete é que <b>o que não entrou fica visível e nomeado</b> — não que tudo entra perfeito. Base antiga tem dado ruim; fingir que não tem é o que estraga a base nova.</p></div>
    </div>
'''))

# b43 — cadastros base: antes/depois da conciliação de nomes
grava('b43-cadastros-base.html', slide('b-cadastros-base', 'corpo centro',
    cab(u'Cadastros · rede', u'Nomes diferentes,', u'exame igual'),
    u'''    <div class="antes-depois" style="flex:none">
      <div class="col antes">
        <p class="rot-ad">Em cada sistema</p>
        <p class="frase">“US ABDOME TOTAL” no SISREG · “Ultrassonografia de abdômen total” no SERNIT · “USG abd. total” no prontuário.</p>
        <p class="apoio">Três nomes, três filas, três contagens que ninguém soma.</p>
      </div>
      <div class="meio">''' + SETA_D + u'''</div>
      <div class="col depois">
        <p class="rot-ad">No catálogo</p>
        <p class="frase">Um procedimento só, com o nome que cada sistema usa pendurado nele.</p>
        <p class="apoio">O pareamento é sugerido pela máquina e <b>confirmado por pessoa</b>. A fila passa a ser somável.</p>
      </div>
    </div>
    <div class="grade g3" style="margin-top:20px;flex:none">
      <div class="bloco"><p class="b-olho">Unidades</p><p class="b-titulo">Vêm do cadastro nacional</p><p class="b-texto">Código, nome e endereço do registro oficial. Unidade nova aparece sem digitação.</p></div>
      <div class="bloco"><p class="b-olho">Profissionais</p><p class="b-titulo">Médicos e demais</p><p class="b-texto">Conselho, especialidade e onde atende — o que diz quem pode laudar e quem assina o quê.</p></div>
      <div class="bloco"><p class="b-olho">Procedimentos</p><p class="b-titulo">A tabela oficial</p><p class="b-texto">O catálogo nacional, com o nome que cada sistema de regulação usa para o mesmo exame.</p></div>
    </div>
'''))

# =============================================================== GESTÃO
# b50 — ouvidoria: trilho com a cidadã no início
grava('b50-ouvidoria-det.html', slide('b-ouvidoria-det', 'corpo centro',
    cab(u'Gestão · ouvidoria', u'O que a lei cobra,', u'no lugar dele'),
    trilho([
        ('', 'cidada', u'Entra por qualquer porta', u'Balcão, telefone, WhatsApp, site, app, Fala.BR. Sai com protocolo.'),
        ('', None, u'É triada', u'Seis tipos oficiais, assunto do OuvidorSUS, prioridade. Denúncia corre em trilho próprio.'),
        ('chave', 'gestora', u'Vai a quem resolve', u'A policlínica, o hospital, a regulação, a farmácia — com responsável e prazo.'),
        ('', None, u'O relógio cobra', u'30 dias ao cidadão, prazo menor à área. Cobrança e escalonamento automáticos.'),
        ('fim', None, u'Vira relatório', u'O anual e a pesquisa de satisfação que a Lei 13.460 exige saem da base.'),
    ])
    + faixa(u'A reclamação sobre a fila de cardiologia chega ligada ao pedido de regulação real daquele cidadão. A ouvidoria responde o que aconteceu — não “encaminhamos ao setor”.', u'A diferença de estar junto')))

# b52 — auditoria: a trilha de um registro
grava('b52-auditoria.html', slide('b-auditoria', 'corpo',
    cab(u'Gestão · confiança', u'Quem fez o quê,', u'e quando'),
    u'''    <div class="com-boneco">
      <div class="lado-texto">
        <div class="mini-tela" style="flex:1">
          <div class="mt-barra"><span class="bolas"><i></i><i></i><i></i></span><b>Trilha · solicitação 2026-018842</b> · Raimunda Nonata Freire · cardiologia</div>
          <div class="mt-corpo">
            <div class="linha-tempo">
              <div class="passo"><div class="trilha"><span class="bola"></span><span class="fio"></span></div><div class="conteudo-passo"><p class="quando-tp">18/05 · 10h04 · Policlínica do Centro</p><p class="tit-tp">Aberta por Carla S. (unidade solicitante)</p><p class="txt-tp">Com justificativa e ECG anexado.</p></div></div>
              <div class="passo"><div class="trilha"><span class="bola"></span><span class="fio"></span></div><div class="conteudo-passo"><p class="quando-tp">20/05 · 15h31 · Regulação</p><p class="tit-tp">Assumida por Rita M. — prioridade Normal → Alta</p><p class="txt-tp">Motivo registrado: hipertensão e idade.</p></div></div>
              <div class="passo"><div class="trilha"><span class="bola"></span><span class="fio"></span></div><div class="conteudo-passo"><p class="quando-tp">02/07 · 09h12 · Regulação</p><p class="tit-tp">Devolvida à unidade por Paulo R. — pendência: exame prévio vencido</p><p class="txt-tp">O relógio da espera fica suspenso enquanto a ponta resolve.</p></div></div>
              <div class="passo pendente"><div class="trilha"><span class="bola"></span></div><div class="conteudo-passo"><p class="quando-tp">Hoje</p><p class="tit-tp">Aguardando vaga há 128 dias</p><p class="txt-tp">Cada linha acima tem autor, data e <b>o papel</b> de quem fez.</p></div></div>
            </div>
          </div>
        </div>
      </div>
      <div class="boneco m" style="height:290px;align-self:flex-end"><img src="assets/bonecos/gestora.jpg" alt=""></div>
    </div>
''' + faixa(u'Para responder auditoria e órgão de controle — e, principalmente, para encerrar a discussão interna sobre “quem cancelou isso”, que é o uso mais frequente. Ver também é ato: revelar identidade de denunciante ou abrir a chave de um agendamento fica registrado.', u'Para que serve na prática')))

# b53 — erros e suporte: trilho curto
grava('b53-erros-suporte.html', slide('b-erros-suporte', 'corpo centro',
    cab(u'Gestão · operação', u'Quando alguma coisa', u'não funciona'),
    trilho([
        ('', 'recepcionista', u'A tela falha', u'Acontece em todo sistema. A diferença é o que vem nos dez minutos seguintes.'),
        ('chave', None, u'O sistema avisa primeiro', u'A falha é registrada e a equipe técnica recebe o aviso — antes de o usuário reclamar.'),
        ('', None, u'O usuário recebe um código', u'ERRO-4YCCKQ. Em vez de descrever a tela de memória, informa o código.'),
        ('', None, u'O chamado nasce na tela', u'Abre ali mesmo, acompanha a resposta e sabe em que pé está.'),
        ('fim', 'gestora', u'Reincidência aparece', u'Erro já resolvido que volta gera registro novo — é assim que se descobre correção que não pegou.'),
    ])
    + faixa(u'Nada disso aparece em demonstração. Só depois de instalado — quando é tarde para descobrir que não tem.', u'Por que está neste material')))

# b54 — manual dentro do produto: mini-tela do artigo
grava('b54-manual.html', slide('b-manual', 'corpo centro',
    cab(u'Gestão · manual', u'O manual mora', u'dentro do produto'),
    u'''    <div class="com-boneco" style="flex:none;height:330px">
      <div class="lado-texto">
        <div class="mini-tela" style="flex:1">
          <div class="mt-barra"><span class="bolas"><i></i><i></i><i></i></span>smsmais.saude.niteroi.rj.gov.br/app/manual/<b>confirmacoes</b></div>
          <div class="mt-corpo">
            <div class="mt-cab"><b>Confirmações</b><span class="ajuda">?</span><span style="font-size:10px;color:var(--s500);margin-left:auto">buscar: <i>“número errado”</i> · 3 resultados</span></div>
            <div class="mt-lin sel"><b>As quatro abas</b> · Não confirmados, Confirmados, Contato errado, Pendentes<span class="dir">artigo</span></div>
            <div class="mt-lin"><b>O que fazer quando o número não é do paciente</b><span class="dir">seção</span></div>
            <div class="mt-lin"><b>Simulação: assuma um caso e envie para pendente</b><span class="dir">clicável</span></div>
            <div class="mt-lin"><b>Quem vê a aba Equipe</b> · permissão própria<span class="dir">seção</span></div>
            <p style="font-size:10.5px;color:var(--s500);margin:10px 0 0">O “?” ao lado do título de cada tela leva ao artigo dela — não a um manual genérico de 200 páginas.</p>
          </div>
        </div>
      </div>
      <div class="boneco m" style="position:relative;height:252px;align-self:flex-end">
        <img src="assets/bonecos/recepcionista.jpg" alt="">
        <div class="balao-fala" style="position:absolute;right:-6px;top:-92px;width:236px;font-size:12px">“A menina nova aprendeu sozinha. <b>Clicou no interrogação.</b>”</div>
      </div>
    </div>
''' + faixa(u'Toda alteração que alguém vê ou faz é confrontada com o manual na mesma entrega. Manual desatualizado mente com ar de autoridade — e treinamento que depende de lembrar a implantação não sobrevive à primeira troca de equipe.', u'A regra')))

# =============================================================== A SUA CASA
# b60 — permissões: matriz perfil × módulo
grava('b60-permissoes.html', slide('b-permissoes', 'corpo',
    cab(u'Sua casa · acesso', u'Cada um enxerga', u'o que é dele'),
    u'''    <div class="grade g-3-2" style="flex:1;min-height:0">
      <div class="grafico" style="display:flex;flex-direction:column">
        <p class="g-tit">Perfis × módulos — um recorte</p>
        <table class="matriz" style="flex:1">
          <thead><tr><th>Perfil</th><th>Regulação</th><th>Exames</th><th>Laudo</th><th>Atendimento</th><th>Ouvidoria</th><th>Indicadores</th><th>Auditoria</th></tr></thead>
          <tbody>
            <tr><td>Recepção da unidade</td><td><span class="parte">½</span></td><td><span class="ok">✓</span></td><td><span class="nao">–</span></td><td><span class="ok">✓</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td></tr>
            <tr><td>Regulador</td><td><span class="ok">✓</span></td><td><span class="parte">½</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="parte">½</span></td><td><span class="nao">–</span></td></tr>
            <tr><td>Médico radiologista</td><td><span class="nao">–</span></td><td><span class="ok">✓</span></td><td><span class="ok">✓</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td></tr>
            <tr><td>Ouvidor</td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="nao">–</span></td><td><span class="ok">✓</span></td><td><span class="parte">½</span></td><td><span class="nao">–</span></td></tr>
            <tr><td>Coordenação</td><td><span class="parte">½</span></td><td><span class="parte">½</span></td><td><span class="nao">–</span></td><td><span class="ok">✓</span></td><td><span class="parte">½</span></td><td><span class="ok">✓</span></td><td><span class="ok">✓</span></td></tr>
            <tr><td>Direção</td><td><span class="parte">½</span></td><td><span class="parte">½</span></td><td><span class="nao">–</span></td><td><span class="parte">½</span></td><td><span class="parte">½</span></td><td><span class="ok">✓</span></td><td><span class="ok">✓</span></td></tr>
          </tbody>
        </table>
        <p class="g-nota"><span class="ok" style="display:inline-block;width:14px;height:14px;line-height:14px;font-size:9px;border-radius:50%;background:#dcfce7;color:#15803d;text-align:center">✓</span> vê e faz &nbsp; <span style="display:inline-block;width:14px;height:14px;line-height:14px;font-size:8px;border-radius:50%;background:#fef3c7;color:#92400e;text-align:center">½</span> só consulta &nbsp; – não aparece no menu. Um recorte de 7 dos 75 módulos.</p>
      </div>
      <div style="display:flex;flex-direction:column;gap:11px">
        <div class="bloco" style="flex:1"><div class="numerao"><span class="n">75</span><span class="u">módulos × 4 ações</span></div><p class="numerao-legenda">ver, incluir, alterar, arquivar — combinados em perfis prontos que se aplicam a quem chega, sem remontar permissão por pessoa.</p><p class="b-olho" style="margin-top:14px">Por unidade</p><p class="b-texto" style="margin:0">Quem é da unidade vê a unidade; quem é da regulação vê a rede. Na dúvida, o sistema <b>nega</b>.</p></div>
        <div class="bloco escuro"><p class="b-olho" style="color:var(--nit-amarelo)">Assuntos separados de propósito</p><p class="b-texto">Ver a produção das colegas, revelar identidade de denunciante, corrigir de quem é um exame e disparar mensagem ao cidadão são permissões próprias, desligadas por padrão. Não são graus do mesmo acesso — são pessoas diferentes.</p></div>
      </div>
    </div>
'''))

# b61 — instituição: mini-tela do cadastro da marca
grava('b61-instituicao.html', slide('b-instituicao', 'corpo centro',
    cab(u'Sua casa · identidade', u'A marca vem do cadastro,', u'não do código'),
    u'''    <div class="grade g-3-2" style="flex:none;align-items:stretch">
      <div class="mini-tela">
        <div class="mt-barra"><span class="bolas"><i></i><i></i><i></i></span><b>Sistema › Instituição</b></div>
        <div class="mt-corpo">
          <div class="mt-lin"><b>Nome</b><span class="dir">Fundação Municipal de Saúde de Niterói</span></div>
          <div class="mt-lin"><b>Cor primária</b><span class="dir"><span style="display:inline-block;width:14px;height:14px;border-radius:4px;background:#ee7219;vertical-align:middle;margin-right:6px"></span>#EE7219</span></div>
          <div class="mt-lin"><b>Logo</b><span class="dir"><img src="assets/niteroi-saude.png" style="height:22px;vertical-align:middle" alt=""></span></div>
          <div class="mt-lin"><b>Domínio</b><span class="dir">smsmais.saude.niteroi.rj.gov.br</span></div>
          <div class="mt-lin"><b>WhatsApp oficial</b><span class="dir">+55 21 •••• ••••</span></div>
          <div class="mt-lin sel"><b>Encarregado de dados (LGPD)</b><span class="dir">lgpd@saude.niteroi.rj.gov.br</span></div>
          <p style="font-size:10.5px;color:var(--s500);margin:10px 0 0">O que se salva aqui aparece no login, no PDF do laudo, na página pública de verificação e no app do cidadão.</p>
        </div>
      </div>
      <div style="display:flex;flex-direction:column;gap:11px">
        <div class="bloco" style="flex:1"><p class="b-olho">Instância</p><p class="b-titulo">Servidor e banco próprios</p><p class="b-texto">Os dados de Niterói não dividem lugar com os de ninguém. O que acontece em outro cliente não afeta esta casa.</p></div>
        <div class="bloco realce"><p class="b-olho">Por isso</p><p class="b-titulo">Trocar de município é cadastro</p><p class="b-texto">Este material inteiro está em laranja porque a cor veio deste campo. Nenhuma linha de código mudou.</p></div>
      </div>
    </div>
'''))

# b62 — segurança: a regra do destinatário como decisão
grava('b62-seguranca.html', slide('b-seguranca', 'corpo centro',
    cab(u'Sua casa · segurança', u'Resultado de exame não vai', u'para telefone duvidoso'),
    u'''    <div class="decisao" style="flex:none">
      <div class="caixa" style="grid-row:1;grid-column:1"><b>Laudo ficou pronto</b>o sistema vai avisar a paciente</div>
      <div class="liga h" style="grid-row:1;grid-column:2">''' + SETA_D + u'''</div>
      <div class="losango-wrap" style="grid-row:1;grid-column:3"><div class="losango"><span>O número é dela? Ela autorizou?</span></div></div>
      <div class="liga h" style="grid-row:1;grid-column:4"><span class="rotulo">SIM</span>''' + SETA_D + u'''</div>
      <div class="caixa robo" style="grid-row:1;grid-column:5"><b>Envia</b>com o aceite registrado: data e versão do termo</div>
      <div class="liga baixo" style="grid-row:2;grid-column:3"><span class="rotulo">NÃO / NÃO SEI</span>''' + SETA_B + u'''</div>
      <div class="liga baixo" style="grid-row:2;grid-column:5"><span class="rotulo">SEMPRE</span>''' + SETA_B + u'''</div>
      <div class="caixa humano" style="grid-row:3;grid-column:3"><b>Não envia</b>vira pendência para a recepção confirmar o contato</div>
      <div class="caixa" style="grid-row:3;grid-column:5"><b>Fica na trilha</b>quem viu, quem enviou, para qual número</div>
    </div>
    <div class="grade g3" style="margin-top:22px;flex:none">
      <div class="bloco" style="padding:11px 14px"><p class="b-olho">Acesso</p><p class="b-texto" style="margin:0">Mínimo necessário. O sistema nega por padrão e libera por decisão.</p></div>
      <div class="bloco" style="padding:11px 14px"><p class="b-olho">Registro</p><p class="b-texto" style="margin:0">Acesso a dado sensível deixa rastro. Em incidente, dá para dizer o que foi exposto.</p></div>
      <div class="bloco realce" style="padding:11px 14px"><p class="b-olho">Vínculo</p><p class="b-texto" style="margin:0">Mãe pelo filho, filha pelo pai: o vínculo declarado fica registrado — e o negado não recebe.</p></div>
    </div>
'''))

# b63 — roteiro: entregue × no roteiro, com checks
grava('b63-roteiro.html', slide('b-roteiro', 'corpo centro',
    cab(u'Fecho · evolução', u'O que está pronto', u'e o que está no roteiro'),
    u'''    <div class="grade g2" style="flex:none">
      <div class="bloco">
        <p class="b-olho">Entregue — o que este material mostrou</p>
        <ul class="checks" style="margin-top:8px">
          <li><span class="ck">✓</span><span><b>Regulação e fila</b> — SERNIT, SISREG e SER lado a lado, com trilha</span></li>
          <li><span class="ck">✓</span><span><b>Agenda</b> — oferta × ocupação, alterações avisadas, simulação</span></li>
          <li><span class="ck">✓</span><span><b>Exames e laudos</b> — lista por aparelho, acervo único, laudo assinado no celular</span></li>
          <li><span class="ck">✓</span><span><b>Contato com o cidadão</b> — app, WhatsApp oficial, confirmação, robô</span></li>
          <li><span class="ck">✓</span><span><b>Transporte de pacientes</b>, <b>ouvidoria</b>, <b>indicadores</b>, consulta em português</span></li>
          <li><span class="ck">✓</span><span><b>Telefonia IP</b> das unidades, central e gestão de ramais</span></li>
        </ul>
      </div>
      <div class="bloco realce">
        <p class="b-olho">No roteiro — marcado como roteiro</p>
        <ul class="checks" style="margin-top:8px">
          <li><span class="ck rot">→</span><span><b>Ramal por login</b> e a chamada gravada e transcrita dentro do cadastro do paciente. A telefonia já opera; falta amarrar ao cadastro.</span></li>
          <li><span class="ck rot">→</span><span><b>Resultado laboratorial.</b> A solicitação já é classificada e regulada como as demais; guardar e exibir o resultado é o passo seguinte.</span></li>
        </ul>
        <p class="b-texto" style="margin-top:10px">Duas linhas. Curtas e verificáveis — e é por isso que estão escritas.</p>
      </div>
    </div>
''' + faixa(u'A régua deste material: o que roda aparece como entrega, o que falta aparece como roteiro — e nada aparece como os dois. Um material que só promete não deixa ninguém conferir nada depois.', u'Por que dizer isto em voz alta')))

# b64 — ressalva de método: gestora + balão
grava('b64-ressalva.html', slide('b-ressalva', 'corpo centro',
    cab(u'Fecho · método', u'Como este material', u'foi montado'),
    u'''    <div class="com-boneco" style="flex:none;align-items:flex-end;gap:26px">
      <div class="boneco g" style="height:300px;position:relative">
        <img src="assets/bonecos/gestora.jpg" alt="">
        <div class="balao-fala" style="position:absolute;left:80%;top:0;width:250px;font-size:12.5px">“Não houve levantamento de campo em Niterói. <b>O que está aqui é o produto</b>; o volume de vocês, eu ainda não conheço.”</div>
      </div>
      <div class="lado-texto" style="gap:11px">
        <div class="bloco" style="flex:1"><p class="b-olho">As telas</p><p class="b-titulo">São o sistema real</p><p class="b-texto">Reproduzem o que o produto faz hoje, campo a campo. O que muda nelas é o cenário, não o comportamento.</p></div>
        <div class="bloco" style="flex:1"><p class="b-olho">Os dados</p><p class="b-titulo">São cenário, não operação</p><p class="b-texto">Nomes de paciente, protocolos e números são fictícios e coerentes entre si — a mesma Terezinha atravessa a fila, o WhatsApp e a ligação. Nenhum dado real de paciente entrou aqui.</p></div>
        <div class="bloco escuro"><p class="b-olho" style="color:var(--nit-amarelo)">O que vem depois</p><p class="b-texto">Uma conversa com a equipe para entender os sistemas de hoje e por qual frente faz sentido começar. Com isso, a proposta sai com número — e com o que dá para prometer.</p></div>
      </div>
    </div>
'''))
