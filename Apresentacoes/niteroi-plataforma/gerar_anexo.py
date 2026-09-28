# -*- coding: utf-8 -*-
"""Gera os slides de aprofundamento do DECK B (anexo).

Mesmo tom do deck A: o que o módulo FAZ, em linguagem de serviço. Sem jargão de
arquitetura — a régua está em ROTEIRO.md §"O tom".

Cada slide é escrito como dado (título, cards, faixa) e sai no HTML padrão do deck,
para que os 40 fiquem consistentes entre si e uma correção de estilo valha para todos.
"""
import io, os

AQUI = os.path.dirname(os.path.abspath(__file__))
RODAPE = (u'  <div class="rodape">\n'
          u'    <span class="esq"><span class="ponto"></span> SMSMais &middot; '
          u'Fundação Municipal de Saúde de Niterói</span>\n'
          u'    <span class="num">{{N}}</span>\n'
          u'  </div>\n')


def bloco(b):
    """Um cartão. Chaves: titulo, texto | olho, num, realce, escuro, lista."""
    cls = u'bloco'
    if b.get('realce'):
        cls += u' realce'
    if b.get('escuro'):
        cls += u' escuro'
    h = u'      <div class="%s">\n' % cls
    if b.get('num'):
        h += u'        <p class="b-num">%s</p>\n' % b['num']
    if b.get('olho'):
        cor = u' style="color:var(--nit-amarelo)"' if b.get('escuro') else u''
        h += u'        <p class="b-olho"%s>%s</p>\n' % (cor, b['olho'])
    if b.get('titulo'):
        h += u'        <p class="b-titulo">%s</p>\n' % b['titulo']
    for t in ([b['texto']] if isinstance(b.get('texto'), str) else b.get('texto', [])):
        h += u'        <p class="b-texto">%s</p>\n' % t
    if b.get('lista'):
        cor = u' style="color:rgba(255,255,255,.82)"' if b.get('escuro') else u''
        h += u'        <ul class="lista"%s>\n' % (u' style="margin-top:6px"')
        for li in b['lista']:
            h += u'          <li%s>%s</li>\n' % (cor, li)
        h += u'        </ul>\n'
    h += u'      </div>\n'
    return h


def slide(ident, olho, titulo, destaque, linha, blocos, grade=u'g3', faixa=None):
    """Monta um slide de conteúdo. `destaque` é o trecho em laranja do título."""
    # Uma linha de cartões fica melhor centralizada; duas linhas devem preencher
    # a altura, senão sobra branco em cima e embaixo. Com faixa no pé, sempre estica.
    colunas = int(grade[1]) if grade[1:2].isdigit() else 3
    duas_linhas = len(blocos) > colunas
    estica = bool(faixa) or duas_linhas
    corpo_cls = u'corpo baixo' if linha else u'corpo'
    if not estica:
        corpo_cls += u' centro'
    h = u'<section class="slide" id="%s">\n' % ident
    h += u'  <div class="fita-marca"><i></i><i></i><i></i><i></i><i></i></div>\n'
    h += u'  <div class="cabeca">\n    <div>\n'
    h += u'      <p class="olho">%s</p>\n' % olho
    h += u'      <h1 class="titulo">%s <em>%s</em></h1>\n' % (titulo, destaque)
    if linha:
        h += u'      <p class="linha-fina">%s</p>\n' % linha
    h += u'    </div>\n'
    h += u'    <div class="marca-canto"><img src="assets/niteroi-saude.png" alt=""></div>\n'
    h += u'  </div>\n\n'
    h += u'  <div class="%s">\n' % corpo_cls
    h += u'    <div class="grade %s%s">\n' % (grade, u' cresce' if estica else u'')
    for b in blocos:
        h += bloco(b)
    h += u'    </div>\n'
    if faixa:
        h += u'\n    <div class="bloco escuro" style="margin-top:14px;flex:none">\n'
        if faixa.get('olho'):
            h += u'      <p class="b-olho" style="color:var(--nit-amarelo)">%s</p>\n' % faixa['olho']
        if faixa.get('titulo'):
            h += u'      <p class="b-titulo">%s</p>\n' % faixa['titulo']
        h += u'      <p class="b-texto">%s</p>\n' % faixa['texto']
        h += u'    </div>\n'
    h += u'  </div>\n\n' + RODAPE + u'</section>\n'
    return h


def secao(ident, numero, titulo, sub, foto=None):
    h = u'<section class="slide secao%s" id="%s">\n' % (u' foto-fundo' if foto else u'', ident)
    if foto:
        h += u'  <div class="fundo"><img src="assets/cenas/%s" alt=""></div>\n' % foto
    else:
        h += u'  <img class="brasao-fantasma sangria" src="assets/niteroi-brasao-offwhite.png" alt="">\n'
    h += u'  <div class="conteudo">\n'
    h += u'    <p class="numero">%s</p>\n' % numero
    h += u'    <h2>%s</h2>\n' % titulo
    h += u'    <p class="sub">%s</p>\n' % sub
    h += u'    <div class="regua"></div>\n  </div>\n'
    h += RODAPE + u'</section>\n'
    return h


def grava(nome, html):
    with io.open(os.path.join(AQUI, 'slides', nome), 'w', encoding='utf-8') as f:
        f.write(html)
    return nome
