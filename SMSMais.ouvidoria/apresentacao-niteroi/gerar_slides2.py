# -*- coding: utf-8 -*-
"""Parte 2 dos slides narrativos: evidência, fluxo, prazos, sigilo e divisores."""
import io, os
from gerar_slides import FITA, MARCA, RODAPE, secao, cab, slide

AQUI = os.path.dirname(os.path.abspath(__file__))
A = {}

# --------------------------------------------------------------- 06 evidência
KPIS = [
    ('', u'53,1%', u'das manifestações municipais de saúde são <b>solicitação de acesso</b> &mdash; vaga, exame, medicamento. Reclamação, 29,8%.', 'OuvSUS/MS 2024'),
    ('azul', u'30,4%', u'entram por <b>telefone</b> do estado ou do município; 22,7% pelo Disque 136; 20,1% presencial; 14,4% internet.', 'OuvSUS/MS 2024'),
    ('verde', u'92%', u'no prazo é a marca do Rio de Janeiro em 2025, com tempo médio de 12 dias &mdash; a régua de uma rede madura.', u'Ouvidoria-Geral do Rio'),
    ('vermelho', u'25%', u'fora do prazo no Recife em 2024, mesmo com 97% respondidas. O gargalo é o relógio, não o canal.', u'CGM Recife 2024'),
]
kpis = u'\n'.join(
    u'      <div class="kpi %s">\n        <div class="v">%s</div>\n'
    u'        <div class="r">%s</div>\n'
    u'        <div class="r" style="color:var(--s500);font-size:10.5px;margin-top:7px">%s</div>\n      </div>'
    % (c, v, r, f) for c, v, r, f in KPIS)

A['06-evidencia.html'] = slide(
    'evidencia',
    cab(u'Evidência', u'O que as outras ouvidorias de saúde <em>já mediram</em>',
        u'Nenhum número aqui é projeção: todos saíram de relatório público oficial, os mais recentes de 2024 e 2025. E '
        u'todos apontam para o mesmo lugar: o problema real não é falta de canal, é prazo e estoque.'),
    u'    <div class="kpi-fila">\n' + kpis + u'\n    </div>\n'
    u'    <div class="grade g2 cresce" style="margin-top:18px">\n'
    u'      <div class="bloco">\n'
    u'        <p class="b-olho">O caminho já trilhado</p>\n'
    u'        <p class="b-titulo">Secretaria de saúde com sistema próprio não é experimento</p>\n'
    u'        <ul class="lista">\n'
    u'          <li>A <b>SMS de São Paulo</b> ficou dez anos no OuvidorSUS e migrou, em 2025, para sistema '
    u'municipal próprio da saúde &mdash; rede de 58 unidades e cerca de 1.900 pontos de resposta com '
    u'login individual.</li>\n'
    u'          <li>Na própria SMS-SP, a resolutividade saltou de <b>50,9% (2015) para 76,6% (2016)</b> depois que a rede '
    u'de pontos de resposta foi estruturada. É o ponto de resposta que move o indicador.</li>\n'
    u'          <li>A árvore nacional de tipificação do SUS tem <b>23 assuntos e 1.897 itens</b> no '
    u'último nível. Uma lista genérica de assuntos de prefeitura não dá conta da saúde.</li>\n'
    u'        </ul>\n      </div>\n'
    u'      <div class="bloco escuro">\n'
    u'        <p class="b-olho" style="color:var(--nit-amarelo)">O risco de não medir</p>\n'
    u'        <p class="b-titulo">O canal paralelo cobra caro</p>\n'
    u'        <ul class="lista">\n'
    u'          <li style="color:rgba(255,255,255,.82)">Em Belo Horizonte, <b style="color:#fff">53,1% das '
    u'manifestações chegam por telefone</b> e só 0,15% por WhatsApp &mdash; porque lá o WhatsApp '
    u'não é canal oficial. Em Niterói ele é. Ou vira registro, ou vira conversa perdida.</li>\n'
    u'          <li style="color:rgba(255,255,255,.82)">No Espírito Santo, 2024 fechou com '
    u'<b style="color:#fff">623 manifestações acima de 60 dias</b>. Sem estoque à vista, o atraso '
    u'só aparece quando vira notícia.</li>\n'
    u'          <li style="color:rgba(255,255,255,.82)">Contrato de organização social já cobra isso: '
    u'no HEUE/SESA-ES, <b style="color:#fff">&ldquo;Atenção ao Usuário&rdquo; vale 25% da parte '
    u'variável</b>, com meta de 85% de resolução e primeira tratativa em 7 dias úteis.</li>\n'
    u'        </ul>\n      </div>\n    </div>')

# --------------------------------------------------------------- 07 seção fluxo
A['07-secao-fluxo.html'] = secao(
    'secao-fluxo', 'Parte 2', u'O caminho de<br>uma manifestação',
    u'Do balcão à resposta conclusiva, com dois relógios correndo e uma trilha que ninguém apaga. '
    u'É esta máquina de estados que o sistema impõe &mdash; e que uma planilha nunca vai impor.')

# --------------------------------------------------------------- 08 ciclo
ETAPAS = [
    (u'Registrada', u'Dia 0', u'Protocolo e código de acesso. Recibo pelo canal de entrada.', 's-registrada'),
    (u'Em triagem', u'Dia 1', u'Tipo, assunto, unidade, prioridade, marcadores. Desmembra, funde, arquiva duplicata.', 's-triagem'),
    (u'Encaminhada à área', u'Dia 3', u'Vai ao ponto de resposta com prazo próprio. Denúncia só vai pseudonimizada.', 's-encaminhada'),
    (u'Respondida pela área', u'Dia 18', u'A unidade responde. A ouvidoria cobra e escalona enquanto não vem.', 's-resparea'),
    (u'Em validação', u'Dia 19', u'A ouvidoria lê. Resposta vaga volta à área com metade do prazo.', 's-validacao'),
    (u'Respondida ao cidadão', u'Dia 21', u'Conteúdo mínimo do tipo, linguagem simples, resolutividade e situação final.', 's-respondida'),
    (u'Concluída', u'Dia 51', u'Trinta dias sem recurso e o sistema conclui sozinho.', 's-concluida'),
]
passos = u'\n'.join(
    u'        <div class="bloco" style="padding:12px 13px;position:relative">\n'
    u'          <span class="pill %s" style="font-size:11px">%s</span>\n'
    u'          <p style="margin:8px 0 0;font-family:var(--fonte-display);font-size:19px;font-weight:700;'
    u'color:var(--nit-laranja);line-height:1">%s</p>\n'
    u'          <p class="b-texto" style="font-size:11.5px;margin-top:5px">%s</p>\n        </div>'
    % (cls, nome, dia, txt) for nome, dia, txt, cls in ETAPAS)

DESVIOS = [
    (u'Aguardando complementação', u'Falta dado do cidadão. <b>Suspende o relógio</b>, vale uma vez só, e em 20 dias sem resposta o sistema arquiva.'),
    (u'Em recurso', u'O cidadão discorda. Volta a tramitar; pode ser reencaminhada e respondida de novo. Uma vez.'),
    (u'Arquivada', u'Um dos nove motivos tipificados. Duplicidade cita o protocolo original.'),
    (u'Encaminhada a outro órgão', u'Não é saúde municipal. Registra órgão e protocolo de destino &mdash; e <b>não admite prorrogação</b>.'),
]
desvios = u'\n'.join(
    u'        <div style="display:flex;gap:9px;align-items:flex-start;margin-bottom:9px">\n'
    u'          <span class="tag contorno" style="flex:none;min-width:auto">%s</span>\n'
    u'          <span style="font-size:12px;line-height:1.42;color:var(--tinta-fraca)">%s</span>\n        </div>'
    % (nome, txt) for nome, txt in DESVIOS)

A['08-ciclo.html'] = slide(
    'ciclo',
    cab(u'Máquina de estados', u'Onze estados, <em>vinte e um tipos de evento</em>, nenhum atalho',
        u'O caminho feliz tem sete paradas. Os dias são de um caso típico da fila: uma solicitação de '
        u'cardiologia encaminhada à Central de Regulação.'),
    u'    <div class="grade" style="grid-template-columns:repeat(7,1fr);gap:11px">\n' + passos + u'\n    </div>\n'
    u'    <div class="grade g2 cresce" style="margin-top:18px">\n'
    u'      <div class="bloco">\n'
    u'        <p class="b-olho">Quando sai do trilho</p>\n' + desvios + u'\n      </div>\n'
    u'      <div class="bloco realce">\n'
    u'        <p class="b-olho">O que o sistema faz sozinho</p>\n'
    u'        <ul class="lista">\n'
    u'          <li><b>Arquiva</b> a manifestação parada em complementação há mais de 20 dias, '
    u'e avisa o cidadão.</li>\n'
    u'          <li><b>Conclui</b> a que foi respondida há 30 dias e não teve recurso.</li>\n'
    u'          <li><b>Recalcula</b> o prazo quando a prioridade muda na triagem &mdash; e <b>congela</b> o '
    u'prazo já concedido depois do encaminhamento.</li>\n'
    u'          <li><b>Devolve metade</b> do prazo original à área quando a resposta volta para '
    u'reanálise (mínimo de 2 dias).</li>\n'
    u'          <li>Roda de <b>6 em 6 horas</b>; se um ciclo falha, o próximo tenta de novo.</li>\n'
    u'        </ul>\n      </div>\n    </div>')

# --------------------------------------------------------------- 09 prazos
A['09-prazos.html'] = slide(
    'prazos',
    cab(u'Prazos', u'Dois relógios, não um',
        u'O prazo do cidadão é o da lei e nunca muda de dono. O prazo da área é de gestão, '
        u'curto, e é o que a ouvidoria cobra. Confundir os dois é o erro clássico de sistema de chamado genérico.'),
    u'    <div class="grade g2 cresce" style="gap:18px">\n'
    u'      <div class="bloco" style="border-top:4px solid var(--nit-azul)">\n'
    u'        <p class="b-olho" style="color:var(--nit-azul)">Relógio 1 &middot; do cidadão</p>\n'
    u'        <p class="b-num" style="color:var(--nit-azul)">30 <span style="font-size:22px">+ 30 dias</span></p>\n'
    u'        <p class="b-texto">Contados do registro. A prorrogação é <b>uma só</b>, exige '
    u'justificativa de no mínimo 20 caracteres, e essa justificativa vai ao cidadão &mdash; como manda '
    u'o art. 16, <i>caput</i>. A segunda tentativa o sistema recusa.</p>\n'
    u'      </div>\n'
    u'      <div class="bloco" style="border-top:4px solid var(--nit-laranja)">\n'
    u'        <p class="b-olho">Relógio 2 &middot; da área</p>\n'
    u'        <p class="b-num">20 <span style="font-size:22px">/ 10 / 2 dias</span></p>\n'
    u'        <p class="b-texto">Conforme a prioridade definida na triagem: Normal 20 dias corridos, Alta 10, '
    u'<b>Urgente 2 dias úteis</b>. O padrão de 20 dias é o do art. 16, § único, que dá à ouvidoria o direito de exigir resposta da área. O ponto de resposta pode ter prazo próprio &mdash; o TFD com 5 dias, '
    u'o hospital com 15 &mdash; e o encaminhamento pode sobrescrever caso a caso.</p>\n'
    u'      </div>\n    </div>\n'
    u'    <div class="grade g4" style="margin-top:18px">\n'
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <p class="b-titulo" style="font-size:15px">Suspender</p>\n'
    u'        <p class="b-texto" style="font-size:12.5px">Pedir complementação para o relógio do '
    u'cidadão. Quando a resposta chega, os dias parados são <b>devolvidos ao prazo</b>. Uma vez por '
    u'manifestação.</p>\n      </div>\n'
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <p class="b-titulo" style="font-size:15px">Cobrar e escalonar</p>\n'
    u'        <p class="b-texto" style="font-size:12.5px">A cobrança fica na trilha, onde os membros do '
    u'ponto veem. O escalonamento é ação de <b>gestão</b>: leva o caso à chefia e à '
    u'autoridade máxima.</p>\n      </div>\n'
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <p class="b-titulo" style="font-size:15px">Devolver à área</p>\n'
    u'        <p class="b-texto" style="font-size:12.5px">Resposta vaga não vai ao cidadão. Volta à '
    u'área dizendo o que falta, com <b>metade do prazo original</b>, mínimo de 2 dias.</p>\n      </div>\n'
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <p class="b-titulo" style="font-size:15px">Ver o atraso</p>\n'
    u'        <p class="b-texto" style="font-size:12.5px">Cada linha da fila mostra o chip de prazo em verde, '
    u'âmbar ou vermelho, e a aba <b>Atrasadas</b> é uma só consulta. Estoque e faixa de prazo '
    u'saem no painel.</p>\n      </div>\n    </div>\n'
    u'    <p style="margin:12px 0 0;font-size:11px;color:var(--s500)">Todos os sete prazos são '
    u'configuráveis por instância, na tela de Configuração &mdash; sem tocar em código.</p>')

# --------------------------------------------------------------- 10 sigilo
A['10-sigilo.html'] = slide(
    'sigilo',
    cab(u'Sigilo e LGPD', u'Três níveis de identidade, <em>um trilho isolado para a denúncia</em>',
        u'Dado de saúde é dado sensível, e a identidade de quem denuncia é protegida por decreto. '
        u'Aqui isso não é política escrita num manual: é comportamento do código.'),
    u'    <div class="grade g3 cresce" style="gap:16px">\n'
    u'      <div class="bloco">\n'
    u'        <span class="pill s-registrada" style="align-self:flex-start;font-size:12px">Identificada</span>\n'
    u'        <p class="b-texto" style="margin-top:9px">Nome, CPF e contato ficam visíveis <b>só para a '
    u'ouvidoria</b>. O ponto de resposta recebe o caso sem saber de quem é. CPF basta para identificar &mdash; '
    u'a lei proíbe exigir outro número.</p>\n      </div>\n'
    u'      <div class="bloco">\n'
    u'        <span class="pill id-restrita" style="align-self:flex-start;font-size:12px">Sigilosa</span>\n'
    u'        <p class="b-texto" style="margin-top:9px">Os dados estão guardados, mas a tela mostra '
    u'<b>&ldquo;restrito&rdquo; para todo mundo</b> &mdash; inclusive para quem tem a permissão de sigilo. '
    u'A identidade só sai por uma ação explícita, com justificativa.</p>\n      </div>\n'
    u'      <div class="bloco">\n'
    u'        <span class="pill id-restrita" style="align-self:flex-start;font-size:12px">Anônima</span>\n'
    u'        <p class="b-texto" style="margin-top:9px">Só em denúncia. Não gera código de '
    u'acesso, não admite complementação, não recebe notificação e <b>não tem '
    u'identidade a revelar</b>. O sistema conclui no ato da resposta.</p>\n      </div>\n    </div>\n'
    u'    <div class="grade g-3-2" style="margin-top:18px">\n'
    u'      <div class="bloco escuro">\n'
    u'        <p class="b-olho" style="color:var(--nit-amarelo)">O trilho da denúncia</p>\n'
    u'        <div class="grade g2" style="gap:14px">\n'
    u'          <ul class="lista">\n'
    u'            <li style="color:rgba(255,255,255,.82)"><b style="color:#fff">Admissibilidade primeiro.</b> '
    u'A denúncia só vai à apuração depois do juízo de autoria, materialidade e '
    u'competência &mdash; com data e autor.</li>\n'
    u'            <li style="color:rgba(255,255,255,.82)"><b style="color:#fff">Teor pseudonimizado.</b> A '
    u'corregedoria nunca lê o texto original: lê a versão reescrita sem nada que identifique quem '
    u'denunciou. Sem ela, o encaminhamento é bloqueado.</li>\n'
    u'          </ul>\n'
    u'          <ul class="lista">\n'
    u'            <li style="color:rgba(255,255,255,.82)"><b style="color:#fff">Cada acesso vira linha.</b> '
    u'Revelar identidade exige justificativa e grava usuário, data, IP e motivo numa tabela própria, '
    u'que não se apaga.</li>\n'
    u'            <li style="color:rgba(255,255,255,.82)"><b style="color:#fff">Reclassificou, perdeu.</b> '
    u'Trocar o tipo de uma denúncia apaga a habilitação: o juízo era da denúncia, '
    u'não do caso.</li>\n'
    u'          </ul>\n        </div>\n      </div>\n'
    u'      <div class="bloco realce">\n'
    u'        <p class="b-olho">Quatro permissões separadas</p>\n'
    u'        <table class="tabela-doc" style="font-size:11.5px">\n'
    u'          <tbody>\n'
    u'            <tr><td>Ouvidoria</td><td>fila, triagem, encaminhamento e resposta</td></tr>\n'
    u'            <tr><td>Gestão</td><td>catálogo, configuração, painel, escalonar</td></tr>\n'
    u'            <tr><td>Sigilo</td><td>denúncia e identidade do manifestante</td></tr>\n'
    u'            <tr><td>Ponto de resposta</td><td>só o que foi encaminhado ao seu ponto</td></tr>\n'
    u'          </tbody>\n        </table>\n'
    u'        <p class="b-texto" style="margin-top:10px;font-size:12px">Quem não tem sigilo '
    u'<b>não enxerga denúncia nenhuma</b> &mdash; nem na busca. Sem usuário válido, o sistema '
    u'nega tudo: <i>fail-closed</i>.</p>\n      </div>\n    </div>')

# --------------------------------------------------------------- 11 seção telas
A['11-secao-telas.html'] = secao(
    'secao-telas', 'Parte 3', u'O sistema,<br>tela a tela',
    u'As telas a seguir são o módulo real, '
    u'com a marca, as unidades e o vocabulário de Niterói. A cor vem do cadastro da instituição, '
    u'não do código: é assim que o painel fica laranja sem uma linha nova.')

if __name__ == '__main__':
    for nome, conteudo in A.items():
        io.open(os.path.join(AQUI, 'slides', nome), 'w', encoding='utf-8').write(conteudo)
        print('escrito: %s (%d)' % (nome, len(conteudo)))
