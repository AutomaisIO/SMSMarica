# -*- coding: utf-8 -*-
"""Gera os slides que exibem as telas (o 'print' dentro da moldura de navegador)."""
import io, os

AQUI = os.path.dirname(os.path.abspath(__file__))

CADEADO = ('<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" '
           'stroke-linecap="round"><rect x="3" y="11" width="18" height="11" rx="2"/>'
           '<path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>')


def tela_slide(arquivo, tela, olho, titulo, url, legendas, num_inicial=None):
    itens = u'\n'.join(
        u'    <div class="item"><span class="n">%d</span><span class="t">%s</span></div>' % (i + 1, t)
        for i, t in enumerate(legendas))
    return (
        u'<section class="slide tela" id="tela-%s">\n'
        u'  <div class="fita-marca"><i></i><i></i><i></i><i></i><i></i></div>\n'
        u'  <div class="cabeca">\n'
        u'    <div>\n      <p class="olho">%s</p>\n      <h1 class="titulo">%s</h1>\n    </div>\n'
        u'    <div class="marca-canto"><img src="assets/niteroi-saude.png" alt=""></div>\n'
        u'  </div>\n'
        u'  <div class="moldura">\n'
        u'    <div class="barra-nav">\n'
        u'      <div class="bolas"><i></i><i></i><i></i></div>\n'
        u'      <div class="url">%s%s</div>\n'
        u'    </div>\n'
        u'    <div class="palco">{{TELA:%s}}</div>\n'
        u'  </div>\n'
        u'  <div class="legenda-fila">\n%s\n  </div>\n'
        u'  <div class="rodape"><span class="esq"></span><span class="num">{{N}}</span></div>\n'
        u'</section>\n' % (tela, olho, titulo, CADEADO, url, tela, itens))


P = 'smsmais.saude.niteroi.rj.gov.br'

TELAS = [
    dict(arquivo='91-tela-registrar.html', tela='registrar',
         olho=u'A tela &middot; Registro',
         titulo=u'Registrar o que chegou pelo <em>balcão, telefone ou WhatsApp</em>',
         url=P + u'/app/ouvidoria/registrar',
         legendas=[
             u'<b>Quatro campos obrigatórios</b>: tipo, identificação, canal e o relato.',
             u'<b>O relato é o registro</b> &mdash; não existe campo &ldquo;motivo&rdquo;.',
             u'<b>CPF basta</b> para identificar; o cidadão vem do hub, sem redigitar.',
             u'Ao salvar, <b>protocolo e código de acesso</b> saem na hora.']),

    dict(arquivo='92-tela-detalhe.html', tela='detalhe',
         olho=u'A tela &middot; Tramitação',
         titulo=u'Uma manifestação, <em>do começo ao fim</em>, numa tela só',
         url=P + u'/app/ouvidoria/2026-004417',
         legendas=[
             u'<b>Dois prazos visíveis</b>: o do cidadão e o da área, com a cor do risco.',
             u'<b>A barra de ações</b> é calculada: status &times; permissão &times; papel.',
             u'<b>Trilha append-only</b>, marcando o que o cidadão vê e o que é interno.',
             u'<b>Ligada ao pedido de regulação</b> que originou a queixa.']),

    dict(arquivo='93-tela-responder.html', tela='responder',
         olho=u'A tela &middot; Resposta',
         titulo=u'A resposta conclusiva <em>tem conteúdo mínimo por lei</em>',
         url=P + u'/app/ouvidoria/2026-004417',
         legendas=[
             u'<b>Conclusiva ou intermediária</b> &mdash; só a conclusiva encerra o prazo.',
             u'A tela <b>lembra o que a norma exige</b> para aquele tipo (PN CGU 116, art. 29).',
             u'<b>Resolvida ou não</b>: é daqui que sai o indicador de resolubilidade.',
             u'<b>Situação final estruturada</b>, coerente com o tipo da manifestação.']),

    dict(arquivo='94-tela-meu-ponto.html', tela='meu-ponto',
         olho=u'A tela &middot; Quem responde',
         titulo=u'A policlínica vê o caso &mdash; <em>e não vê quem reclamou</em>',
         url=P + u'/app/ouvidoria/meu-ponto',
         legendas=[
             u'<b>Não existe a coluna Manifestante.</b> O dado nem sai da API.',
             u'Cada ponto vê <b>só o que lhe foi encaminhado</b>, com login individual.',
             u'<b>O prazo da área</b> é o relógio que interessa a quem responde.',
             u'Em denúncia, o que chega aqui é o <b>teor pseudonimizado</b>.']),

    dict(arquivo='95-tela-painel.html', tela='painel',
         olho=u'A tela &middot; Estatísticas',
         titulo=u'O painel: <em>volume, prazo, tempo e resolutividade</em> por período',
         url=P + u'/app/ouvidoria/painel',
         legendas=[
             u'<b>Seis números</b> que a Lei 13.460 vai cobrar no relatório anual.',
             u'<b>No prazo e estoque</b> são os indicadores que revelam a fila escondida.',
             u'<b>Assuntos mais frequentes</b>: onde a gestão deve agir, em ordem.',
             u'Filtra por <b>período e por unidade</b> &mdash; é o relatório trimestral.']),

    dict(arquivo='96-tela-pontos.html', tela='pontos-resposta',
         olho=u'A tela &middot; A rede',
         titulo=u'A rede que responde: <em>unidade, área central e apuração</em>',
         url=P + u'/app/ouvidoria/pontos-resposta',
         legendas=[
             u'<b>Três tipos de ponto</b> &mdash; e denúncia só vai para apuração.',
             u'<b>Prazo próprio</b> por ponto: o TFD em 5 dias, o hospital em 15.',
             u'<b>Titular e membros</b>, com inativação quando a pessoa sai.',
             u'<b>Em aberto</b> mostra, na hora, qual área está segurando a fila.']),

    dict(arquivo='97-tela-assuntos.html', tela='assuntos',
         olho=u'A tela &middot; Catálogo',
         titulo=u'A taxonomia é <em>da saúde</em>, não uma lista de assuntos de prefeitura',
         url=P + u'/app/ouvidoria/assuntos',
         legendas=[
             u'<b>Dois níveis</b>: assunto e subassunto, com ordem de exibição.',
             u'<b>Código do OuvidorSUS</b> em cada nó, pronto para exportar.',
             u'<b>22 assuntos já semeados</b> do Manual do Ministério da Saúde.',
             u'<b>Marcadores livres</b> para o que a taxonomia oficial não cobre.']),

    dict(arquivo='98-tela-configuracao.html', tela='configuracao',
         olho=u'A tela &middot; Ajuste',
         titulo=u'Os sete prazos são <em>parâmetro</em>, não código',
         url=P + u'/app/ouvidoria/configuracao',
         legendas=[
             u'<b>Cada campo cita a norma</b> que o sustenta, na própria tela.',
             u'A casa pode <b>se dar prazo menor</b> que o legal &mdash; e quase sempre deve.',
             u'<b>Aviso por WhatsApp</b> liga e desliga aqui, com a trava do sigilo.',
             u'<b>Texto do recibo</b> editável, com protocolo, código e prazo.']),

    dict(arquivo='99-tela-cidadao.html', tela='cidadao',
         olho=u'O lado do cidadão',
         titulo=u'E do outro lado: <em>acompanhar pelo protocolo</em>, sem login',
         url=u'ouvidoria.saude.niteroi.rj.gov.br',
         legendas=[
             u'<b>Protocolo + código de acesso</b> abrem o caso. Sem cadastro, sem senha.',
             u'O cidadão vê <b>só os eventos marcados como visíveis</b>.',
             u'Pode <b>complementar e recorrer</b> pela própria página.',
             u'<b>As rotas públicas já existem</b> &mdash; falta publicar a página.']),
]

if __name__ == '__main__':
    for t in TELAS:
        html = tela_slide(t['arquivo'], t['tela'], t['olho'], t['titulo'], t['url'], t['legendas'])
        io.open(os.path.join(AQUI, 'slides', t['arquivo']), 'w', encoding='utf-8').write(html)
        print('escrito: %s -> {{TELA:%s}}' % (t['arquivo'], t['tela']))
