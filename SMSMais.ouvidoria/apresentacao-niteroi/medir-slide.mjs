import puppeteer from 'puppeteer-core'; import {pathToFileURL} from 'node:url'; import path from 'node:path'
const b=await puppeteer.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:'new',args:['--allow-file-access-from-files']})
const p=await b.newPage(); await p.setViewport({width:1280,height:720})
await p.goto(pathToFileURL(path.resolve('apresentacao.html')).href,{waitUntil:'networkidle0'})
console.log(await p.evaluate((id)=>{
  const s=document.getElementById(id); if(!s) return 'slide nao achado'
  const c=s.querySelector('.corpo'); const cr=c.getBoundingClientRect()
  const out=[`corpo: ${Math.round(cr.width)}x${Math.round(cr.height)} @top ${Math.round(cr.top)}`]
  const anda=(el,ind)=>{ for(const k of el.children){const r=k.getBoundingClientRect()
    out.push(`${' '.repeat(ind)}${k.tagName.toLowerCase()}.${(k.className||'').toString().split(' ').slice(0,2).join('.')} ${Math.round(r.width)}x${Math.round(r.height)}`)
    if(ind<4 && k.children.length && r.height>60) anda(k,ind+2)}}
  anda(c,2); return out.slice(0,30).join('\n')
}, process.argv[2]||'fundamentos')); await b.close()
