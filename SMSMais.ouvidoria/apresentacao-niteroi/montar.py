# -*- coding: utf-8 -*-
"""Monta a apresentação: injeta o shell do painel em cada tela e os slides no deck.

  telas/conteudo/<nome>.html  -> conteúdo do <div class="app-wrap">
  telas/<nome>.meta           -> (opcional) ativo=Fila / usuario=Paulo Rezende|...|PR / unidade=...
  slides/*.html               -> slides na ordem do arquivo ORDEM.txt
"""
import io, os, re, sys, json

AQUI = os.path.dirname(os.path.abspath(__file__))

def ler(p):
    with io.open(p, encoding='utf-8') as f:
        return f.read()

SHELL = ler(os.path.join(AQUI, 'telas', '_shell.html'))
SHELL = SHELL.split('<div class="app">', 1)[1]
SHELL = '<div class="app">' + SHELL


def montar_tela(nome):
    """Devolve o <div class="app"> completo da tela <nome>."""
    corpo = ler(os.path.join(AQUI, 'telas', 'conteudo', nome + '.html'))
    meta = {}
    mp = os.path.join(AQUI, 'telas', 'conteudo', nome + '.meta')
    if os.path.exists(mp):
        for linha in ler(mp).splitlines():
            if '=' in linha and not linha.strip().startswith('#'):
                k, v = linha.split('=', 1)
                meta[k.strip()] = v.strip()

    html = SHELL

    # item ativo da sidebar
    ativo = meta.get('ativo', 'Fila')
    html = html.replace('<a class="item ativo">', '<a class="item">')
    # marca ativo no item cujo <span> bate com o rótulo
    padrao = re.compile(r'(<a class="item">)((?:(?!</a>).)*?<span>' + re.escape(ativo) + r'</span>)', re.S)
    novo, n = padrao.subn(r'<a class="item ativo">\2', html, count=1)
    if n == 0:
        raise SystemExit('tela %s: item de menu "%s" nao encontrado' % (nome, ativo))
    html = novo

    # usuário do topo e da lateral
    if meta.get('usuario'):
        nome_u, email_u, iniciais = [x.strip() for x in meta['usuario'].split('|')]
        html = html.replace('>Rita Menezes<', '>' + nome_u + '<')
        html = html.replace('rita.menezes@saude.niteroi.rj.gov.br', email_u)
        html = html.replace('class="av">RM<', 'class="av">' + iniciais + '<')
    if meta.get('unidade'):
        html = html.replace('<span>Todas as unidades</span>', '<span>' + meta['unidade'] + '</span>')
    if meta.get('sem_contadores') == 'sim':
        html = re.sub(r'<span class="cont">\d+</span>', '', html)

    # 'apenas=Rotulo|Rotulo' — o menu mostra so esses itens da secao Ouvidoria.
    # E o que o usuario com uma permissao so realmente ve: a sidebar e montada
    # a partir das permissoes resolvidas no login.
    if meta.get('apenas'):
        mantidos = [x.strip() for x in meta['apenas'].split('|')]
        def filtra(m):
            bloco = m.group(0)
            rot = re.search(r'<span>([^<]+)</span>', bloco)
            return bloco if (rot and rot.group(1) in mantidos) else ''
        html = re.sub(r'<a class="item(?: ativo)?">.*?</a>\s*', filtra, html, flags=re.S)

    return html.replace('<!-- CONTEÚDO -->', corpo)


def main():
    ordem = [l.strip() for l in ler(os.path.join(AQUI, 'ORDEM.txt')).splitlines()
             if l.strip() and not l.strip().startswith('#')]
    partes = []
    n_slide = 0
    total = len(ordem)
    for arq in ordem:
        s = ler(os.path.join(AQUI, 'slides', arq))
        n_slide += 1
        # {{TELA:nome}} -> painel montado
        for m in set(re.findall(r'\{\{TELA:([a-z0-9_-]+)\}\}', s)):
            s = s.replace('{{TELA:%s}}' % m, montar_tela(m))
        s = s.replace('{{N}}', str(n_slide)).replace('{{TOTAL}}', str(total))
        partes.append(s)

    doc = ler(os.path.join(AQUI, 'base.html'))
    doc = doc.replace('{{SLIDES}}', '\n'.join(partes))
    with io.open(os.path.join(AQUI, 'apresentacao.html'), 'w', encoding='utf-8') as f:
        f.write(doc)
    print('%d slides montados -> apresentacao.html' % n_slide)


if __name__ == '__main__':
    main()
