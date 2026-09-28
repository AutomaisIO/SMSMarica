# -*- coding: utf-8 -*-
"""Gramáticas visuais do anexo: helpers de composição (cabeçalho, faixa, convergência, trilho).

Cada slide é composto à mão com a gramática que o conteúdo pede — trilho,
convergência, barras, série, decisão, mini-tela, boneco — em vez de um template.
"""
import io, os

AQUI = os.path.dirname(os.path.abspath(__file__))
SETA_D = ('<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" '
          'stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14"/><path d="m13 6 6 6-6 6"/></svg>')
SETA_B = ('<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" '
          'stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="m6 13 6 6 6-6"/></svg>')
RODAPE = (u'  <div class="rodape">\n    <span class="esq"><span class="ponto"></span> SMSMais &middot; '
          u'Fundação Municipal de Saúde de Niterói</span>\n    <span class="num">{{N}}</span>\n  </div>\n')


def cab(olho, titulo, destaque, linha=None):
    h = u'  <div class="fita-marca"><i></i><i></i><i></i><i></i><i></i></div>\n  <div class="cabeca">\n    <div>\n'
    h += u'      <p class="olho">%s</p>\n      <h1 class="titulo">%s <em>%s</em></h1>\n' % (olho, titulo, destaque)
    if linha:
        h += u'      <p class="linha-fina">%s</p>\n' % linha
    h += u'    </div>\n    <div class="marca-canto"><img src="assets/niteroi-saude.png" alt=""></div>\n  </div>\n'
    return h


def slide(ident, corpo_cls, cabecalho, miolo):
    return (u'<section class="slide" id="%s">\n%s\n  <div class="%s">\n%s  </div>\n\n%s</section>\n'
            % (ident, cabecalho, corpo_cls, miolo, RODAPE))


def faixa(texto, olho=None, claro=False):
    cls = u'bloco' if claro else u'bloco escuro'
    h = u'    <div class="%s" style="margin-top:16px;flex:none">\n' % cls
    if olho:
        cor = u'' if claro else u' style="color:var(--nit-amarelo)"'
        h += u'      <p class="b-olho"%s>%s</p>\n' % (cor, olho)
    h += u'      <p class="b-texto" style="margin:0">%s</p>\n    </div>\n' % texto
    return h


def converge(fontes, alvo_olho, alvo_tit, alvo_txt):
    h = u'    <div class="converge">\n      <div class="fontes">\n'
    for b, s in fontes:
        h += u'        <div class="fonte"><span class="pt"></span><b>%s</b><span>%s</span></div>\n' % (b, s)
    n = len(fontes)
    # feixe: cada fonte manda uma curva para o centro-direita
    # viewBox fixo e esticado: a curva i sai da fração (i+½)/n da altura, que é onde a caixa i
    # está quando as fontes se espalham por space-around; vector-effect mantém a espessura.
    h += u'      </div>\n      <div class="funil"><svg viewBox="0 0 70 100" preserveAspectRatio="none" fill="none" stroke="#f6b687" stroke-width="2">\n'
    for i in range(n):
        y = (i + 0.5) * 100.0 / n
        h += u'        <path d="M 0 %.1f C 35 %.1f, 35 50, 70 50" vector-effect="non-scaling-stroke"/>\n' % (y, y)
    h += u'      </svg></div>\n'
    h += (u'      <div class="alvo"><p class="a-olho">%s</p><p class="a-tit">%s</p><p class="a-txt">%s</p></div>\n'
          u'    </div>\n' % (alvo_olho, alvo_tit, alvo_txt))
    return h


def trilho(nos, retorno=None):
    """nos: lista de (classe, boneco|None, titulo, texto)."""
    h = u'    <div class="trilho-wrap">\n      <div class="trilho-nos">\n'
    for i, (cls, bon, t, d) in enumerate(nos, 1):
        vis = u'' if bon else u' style="visibility:hidden"'
        img = bon or 'recepcionista'
        h += (u'        <div class="no %s"><div class="quem"%s><img src="assets/bonecos/%s.jpg" alt=""></div>'
              u'<div class="disco">%d</div><p class="t">%s</p><p class="d">%s</p></div>\n' % (cls, vis, img, i, t, d))
    h += u'      </div>\n'
    if retorno:
        h += (u'      <div class="retorno baixo"><svg viewBox="0 0 600 50" preserveAspectRatio="none" fill="none">'
              u'<path d="M 600 2 C 600 46, 0 46, 0 8" stroke="#d97706" stroke-width="2.5" stroke-dasharray="6 5"/>'
              u'<path d="M -9 18 L 0 4 L 9 18" stroke="#d97706" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>'
              u'<span class="rot-ret">%s</span></div>\n' % retorno)
    h += u'    </div>\n'
    return h


def grava(nome, html):
    with io.open(os.path.join(AQUI, 'slides', nome), 'w', encoding='utf-8') as f:
        f.write(html)
    print(nome)


