# SMSMais · Atualizador

Programa pequeno (≈650 KB, um executável só, sem dependência nenhuma) que fica residente no PC e
mantém a **extensão do Chrome** — e a si mesmo — em dia. Ícone **"+"** perto do relógio.

- **Não pede administrador**: instala na pasta comum `C:\SMSMais\`, abre sozinho a cada logon pela
  chave `Run` do próprio usuário, nunca abre o UAC.
- **Genérico**: o executável é igual para todo município. O que é de cada instalação (endereço da
  plataforma, token do computador) fica no `config.json`.
- **Tudo hospedado na plataforma**: o instalador, o conteúdo da extensão e as versões novas do
  atualizador saem da API da plataforma, e só para computador autorizado. Nada em repositório
  público.

> Estado em 01/10/2026: programa, API e telas do painel **prontos e ensaiados juntos**, localmente
> (o atualizador de verdade contra a API de verdade). **Nada foi commitado nem publicado em
> produção.** A decisão está no [ADR-0064](../docs/adr/0064-extensao-chrome-distribuida-pela-plataforma.md).

## Como a pessoa usa

1. No painel, logada, clica em **Extensão Chrome** e baixa o instalador.
2. Executa. O instalador já chega sabendo o endereço da plataforma e com um código de autorização
   de uso único (emitido para quem clicou): **não se digita nada**. Ele se instala, autoriza o
   computador e baixa a extensão para `C:\SMSMais\extensao`.
3. **Só na primeira vez**, no Chrome: `chrome://extensions` → ligar **Modo do desenvolvedor** →
   **Carregar sem compactação** → escolher `C:\SMSMais\extensao`. A janela do atualizador mostra
   esses passos e abre a pasta.

Daí em diante é sozinho: a cada 10 minutos o atualizador pergunta à plataforma a versão publicada;
se mudou, baixa, troca os arquivos e a extensão **se recarrega sozinha** em até 30 s. Um aviso do
Windows sai do ícone ("Extensão atualizada — v…") pedindo o F5 nas páginas abertas.

Em PC que já tem a pasta do `.bat` antigo (`C:\SMSMais\extensao-sisreg`), o atualizador **continua
nela** — o Chrome já carrega de lá, não precisa "Carregar sem compactação" de novo.

## O ícone na bandeja

Clique (qualquer botão) abre o menu: a versão do atualizador, a situação da extensão, **Verificar
agora**, **Abrir a pasta da extensão**, **Abrir o registro**, **Configurar…** e **Sair**.

No Windows 11 o ícone nasce dentro dos "ícones ocultos" (a setinha **^**). Para deixá-lo sempre à
vista, arraste-o para a barra — é do Windows, o programa não consegue fazer isso sozinho.

O atualizador também avisa quando **o Chrome está aberto sem a extensão em ordem**: não carregada,
desativada, ou com o modo desenvolvedor desligado (sem ele o Chrome desativa a extensão). Ele não
fala com o Chrome: só **lê** o arquivo `Secure Preferences` do perfil.

## Dev × publicado (canais)

O que está na árvore de desenvolvimento **não chega a PC nenhum**. Só chega o que foi **publicado
na plataforma**, e em dois canais:

| Canal | Quem recebe |
|---|---|
| `teste` | só os computadores que a plataforma marcou como de teste |
| `prod` | todos |

O canal é um **atributo do computador na plataforma** (muda-se no painel, sem encostar no PC).
Computador em `teste` recebe a mais nova entre `teste` e `prod`. Vale igual para a extensão e para
o próprio atualizador: publica-se em `teste`, confere-se num PC de teste, promove-se a `prod`.

Nunca se rebaixa: versão igual ou menor que a instalada não é atualização.

## Publicar uma versão

Tudo pelo painel, em **Sistema → Extensão Chrome → Versões** (módulo de permissão `ExtensaoNavegador`):

1. **Gerar o arquivo.**
   - Extensão: `bash SMSMais.chrome/empacotar.sh` → `SMSMais.atualizador/dist/extensao-<versão>.zip`
     (a versão é a do `manifest.json`; o script recusa pacote com arquivo faltando).
   - Atualizador: subir a versão no `Cargo.toml`, `cargo build --release` →
     `target/release/smsmais-atualizador.exe`.
2. **Publicar versão** na tela (o atualizador pede a versão; a da extensão é lida do pacote). Entra
   em **teste**.
3. Conferir num computador de teste (aba Computadores → *Passar para teste*; no PC, *Verificar agora*).
4. **Pôr em produção.** Na primeira publicação do atualizador é preciso promover logo: o
   instalador que o painel entrega é o atualizador em produção.

**Por script (opcional):** na aba *Publicação por API* gera-se uma chave que só publica e promove.

```bash
curl -X POST $API/extensao/publicacao/pacotes -H "X-Chave-Publicacao: $CHAVE" \
  -F artefato=Extensao -F arquivo=@SMSMais.atualizador/dist/extensao-0.5.33.zip
curl -X POST $API/extensao/publicacao/pacotes -H "X-Chave-Publicacao: $CHAVE" \
  -F artefato=Atualizador -F versao=0.1.0 -F arquivo=@SMSMais.atualizador/target/release/smsmais-atualizador.exe
curl -X POST $API/extensao/publicacao/pacotes/<id>/promover -H "X-Chave-Publicacao: $CHAVE"
```

## Onde as coisas ficam

```
C:\SMSMais\
  atualizador\
    smsmais-atualizador.exe   o programa instalado (sem o rabicho do instalador)
    config.json               pasta_extensao, plataforma_api, token_protegido, intervalo_minutos
    estado.json               arquivos que ele pôs na pasta da extensão; canal informado pela plataforma
    atualizador.log           registro (roda aos 512 KB)
  extensao\                   a pasta que o Chrome carrega
```

- A pasta é **comum a todos os usuários do PC**: uma instalação, uma autorização e uma extensão
  por computador. Cada usuário do Windows só precisa executar o instalador uma vez (para o início
  automático dele) e fazer o "Carregar sem compactação" no Chrome dele.
- Se o disco estiver trancado (não deixa criar `C:\SMSMais`), cai em `%LOCALAPPDATA%\SMSMais`.
- O **token** fica no `config.json` **cifrado pelo Windows para este computador** (DPAPI, escopo
  de máquina): qualquer usuário daquele PC usa; copiado para outro PC, não abre.
- O `config.json` é relido a cada ciclo — mudar o arquivo vale sem reiniciar.

## Linha de comando

```
smsmais-atualizador                     duplo clique: instala; já instalado, fica residente
smsmais-atualizador --instalar [--pasta DIR] [--silencioso]
smsmais-atualizador --configurar [ENDERECO] [--sem-navegador]
smsmais-atualizador --agora             pede ao residente para conferir agora
smsmais-atualizador --uma-vez           confere e atualiza neste processo, e sai
smsmais-atualizador --status
smsmais-atualizador --desinstalar
smsmais-atualizador --versao
```

`--configurar` é o caminho para quando o instalador não veio do painel (ou o código venceu): o
atualizador pede um código à API, abre a página do painel e espera alguém logado clicar em
autorizar. Aceita o endereço do painel ou o da API, com ou sem `https://`.

## Contrato com a plataforma

O que o atualizador usa da API (`ExtensaoDistribuicaoController`, no `SMSMais.server`; as rotas de
administração e de publicação estão no ADR-0064 e no OpenAPI):

| Rota | Quem chama | O que faz |
|---|---|---|
| `GET /publico/instituicao` | atualizador | confirma que o endereço é a plataforma e dá o nome |
| `GET /extensao/instalador` (botão **Extensão Chrome** do painel) | pessoa logada | devolve o executável + o **rabicho** (abaixo) |
| `POST /extensao/dispositivos/ativacoes` `{computador, versaoAtualizador}` | atualizador | `200 {codigo, urlAutorizar, expiraEmSegundos, intervaloSegundos}` |
| página `/app/extensao/autorizar/<código>` (o `urlAutorizar`) | pessoa logada | "Autorizar este computador" |
| `POST /extensao/dispositivos/ativacoes/token` `{codigo, computador, versaoAtualizador}` | atualizador | `200 {token}` · `202` aguardando · `410` recusada, usada ou vencida |
| `GET /extensao/versao?instalada=&atualizador=&chrome=` | atualizador (Bearer) | `{version, canal}` — e recebe o **inventário** do PC |
| `GET /extensao/pacote` | atualizador (Bearer) | `.zip` da extensão |
| `GET /extensao/atualizador/versao` | atualizador (Bearer) | `{versao, sha256}` |
| `GET /extensao/atualizador/pacote` | atualizador (Bearer) | o executável (sem rabicho) |

- **Bearer** = o token do computador. `401`/`403` = computador revogado: o atualizador avisa e
  pede o Configurar de novo.
- **Rabicho do instalador**: o servidor acrescenta ao fim do executável
  `\n#SMSMAIS-INSTALADOR:{"api":"https://api…","codigo":"…"}`. O `codigo` é um código de ativação
  já autorizado (quem baixou estava logado), de **uso único** e validade curta. Ao instalar, o
  atualizador troca o código pelo token e grava o executável **sem** o rabicho.
- **Inventário**: a cada consulta o PC diz a versão da extensão instalada, a do atualizador e a
  situação no Chrome (`carregada`, `nao-carregada`, `desativada`, `modo-dev-desligado`, `fechado`,
  `sem-perfil`). É o que permite ao painel mostrar "PC tal está com o Chrome aberto sem a extensão".
- **Pacote da extensão**: um `.zip` com os arquivos (pode ter uma pasta de cima, como o "Download
  ZIP" do GitHub). O atualizador ignora `.bat`, `.exe`, `.cmd`, `.ps1` e o que começa com ponto, e
  **recusa** o pacote que não se sustenta: sem `manifest.json`, manifest inválido, arquivo que o
  manifest manda carregar e não veio, caminho que sai da pasta.
- Só `https` (HTTP puro só para `127.0.0.1`/`localhost`, que é o ensaio).

## Garantias

- **Troca atômica por arquivo** (grava ao lado e renomeia) e `manifest.json` **por último**: o
  Chrome nunca lê arquivo pela metade, e a extensão só recarrega com tudo no lugar.
- **Só apaga o que ele mesmo pôs**: arquivo que saiu de uma versão para a outra some; arquivo
  alheio na pasta não é tocado.
- **Auto-atualização conferida**: SHA-256 do publicado, e o executável novo tem de abrir nesta
  máquina e responder a versão anunciada antes de virar o oficial. Falhou qualquer coisa, fica o
  que estava.
- **Um residente por sessão**; com dois usuários logados ao mesmo tempo, só um troca os arquivos
  por vez, e o outro renasce do executável novo quando percebe que o disco mudou.
- **Rede do Windows** (WinINet): respeita o proxy e os certificados do PC, como o navegador.
- Erro repetido vira **uma** linha no registro (PC sem internet por horas não enche o log).

## Limites conhecidos

- **O "Modo do desenvolvedor" do Chrome tem de ficar ligado.** Desligado, o Chrome desativa a
  extensão sem compactação. O atualizador avisa, mas não consegue religar.
- **O "Carregar sem compactação" é manual, uma vez por perfil do Chrome.** Não há como um programa
  sem administrador instalar extensão no Chrome.
- **Páginas já abertas** seguem com o código antigo até o F5 (limite do Chrome).
- **Extensão publicada com o service worker quebrado não se recarrega mais sozinha** (quem vigia a
  versão é o service worker). O atualizador entrega a correção no disco, e ela entra no próximo
  ↻ manual ou quando o Chrome reabrir.
- **Executável sem assinatura digital**: ao executar um arquivo baixado pelo navegador, o Windows
  pode mostrar "O Windows protegeu o computador" (Mais informações → Executar assim mesmo). Um
  certificado de assinatura de código resolve — e, com assinatura, o rabicho do instalador precisa
  de outro transporte (o nome do arquivo, por exemplo), porque acrescentar bytes invalida a
  assinatura.
- **Pasta comum** = qualquer usuário do PC consegue alterar o executável e a extensão que os
  outros usam. É o mesmo que já valia para `C:\SMSMais\extensao-sisreg`; em PC com usuários que
  não confiam uns nos outros, não serve.

## Compilar

```bash
cd SMSMais.atualizador
cargo test                    # 29 testes
cargo build --release         # target/release/smsmais-atualizador.exe
python recursos/gerar-icone.py <logo-automais.png>   # só se o ícone mudar (precisa do Pillow)
```

Rust (toolchain `x86_64-pc-windows-msvc`) com as ferramentas de build do Visual Studio. O CRT é
estático: o executável não precisa do Visual C++ Redistributable. A versão é a do `Cargo.toml`.

| Arquivo | Papel |
|---|---|
| `src/main.rs` | linha de comando, instalar, configurar, o laço do residente |
| `src/extensao.rs` | conferir a versão, abrir o pacote, trocar os arquivos |
| `src/proprio.rs` | auto-atualização e o rabicho do instalador |
| `src/plataforma.rs` | a conversa com a API (ativação, download autenticado) |
| `src/navegador.rs` | a extensão está carregada no Chrome? (leitura do perfil) |
| `src/bandeja.rs`, `src/dialogo.rs` | ícone, menu, avisos e a janela do endereço |
| `src/config.rs` | `config.json` e `estado.json` |
| `src/sistema.rs` | Windows: pastas, registro, cofre, instância única, log |
| `src/rede.rs` | HTTP por WinINet |
| `recursos/` | o ícone "+" e o script que o gera |

Para ensaiar sem encostar na instalação de verdade, a variável `SMSMAIS_RAIZ` troca a raiz (e o
nome do valor no registro, do mutex e dos eventos ganham um sufixo).

## Como foi ensaiado (30/09 e 01/10/2026)

Numa raiz isolada, contra um servidor local que implementa o contrato acima e um Chrome for
Testing 154 com a extensão carregada da pasta mantida pelo atualizador:

| Ensaio | Resultado |
|---|---|
| Instalador "do painel" (executável + rabicho) | instalou, autorizou sem digitar nada, gravou o executável limpo; token cifrado no arquivo |
| Mesmo código de ativação usado de novo | `410` — não autoriza duas vezes |
| Configurar pelo navegador (pedir código, autorizar, receber token) | token guardado 1 s depois do clique em autorizar |
| Versão publicada só em `teste`, PC em `prod` | não recebeu |
| PC movido para `teste` no servidor | recebeu; extensão recarregada sozinha em 13 s; `chrome://extensions` mostra a versão nova |
| Arquivo que saiu de uma versão para a outra | apagado; arquivo alheio na pasta, intocado |
| Auto-atualização com SHA-256 errado | recusada, ficou a versão instalada |
| Auto-atualização anunciando versão diferente da do executável | recusada |
| Auto-atualização legítima (0.1.0 → 0.1.1 → 0.1.2) | trocou, renasceu, limpou o `.antigo` no ciclo seguinte |
| Modo desenvolvedor desligado no Chrome | detectado, avisado e informado à plataforma |
| Computador revogado (`401`) | aviso claro; voltou sozinho quando a plataforma liberou |
| Residente ligado por horas e 8 ciclos seguidos | 2,7 MB de memória privada, 292 handles, estável |
| Desinstalar | início automático removido, residente encerrado, pasta apagada; extensão intocada |

Depois, **o atualizador de verdade contra a API de verdade** (o `SMSMais.server` subido localmente
com as rotinas de fundo desligadas, apontado para a bancada de testes):

| Ensaio | Resultado |
|---|---|
| Rotas sem credencial, com token inventado, com chave inventada | `401` em todas |
| Chave de publicação gerada; publicar e promover por ela | atualizador 0.1.0 e extensão 0.5.33 em produção, marcados "pela API" |
| Instalador antes de haver atualizador em produção | `409` |
| `GET /extensao/instalador` como usuário logado → executar | computador autorizado, extensão 0.5.32 → 0.5.33 no disco |
| Mesmo instalador num "segundo PC"; instalador publicado como atualizador | `410`; recusado com o motivo |
| Extensão 0.5.34 publicada; PC de produção | não recebeu; movido para teste pela API de administração, recebeu |
| Atualizador 0.1.1 publicado em teste | o PC se atualizou sozinho |
| Configurar: pedido → página do painel → autorizar | autorizado |
| Revogar o computador | o atualizador avisou e parou de receber |
| Painel, num navegador de teste | publicar e pôr em produção pela tela; recusa dentro do modal; aba Computadores com o inventário; página de autorizar; chave da API mostrada uma vez |

Não foi ensaiado: PC real da recepção (antivírus, proxy da prefeitura, disco trancado), dois
usuários do Windows logados ao mesmo tempo, e o clique em *Configurar…* no menu do ícone com o
código final (a janela foi aberta e conferida; o fluxo que ela dispara foi ensaiado pela linha de
comando).
