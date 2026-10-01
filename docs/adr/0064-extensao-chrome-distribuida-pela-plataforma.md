# ADR-0064 — Extensão do Chrome distribuída pela plataforma, com atualizador próprio nos computadores

**Status:** proposto (implementado localmente em 2026-10-01; falta deploy) · **Data:** 2026-10-01
**Relacionado:** [ADR-0043](./0043-instancia-por-municipio.md) (uma instância por município; nada
institucional em código) · [ADR-0046](./0046-rename-smsmais.md) (carve-outs de nome) ·
[ADR-0019](./0019-anexos-exame-pwa-qr-armazenamento.md) (token próprio para quem não tem login)

## Contexto

A extensão do Chrome (`SMSMais.chrome/`) é carregada **sem compactação**, em modo desenvolvedor, nos
computadores da recepção. Até aqui a distribuição era um repositório **público** no GitHub mais um
`.bat` em cada PC (`git pull`), e depois alguém clicava em ↻ no `chrome://extensions`. Três
problemas:

- **O código da extensão ficava exposto** num repositório público.
- **Atualizar dependia de gente** em cada computador, e de ter Git instalado.
- **Não havia fronteira entre desenvolver e entregar**: a pasta de desenvolvimento sobe de versão
  várias vezes por hora; o que os PCs recebem tem de ser uma decisão.

Pedido do Bernardo (30/09 e 01/10/2026), em ordem: um programa que se atualize sozinho e atualize a
extensão, recarregando-a sem clique; que instale **sem administrador** e abra no logon; com o
conteúdo da extensão **só para quem está autenticado na plataforma**; distinguindo dev de
produção, no programa e na extensão; **tudo hospedado no nosso servidor** (nada no GitHub), com um
botão "Extensão Chrome" no painel; ícone do "+" da Automais na bandeja; pasta comum a todos os
usuários do PC; e, em Sistema, um gerenciador de versões — com uma API para publicar por script,
como opção da tela.

Medido antes de decidir (Chrome for Testing 154):

- `chrome.runtime.reload()` numa extensão sem compactação **relê tudo do disco, inclusive o
  manifest**; e `fetch` do próprio `manifest.json` devolve o que está no disco. Logo a extensão
  consegue perceber sozinha que foi trocada.
- Com o **modo desenvolvedor desligado**, o Chrome **desativa** a extensão sem compactação.
- O estado da extensão é legível, sem falar com o Chrome, no `Secure Preferences` do perfil.
- Não há como um programa **sem administrador** instalar extensão no Chrome: o "Carregar sem
  compactação" continua manual, uma vez por perfil.

## Decisão

### 1. Um atualizador genérico nos computadores (`SMSMais.atualizador/`)

Programa em Rust, um executável de ≈650 KB sem dependências, **igual para todo município**. Instala
em `C:\SMSMais\` (pasta comum, criável por usuário comum), abre no logon pela chave `Run` do
usuário, e fica na bandeja com o "+" da Automais. Nada institucional dentro dele: o endereço da
plataforma e o token chegam por configuração (`config.json`), o token cifrado por DPAPI de
**máquina**.

A cada 10 minutos ele pergunta à plataforma a versão publicada; se mudou, baixa o pacote, troca os
arquivos da pasta (troca atômica por arquivo, `manifest.json` **por último**) e a própria extensão
se recarrega. Ele também se atualiza (SHA-256 conferido, executável novo ensaiado antes da troca) e
avisa quando o Chrome está aberto sem a extensão em ordem.

### 2. Quem recarrega a extensão é a extensão

`background.js` compara, a cada batimento (30 s), a versão do `manifest.json` do disco com a que
está rodando, e chama `chrome.runtime.reload()`. Não há canal entre o atualizador e o Chrome: o
sinal é o próprio arquivo. Vale também na máquina de desenvolvimento.

### 3. A plataforma hospeda tudo, e só entrega a computador autorizado

Quatro tabelas em `smsmarica`: `extensao_pacote` (o arquivo, em `bytea`), `extensao_dispositivo`,
`extensao_ativacao` e `extensao_chave_publicacao`. Rotas em `/extensao` (`ExtensaoDistribuicaoController`).

- **Autorização é de pessoa logada, para um computador.** O computador recebe um **token próprio**
  (só o hash fica no banco), revogável na tela. Não se digita senha no atualizador.
  - Pelo **instalador**: o botão "Extensão Chrome" do painel entrega o executável com um rabicho no
    fim do arquivo — `\n#SMSMAIS-INSTALADOR:{"api","codigo"}` —, onde `codigo` é um código de
    ativação **já autorizado por quem baixou**, de uso único, válido por 1 hora. A pessoa baixa,
    executa e o computador fica ligado. O atualizador se instala **sem** o rabicho.
  - Pelo **Configurar**: o atualizador pede um código, abre `/app/extensao/autorizar/<código>` no
    navegador, e alguém logado autoriza (10 minutos). O código vai no **caminho** da URL porque a
    ida ao login guarda só o pathname.
- **Baixar o instalador e autorizar um computador não pedem módulo** — basta estar logado, mesma
  regra do envio das capturas. A **administração** pede o módulo novo `ExtensaoNavegador = 80`.
- A cada consulta o computador informa o próprio estado (versão da extensão, do atualizador e a
  situação no Chrome): é o **inventário** da tela de computadores.

### 4. Dev × produção: publicar é um ato, em dois canais

O que está na pasta de desenvolvimento não chega a computador nenhum. Só chega o que foi
**publicado** (`extensao_pacote`), e a versão publicada nasce no canal de **teste**; **promovida**,
vai para todos.

- O **canal é atributo do computador na plataforma** (troca-se na tela, sem tocar o PC). Computador
  de teste recebe a mais nova publicada; de produção, a mais nova promovida.
- **Computador não rebaixa.** Por isso não se publica versão igual ou menor que a última (inclusive
  as retiradas), e o conserto de versão ruim é publicar uma maior. "Retirar" só tira de circulação.
- **O pacote é conferido ao publicar com as mesmas regras do atualizador** (manifest válido,
  arquivos que ele manda carregar presentes, caminho que não sai da pasta). Um pacote que o
  computador recusaria não chega a ser anunciado.
- O instalador é sempre o atualizador **em produção**.

### 5. API de publicação é opção da tela, com chave de uso restrito

Publicar pelo painel é o caminho normal. Quem administra pode **ligar** a publicação por API
gerando uma chave (`X-Chave-Publicacao`), mostrada uma única vez, uma ativa por vez. Ela serve só
para listar, publicar e promover (`/extensao/publicacao/*`) — **não** é um API Token do sistema e
não abre mais nada. O que entra por ela fica marcado ("pela API").

## Consequências

- **O repositório público `SMSMais/extensao-sisreg` e o `publicar.sh` deixam de ser o caminho.**
  A migração dos PCs que já têm a extensão: instalar o atualizador pelo painel (ele adota a pasta
  `C:\SMSMais\extensao-sisreg` que o `.bat` criou, sem refazer o "Carregar sem compactação") e
  carregar uma última vez uma versão ≥ 0.5.33 (a que sabe se recarregar).
- **O modo desenvolvedor passa a ser condição de funcionamento vigiada**: o atualizador avisa na
  bandeja e a tela de computadores mostra em vermelho.
- **Extensão publicada com service worker quebrado não se recarrega mais sozinha.** A correção
  chega ao disco, mas só entra no próximo ↻ manual ou quando o Chrome reabrir. Daí o canal de teste.
- **Pasta comum** = qualquer usuário do Windows daquele PC altera o que os outros usam. É o mesmo
  risco que já existia em `C:\SMSMais\extensao-sisreg`.
- **Executável sem assinatura digital**: o Windows pode pedir confirmação ao executar o arquivo
  baixado. Com assinatura, o rabicho do instalador precisa de outro transporte (o nome do arquivo),
  porque acrescentar bytes invalida a assinatura.
- O perfil Admin ganha o módulo 80 pelo seeder (roda com `AutoMigrate`); perfis próprios precisam
  ser marcados na tela de Perfis.
- Banco: os pacotes ficam em `bytea` (≈50 KB a extensão, ≈650 KB o atualizador, uma linha por
  versão). A consulta de versão usa cache de 30 s em memória.

## Alternativas consideradas

- **Chrome Web Store / política corporativa**: a loja expõe e demora; política exige administrador
  e máquina gerenciada. Recusadas (ver `docs/automacao-navegador-opcoes.md`).
- **Manter o GitHub público só para o executável**: foi o primeiro desenho; trocado pelo pedido de
  hospedar tudo no servidor — fica uma origem só, com um controle de acesso só.
- **Native Messaging** (a extensão entrega o pacote ao programa): dispensaria o token do
  computador, mas faz a atualização depender de a extensão estar viva — uma versão quebrada nunca
  mais se consertaria.
- **API Token do sistema para publicar**: abre a API inteira. A chave própria faz uma coisa só.
- **C# com AOT** no lugar de Rust: funcionaria; Rust deu o executável menor, sem runtime e sem
  Visual C++ Redistributable nos PCs.

## Como foi verificado (01/10/2026)

- Atualizador: 29 testes; ensaio de ponta a ponta numa raiz isolada (instalar, autorizar, baixar,
  canais, auto-atualização com recusas, revogação, modo desenvolvedor desligado, desinstalar).
- Backend: 29 testes contra Postgres real (bancada), rodados duas vezes seguidas.
- **O atualizador real contra a API real** (subida localmente com `Workers:Desligados`, apontada
  para a bancada): instalador baixado como usuário logado, autorização pelo rabicho e pelo
  Configurar, publicação e promoção pela API de publicação, troca de canal, auto-atualização,
  revogação.
- Painel: as três telas navegadas num navegador de teste, publicando e promovendo pela tela.
- **Não verificado**: PC real da recepção (antivírus, proxy, disco trancado) e dois usuários do
  Windows logados ao mesmo tempo.
