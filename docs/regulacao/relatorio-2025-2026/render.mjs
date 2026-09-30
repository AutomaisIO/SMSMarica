// Gera o PDF de cada relatório HTML (A4 paisagem) com o Chrome local e VERIFICA antes de aceitar:
// imagem quebrada, tabela/gráfico mais largo que a página, célula cortada. Falha com exit 1 se achar.
// Baseado em SMSMais.ouvidoria/apresentacao-marica/render.mjs (usa o puppeteer-core instalado lá).
import { createRequire } from 'node:module'
import { fileURLToPath, pathToFileURL } from 'node:url'
import path from 'node:path'

const require = createRequire(path.resolve('C:/Projetos GIT/SMSMarica/SMSMais.ouvidoria/apresentacao-marica/package.json'))
const puppeteer = require('puppeteer-core')
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe'
const here = path.dirname(fileURLToPath(import.meta.url))
const alvos = process.argv.slice(2).length ? process.argv.slice(2) : ['sisreg', 'ser', 'sernit', 'esussg']

const browser = await puppeteer.launch({ executablePath: CHROME, headless: 'new', args: ['--font-render-hinting=none'] })
let falhou = false
for (const alvo of alvos) {
  const page = await browser.newPage()
  // largura útil do A4 paisagem com 12 mm de margem: 273 mm ≈ 1032 px
  await page.setViewport({ width: 1032, height: 740 })
  await page.emulateMediaType('print')
  const html = path.join(here, `relatorio-${alvo}.html`)
  await page.goto(pathToFileURL(html).href, { waitUntil: 'load' })
  const problemas = await page.evaluate(() => {
    const out = []
    for (const i of document.images) if (!i.complete || i.naturalWidth === 0) out.push('imagem quebrada')
    const largura = document.documentElement.clientWidth
    for (const el of document.querySelectorAll('table, svg, .tiles, .duas')) {
      const r = el.getBoundingClientRect()
      if (r.right > largura + 1) out.push(`${el.tagName.toLowerCase()} passa da largura (${Math.round(r.right)} > ${largura}): ${(el.closest('section')?.querySelector('h3')?.textContent || '').slice(0, 60)}`)
    }
    for (const td of document.querySelectorAll('table.serie td:not(.rot)')) {
      if (td.scrollWidth > td.clientWidth + 1) out.push(`célula cortada: "${td.textContent}" em ${(td.closest('section')?.querySelector('h3')?.textContent || '').slice(0, 50)}`)
    }
    return out
  })
  const pdf = path.join(here, `indicadores-regulacao-${alvo}-2025-2026.pdf`)
  await page.pdf({ path: pdf, format: 'A4', landscape: true, printBackground: true, preferCSSPageSize: true,
    displayHeaderFooter: true, headerTemplate: '<span></span>',
    footerTemplate: '<div style="font-size:7px;color:#8a8f96;width:100%;text-align:right;padding-right:12mm">Página <span class="pageNumber"></span> de <span class="totalPages"></span></div>' })
  if (problemas.length) {
    falhou = true
    console.log(`✗ ${alvo}: ${problemas.length} problema(s)`)
    for (const p of [...new Set(problemas)].slice(0, 15)) console.log('   -', p)
  } else console.log(`✓ ${alvo} → ${path.basename(pdf)}`)
  await page.close()
}
await browser.close()
process.exit(falhou ? 1 : 0)
