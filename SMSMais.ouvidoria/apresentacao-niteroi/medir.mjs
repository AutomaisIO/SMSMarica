import puppeteer from 'puppeteer-core'
import { pathToFileURL } from 'node:url'
import path from 'node:path'
const b = await puppeteer.launch({ executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe', headless:'new', args:['--allow-file-access-from-files'] })
const p = await b.newPage(); await p.setViewport({width:1280,height:720})
await p.goto(pathToFileURL(path.resolve(process.argv[2] || 'apresentacao.html')).href,{waitUntil:'networkidle0'})
const r = await p.evaluate(()=>{
  const app = document.querySelector('.app'); if(!app) return 'sem .app'
  const main = app.querySelector('.app-main'); const wrap = app.querySelector('.app-wrap')
  const mr = main.getBoundingClientRect(), ar = app.getBoundingClientRect()
  const filhos = [...wrap.children].map(c=>{const b=c.getBoundingClientRect();return {tag:c.tagName.toLowerCase()+'.'+(c.className||'').split(' ')[0], h:+(b.height/0.83428).toFixed(1), topRel:+((b.top-mr.top)/0.83428).toFixed(1)}})
  return { app:{w:+(ar.width/0.83428).toFixed(0), h:+(ar.height/0.83428).toFixed(0)},
           main:{h:+(mr.height/0.83428).toFixed(1)},
           util:+(mr.height/0.83428 - 52).toFixed(1),
           usado:+((wrap.getBoundingClientRect().height)/0.83428).toFixed(1),
           filhos }
})
console.log(JSON.stringify(r,null,1)); await b.close()
