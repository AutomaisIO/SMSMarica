// Converte .md em .html estilizado (cópia para humanos).
// Uso: node md2html.mjs <arquivo.md> [outro.md ...]  → gera <arquivo>.html ao lado.
import fs from "fs";
import path from "path";

const esc = s => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
function inline(s) {
  return esc(s)
    .replace(/`([^`]+)`/g, "<code>$1</code>")
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/\*([^*]+)\*/g, "<em>$1</em>")
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
}

function md2body(md) {
  const lines = md.replace(/\r\n/g, "\n").split("\n");
  const out = [];
  let i = 0, list = null; // 'ul' | 'ol'
  const closeList = () => { if (list) { out.push(`</${list}>`); list = null; } };
  while (i < lines.length) {
    const l = lines[i];
    if (/^\s*$/.test(l)) { closeList(); i++; continue; }
    if (/^---+\s*$/.test(l)) { closeList(); out.push("<hr>"); i++; continue; }
    const h = l.match(/^(#{1,6})\s+(.*)$/);
    if (h) { closeList(); const n = h[1].length; out.push(`<h${n}>${inline(h[2])}</h${n}>`); i++; continue; }
    if (/^\|/.test(l)) {
      closeList();
      const rows = [];
      while (i < lines.length && /^\|/.test(lines[i])) { rows.push(lines[i]); i++; }
      const cells = r => r.replace(/^\||\|$/g, "").split("|").map(c => c.trim());
      const head = cells(rows[0]);
      const body = rows.slice(rows[1] && /^\|[\s:-]+\|/.test(rows[1] + "|") ? 2 : 1);
      out.push('<div class="tw"><table><thead><tr>' + head.map(c => `<th>${inline(c)}</th>`).join("") + "</tr></thead><tbody>");
      for (const r of body) out.push("<tr>" + cells(r).map(c => `<td>${inline(c)}</td>`).join("") + "</tr>");
      out.push("</tbody></table></div>");
      continue;
    }
    const li = l.match(/^\s*[-*]\s+(.*)$/), oli = l.match(/^\s*\d+\.\s+(.*)$/);
    if (li || oli) {
      const want = li ? "ul" : "ol";
      if (list !== want) { closeList(); out.push(`<${want}>`); list = want; }
      out.push(`<li>${inline((li || oli)[1])}</li>`); i++; continue;
    }
    closeList();
    // parágrafo (agrega linhas seguidas)
    const par = [l];
    while (i + 1 < lines.length && !/^\s*$/.test(lines[i + 1]) && !/^(#|\||[-*]\s|\d+\.\s|---)/.test(lines[i + 1])) { i++; par.push(lines[i]); }
    out.push(`<p>${inline(par.join(" "))}</p>`); i++;
  }
  closeList();
  return out.join("\n");
}

const CSS = `
:root{--bg:#f4f6f5;--surface:#fff;--ink:#1b2427;--muted:#5c6b70;--border:#d3dcda;--accent:#0782a1;--accent2:#b45309;--code:#eceff0}
@media(prefers-color-scheme:dark){:root{--bg:#14181b;--surface:#1c2226;--ink:#e6ebec;--muted:#93a1a6;--border:#2e383d;--accent:#189aa8;--accent2:#c67927;--code:#232b30}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font-family:system-ui,-apple-system,"Segoe UI",sans-serif;line-height:1.6;padding:32px 20px 64px}
main{max-width:920px;margin:0 auto}
h1{font-family:ui-monospace,Consolas,monospace;font-size:1.25rem;text-transform:uppercase;letter-spacing:.04em;border-bottom:2px solid var(--accent);padding-bottom:8px}
h2{font-family:ui-monospace,Consolas,monospace;font-size:.95rem;text-transform:uppercase;letter-spacing:.08em;color:var(--accent);margin-top:2.2em}
h3{font-size:1rem;margin-top:1.6em}
a{color:var(--accent)}code{font-family:ui-monospace,Consolas,monospace;background:var(--code);padding:1px 5px;border-radius:3px;font-size:.88em}
hr{border:none;border-top:1px solid var(--border);margin:2em 0}
.tw{overflow-x:auto;margin:12px 0}
table{border-collapse:collapse;width:100%;background:var(--surface);border:1px solid var(--border);font-size:.85rem}
th,td{text-align:left;padding:8px 12px;border-top:1px solid var(--border);vertical-align:top}
thead th{border-top:none;font-size:.68rem;text-transform:uppercase;letter-spacing:.08em;color:var(--muted)}
li{margin:3px 0}p,li{max-width:75ch}
`;

for (const f of process.argv.slice(2)) {
  const md = fs.readFileSync(f, "utf8");
  const title = (md.match(/^#\s+(.*)$/m) || [, path.basename(f, ".md")])[1].replace(/[*`]/g, "");
  const html = `<!doctype html>\n<html lang="pt-BR">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1">\n<title>${esc(title)}</title>\n<style>${CSS}</style>\n</head>\n<body>\n<main>\n${md2body(md)}\n</main>\n</body>\n</html>\n`;
  const outPath = f.replace(/\.md$/i, ".html");
  fs.writeFileSync(outPath, html);
  console.log("gerado:", outPath);
}
