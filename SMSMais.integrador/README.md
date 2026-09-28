# SMSMais · Integrador — agente Windows (PoC)

Agente instalado no PC do operador que **lança o Chrome com perfil próprio e a porta de depuração
ligada (CDP)** e faz, sem extensão, o que a extensão `SMSMais.chrome` faz: observa o operador nos
sistemas configurados (SISREG, Ecossistemas), desenha o selo/LED, o blur e a janela de tráfego
sobre a página, e manda as capturas em lote para a **mesma rota** da extensão
(`POST /extensao/sisreg/capturas`). O backend não muda; os dois canais convivem.

Por que existe: o Bernardo quer algo que **se instale na máquina e se auto-atualize**, sem Chrome
Web Store e sem "modo do desenvolvedor", e que depois cresça para o desktop. O estudo das opções
está em [`docs/automacao-navegador-opcoes.md`](../docs/automacao-navegador-opcoes.md).

Regra de ouro (herdada da extensão): o agente **só observa**. Nunca dispara requisição para o
SISREG — não gasta o orçamento anti-robô nem abre sessão. Tudo que ele registra foi o operador
quem fez, com a senha dele.

## Estado (PoC v0.1.0, 20/09/2026)

Validado ao vivo neste PC: Chrome sobe com perfil próprio e **sem** a barra "controlado por
software de teste"; a aba do SISREG é anexada; o script injetado roda no frame de cima **e no
iframe** (`barra-brasil.html` chegou pelo binding do topo); as requisições do SISREG são vistas
e, em modo mínimo, **nada** sai. Ainda **não** validado: login no painel dentro desse Chrome →
LED verde; cancelamento de teste → evento `cancelou`; corpo do `autorizador` em modo análise.

## Rodar

```bash
cd SMSMais.integrador
dotnet run --project src/SMSMais.Integrador.Agente -- --verboso
# opções:
#   --api URL     backend (padrão http://localhost:5080 — produção é opt-in, com OK)
#   --painel URL  painel de onde a sessão é lida (padrão https://smsmarica.online)
#   --url URL     primeira aba (padrão https://sisregiii.saude.gov.br/)
#   --edge        Edge em vez do Chrome
#   --verboso     loga cada captura (kind + caminho), sem conteúdo
```

Tudo fica por usuário, sem admin, em `%LOCALAPPDATA%\SMSMais\integrador\`:
`chrome\` (perfil: cookies do SISREG persistem entre execuções), `install.json` (installId),
`agente.log`.

## Como funciona

| Arquivo | Papel (equivalente na extensão) |
|---|---|
| `Program.cs` | boot: config → lança o Chrome → observa → envia |
| `Config.cs` | `API_BASE`, `PAINEL_ORIGIN`, `SITIOS` com `blur` e `modo` (`config.js`) |
| `Catalogo.cs` | endpoints e etapas do SISREG mapeados, campos sensíveis (`endpoints.js`) |
| `ChromeLauncher.cs` | acha o `chrome.exe` (App Paths) e lança com `--user-data-dir` próprio, sem `--enable-automation` |
| `Observador.cs` | anexa toda aba/popup; `Network` só nos hosts da lista; envio (`webRequest`), corpo de AJAX pela rede (`capture-hook.js`, mas sem hook e pegando tudo), HTML da tela e operador pelo script; modo mínimo (`background.js`) |
| `Enviador.cs` | sessão do painel, marca, buffer, lote gzip com Bearer, estado do LED (`background.js`) |
| `Ui/pagina.js` | injetado em toda página antes do script dela: no painel lê `localStorage['smsmarica.auth']` (`auth-content.js`); nos sítios captura a tela e desenha selo/blur/janela (`content.js`) |

Ponte página ↔ agente: a página chama `window.__smsmaisIntegradorBind(json)` (CDP
`Runtime.addBinding`); o agente chama `window.__smsmaisIntegrador.{estado,trafego,requisicao}` no
frame de cima. O iframe do SISREG é da mesma origem e usa o binding do topo.

Item enviado ganha `canal: "cdp"` e o lote vai com `versao: "integrador-0.1.0"` — é assim que o
`GET /extensao/sisreg/capturas/resumo` distingue este canal da extensão.

## Decisões e armadilhas

- **Perfil separado é obrigatório**: desde o Chrome 136 ele recusa depurar o perfil padrão. O
  operador **abre o SISREG por este Chrome** (ícone do SMSMais). Se abrir também no Chrome pessoal,
  a sessão única do SISREG derruba uma das duas.
- **Sem `--app`, o agente já vê todas as abas** (auto-attach do CDP). Fora dos hosts de `SITIOS`
  nada é lido — nem o `Network` é ligado.
- **Chrome cai junto com o agente**: o PuppeteerSharp amarra o processo do Chrome ao do agente.
  Matar o agente (Ctrl+C ou Gerenciador de Tarefas) fecha o Chrome. Endurecer isso (pipe +
  relance) fica para depois da PoC.
- **Transporte** hoje é porta de depuração em `127.0.0.1` escolhida pelo Chrome. Qualquer processo
  local poderia anexar; o endurecimento é `--remote-debugging-pipe`.
- O parser do "agendou" (número na tela de confirmação) é o mesmo regex da extensão e continua
  **não testado ao vivo**.
- A API em `localhost:5080` com painel em `smsmarica.online` só funciona se o token do painel for
  aceito pelo backend local (mesma chave JWT). Para o teste ponta a ponta, apontar `--api` para a
  produção **com OK explícito** — é escrita em `smsmarica.sisreg_captura_navegador`.

## Fora da PoC (próximos)

Auto-update (Velopack), bandeja, instalador perUser (WiX, como o Assinador), pipe em vez de porta,
`Fetch` (bloquear/alterar requisição), Native Messaging para o desktop, escrita no SISREG pela
sessão do operador (candidato para a fase 2 do ADR-0059). Tudo isso entra numa ADR depois da PoC
validada ponta a ponta.
