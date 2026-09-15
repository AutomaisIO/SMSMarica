# SMSMais · Ponte SISREG — extensão Chrome

Registra no SMSMais as operações que o usuário faz no SISREG, para **antecipar** o que hoje só
chega pela varredura diária. Nesta fase de piloto o objetivo é **capturar tudo** (envio e retorno)
para depois a gente analisar e decidir os endpoints definitivos.

Regra de ouro: a extensão **só observa** o SISREG. Nunca dispara requisição para lá — não gasta o
orçamento anti-robô nem abre sessão. Tudo que ela registra foi o próprio usuário quem fez, com a
senha dele.

## O que ela faz (v0.2.0)

1. **Bloqueia o SISREG (blur) até o SMSMarica estar conectado.** A extensão "acha" a sessão do
   painel `smsmarica.online` já aberta/logada no navegador; se não houver, o modal pede o login
   (o login é feito **no nosso site**, nunca dentro da página do SISREG).
2. **Selo discreto** no canto, com a marca do SMSMarica e um **LED** de status:

   | LED | Significado |
   |---|---|
   | cinza | sem sessão no SMSMarica |
   | verde | conectado |
   | âmbar piscando | enviando capturas |
   | verde com halo | acabou de registrar |
   | vermelho | SMSMarica fora de alcance (as capturas ficam guardadas) |

3. **Captura** cada operação: o **envio** (URL + campos do formulário, com `etapa` e o evento de
   negócio — cancelamento, confirmação, falta…) e o **retorno** (o HTML da tela que carregou e os
   corpos de AJAX do `sisreg_ajax`).
4. **Envia em lote** (comprimido) para a nossa API, autenticando com o **mesmo login** do painel.

O que o usuário final vê é só o selo com o LED. A lista de requisições existe para depuração e
fica escondida: **alt+clique no selo** abre/fecha.

## Decisões desta fase

- **API fora do ar → o SISREG continua liberado** (o blur exige o login, não a API). As capturas
  ficam guardadas na memória da extensão e são reenviadas quando a API volta.
- **Guardar tudo**, sem expurgo — é material para o agente analisar; depois se descarta.
- **Só modo desenvolvedor**, sem distribuição em massa por enquanto.

## Instalar (modo desenvolvedor)

1. `chrome://extensions` → ligar **Modo do desenvolvedor**.
2. **Carregar sem compactação** → escolher a pasta `SMSMais.chrome/`.
3. Fixar o ícone na barra.

Ao editar arquivos: **↻** no card da extensão; para o painel/blur (`content.js`), dar **F5** na
aba do SISREG também.

## Configurar (antes de testar)

`config.js`:
- `API_BASE` — para onde as capturas vão. Padrão `http://localhost:5080` (backend na sua máquina).
- `PAINEL_ORIGIN` — de onde a extensão lê o login. Padrão `https://smsmarica.online`.

## Como funciona (arquivos)

| Arquivo | Papel |
|---|---|
| `manifest.json` | MV3; hosts do SISREG, do painel e da API |
| `config.js` | endereços e parâmetros de lote |
| `endpoints.js` | catálogo de endpoints/etapas conhecidos e campos mascarados (`senha`) |
| `background.js` | o cérebro: guarda o login, observa o tráfego, envia os lotes, mantém o LED |
| `capture-hook.js` | roda no mundo da página (todos os frames) para copiar as respostas de fetch/XHR |
| `content.js` | roda em todos os frames: captura o HTML da tela; no frame de cima desenha o selo e o blur |
| `auth-content.js` | roda em `smsmarica.online`: lê a sessão do painel e entrega ao cérebro |
| `popup.*` | status rápido ao clicar no ícone |

### Por que o envio sai do "cérebro", e não da página

Enviar de dentro da página do SISREG seria bloqueado pela política de segurança dela (CSP) e pelo
CORS. O service worker não sofre nenhum dos dois (tem permissão do host), então é ele quem fala
com a nossa API. A senha do SMSMarica só é digitada no nosso site; a extensão nunca a vê.

## Testar

1. ↻ na extensão e F5 no SISREG. O selo deve mostrar **v0.2.0** no popup.
2. Sem sessão no SMSMarica: o SISREG aparece **borrado** com o modal "Entrar no SMSMarica".
3. Clicar em **Entrar** → logar no painel → o blur some e o LED fica **verde**.
4. Navegar/operar no SISREG → o LED pisca **âmbar** ao enviar. Alt+clique no selo para ver as
   capturas.
5. Sem backend rodando, o LED fica **vermelho** e as capturas se acumulam (é o esperado).

## Armadilhas já vistas

- **Cada pessoa com o SEU operador do SISREG — nunca o do robô de produção** (sessão única).
- **Sessão do SISREG derrubada não muda a URL:** `/cgi-bin/index` vira a tela de login com "logon
  em outra estação de trabalho".
- O iframe principal é `id="f_main"` **com `name="f_principal"`**.

## Pendente (backend)

A rota `POST /extensao/sisreg/capturas` **ainda não existe** — é o próximo passo (tabela nova,
permissão nova e a habilitação de descompressão do corpo no servidor). Enquanto isso, o LED do
piloto fica vermelho e as capturas ficam só na memória da extensão.
