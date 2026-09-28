"""Compõe artes de cabeçalho WhatsApp no estilo da casa a partir das artes existentes.

Uso: python compor.py <saida.png> <variante> "<titulo|linhas>" "<subtitulo>" <icone_hex|->
  variante: vermelho (personagem sorrindo, fundo vermelho, texto branco à direita)
            branco   (personagem sorrindo com celular à direita, fundo branco, texto vermelho à esquerda)
            branco-preocupada (personagem preocupada à direita, fundo branco + bloco vermelho)
"""
import sys
from PIL import Image, ImageDraw, ImageFont

DOCS = "C:/Projetos GIT/SMSMarica/docs/comunicacao"
VERMELHO = (181, 30, 33)
VERMELHO_LOGO = (194, 23, 32)
CINZA = (70, 70, 70)


def fonte(nome, tam):
    return ImageFont.truetype(f"Montserrat-{nome}.ttf", tam)


def logo_colorida(cor, largura):
    logo = Image.open(f"{DOCS}/marica_logo.png").convert("RGBA")
    alfa = logo.getchannel("A")
    solido = Image.new("RGBA", logo.size, cor + (255,))
    solido.putalpha(alfa)
    esc = largura / logo.size[0]
    return solido.resize((largura, int(logo.size[1] * esc)), Image.LANCZOS)


def ajustar(draw, linhas, nome, tam_max, largura_max, tam_min=30):
    tam = tam_max
    while tam > tam_min:
        f = fonte(nome, tam)
        if all(draw.textlength(l, font=f) <= largura_max for l in linhas):
            return f, tam
        tam -= 2
    return fonte(nome, tam_min), tam_min


def escrever_bloco(im, cx, y, titulo, subtitulo, icone, cor_titulo, cor_sub, cor_icone, largura, tam_titulo=64):
    d = ImageDraw.Draw(im)
    linhas = [l.strip() for l in titulo.split("|") if l.strip()]
    ft, tam = ajustar(d, linhas, "ExtraBold", tam_titulo, largura)
    alt = int(tam * 1.12)
    for l in linhas:
        w = d.textlength(l, font=ft)
        d.text((cx - w / 2, y), l, font=ft, fill=cor_titulo)
        y += alt
    if subtitulo:
        y += int(tam * 0.25)
        subl = [l.strip() for l in subtitulo.split("|") if l.strip()]
        fs, tams = ajustar(d, subl, "SemiBold", int(tam * 0.56), largura, 22)
        for l in subl:
            w = d.textlength(l, font=fs)
            d.text((cx - w / 2, y), l, font=fs, fill=cor_sub)
            y += int(tams * 1.25)
    if icone and icone != "-":
        y += int(tam * 0.3)
        fi = ImageFont.truetype("MaterialIcons-Regular.ttf", int(tam * 1.15))
        g = chr(int(icone, 16))
        w = d.textlength(g, font=fi)
        d.text((cx - w / 2, y), g, font=fi, fill=cor_icone)
    return y


def compor(saida, variante, titulo, subtitulo, icone):
    if variante == "vermelho":
        base = Image.open("agendamento-proximo.jpg").convert("RGB")
        w, h = base.size
        # apaga o texto original: replica a coluna 498 (fundo vermelho) para a direita
        faixa = base.crop((498, 0, 499, h)).resize((w - 500, h))
        base.paste(faixa, (500, 0))
        logo = logo_colorida((255, 255, 255), 520)
        base.paste(logo, (812 - 260, 70), logo)
        escrever_bloco(base, 812, 70 + logo.size[1] + 40, titulo, subtitulo, icone,
                       (255, 255, 255), (255, 255, 255), (255, 255, 255), 540)
    elif variante == "branco":
        peq = Image.open(f"{DOCS}/imagens-whatsapp/04-informacoes-ao-cidadao.png").convert("RGB")
        base = peq.resize((peq.size[0] * 2, peq.size[1] * 2), Image.LANCZOS)  # 988x520
        w, h = base.size
        d = ImageDraw.Draw(base)
        d.rectangle((0, 0, 590, h), fill=(255, 255, 255))
        logo = logo_colorida(VERMELHO_LOGO, 470)
        base.paste(logo, (300 - 235, 40), logo)
        escrever_bloco(base, 300, 40 + logo.size[1] + 24, titulo, subtitulo, icone,
                       VERMELHO_LOGO, CINZA, VERMELHO_LOGO, 560, tam_titulo=58)
    elif variante == "branco-preocupada":
        base = Image.open("cancelamento-branco-invertido.jpg").convert("RGB")
        w, h = base.size
        d = ImageDraw.Draw(base)
        d.rectangle((0, 0, 650, h), fill=(255, 255, 255))
        logo = logo_colorida(VERMELHO_LOGO, 520)
        base.paste(logo, (340 - 260, 60), logo)
        escrever_bloco(base, 340, 60 + logo.size[1] + 30, titulo, subtitulo, icone,
                       VERMELHO_LOGO, CINZA, VERMELHO_LOGO, 600)
    else:
        raise SystemExit("variante desconhecida")
    # Proporção 1,91:1 recomendada pela Meta (fora dela o WhatsApp corta o centro no celular).
    w, h = base.size
    alvo_h = int(round(w / 1.91))
    if alvo_h < h:
        sobra = h - alvo_h
        topo = sobra // 3  # tira menos em cima (logo) do que embaixo (ombros da personagem)
        base = base.crop((0, topo, w, topo + alvo_h))
    base.save(saida, quality=92)
    print("gravado", saida, base.size)


if __name__ == "__main__":
    compor(*sys.argv[1:6])
