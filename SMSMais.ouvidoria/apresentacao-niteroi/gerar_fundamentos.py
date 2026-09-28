# -*- coding: utf-8 -*-
"""Slide de fundamentos: em que base normativa e em que modelos o módulo foi desenhado."""
import io, os
from gerar_slides import cab, slide

AQUI = os.path.dirname(os.path.abspath(__file__))

NORMAS = [
    (u'Lei 13.460/2017',
     u'a espinha: protocolo, 30+30, identidade restrita, relatório anual e pesquisa de satisfação'),
    (u'Decreto 9.492/2018',
     u'complementação, suspensão de prazo e conteúdo da resposta'),
    (u'Decreto 10.153/2019',
     u'salvaguardas ao denunciante e controle de acesso registrado'),
    (u'Lei 13.608/2018',
     u'o município deve manter ouvidoria ou correição para irregularidades'),
    (u'LAI 12.527 &middot; LGPD 13.709',
     u'informação pessoal restrita; dado de saúde é sensível'),
    (u'PRC GM/MS 1/2017, arts. 109–119',
     u'a rede de ouvidorias do SUS e o dever de <b>usar os dados</b>'),
    (u'Port. SGEP/MS 8/2007 &middot; Leis 8.080 e 8.142',
     u'regulamento do OuvidorSUS e o Conselho de Saúde'),
]

MODELOS = [
    (u'OuvidorSUS 3', u'MS / DATASUS',
     u'De onde vieram a <b>tipologia dos seis tipos</b> e a árvore de assunto com código. '
     u'A tipificação nacional tem 23 assuntos e 1.897 itens no último nível.'),
    (u'PN CGU 116/2024 &middot; Fala.BR', u'Controladoria-Geral da União',
     u'Não obriga o município, mas é a <b>melhor especificação funcional pública</b> '
     u'que existe: prazo suspenso, conteúdo mínimo por tipo, arquivamento tipificado, sigilo da denúncia.'),
    (u'ISO 10002', u'gestão de reclamações',
     u'Acessibilidade do canal, acuso de recebimento, acompanhamento e análise crítica pela direção.'),
]

OUVIDORIAS = [
    (u'SMS de São Paulo', u'a rede de <b>pontos de resposta</b> com titular e login individual'),
    (u'Ouvidoria-Geral do Rio', u'a régua: &gt;90% no prazo, &lt;10 dias, estoque &le;10%'),
    (u'SMSA Belo Horizonte', u'o relatório por <b>faixa de prazo</b> e por canal'),
    (u'CGM Recife', u'a prova de que o gargalo é o tempo, não o canal'),
    (u'SESA Espírito Santo', u'o <b>indicador contratual</b> de organização social'),
    (u'SES-RJ, SES-SP, Fortaleza, Curitiba', u'prazos por prioridade e ouvidoria dentro do app'),
    (u'Ouvidoria-Geral de Maricá', u'o que um relatório municipal <b>ainda não</b> mede'),
]

normas = u'\n'.join(
    u'          <li style="font-size:11.5px;line-height:1.4;margin-bottom:6px">'
    u'<b>%s</b> &mdash; %s</li>' % (n, d) for n, d in NORMAS)

modelos = u'\n'.join(
    u'        <div style="padding-bottom:8px;margin-bottom:8px%s">\n'
    u'          <div class="fl ai-c g8" style="margin-bottom:3px;flex-wrap:wrap">\n'
    u'            <span class="b600" style="font-size:13px;color:var(--tinta)">%s</span>\n'
    u'            <span class="txt-xs c-mudo">%s</span>\n          </div>\n'
    u'          <p class="b-texto" style="font-size:11.5px;line-height:1.4">%s</p>\n        </div>'
    % (';border-bottom:1px solid var(--s200)' if i < len(MODELOS) - 1 else '', t, f, d)
    for i, (t, f, d) in enumerate(MODELOS))

ouvidorias = u'\n'.join(
    u'          <li style="font-size:11.5px;line-height:1.4;margin-bottom:6px">'
    u'<b>%s</b> &mdash; %s</li>' % (n, d) for n, d in OUVIDORIAS)

HTML = slide(
    'fundamentos',
    cab(u'Fundamento', u'Em que isto foi <em>baseado</em>',
        u'Nada aqui saiu de opinião. Saiu de <b>três fontes</b>: a norma que obriga, os sistemas '
        u'nacionais de referência e o que as ouvidorias de saúde que já funcionam publicam.'),
    u'    <div class="grade cresce" style="grid-template-columns:1.04fr 1fr .96fr;gap:14px">\n'
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <p class="b-olho">1 &middot; A norma que obriga</p>\n'
    u'        <ul class="lista" style="margin-top:2px">\n' + normas + u'\n        </ul>\n'
    u'        <p class="b-texto" style="font-size:11px;margin-top:auto;padding-top:6px;color:var(--s500)">'
    u'O detalhe artigo por artigo, e o comportamento do sistema que cumpre cada um, está no próximo slide.</p>\n'
    u'      </div>\n'
    u'      <div class="bloco" style="padding:14px 16px">\n'
    u'        <p class="b-olho">2 &middot; Os sistemas de referência</p>\n' + modelos + u'\n'
    u'        <p class="b-texto" style="font-size:11px;margin-top:auto;color:var(--s500)">'
    u'A adesão ao Fala.BR e ao OuvidorSUS é voluntária e exige ato normativo próprio &mdash; '
    u'o mesmo ato que um sistema próprio também pede.</p>\n'
    u'      </div>\n'
    u'      <div class="bloco realce" style="padding:14px 16px">\n'
    u'        <p class="b-olho">3 &middot; As ouvidorias estudadas</p>\n'
    u'        <ul class="lista" style="margin-top:2px">\n' + ouvidorias + u'\n        </ul>\n'
    u'        <p class="b-texto" style="font-size:11px;margin-top:auto;padding-top:6px;color:var(--p800)">'
    u'De cada uma foi lido o relatório, a portaria ou o termo de referência &mdash; não a '
    u'página institucional.</p>\n'
    u'      </div>\n    </div>\n'
    u'    <div class="fl g14 ai-c" style="margin-top:14px;padding:11px 16px;background:var(--tinta);'
    u'border-radius:12px;color:#fff">\n'
    u'      <svg viewBox="0 0 24 24" fill="none" stroke="var(--nit-amarelo)" stroke-width="2" '
    u'stroke-linecap="round" stroke-linejoin="round" style="width:19px;height:19px;flex:none">'
    u'<path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H19a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H6.5a1 1 0 0 1 0-5H20"/></svg>\n'
    u'      <p style="margin:0;font-size:12px;line-height:1.45;color:rgba(255,255,255,.88)">'
    u'<b style="color:#fff">Método.</b> O levantamento foi feito sobre '
    u'<b style="color:#fff">documentos públicos lidos na fonte</b> &mdash; leis, decretos, portarias, '
    u'manuais do Ministério da Saúde e da CGU e relatórios de gestão de ouvidorias municipais '
    u'e estaduais. Cada requisito do módulo aponta para o artigo que o obriga ou para a prática que o '
    u'inspirou; o que é inferência está marcado como inferência. A documentação '
    u'de origem acompanha a entrega.</p>\n'
    u'    </div>',
    'corpo')

if __name__ == '__main__':
    io.open(os.path.join(AQUI, 'slides', '04-fundamentos.html'), 'w', encoding='utf-8').write(HTML)
    print('escrito: 04-fundamentos.html (%d)' % len(HTML))
