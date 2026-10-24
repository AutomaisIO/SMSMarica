"""Monta o HTML do recurso gráfico da Play (1024x500), com as fontes e as imagens embutidas.

Rode da pasta SMSMais.cidadao.app: `python loja/google-play/grafico/montar_grafico.py`. A foto do HTML
sai pelo Edge sem janela (comando no loja/google-play/README.md).
"""
import base64
import pathlib

APP = pathlib.Path(__file__).resolve().parents[3]
SAIDA = pathlib.Path(__file__).resolve().parent / 'grafico.html'


def b64(caminho):
    return base64.b64encode((APP / caminho).read_bytes()).decode()


fontes = {
    ('Bricolage', 700): 'assets/fonts/BricolageGrotesque-Bold.ttf',
    ('Inter', 400): 'assets/fonts/Inter-Regular.ttf',
    ('Inter', 600): 'assets/fonts/Inter-SemiBold.ttf',
}
css_fontes = '\n'.join(
    f"@font-face{{font-family:{nome};font-weight:{peso};"
    f"src:url(data:font/ttf;base64,{b64(arq)}) format('truetype')}}"
    for (nome, peso), arq in fontes.items()
)
logo = b64('assets/brand/marica_logo.png')
tela1 = b64('loja/google-play/imagens/01-inicio.png')
tela2 = b64('loja/google-play/imagens/02-exames-agendados.png')

html = f"""<!doctype html>
<html lang="pt-BR"><head><meta charset="utf-8"><title>grafico</title>
<style>
{css_fontes}
html,body{{margin:0;width:1024px;height:500px;overflow:hidden}}
body{{position:relative;background:#FBF8F6;font-family:Inter,sans-serif;color:#201A1B}}
.arco{{position:absolute;right:-150px;top:-140px;width:700px;height:780px;border-radius:50%;
  background:linear-gradient(160deg,#ECE3E1 0%,#E3D6D3 100%)}}
.texto{{position:absolute;left:64px;top:0;height:500px;width:470px;display:flex;flex-direction:column;justify-content:center}}
.logo{{width:360px;display:block}}
h1{{font-family:Bricolage,sans-serif;font-weight:700;font-size:66px;line-height:1;letter-spacing:-1.5px;margin:42px 0 14px}}
p{{font-size:23px;line-height:1.38;color:#6F6466;margin:0}}
.fone{{position:absolute;width:214px;height:384px;padding:9px;border-radius:34px;background:#201A1B;
  box-shadow:0 26px 50px -18px rgba(32,26,27,.55)}}
.fone img{{width:214px;height:384px;border-radius:26px;display:block}}
.f1{{left:742px;top:58px;z-index:2}}
.f2{{left:592px;top:92px;z-index:1;transform:rotate(-6deg);opacity:.97}}
</style></head>
<body>
<div class="arco"></div>
<div class="texto">
  <img class="logo" alt="" src="data:image/png;base64,{logo}">
  <h1>Saúde Maricá</h1>
  <p>Consultas, exames, resultados e transporte para tratamento no seu celular.</p>
</div>
<div class="fone f2"><img alt="" src="data:image/png;base64,{tela2}"></div>
<div class="fone f1"><img alt="" src="data:image/png;base64,{tela1}"></div>
</body></html>"""

SAIDA.write_text(html, encoding='utf-8')
print('gerado', SAIDA)
