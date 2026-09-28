# -*- coding: utf-8 -*-
"""Gera os slides narrativos (os de tela são montados pelo montar.py a partir de telas/)."""
import io, os

AQUI = os.path.dirname(os.path.abspath(__file__))
FITA = '  <div class="fita-marca"><i></i><i></i><i></i><i></i><i></i></div>\n'
MARCA = '    <div class="marca-canto"><img src="assets/niteroi-saude.png" alt=""></div>\n'
RODAPE = ('  <div class="rodape">\n'
          '    <span class="esq"><span class="ponto"></span> Ouvidoria da Saude &middot; '
          'Fundacao Municipal de Saude de Niteroi</span>\n'
          '    <span class="num">{{N}}</span>\n  </div>\n</section>\n')
RODAPE = RODAPE.replace('Saude', 'Saúde').replace('Fundacao', 'Fundação').replace('Niteroi', 'Niterói')


def secao(id_, numero, h2, sub):
    return (u'<section class="slide secao" id="%s">\n'
            u'  <img class="brasao-fantasma sangria" src="assets/niteroi-brasao-offwhite.png" alt="">\n'
            u'  <div class="conteudo">\n'
            u'    <p class="numero">%s</p>\n'
            u'    <h2>%s</h2>\n'
            u'    <p class="sub">%s</p>\n'
            u'    <div class="regua"></div>\n'
            u'  </div>\n' % (id_, numero, h2, sub)) + RODAPE


def cab(olho, titulo, linha=None):
    s = u'  <div class="cabeca">\n    <div>\n'
    s += u'      <p class="olho">%s</p>\n      <h1 class="titulo">%s</h1>\n' % (olho, titulo)
    if linha:
        s += u'      <p class="linha-fina">%s</p>\n' % linha
    s += u'    </div>\n' + MARCA + u'  </div>\n'
    return s


def slide(id_, cabecalho, corpo, classe_corpo='corpo'):
    return (u'<section class="slide" id="%s">\n%s%s  <div class="%s">\n%s\n  </div>\n%s'
            % (id_, FITA, cabecalho, classe_corpo, corpo, RODAPE))


A = {}

# ============================ PARTE 1 — A LEI ============================
A['03-secao-lei.html'] = secao(
    'secao-lei', 'Parte 1', u'O que a lei<br>já exige de Niterói',
    u'A Lei 13.460/2017 está em vigor para Niterói desde dezembro de 2018 (art. 25, II &mdash; municípios entre cem e quinhentos mil habitantes). Ela não pede um canal &mdash; pede '
    u'protocolo, prazo, trilha, relatório e pesquisa de satisfação. Cada obrigação '
    u'virou um comportamento do sistema.')

LINHAS_LEI = [
    (u'Nunca recusar; múltiplos canais; verbal reduzida a termo',
     u'L 13.460 arts. 10 §4º e 11',
     u'Quatro campos bastam para registrar &mdash; tipo, identificação, teor e canal. O cadastro de canal tem catorze opções.'),
    (u'CPF basta para identificar; vedado campo &ldquo;motivo&rdquo;',
     u'L 13.460 arts. 10 §2º e 10-A',
     u'Nome + CPF <i>ou</i> telefone. Não existe campo &ldquo;motivo&rdquo; em formulário nenhum do módulo.'),
    (u'Comprovante de recebimento e ciência da decisão',
     u'L 13.460 art. 12, II e V',
     u'Protocolo <span class="mono">AAAA-NNNNNN</span> + código de acesso de 8 caracteres (só o hash é guardado).'),
    (u'30 dias, prorrogáveis uma vez, com justificativa',
     u'L 13.460 art. 16, caput',
     u'Prazo calculado no registro; a segunda prorrogação é recusada. A justificativa vai ao cidadão.'),
    (u'Complementação suspende o prazo uma única vez',
     u'Dec. 9.492/2018; PN CGU 116 art. 25',
     u'Estado próprio, trava de uso único, contador de dias suspensos e arquivamento automático em 20 dias.'),
    (u'Resposta conclusiva com conteúdo mínimo por tipo',
     u'PN CGU 116/2024 arts. 28–30',
     u'A tela exibe o texto exigido para aquele tipo e só conclui com resolutividade e situação final coerentes.'),
    (u'Arquivamento só por motivo tipificado',
     u'PN CGU 116/2024 art. 31',
     u'Lista fechada de nove motivos; duplicidade exige citar o protocolo original.'),
    (u'Identificação do manifestante tem acesso restrito',
     u'L 13.460 art. 10 §7º; LAI art. 31',
     u'Três níveis (identificada, sigilosa, anônima). O ponto de resposta nunca recebe dados do manifestante.'),
    (u'Proteção do denunciante e log de acesso à identidade',
     u'Dec. 10.153/2019; Lei 13.608',
     u'Teor pseudonimizado obrigatório para encaminhar; cada revelação grava usuário, data, IP e justificativa.'),
    (u'Relatório de gestão anual publicado na íntegra',
     u'L 13.460 arts. 14 e 15',
     u'Volume, motivos, recorrentes e providências saem do painel, por período, unidade, tipo e assunto.'),
    (u'Pesquisa de satisfação ao menos anual, com ranking',
     u'L 13.460 art. 23 §§1º e 2º',
     u'Pesquisa pós-resposta e ranking público de reclamações, no módulo de Pesquisas já existente.'),
    (u'Dado de saúde é sensível; acesso por necessidade',
     u'LGPD arts. 11, 23 e 46',
     u'Quatro permissões separadas, <i>fail-closed</i>; retenção prevista em 5+5 anos (denúncia, 5+15).'),
]
tr = u'\n'.join(u'        <tr><td>%s</td><td class="norma">%s</td><td>%s</td></tr>' % l for l in LINHAS_LEI)
A['04-lei-tabela.html'] = slide(
    'lei-tabela',
    cab(u'Conformidade', u'Doze obrigações legais, <em>doze comportamentos do sistema</em>'),
    u'    <table class="tabela-doc">\n'
    u'      <thead><tr><th style="width:30%">O que a norma obriga</th><th style="width:19%">Norma</th>'
    u'<th>Como o módulo cumpre</th></tr></thead>\n      <tbody>\n' + tr + u'\n      </tbody>\n    </table>',
    'corpo alto')

TIPOS = [
    (u'Solicitação', u'Atender', 'p-solicitacao',
     u'Contém um <b>requerimento</b>: vaga, exame, medicamento, transporte. Nunca anônima nem sigilosa.', u'53,1%'),
    (u'Reclamação', u'Apurar', 'p-reclamacao',
     u'Insatisfação com o serviço ou com a conduta, <b>sem</b> requerimento. Admite sigilo.', u'29,8%'),
    (u'Elogio', u'Conhecer', 'p-elogio',
     u'Satisfação com o serviço ou o profissional. A resposta informa que foi ao agente e à chefia.', u'6,8%'),
    (u'Denúncia', u'Apurar', 'p-denuncia',
     u'Irregularidade que depende de órgão apuratório. <b>Único tipo que admite anonimato.</b>', u'5,8%'),
    (u'Informação', u'Atender', 'p-informacao',
     u'Pergunta sobre serviço, horário, fluxo. Não confundir com pedido de LAI, que é outro prazo.', u'2,2%'),
    (u'Sugestão', u'Conhecer', 'p-sugestao',
     u'Proposta de melhoria. A resposta traz a posição do gestor e, se acatada, o prazo estimado.', u'0,4%'),
]
cards = u'\n'.join(
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <div class="fl ai-c jc-b" style="margin-bottom:7px">\n'
    u'          <span class="pill %s" style="font-size:12.5px;padding:3px 10px">%s</span>\n'
    u'          <span class="tag cinza">%s</span>\n        </div>\n'
    u'        <p class="b-texto" style="font-size:12.5px">%s</p>\n'
    u'        <p style="margin:9px 0 0;font-family:var(--fonte-display);font-size:26px;font-weight:700;'
    u'color:var(--nit-laranja);line-height:1">%s <span style="font-family:var(--fonte-ui);font-size:10.5px;'
    u'font-weight:500;color:var(--s500)">das municipais</span></p>\n      </div>'
    % (cls, nome, prov, desc, pct) for nome, prov, cls, desc, pct in TIPOS)

A['05-tipologia.html'] = slide(
    'tipologia',
    cab(u'Tipologia', u'Seis tipos oficiais &mdash; e a <em>providência</em> que cada um obriga',
        u'A tipologia é fechada e vem do Manual das Ouvidorias do SUS. Ela não é rótulo: define quem pode '
        u'ficar sem se identificar, que resposta a lei exige e como a manifestação é encerrada.'),
    u'    <div class="grade g3" style="height:auto">\n' + cards + u'\n    </div>\n'
    u'    <div class="fl g16 ai-s" style="margin-top:16px">\n'
    u'      <div class="bloco realce f1" style="padding:13px 16px">\n'
    u'        <p class="b-titulo" style="font-size:15px;margin-bottom:5px">Mais um: comunicação de irregularidade</p>\n'
    u'        <p class="b-texto" style="font-size:12.5px">Denúncia obrigatoriamente anônima, sem acompanhamento e sem '
    u'resposta &mdash; não há a quem devolver. No banco não é um sétimo tipo: é a denúncia '
    u'registrada como anônima, que o sistema conclui no ato.</p>\n      </div>\n'
    u'      <div class="bloco escuro f1" style="padding:13px 16px">\n'
    u'        <p class="b-titulo" style="font-size:15px;margin-bottom:5px">Por que isso decide o projeto</p>\n'
    u'        <p class="b-texto" style="font-size:12.5px">Metade do que chega a uma ouvidoria municipal de saúde é '
    u'<b>pedido de acesso</b>. Ela é, em grande parte, a porta de queixa da <b>Regulação</b> e da '
    u'<b>Farmácia</b> &mdash; e por isso precisa enxergar a fila, não só registrar o desabafo.</p>\n      </div>\n'
    u'    </div>\n'
    u'    <p style="margin:11px 0 0;font-size:11px;color:var(--s500)">Percentuais: Relatório Anual de Gestão da '
    u'Ouvidoria-Geral do SUS (OuvSUS/MS), 2024 &mdash; 436.004 manifestações sobre serviços do SUS sob <b>gestão municipal</b>, de 618.727 no total do SUS no ano.</p>')

if __name__ == '__main__':
    for nome, conteudo in A.items():
        io.open(os.path.join(AQUI, 'slides', nome), 'w', encoding='utf-8').write(conteudo)
        print('escrito: %s (%d)' % (nome, len(conteudo)))
