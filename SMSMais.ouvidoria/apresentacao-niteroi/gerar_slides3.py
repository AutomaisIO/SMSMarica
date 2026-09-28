# -*- coding: utf-8 -*-
"""Parte 3: por que este módulo, indicadores, instância própria, implantação e fecho."""
import io, os
from gerar_slides import FITA, MARCA, RODAPE, secao, cab, slide

AQUI = os.path.dirname(os.path.abspath(__file__))
A = {}

# ------------------------------------------------------- 20 seção: por que este
A['20-secao-porque.html'] = secao(
    'secao-porque', 'Parte 4', u'Por que um módulo<br>da saúde, e não<br>uma ouvidoria genérica',
    u'Um sistema de chamados registra a queixa. Este registra a queixa <b>ligada à unidade pelo CNES, '
    u'ao paciente do hub FHIR e ao pedido de regulação que a originou</b> &mdash; que é de onde '
    u'vem metade do que chega.')

# ------------------------------------------------------- 21 diferenciais
DIF = [
    (u'Enxerga a Regulação',
     u'A manifestação aponta para o <b>pedido de regulação real</b> que a originou, e a '
     u'Central de Regulação é um ponto de resposta como qualquer outro. Metade das queixas de '
     u'saúde é sobre a fila &mdash; sem esse vínculo, a ouvidoria responde &ldquo;encaminhamos '
     u'ao setor&rdquo; e nada mais.'),
    (u'Taxonomia da saúde, não da prefeitura',
     u'Árvore assunto &rsaquo; subassunto com <b>código do OuvidorSUS</b> em cada nó, já '
     u'semeada com os 22 assuntos do Manual do Ministério da Saúde e editável pelo ouvidor. '
     u'Compatibilidade preparada para o dia em que o município exportar.'),
    (u'Ponto de resposta é entidade de primeira classe',
     u'Unidade, área central ou apuração, com titular, membros, login individual e prazo '
     u'próprio. Foi isso que levou a resolutividade da SMS-SP de <b>50,9% a 76,6%</b> &mdash; é o '
     u'ponto de resposta que move o indicador, não o formulário.'),
    (u'Paciente e profissional vindos do hub FHIR',
     u'O CPF resolve o cidadão no hub; a unidade é a do CNES; o profissional citado é o '
     u'<i>Practitioner</i> &mdash; e a manifestação nunca entra no prontuário por causa disso. O cidadão não '
     u'repete o que a secretaria já sabe.'),
    (u'WhatsApp oficial, o mesmo que já confirma consulta',
     u'O aviso de cada etapa sai pelo número oficial da secretaria, com a regra de destinatário '
     u'correto. Em manifestação sigilosa a mensagem leva <b>só protocolo e etapa</b> &mdash; '
     u'nunca o teor. Em anônima, não sai.'),
    (u'Alimenta contrato de gestão',
     u'&ldquo;Resolubilidade de Ouvidorias&rdquo; deixa de ser planilha do prestador e passa a ser apurada '
     u'na própria base, por unidade e por período &mdash; do mesmo jeito que os demais indicadores '
     u'contratuais do SMSMais.'),
]
dif = u'\n'.join(
    u'      <div class="bloco">\n'
    u'        <p class="b-titulo">%s</p>\n        <p class="b-texto">%s</p>\n      </div>'
    % (t, d) for t, d in DIF)

A['21-diferenciais.html'] = slide(
    'diferenciais',
    cab(u'Diferencial', u'Seis coisas que um sistema genérico <em>não tem como fazer</em>'),
    u'    <div class="grade g3 cresce">\n' + dif + u'\n    </div>',
    'corpo alto')

# ------------------------------------------------------- 22 indicadores
IND = [
    (u'Resolubilidade', u'resolvidas &times; 100 / recebidas no período', u'&ge; 90%', u'meta do indicador contratual'),
    (u'Atendimento ao prazo', u'encerradas no prazo legal &times; 100 / encerradas', u'&gt; 90%', u'meta oficial do Rio'),
    (u'Tempo médio de resposta', u'média(resposta &minus; registro), sem os dias suspensos', u'&lt; 10 dias', u'Rio realizou 12 dias em 2025'),
    (u'Estoque', u'em tratamento no fim do período &times; 100 / recebidas em 30 dias', u'&le; 10%', u'o indicador que revela a fila'),
    (u'Faixa de prazo', u'% até 30 dias / 31 a 60 / acima de 60', u'&ge; 88% até 30 d', u'BH realizou 88,6% em 2024'),
    (u'Tempo da área', u'média(resposta da área &minus; encaminhamento), por ponto', u'&le; 20 dias', u'prioridade Normal'),
    (u'Retrabalho da área', u'devoluções para reanálise &times; 100 / respostas recebidas', u'&mdash;', u'qualidade da resposta, não volume'),
    (u'Satisfação com a ouvidoria', u'(muito satisfeito + satisfeito) &times; 100 / respondentes', u'&ge; 90%', u'meta contratual de OS no ES'),
]
ind = u'\n'.join(
    u'        <tr><td>%s</td><td style="font-size:11.5px">%s</td>'
    u'<td class="ok nowrap">%s</td><td class="norma">%s</td></tr>' % l for l in IND)

A['22-indicadores.html'] = slide(
    'indicadores',
    cab(u'Prestação de contas', u'O painel não é enfeite: é o <em>relatório anual</em> começando a ser escrito',
        u'Os arts. 14, 15 e 23 da Lei 13.460 exigem relatório de gestão publicado na íntegra e '
        u'pesquisa de satisfação ao menos anual, com ranking de reclamações. Tudo abaixo sai '
        u'da própria base, por período e por unidade.'),
    u'    <div class="grade g-3-2 cresce" style="gap:18px">\n'
    u'      <div class="bloco" style="padding:16px 18px">\n'
    u'        <table class="tabela-doc">\n'
    u'          <thead><tr><th style="width:26%">Indicador</th><th style="width:40%">Fórmula</th>'
    u'<th style="width:14%">Meta</th><th>Referência</th></tr></thead>\n'
    u'          <tbody>\n' + ind + u'\n          </tbody>\n        </table>\n      </div>\n'
    u'      <div style="display:flex;flex-direction:column;gap:14px">\n'
    u'        <div class="bloco realce" style="padding:14px 16px">\n'
    u'          <p class="b-titulo" style="font-size:15px">Para quem o relatório vai</p>\n'
    u'          <ul class="lista" style="margin-top:4px">\n'
    u'            <li style="font-size:12.5px">Mensal ao gestor da FMS</li>\n'
    u'            <li style="font-size:12.5px"><b>Trimestral por unidade</b>, com campo de considerações do gestor</li>\n'
    u'            <li style="font-size:12.5px">Anual publicado, nos itens do art. 15</li>\n'
    u'            <li style="font-size:12.5px">Ao <b>Conselho Municipal de Saúde</b> (Lei 8.142)</li>\n'
    u'          </ul>\n        </div>\n'
    u'        <div class="bloco escuro" style="padding:14px 16px">\n'
    u'          <p class="b-titulo" style="font-size:15px">O que hoje ninguém publica</p>\n'
    u'          <p class="b-texto" style="font-size:12.5px">Tempo médio de resposta, percentual dentro do '
    u'prazo legal, volume por canal, satisfação e resolutividade <b>por unidade</b>. São esses '
    u'números que transformam a ouvidoria de caixa de reclamação em instrumento de gestão '
    u'&mdash; e é o que a Portaria de Consolidação 1/2017 manda o gestor usar.</p>\n'
    u'        </div>\n      </div>\n    </div>')

# ------------------------------------------------------- 23 instância
A['23-instancia.html'] = slide(
    'instancia',
    cab(u'Entrega', u'Uma instância <em>de Niterói</em>, não uma conta num sistema compartilhado',
        u'O SMSMais é entregue como uma instalação por município: servidor, banco e '
        u'domínio próprios. O isolamento do dado é físico, não lógico &mdash; '
        u'não existe um campo &ldquo;município&rdquo; separando Niterói de outra prefeitura.'),
    u'    <div class="grade g3" style="gap:16px">\n'
    u'      <div class="bloco">\n'
    u'        <div class="icone-quadro">\n'
    u'          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>\n'
    u'        </div>\n'
    u'        <p class="b-titulo">Isolamento físico</p>\n'
    u'        <p class="b-texto">Banco próprio, servidor próprio, backup próprio. Um incidente em '
    u'outra prefeitura não alcança o dado de Niterói, e a LGPD fica muito mais simples de '
    u'sustentar diante do encarregado.</p>\n      </div>\n'
    u'      <div class="bloco">\n'
    u'        <div class="icone-quadro">\n'
    u'          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="13.5" cy="6.5" r=".5" fill="currentColor"/><circle cx="17.5" cy="10.5" r=".5" fill="currentColor"/><circle cx="8.5" cy="7.5" r=".5" fill="currentColor"/><circle cx="6.5" cy="12.5" r=".5" fill="currentColor"/><path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z"/></svg>\n'
    u'        </div>\n'
    u'        <p class="b-titulo">A marca vem do cadastro</p>\n'
    u'        <p class="b-texto">Nome da fundação, logo, cor, domínio, número de WhatsApp, '
    u'encarregado de dados e contatos legais ficam num <b>cadastro institucional</b> lido pela API. As telas '
    u'que você acabou de ver ficaram laranja <b>sem uma linha de código nova</b>.</p>\n      </div>\n'
    u'      <div class="bloco">\n'
    u'        <div class="icone-quadro">\n'
    u'          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Z"/><path d="M6 12H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2"/><path d="M18 9h2a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-2"/><path d="M10 6h4"/><path d="M10 10h4"/><path d="M10 14h4"/></svg>\n'
    u'        </div>\n'
    u'        <p class="b-titulo">Escopo por unidade, dentro de casa</p>\n'
    u'        <p class="b-texto">A rede de Niterói tem policlínicas, hospitais, maternidade, SPAs e '
    u'os módulos do Médico de Família. Cada uma é uma unidade com CNES, e o recorte por '
    u'unidade vale dentro do município &mdash; é o que faz o relatório trimestral por unidade sair sozinho.</p>\n'
    u'      </div>\n    </div>\n'
    u'    <div class="grade g2 cresce" style="margin-top:18px">\n'
    u'      <div class="bloco realce">\n'
    u'        <p class="b-olho">O que está entregue e rodando</p>\n'
    u'        <div class="grade g2" style="gap:6px 14px">\n'
    u'          <ul class="lista">\n'
    u'            <li style="font-size:12.5px">Dez tabelas, 36 rotas de API</li>\n'
    u'            <li style="font-size:12.5px">Oito telas de painel</li>\n'
    u'            <li style="font-size:12.5px">Quatro permissões separadas</li>\n'
    u'            <li style="font-size:12.5px">54 testes automatizados</li>\n'
    u'          </ul>\n'
    u'          <ul class="lista">\n'
    u'            <li style="font-size:12.5px">Rotina automática de 6 em 6 horas</li>\n'
    u'            <li style="font-size:12.5px">Endpoints públicos do cidadão</li>\n'
    u'            <li style="font-size:12.5px">Artigo no Manual do usuário</li>\n'
    u'            <li style="font-size:12.5px">Avisos por WhatsApp</li>\n'
    u'          </ul>\n        </div>\n      </div>\n'
    u'      <div class="bloco">\n'
    u'        <p class="b-olho">No roteiro de evolução</p>\n'
    u'        <div class="grade g2" style="gap:6px 14px">\n'
    u'          <ul class="lista">\n'
    u'            <li style="font-size:12.5px">Site público <b>ouvidoria.saude.niteroi.rj.gov.br</b></li>\n'
    u'            <li style="font-size:12.5px">Ouvidoria dentro do app do cidadão</li>\n'
    u'            <li style="font-size:12.5px">Captação pelo WhatsApp: a conversa vira protocolo</li>\n'
    u'            <li style="font-size:12.5px">Pesquisa de satisfação pós-resposta</li>\n'
    u'          </ul>\n'
    u'          <ul class="lista">\n'
    u'            <li style="font-size:12.5px">Relatório trimestral e anual em um clique</li>\n'
    u'            <li style="font-size:12.5px">Tipificação completa do OuvidorSUS 3</li>\n'
    u'            <li style="font-size:12.5px">Exportação para o Fala.BR</li>\n'
    u'            <li style="font-size:12.5px">Sugestão de tipo e assunto por IA, com revisão humana</li>\n'
    u'          </ul>\n        </div>\n      </div>\n    </div>')

# ------------------------------------------------------- 24 implantação
PASSOS = [
    (u'1', u'Semana 1–2', u'Instalação da instância',
     u'Servidor, banco e domínio da FMS. Cadastro institucional com marca, cores, canais e encarregado '
     u'de dados. Importação da rede de unidades pelo CNES.'),
    (u'2', u'Semana 2–3', u'Desenho da rede de resposta',
     u'Quais policlínicas, hospitais e áreas centrais respondem; quem é titular de cada ponto; '
     u'qual o prazo interno de cada um. É a decisão que mais pesa no resultado.'),
    (u'3', u'Semana 3–4', u'Taxonomia e prazos',
     u'Ajuste dos 22 assuntos à realidade de Niterói, marcadores locais e os sete prazos da '
     u'configuração. Nada disso exige desenvolvimento.'),
    (u'4', u'Semana 4–5', u'Portaria e treinamento',
     u'Minuta de portaria da FMS instituindo o sistema como registro único da ouvidoria da saúde, '
     u'nomeando pontos de resposta e prazos. Treinamento da ouvidoria e dos titulares.'),
    (u'5', u'Semana 5–6', u'Entrada em operação',
     u'Começa pelo registro interno &mdash; o balcão, o WhatsApp e o e-mail da ouvidoria passam a '
     u'virar protocolo. O canal público ao cidadão entra depois, quando a rede já responde no prazo.'),
]
passos = u'\n'.join(
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <div class="fl ai-c g10" style="margin-bottom:8px">\n'
    u'          <span style="width:26px;height:26px;border-radius:50%%;background:var(--nit-laranja);color:#fff;'
    u'font-weight:700;font-size:13px;display:flex;align-items:center;justify-content:center;flex:none">%s</span>\n'
    u'          <span class="tag cinza">%s</span>\n        </div>\n'
    u'        <p class="b-titulo" style="font-size:14.5px">%s</p>\n'
    u'        <p class="b-texto" style="font-size:12px">%s</p>\n      </div>'
    % (n, sem, tit, txt) for n, sem, tit, txt in PASSOS)

A['24-implantacao.html'] = slide(
    'implantacao',
    cab(u'Implantação', u'Seis semanas &mdash; e só uma delas <em>depende de tecnologia</em>',
        u'O software está pronto e rodando. O que a implantação exige é '
        u'decisão institucional: quem responde pelo quê, em quanto tempo, e o ato normativo que '
        u'sustenta isso.'),
    u'    <div class="grade g5 cresce" style="gap:12px">\n' + passos + u'\n    </div>\n'
    u'    <div class="grade g-2-1" style="margin-top:18px">\n'
    u'      <div class="bloco escuro">\n'
    u'        <p class="b-olho" style="color:var(--nit-amarelo)">O que precisa ser decidido &mdash; não programado</p>\n'
    u'        <div class="grade g2" style="gap:6px 14px">\n'
    u'          <ul class="lista">\n'
    u'            <li style="color:rgba(255,255,255,.82);font-size:12.5px">Quem é o <b style="color:#fff">ouvidor da saúde</b> e sua equipe</li>\n'
    u'            <li style="color:rgba(255,255,255,.82);font-size:12.5px">Titular e suplente de <b style="color:#fff">cada ponto de resposta</b></li>\n'
    u'            <li style="color:rgba(255,255,255,.82);font-size:12.5px">Prazo interno da área (a lei dá 30 ao cidadão; a casa se dá menos)</li>\n'
    u'          </ul>\n'
    u'          <ul class="lista">\n'
    u'            <li style="color:rgba(255,255,255,.82);font-size:12.5px">Qual unidade apuratória recebe <b style="color:#fff">denúncia</b></li>\n'
    u'            <li style="color:rgba(255,255,255,.82);font-size:12.5px">Se o <b style="color:#fff">WhatsApp da ouvidoria</b> entra no roteador oficial</li>\n'
    u'            <li style="color:rgba(255,255,255,.82);font-size:12.5px">A <b style="color:#fff">portaria</b> &mdash; que o Fala.BR e o OuvidorSUS também exigiriam</li>\n'
    u'          </ul>\n        </div>\n      </div>\n'
    u'      <div class="bloco realce">\n'
    u'        <p class="b-olho">Uma ressalva honesta</p>\n'
    u'        <p class="b-texto" style="font-size:12.5px">Este material foi montado a partir do que a FMS '
    u'publica e do que a norma exige. <b>Não houve levantamento de campo em Niterói</b>: volume '
    u'anual, base normativa municipal e fluxo interno atual precisam ser levantados com a equipe antes de '
    u'qualquer proposta formal. Os números das telas são simulação coerente, não dado real.</p>\n'
    u'      </div>\n    </div>')

# ------------------------------------------------------- 25 encerramento
# O slide de fecho e um arquivo estatico: slides/25-fecho.html

if __name__ == '__main__':
    for nome, conteudo in A.items():
        io.open(os.path.join(AQUI, 'slides', nome), 'w', encoding='utf-8').write(conteudo)
        print('escrito: %s (%d)' % (nome, len(conteudo)))
