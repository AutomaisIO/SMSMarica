/**
 * Padrão visual do documento de atendimento (o mesmo do painel e do app nativo, nas cores do app).
 *
 * O HTML chega do servidor em três formas: eDoc do Salux (`.edoc-campo`: rótulo + valor), Boletim
 * do Klinikos (`h3` + `p`, sem classe) e evolução em texto (`.edoc-texto`, parágrafos). Ele abre
 * num iframe sem scripts, que não herda o CSS do app — sem este estilo, era texto solto na fonte
 * padrão do navegador. No celular o rótulo vai acima do valor, não ao lado.
 */
const CSS = `
*{box-sizing:border-box}
html{-webkit-text-size-adjust:100%}
body{margin:0;padding:20px 16px 32px;background:#fff;color:#201A1B;font:15px/1.6 "Inter Variable",system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;overflow-wrap:anywhere}
.edoc-doc+.edoc-doc{margin-top:24px;padding-top:16px;border-top:1px solid #ECE3E1}
.edoc-titulo{margin:0 0 12px;padding-bottom:8px;border-bottom:2px solid #E5F2F1;color:#0A5E5D;font-size:16px;font-weight:600;line-height:1.35}
.edoc-campo{padding:10px 0;border-bottom:1px solid #ECE3E1}
.edoc-campo:last-child{border-bottom:0}
.edoc-rotulo,h3:not(.edoc-titulo){display:block;margin:0 0 2px;color:#6F6466;font-size:11px;font-weight:600;letter-spacing:.06em;line-height:1.4;text-transform:uppercase}
.edoc-valor{white-space:pre-wrap}
.edoc-linha+.edoc-linha{margin-top:2px}
h3:not(.edoc-titulo){margin:18px 0 4px;padding-top:14px;border-top:1px solid #ECE3E1}
h3:not(.edoc-titulo):first-child{margin-top:0;padding-top:0;border-top:0}
p{margin:0 0 12px}
p:last-child{margin-bottom:0}
.edoc-vazio{color:#6F6466;font-style:italic}
`;

/** Página completa para o `srcDoc` do visualizador. */
export function paginaDoDocumento(conteudoHtml: string): string {
  return `<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><style>${CSS}</style></head><body>${conteudoHtml}</body></html>`;
}
