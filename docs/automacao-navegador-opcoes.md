# Observar e agir no navegador a partir de algo instalado no Windows — estudo das opções

**Data:** 20/09/2026 · **Contexto:** a extensão `SMSMais.chrome` (v0.4.0, em produção) observa o
operador no SISREG e no Ecossistemas e manda ao hub. Ela é distribuída em modo desenvolvedor com um
`.bat` de `git pull`. O Bernardo não quer depender da Chrome Web Store, precisa de **auto-atualização**
a partir de algo **instalado na máquina**, e quer que a solução comece no navegador (ler e
interceptar requisições, clicar, gerar modais e telas sobre a página) e depois cresça para o
desktop. Este estudo cobre **só a missão web**. Resultado: PoC em `SMSMais.integrador/`.

Fatos que pesam em qualquer opção:

- **SISREG tem sessão única por operador**: qualquer login novo derruba o anterior; robô e humano
  se derrubam. O backend fala com o SISREG por `HttpClient` puro com operador dedicado.
- **reCAPTCHA após ~700 requisições por sessão**; relogar não resolve, só humano no navegador.
- **ADR-0059 fase 2** (escrita no SISREG) foi desenhada como senha do operador em memória no
  servidor + escrita com allowlist após spike.
- Já existe precedente de **agente desktop + instalador WiX** no monorepo
  (`Automais.Assinador.Agente`, ADR-0015): protocolo `automais-assinador://`, efêmero, sem socket.

## 1. O que "sessão" significa

O SISREG identifica quem está logado por cookies. Cada programa tem o seu pote de cookies: o
Chrome do operador, um app nosso, o Playwright, o `HttpClient` do servidor. Logar cria uma sessão
nova para aquele operador e o SISREG **derruba a anterior** ("logon em outra estação de trabalho").

| Termo | Significado prático |
|---|---|
| **A do operador** | A ferramenta trabalha dentro do Chrome que o operador já logou. Usa os cookies dele, não faz login, nada é derrubado. Cada requisição que ela dispara conta no orçamento anti-robô dele como um clique. (Extensão; `chrome.debugger`.) |
| **Própria** | A ferramenta tem pote próprio e o operador loga **nela** (Chrome com perfil nosso, app WebView2). Se ele usa só a ferramenta para o SISREG, é a única sessão. Se abrir o SISREG também no Chrome normal, um derruba o outro. |
| **Nova** | Playwright ou o servidor recebem a senha e fazem login. Nasce mais uma sessão do mesmo operador e a do navegador dele cai na hora (aviso (e) da ADR-0059). Só não colide se cada robô tiver login próprio. |

Consequência: **tudo que age dentro do navegador do operador não tem problema de sessão**; tudo
que loga por fora tem.

## 2. As opções web

Colunas: **Auto-atualiza sem loja** (o que a máquina precisa para a versão nova aparecer sozinha)
e **Operador vê "monitorada"?** (o que o Chrome mostra por conta própria, fora do selo que nós
desenhamos e controlamos).

| # | Opção | Corpo resp. | Clicar/modais | Sessão | Auto-atualiza sem loja | Operador vê "monitorada"? | Sem admin |
|---|---|---|---|---|---|---|---|
| 1 | Extensão descompactada + agente que mantém a pasta; a extensão faz `chrome.runtime.reload()` ao ver versão nova | Parcial (hook fetch/XHR) | Sim | A do operador | Sim (agente) | Não; só o nosso selo. Modo dev ligado | Sim |
| 2 | Extensão por **política** (`ExtensionInstallForcelist` + `update_url` nosso, CRX hospedado) | Parcial; ganha `webRequestBlocking` | Sim | A do operador | Sim (o Chrome busca `update.xml` a cada ~5 h) | Não na aba; menu "Gerenciado pela sua organização" | **Não**: política de máquina (HKLM) **e** PC gerenciado (domínio AD, Entra ID ou CBCM) |
| 3 | Extensão + **`chrome.debugger`** (CDP de dentro da aba) | **Total** | Sim, + `Input.*` | A do operador | Como 1/2 | **Sim**: barra amarela "começou a depurar" enquanto anexada (some com `--silent-debugger-extension-api` no atalho) | Sim |
| 4 | **Agente lança o Chrome com CDP** (perfil próprio) | **Total** (+ `Fetch` altera) | Sim, + `Input.*` | Própria | Sim (só o agente) | Sem barra (sem `--enable-automation`). É "outro Chrome" (sem favoritos/senhas do operador) | Sim |
| 5 | Playwright/Puppeteer lançando o Chrome | Total | Sim (script) | Própria ou nova | Sim | Barra "controlado por software de teste" por padrão, removível. Também é outro Chrome | Sim |
| 6 | **App WebView2** (.NET WPF/WinForms), navegador nosso | Total, e altera (`WebResourceRequested`) | Sim, + telas nativas | Própria por operador | Sim (perUser + Velopack) | Nada do Chrome: é o nosso app. Operador tem que usar o app | Sim (perUser) |
| 7 | Electron/Tauri (variante do 6) | Total | Sim | Própria | Sim | Nada | Sim |
| 8 | Proxy local MITM (CA raiz + proxy por usuário) | Total; injeta em qualquer navegador | Só por injeção | A do operador | Sim | Nenhum aviso | Sim — **descartar** (CA em PC da Prefeitura é bandeira vermelha; LGPD; WAF do SISREG) |

## 3. Instalar a extensão sem modo dev

Chrome no Windows só aceita extensão fora da loja por **política de empresa**:

| Caminho | Situação |
|---|---|
| Registro "external extension" | Só aceita extensão **da loja** desde 2013 |
| Arrastar `.crx` | Bloqueado fora da loja |
| Flag `--load-extension` no atalho | **Removida do Chrome de marca no 137** (2025); só em Chromium/Chrome for Testing. Edge: a conferir |
| `ExtensionInstallForcelist` + `update_url` nosso | Funciona, sem modo dev, auto-update pelo Chrome, extensão travada, `webRequestBlocking`. Exige HKLM (admin) **e** PC gerenciado |
| Chrome Browser Cloud Management (CBCM) | Gratuito. Token de inscrição gravado uma vez em HKLM (admin) torna o Chrome "gerenciado" mesmo fora do domínio |

**Sem admin não existe caminho sem modo dev** no Chrome de marca. E "configurar o Chrome sozinho"
(ligar o toggle + carregar a pasta) não tem API: editar `Secure Preferences` é adulteração (HMAC
por máquina/versão; Chrome restaura e desativa); automatizar a UI de `chrome://extensions` por UI
Automation é viável uma vez por PC, mas frágil por idioma/versão.

## 4. Fusões possíveis entre app Windows e Chrome

| Fusão | O que é | Serve para |
|---|---|---|
| **Native Messaging** | Extensão fala com um `.exe` nosso por stdio (JSON). Registro em `HKCU\...\NativeMessagingHosts` (sem admin) | Ponte oficial: extensão é o olho na aba, o exe faz o que a aba não pode (arquivos, dispositivos, UIA, janelas nativas) |
| Instalação por política | App grava HKLM/CBCM | Instalar, travar e atualizar a extensão sem loja |
| **App lança o Chrome** com flags (`--remote-debugging-pipe`, perfil dedicado) | Chrome vira motor; app fala CDP | Controle total; perfil separado |
| WebView2 | Chromium do Edge embutido no app | Fusão máxima, mas não é "o Chrome do operador" |
| Protocolo customizado (`automais-*://`) | Chrome abre o exe com argumentos (padrão do Assinador) | Painel manda o PC fazer algo pontual |
| UIA sobre a janela do Chrome | App lê a árvore de acessibilidade | Fraco para web (nome/papel/valor) |

## 5. Caminho escolhido: agente lança o Chrome por CDP (opção 4)

Se o nosso agente lança o Chrome com depuração ligada, ele faz tudo que a extensão faz, **sem
extensão**, sem modo dev, sem loja, sem admin, e só o agente atualiza:

| O que a extensão faz | Como fica por CDP |
|---|---|
| Ver envio (URL + campos) | `Network.requestWillBeSent` (+ corpo do POST) |
| Ver corpo da resposta (hoje parcial) | `Network.getResponseBody` — tudo, inclusive o AJAX do `autorizador` |
| Bloquear/alterar requisição | `Fetch.enable` + `continueRequest`/`fulfillRequest` |
| Selo, blur, janela | `Page.addScriptToEvaluateOnNewDocument` injeta o mesmo JS em toda página/iframe |
| Clicar / preencher | `Input.dispatchMouseEvent` / `Runtime.evaluate` |
| Mandar ao hub | O próprio agente (.NET, HTTPS), sem CSP/CORS |
| Login no SMSMais | Script no painel lê `localStorage['smsmarica.auth']` e entrega pelo binding |

**Todas as abas, sem `--app`**: a conexão é do navegador inteiro. `Target.setDiscoverTargets` +
`Target.setAutoAttach` (flatten) anexam toda aba, popup e iframe novos. Regra de LGPD: ligar a
captura **só nos hosts da lista de sítios**; nas demais abas, nada.

O que custa: **perfil separado é obrigatório** (Chrome ≥ 136 recusa depurar o perfil padrão), logo
o operador abre o SISREG **pelo ícone do SMSMais** (sessão própria; abrir também no Chrome pessoal
derruba uma das duas); se o agente morrer o Chrome fica cego (com PuppeteerSharp o Chrome cai
junto); porta de depuração expõe a qualquer processo local (endurecer com pipe).

**Decisão (20/09/2026):** direção = agente por CDP, **sem descartar a extensão** (os dois canais
convivem no mesmo endpoint), com PoC antes de qualquer ADR. PoC em `SMSMais.integrador/`
(.NET 10 + PuppeteerSharp). A alternativa de **escrever no SISREG pela sessão do operador** (ordem
nasce no servidor, agente executa na aba logada, captura o retorno e concilia — zero senha fora do
navegador, zero sessão nova) é candidata para a fase 2 do ADR-0059 e precisa de ADR própria.
