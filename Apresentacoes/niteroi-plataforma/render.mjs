// Renderiza o HTML da apresentação em PDF (16:9, 1 slide = 1 página),
// verifica transbordo por slide e exporta PNG de cada slide para conferência visual.
import puppeteer from 'puppeteer-core'
import { fileURLToPath, pathToFileURL } from 'node:url'
import path from 'node:path'
import fs from 'node:fs/promises'

const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe'
const here = path.dirname(fileURLToPath(import.meta.url))

const htmlPath = path.resolve(here, process.argv[2] || 'apresentacao.html')
const pdfPath = path.resolve(here, process.argv[3] || 'apresentacao.pdf')
const shotsDir = path.resolve(here, 'slides-png')
const wantShots = !process.argv.includes('--no-shots')

const W = 1280, H = 720

const browser = await puppeteer.launch({
  executablePath: CHROME,
  headless: 'new',
  args: ['--allow-file-access-from-files', '--font-render-hinting=none', '--disable-lcd-text'],
})
const page = await browser.newPage()
await page.setViewport({ width: W, height: H, deviceScaleFactor: 2 })
page.on('console', m => { if (m.type() === 'error') console.log('  [console]', m.text()) })
page.on('pageerror', e => console.log('  [pageerror]', e.message))

await page.goto(pathToFileURL(htmlPath).href, { waitUntil: 'networkidle0' })
await page.evaluate(() => document.fonts ? document.fonts.ready : null)
await new Promise(r => setTimeout(r, 600))

// ---- verificação: imagens quebradas ----
const imgs = await page.evaluate(() =>
  [...document.images].filter(i => !i.complete || i.naturalWidth === 0).map(i => i.getAttribute('src'))
)

// ---- verificação: transbordo dentro de cada slide ----
const report = await page.evaluate(({ W, H }) => {
  const TOL = 1.5
  const slides = [...document.querySelectorAll('.slide')]
  return {
    total: slides.length,
    slides: slides.map((s, i) => {
      const r = s.getBoundingClientRect()
      const problems = []
      const dims = { w: Math.round(r.width), h: Math.round(r.height) }
      if (Math.abs(r.width - W) > TOL || Math.abs(r.height - H) > TOL) {
        problems.push(`slide com ${dims.w}x${dims.h}, esperado ${W}x${H}`)
      }
      for (const el of s.querySelectorAll('*')) {
        const st = getComputedStyle(el)
        if (st.display === 'none' || st.visibility === 'hidden' || st.position === 'fixed') continue
        if (el.closest('.sangria') || el.classList.contains('sangria')) continue   // decoracao que sangra de proposito
        const b = el.getBoundingClientRect()
        if (b.width === 0 && b.height === 0) continue
        const over = []
        if (b.bottom > r.bottom + TOL) over.push(`abaixo +${Math.round(b.bottom - r.bottom)}px`)
        if (b.top < r.top - TOL) over.push(`acima ${Math.round(r.top - b.top)}px`)
        if (b.right > r.right + TOL) over.push(`direita +${Math.round(b.right - r.right)}px`)
        if (b.left < r.left - TOL) over.push(`esquerda ${Math.round(r.left - b.left)}px`)
        if (over.length) {
          const tag = el.tagName.toLowerCase() + (el.className && typeof el.className === 'string' ? '.' + el.className.trim().split(/\s+/).slice(0, 3).join('.') : '')
          problems.push(`${tag} → ${over.join(', ')} | "${(el.textContent || '').trim().slice(0, 50)}"`)
        }
      }
      // sobreposição entre o cabeçalho e os blocos de conteúdo (ambos são absolutos)
      const cabeca = s.querySelector('.cabeca')
      if (cabeca) {
        const cr = cabeca.getBoundingClientRect()
        for (const sel of ['.corpo', '.moldura', '.legenda-fila', '.kpi-fila']) {
          const alvo = s.querySelector(sel)
          if (!alvo) continue
          const ar = alvo.getBoundingClientRect()
          const sobra = Math.min(cr.bottom, ar.bottom) - Math.max(cr.top, ar.top)
          if (sobra > TOL && Math.min(cr.right, ar.right) - Math.max(cr.left, ar.left) > TOL) {
            problems.push(`cabecalho invade ${sel} em ${Math.round(sobra)}px (texto do titulo fica escondido)`)
          }
        }
      }

      // conteúdo rolável (clipado por overflow:hidden)
      const scroll = []
      const escalado = el => [...el.children].some(c => getComputedStyle(c).transform !== 'none')
      const check = el => {
        if (escalado(el)) return
        if (getComputedStyle(el).textOverflow === 'ellipsis') return   // truncagem proposital
        if (el.scrollHeight > el.clientHeight + TOL && el.clientHeight > 0) scroll.push(`${el.tagName.toLowerCase()}.${(el.className||'').toString().trim().split(/\s+/)[0]} corta ${el.scrollHeight - el.clientHeight}px na vertical`)
        if (el.scrollWidth > el.clientWidth + TOL && el.clientWidth > 0) scroll.push(`${el.tagName.toLowerCase()}.${(el.className||'').toString().trim().split(/\s+/)[0]} corta ${el.scrollWidth - el.clientWidth}px na horizontal`)
      }
      s.querySelectorAll('*').forEach(el => { const st = getComputedStyle(el); if (st.overflow !== 'visible') check(el) })
      return {
        n: i + 1,
        id: s.id || s.dataset.titulo || '',
        titulo: (s.querySelector('[data-slide-title], h1, h2') || {}).textContent?.trim().slice(0, 60) || '',
        dims,
        problems: [...new Set([...problems, ...scroll])].slice(0, 12),
      }
    }),
  }
}, { W, H })

// ---- PNG por slide ----
if (wantShots) {
  await fs.rm(shotsDir, { recursive: true, force: true })
  await fs.mkdir(shotsDir, { recursive: true })
  const handles = await page.$$('.slide')
  for (let i = 0; i < handles.length; i++) {
    await handles[i].screenshot({ path: path.join(shotsDir, `slide-${String(i + 1).padStart(2, '0')}.png`) })
  }
}

// ---- PDF ----
await page.pdf({
  path: pdfPath,
  printBackground: true,
  preferCSSPageSize: true,
  displayHeaderFooter: false,
  margin: { top: 0, right: 0, bottom: 0, left: 0 },
})

await browser.close()

// ---- relatório ----
const bad = report.slides.filter(s => s.problems.length)
console.log(`\nSlides: ${report.total}`)
for (const s of report.slides) {
  const mark = s.problems.length ? 'X' : 'ok'
  console.log(`  [${mark}] ${String(s.n).padStart(2)}  ${s.dims.w}x${s.dims.h}  ${s.id} — ${s.titulo}`)
  s.problems.forEach(p => console.log(`        ! ${p}`))
}
if (imgs.length) { console.log('\nIMAGENS QUEBRADAS:'); imgs.forEach(s => console.log('  !', s)) }
console.log(`\nPDF: ${pdfPath}`)
const st = await fs.stat(pdfPath)
console.log(`Tamanho: ${(st.size / 1024 / 1024).toFixed(2)} MB`)
console.log(bad.length || imgs.length ? `\nFALHOU: ${bad.length} slide(s) com problema, ${imgs.length} imagem(ns) quebrada(s)` : '\nTUDO CERTO: nenhum transbordo, nenhuma imagem quebrada')
process.exit(bad.length || imgs.length ? 1 : 0)
