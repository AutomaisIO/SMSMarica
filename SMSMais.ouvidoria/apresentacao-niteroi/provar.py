# -*- coding: utf-8 -*-
"""provar.py <tela> — monta um slide só com essa tela e verifica transbordo.
Uso: python provar.py painel"""
import io, os, sys, subprocess
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import montar

nome = sys.argv[1]
slide = '''<section class="slide tela" id="prova">
  <div class="fita-marca"><i></i><i></i><i></i><i></i><i></i></div>
  <div class="cabeca"><div><p class="olho">prova</p><h1 class="titulo">%s</h1></div>
  <div class="marca-canto"><img src="assets/niteroi-saude.png" alt=""></div></div>
  <div class="moldura"><div class="barra-nav"><div class="bolas"><i></i><i></i><i></i></div>
  <div class="url">smsmais.saude.niteroi.rj.gov.br</div></div>
  <div class="palco">%s</div></div>
  <div class="legenda-fila"><div class="item"><span class="n">1</span><span class="t">legenda de prova</span></div></div>
  <div class="rodape"><span class="esq"></span><span class="num">0</span></div>
</section>''' % (nome, montar.montar_tela(nome))

doc = montar.ler(os.path.join(AQUI, 'base.html')).replace('{{SLIDES}}', slide)
io.open(os.path.join(AQUI, 'prova-%s.html' % nome), 'w', encoding='utf-8').write(doc)
print(subprocess.run(['node', 'render.mjs', 'prova-%s.html' % nome, 'prova-%s.pdf' % nome, '--no-shots'],
                     cwd=AQUI, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout)
r = subprocess.run(['node', 'medir.mjs', 'prova-%s.html' % nome], cwd=AQUI, capture_output=True, text=True, encoding='utf-8', errors='replace')
print('--- altura usada x disponivel (area util do painel: 1048 x 558) ---')
print(r.stdout)
