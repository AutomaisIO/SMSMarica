# Gera o ícone do executável e da bandeja: o símbolo "+" da Automais (o programa é genérico, igual
# para todo município — a marca é a de quem faz o produto, não a de uma prefeitura).
#
#   python recursos/gerar-icone.py <caminho-da-logo-automais.png>      (precisa do Pillow)
#
# Entra a logo horizontal (símbolo + "automais"); sai:
#   recursos/automais-simbolo.png   só o símbolo, quadrado, fundo transparente (fonte versionada)
#   recursos/icone.ico              16 a 256 px, o que o build embute no executável
import os
import sys
from PIL import Image

AQUI = os.path.dirname(os.path.abspath(__file__))
if len(sys.argv) < 2:
    sys.exit("uso: python recursos/gerar-icone.py <caminho-da-logo-automais.png>")
LOGO = sys.argv[1]

logo = Image.open(LOGO).convert("RGBA")
w, h = logo.size
e = w / 5000  # as medidas abaixo foram tiradas na logo de 5000 px de largura

# O símbolo ocupa a esquerda; a letra "a" do texto entra por baixo do braço direito dele. Fica só o
# que está à esquerda de x=1750, e some o canto inferior direito, onde a letra aparece.
simbolo = logo.crop((0, 0, int(1750 * e), h))
limpar = Image.new("RGBA", (simbolo.width - int(1300 * e), h - int(1150 * e)), (0, 0, 0, 0))
simbolo.paste(limpar, (int(1300 * e), int(1150 * e)))
simbolo = simbolo.crop(simbolo.getbbox())

# Quadrado, com uma margem pequena para o símbolo não encostar na borda da bandeja.
lado = int(max(simbolo.size) * 1.06)
quadro = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
quadro.paste(simbolo, ((lado - simbolo.width) // 2, (lado - simbolo.height) // 2), simbolo)

quadro.resize((512, 512), Image.LANCZOS).save(os.path.join(AQUI, "automais-simbolo.png"))
quadro.resize((256, 256), Image.LANCZOS).save(
    os.path.join(AQUI, "icone.ico"),
    sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (256, 256)],
)
print("gerados automais-simbolo.png e icone.ico em", AQUI)
