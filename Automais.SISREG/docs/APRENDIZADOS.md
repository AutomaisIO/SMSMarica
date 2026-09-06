# SISREG III — aprendizados da integração

> Laboratório isolado (Python) para mapear o SISREG antes de portar o motor
> para o `SMSMais.server`. Fonte: `https://sisregiii.saude.gov.br`.

## Versão do sistema (observada em 2026-07-01)

- **SISREG III v3.4** — `Versão: 3.4.2026.06` (rodapé) / build `Jun 3 2026`.
- Servidor de **Produção** (`<TITLE>SISREG III - Servidor de Producao</TITLE>`).
- Stack legada: HTML 4.01 Frameset, CGI (`/cgi-bin/...`), jQuery + jQuery-UI.
- Libs internas versionadas: `libcadsus`, `libacesso`, `libmenucache`,
  `libsisregconfig` (todas `2026.06`).

## Login (tela `index.c`)

Form clássico, **POST para `/`**, sem widget de captcha no próprio login:

| Campo       | Valor enviado                                             |
|-------------|----------------------------------------------------------|
| `usuario`   | operador (exibido em MAIÚSCULAS via CSS `text-transform`) |
| `senha`     | **vazio** (limpo pelo JS antes do submit)                |
| `senha_256` | `sha256( senha.toUpperCase() )` em hex minúsculo         |
| `etapa`     | `ACESSO`                                                 |
| `logout`    | vazio (no login) / `"1"` (no logout)                     |

O JS relevante da página:

```js
frm.senha_256.value = hex_sha256( frm.senha.value.toUpperCase() );
frm.senha.value = '';
frm.submit();
```

→ Em Python: `hashlib.sha256(senha.upper().encode()).hexdigest()`
→ Em C# depois: `Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(senha.ToUpper()))).ToLower()`

### ✅ Mecânica validada (2026-07-01)
Rodamos `python login_test.py` contra produção: o motor faz priming → hash →
POST e o SISREG **processa como login real**. Com credencial incorreta ele
devolve a própria tela de login com, no `<div id="mensagem">`:

> **"Login ou senha incorreto(s)."**

Ou seja: POST/hash/cookies/sessão estão **corretos**; só falta a credencial
válida. O client extrai essa mensagem (`_extrair_mensagem`) e a reporta.
Nenhum captcha foi exigido no login (confirmado — sem `g-recaptcha` na resposta).

### 🎉 Login efetivado (2026-07-01)
Com a credencial correta, o POST redireciona para **`/cgi-bin/index`** (a "home"
logada, len ~10,8 KB). Sinais de sucesso confiáveis:
- URL final contém `/cgi-bin/index`;
- HTML tem o iframe principal `id="f_main"` (`name="f_principal"`, `src=/cgi-bin/avisos`);
- barra superior "**Operador:** … **Perfil:** … **Unidade:** …".
- cookies `SESSION` e `ID` passam a vir **preenchidos**.

⚠️ A home logada **mantém** um `formLogin`/`senha_256` escondido (para logout/re-login),
então NÃO dá pra detectar sucesso pela ausência desses campos — usar os sinais acima
(`_login_ok` no client). Falha de credencial volta em `/` com o `<div id="mensagem">`
preenchido ("Login ou senha incorreto(s).").

Perfil observado do operador de teste: **EXECUTANTE/SOLICITANTE** (unidade CDT).
Logout: `GET /?logout=1` (ou POST com `logout=1`).

### ⭐ Existe API OFICIAL do SISREG
O menu logado tem **"Acesso a Api"** →
`https://wiki.saude.gov.br/SISREG/index.php/Página_principal#Acesso_a_API`.
**Avaliar antes de investir em scraping** — API oficial = contrato estável e sem
quebra a cada mudança de tela. Scraping vira fallback.

### Mapa de menus / endpoints (home logada)
Todos sob `/cgi-bin/`. `#` = submenu (dropdown), sem navegação direta.

| Menu | Endpoint |
|------|----------|
| **solicitar** (marcar) | `cadweb50?url=/cgi-bin/marcar` |
| Cancelar Solicitações | `cons_verificar` |
| Preparos | `config_preparo` |
| CNS (Cartão SUS / cadweb) | `cadweb50?standalone=1` |
| Impressão/Confirmação de Agendas | `cons_agendas` |
| **Solicitações** (gerenciador) | `gerenciador_solicitacao` |
| Agendamentos / Data Solicitação | `rellst.pl` |
| Agendados pela Fila de Espera | `cons_fila_espera` |
| Agendados pela Regulação | `cons_marcados_reg` |
| Devolvidos pela Regulação | `cons_negados_reg` |
| PPI / Cotas | `cons_ppi_cotas` |
| Unidades | `cons_unidade` |
| Escalas | `cons_escalas` |
| Grupos / Procedimentos | `cons_pa` |
| Tabela SIGTAP | `cons_procedimento.pl` |
| Prontuários a Enviar / Receber | `cons_prontuario_enviar` / `cons_prontuario_receber` |
| ~~Solicitações Pendentes na Fila de Espera~~ | ~~`cons_pendente_fila_`~~ **⚠️ ERRADO — dá 404. O real é `rel_fila_espera_mun_pendentes.pl`; ver §FILA DE ESPERA** |
| Arquivo Agendamento (txt) | `expo_solicitacoes` |
| Solicitações não confirmadas/Unidade | `rel_amb_faltas_sol.pl` |
| Geração de arquivo BPA (txt) / Consulta | `expo_bpa_v2` / `cons_bpa_v2` |
| Perfil (config) | `config_perfil` |
| Logoff Videofonista | `ctrl_videofonista` |

Frames: topo `/barra-brasil.html`; conteúdo `f_principal` → `/cgi-bin/avisos`.
Boa parte das telas provavelmente carrega dentro do iframe `f_principal`.

### Captcha
- `https://www.google.com/recaptcha/api.js` é carregado na página, **mas não há
  `div.g-recaptcha` no formulário de login** → aparentemente o captcha só é usado
  na tela **"Esqueceu sua senha?"** (`/cgi-bin/recuperar_senha`).
- CSP libera `hcaptcha.com` e `google.com/gstatic` — confirmar se o captcha
  aparece após N tentativas falhas ou em algum fluxo interno. **A validar com login real.**

### Cookies / sessão
- No GET inicial o servidor já seta e expira `SESSION`/`ID` (priming) e cria um
  cookie de WAF `TS01...` (BIG-IP ASM). Manter o mesmo client/cookie jar entre
  o GET de priming e o POST de login.
- Header de segurança: `X-Content-Type-Options: nosniff` + CSP restritiva.

## Abordagem escolhida

**HTTP puro** (`httpx` + `BeautifulSoup`), porque:
1. Login é POST form-encoded simples, sem captcha visível.
2. Hash de senha é reproduzível trivialmente (sha256).
3. Portabilidade direta para `HttpClient` no .NET (motor final vai pro server).

Fallback para Playwright só se aparecer captcha obrigatório ou JS pesado em telas internas.

## 🎯 Telas de agendamento (objetivo: agendamentos confirmados/autorizados)

### `cons_marcados_reg` — "Agendados pela Regulação" ✅ ALVO PRINCIPAL
POST, `etapa=LISTAR_SOLICITACOES`. Retorna as marcações da unidade com **todos os
dados que precisamos**. Colunas da tabela `table_listagem` (11 col):

`Código Solicitação | CNS | Usuário | Endereço | Telefone | Procedimento |
Profissional Executante | Unidade Executante | Data da Execução | Hora da Execução | Avisado`

Parâmetros:
| Campo | Valores |
|-------|---------|
| `etapa` | `LISTAR_SOLICITACOES` |
| `unidade` | CNES (hidden, fixo no perfil = `3132358`) |
| `tp_periodo` | `sol` (data solicitação) \| `aut` (autorização) \| `exe` (execução/atendimento) |
| `dt_inicial` / `dt_final` | `dd/mm/aaaa` |
| `cns_paciente`, `cod_procedimento`, `ds_procedimento` | filtros opcionais |
| `pagina` | índice de página (paginação; ver abaixo) |
| `ordenacao`, `co_solicitacao` | controle |

- **Paginação:** rodapé "Página X **de N**"; 10 registros/página. Campo hidden
  `pagina` é **0-based** (JS `exibirPagina(index, N)`; seta "próxima" = `exibirPagina(1,N)`).
  Iterar `pagina` de `0..N-1`. Total exibido em `SOLICITAÇÕES PESQUISADAS (n)`.
- Ficha detalhe: `etapa=EXIBIR_FICHA` + `co_solicitacao`.
- ✅ **Extrator completo validado** (`extrair_marcados.py`): `tp=exe` 01–31/07/2026
  → **334/334** registros, 34 páginas, **9 unidades executantes** (Ernesto Che
  Guevara 153, CDT 98, Ambulatório Péricles 49, DIMAGEM 20, Conde Modesto Leal 6,
  Radiologia Maricá 3, Radiocenter 2, Materno Infantil 2, Reabilitação 1).
- **Nuance de escopo:** `unidade=3132358` é o **SOLICITANTE** (CDT). Os resultados
  abrangem execuções em várias unidades do município, mas o recorte é "o que o CDT
  solicitou". Cobrir 100% do município (incluindo solicitações de outras unidades)
  pode exigir perfil mais amplo ou iterar por solicitante. **A confirmar com o usuário.**

### `cons_agendas` — "Impressão/Confirmação de Agendas" ✅ AGENDA DO CDT (executante)
**É a tela que dá "o que está agendado PARA o CDT"** (menu CONSULTA AMB →
IMPRESSÃO/CONFIRMAÇÃO DE AGENDAS). Modelo encadeado e **3 campos obrigatórios**:

> **Unidade executante (`ups`) → Profissional (`cpf`) → Procedimento (`pa`)**

Ao escolher a unidade, o `onChange` dispara AJAX que popula o profissional; ao
escolher o profissional, outro AJAX popula os procedimentos:
- `GET /cgi-bin/sisreg_ajax?BUSCA=PROFISSIONAIS_POR_UPS&AJAX_UPS=<cnes>` → XML de profissionais.
- `GET /cgi-bin/sisreg_ajax?BUSCA=PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS&AJAX_CPF=<cpf>&AJAX_UPS=<cnes>` → procedimentos.

⚠️ **Pegadinha que nos custou horas:** o `PROFISSIONAIS_POR_UPS` só retorna a
lista **com sessão plenamente válida**. Via `httpx` voltou `<ROOT></ROOT>` vazio
**porque o login do humano no navegador tinha derrubado a sessão do script**
(sessão única). No navegador (sessão fresca) veio a lista completa (~90 prof.).

Campos do POST de listagem: `co_solicitacao`, `cns_paciente`, `dataInicial`/
`dataFinal` (dd/mm/aaaa), `ups`, `cpf`, `pa`, `cmbTipoOperacao`
(Confirma/Falta/Consulta), `chkboxExibirProcedimentos`/`ExibirTelefones`,
`cmbOrdenacao` (1=data/hora), `cmbMaxResults` (10/25/50), `etapa`
(`ListaConsulta`/`ListaConfirma`/`ListaImpressao`/`ListaFalta`;
`Confirma`/`Falta` = **escrita, não usar sem OK**), `pagina` (0-based), `linhas`.

**PROPRIEDADES DA AGENDA** (cabeçalho do resultado) + linhas com: Código
Solicitação, CNS, Paciente, Nascimento, Idade, Unidade Solicitante, Vaga
Solicitada/Consumida, CID-10, Origem, **Data/Hora** (ex.: `01/07/2026 - QUA - 08:00`),
Telefone(s). Ordenável por data/hora, nome, nascimento, CNS, CID, situação, etc.

Códigos operacionais do CDT (profissional/procedimentos) → `capturas/cons_agendas_referencia.md`
(gitignored, contém CPF). No CDT a agenda de mamografia fica sob o profissional
**MARCO ANTONIO DE OLIVEIRA APPOLINARIO**, procedimentos MAMOGRAFIA BILATERAL / GRUPO-MAMOGRAFIA.

### ✅ MATERIALIZAR A AGENDA INTEIRA DE UMA UNIDADE (2026-07-25)

Script: **`extrair_agenda_unidade.py`** (somente leitura, `etapa=ListaConsulta`).

```bash
python extrair_agenda_unidade.py 25/07/2026 31/07/2026 3132358
```

**Os 3 filtros são obrigatórios NO SERVIDOR, não só no JS.** POST com `ups`
preenchido mas `cpf`/`pa` vazios responde *"A pesquisa não retornou nenhum
resultado"* — não existe atalho "toda a agenda de uma vez". Logo, materializar
a agenda = varrer o produto cartesiano:

```
para cada profissional da unidade      (AJAX PROFISSIONAIS_POR_UPS)
  para cada procedimento do profissional (AJAX PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS)
    para cada página                     (POST etapa=ListaConsulta, pagina 0-based)
```

Dedup por **código de solicitação**: os procedimentos `GRUPO - X` repetem os
itens individuais, então a mesma solicitação aparece em mais de uma combinação.

**Rendimento medido (CDT 3132358, 25→31/07/2026):** 92 profissionais →
256 combinações prof×proc → **352 requisições HTTP → 753 agendamentos únicos
em 47s**. Muitos profissionais não têm procedimento com agenda — isso é normal,
não é falha de sessão.

**Anatomia do registro** — cada agendamento é uma `<table id="tblConsulta<codigo>">`
com 3 `<tr>`:
- tr0: Código Solicitação (no `id`), CNS, Paciente, Nascimento, Idade, Origem, Telefone(s)
- tr1: Unidade Solicitante (+CNES entre parênteses), Vaga Solicitada, Vaga Consumida,
  CID-10, **Data/Hora** (`28/07/2026 - TER - 08:00`), Situação
- tr2: Procedimento(s)

Situações observadas: `Agendamento/Pendente Confirmação/Executante`,
`Agendamento/Confirmado/Executante`, `Agendamento/Falta/Executante`.
Vaga consumida: `RESERVA` | `1ª VEZ` | `RETORNO`.
Total de páginas: texto **"Mostrando Página [n] de N"**; JS `Pagina(index, maxcount)`.

**Encoding (pegadinha):** as telas HTML são ASCII com entidades (`&ccedil;`),
mas o **XML do `sisreg_ajax` é UTF-8 de verdade** — decodificar o AJAX como
latin-1 corrompe nomes de procedimento (`AVALIAÇÃO` → `AVALIAÃÃO`).

### 🚨 CAPTCHA ANTI-BOT (descoberto 2026-07-25) — LIMITE REAL DO SCRAPING

Depois de **~700 requisições** na mesma sessão/IP, o SISREG passa a responder
**todas** as telas com `<SCRIPT>window.location="./recaptcha?cod=0"</SCRIPT>`
(reCAPTCHA, sitekey `6LeMZwgsAAAAAK85NcnUrumUNjhKA2qR2iWXklw4`).

Sintomas e diagnóstico:
- O `sisreg_ajax` **não** mostra o captcha: devolve `<ROOT></ROOT>` **vazio e
  silencioso** — idêntico ao sintoma de sessão única derrubada. Para distinguir,
  fazer um GET numa tela HTML (`cons_agendas`) e procurar `recaptcha`.
- **Relogar NÃO resolve** — o login continua funcionando (a barra "Operador:"
  aparece normal), mas toda tela cai no captcha.
- Só um **humano resolvendo o reCAPTCHA no navegador** com esse operador libera.

Mitigações no script: `PAUSA_SEGUNDOS` (throttle, default 0.35s) entre
requisições, `CaptchaExigido` abortando com mensagem clara, e o `.jsonl`
**incremental** — o parcial é sempre preservado e o resumo final diz
`COBERTURA PARCIAL` com o profissional exato onde parou.

→ **Implicação de arquitetura:** varredura full de período longo (2 meses ≈
2.000+ requisições) **não passa** sem esbarrar no captcha. Para produção:
janelas curtas (1 semana), execução espaçada, e/ou preferir o
**Arquivo Agendamento TXT** (`expo_solicitacoes`) para carga em massa.

### `expo_solicitacoes` — "Arquivo Agendamento (txt)" ✅ MAPEADO (2026-08-03)

**É a MELHOR fonte para materializar agenda** — melhor que raspar o `cons_agendas`. Medido no CDT,
mesmo par profissional × procedimento, 419 registros em julho: **1 requisição** contra **9** da tela
paginada de 50 em 50. E o arquivo ainda traz SIGTAP, datas de solicitação/regulação e endereço, que
a tela de agenda não informa. É a fonte que o motor do `SMSMais.server` usa (ADR-0040).

**Formulário** — `GET /cgi-bin/expo_solicitacoes` e depois `POST` no mesmo caminho:

| Campo | Valor |
|---|---|
| `data1`, `data2` | `dd/MM/yyyy` |
| `cpf` | CPF do profissional (select; mesmos valores do `cons_agendas`) |
| `procedimento` | código `pa` (select, populado por AJAX ao escolher o profissional) |
| `tp_arquivo` | `0` = TXT, `1` = CSV |
| `etapa` | `exportar` |
| `unidade` | CNES (hidden) |

Resposta: `text/plain; charset=utf-8`, direto no corpo — sem download intermediário. Cabeçalho
`CNES;Nome;dt_ini;dt_fim;total` e 38 colunas por linha.

**Coluna 1 = `pa`, coluna 2 = SIGTAP.** É o de-para autoritativo, dito pelo próprio SISREG — não
precisa adivinhar por nome.

🚀 **A UNIDADE INTEIRA SAI EM UMA REQUISIÇÃO — `cpf=0` e `procedimento=0` (medido 27/08/2026).**
Sonda `sonda_export_amplo.py`, CDT 3132358, janela 27/08→25/09 (29 dias), operador
PROGRAMADOR-BERNARDO:

| Cenário | `cpf` | `procedimento` | Linhas | `pa` distintos |
|---|---|---|---|---|
| controle | 085…766 | `1402000` (grupo) | 320 | 4 |
| A | 085…766 | `0` (sentinela) | 320 | 4 |
| B | 085…766 | *(vazio)* | 320 | 4 |
| **C** | **`0`** | **`0`** | **3.286** | **65** |

O cenário C **contém** o controle (todos os 320 nºs de solicitação estão lá) e traz 2.966 a mais.
O `0` é a option-sentinela `Selecione o Profissional` / `Selecione o Procedimento` do próprio
formulário — e o JS dele só valida as datas e o range de 31 dias, nunca esses dois campos.
Consequência: a varredura do CDT, que custa **272 requisições** (uma por par prof×proc habilitado),
cabe em **1**.

> Não confundir com o `cons_agendas`, onde o mesmo experimento **falha**: lá `ups` com `cpf`/`pa`
> vazios devolve "A pesquisa não retornou nenhum resultado" (§ tela de agenda). São telas
> diferentes com regras diferentes — o que vale para uma não vale para a outra.

⚠️ **O teto de 700 NÃO se aplicou nesse recorte:** o cenário C devolveu 3.286 linhas e o cabeçalho
declarou `3286`. Ou o teto é por profissional, ou mudou. **O cabeçalho é a fonte confiável de
truncamento** — `total` bateu exatamente com as linhas nos quatro cenários. Detectar corte contando
linhas *parseadas* é frágil: uma linha rejeitada pelo parser faz 700 virar 699 e o corte passa
despercebido.

⚠️ **Teto de 700 registros por exportação** (medição de 03/08/2026, com `cpf` e `procedimento`
preenchidos). Intervalos de 61 e de 212 dias devolveram exatamente 700. Truncamento **silencioso**:
o cabeçalho diz 700 e as linhas são 700, nada indica que faltou. Quem consumir tem que partir a
janela ao bater no teto.

✅ **Código de GRUPO agrega, e é mais barato** (corrigido em 05/08/2026).
`1402000 GRUPO - ULTRASONOGRAFIA` devolve **120 registros** em 1 requisição, com 4 procedimentos
distintos dentro — e **cada linha traz o SEU `pa` e o SEU SIGTAP** nas colunas 1 e 2, não os do
grupo. Habilitar o grupo custa 1 requisição onde os itens custariam 4.

Há profissional cujo AJAX de procedimentos **só lista códigos de grupo** (ex.: CPF 085…766 no CDT,
com `1402000` e `2500000` e nada mais). Para ele o grupo é o **único** caminho até a agenda.

> ⚠️ Registro do erro, porque a forma se repete: por um tempo esta seção afirmava "grupo devolve 0",
> generalizado de um único teste com `1305000 GRUPO - MAMOGRAFIA`. Aquele grupo devolveu 0 porque a
> agenda de mamografia estava vazia no período — o item individual também devolvia 0 para agosto.
> A amostra era de 1, e a variável observada não era a causa.

⚠️ **Grupo + itens ao mesmo tempo desperdiça requisição** — o mesmo agendamento vem duas vezes e o
consumidor deduplica por nº de solicitação. Habilite um dos dois.

⚠️ **Bloqueado das 08h às 15h.** Confirmado que fora desse horário abre normalmente.

<details>
<summary>Registro anterior, de quando a tela era desconhecida</summary>

### `expo_solicitacoes` — "Arquivo Agendamento (txt)"
⚠️ **Bloqueado das 08h às 15h** (`alert('Aplicativo bloqueado para uso de 8 as 15
horas.')` → redireciona p/ `/cgi-bin/avisos`). Export em massa provavelmente só
fora do horário comercial. **Avaliar após 15h** — pode ser o caminho ideal (txt
estruturado em vez de raspar HTML paginado).

</details>
## ✍️ SUBSÍDIO PARA ESCRITA (mapeado 2026-07-25 — NADA FOI EXECUTADO)

Levantamento **só por GET** dos formulários (nenhum POST de escrita disparado),
para saber o que seria possível automatizar no futuro. **Não usar sem OK explícito.**

| Tela | Endpoint | Etapas de ESCRITA no JS | O que faz |
|------|----------|------------------------|-----------|
| Solicitação de Consultas Ambulatoriais | `marcar` | `LST_ITENS_PA`, `LST_VAGAS` → grava na tela de vagas | **Criar** solicitação/agendamento |
| Consulta de Autorização/Cancelamento | `cons_verificar` | **`EXCLUIR_SOLICITACAO`** | Cancelar solicitação |
| Consulta de Solicitações Ambulatoriais | `gerenciador_solicitacao` | **`CANCELAR_SOLICITACAO`**, **`REENVIAR_REGULACAO`** | Cancelar / devolver à regulação |
| Impressão/Confirmação de Agendas | `cons_agendas` | **`Confirma`**, **`Falta`** | Confirmar comparecimento / registrar falta |
| Consulta de Escalas Ambulatoriais | `cons_escalas` | `EXIBIR_ESCALAS`, `DETALHAR_ESCALA`, `EXPORTAR_ESCALAS` + JS `editarEscala()` | **Grade de vagas** do profissional |
| Cadastro de Preparo | `config_preparo` | `INSERIR_PREPARO`, `ATUALIZAR_PREPARO`, `EXCLUIR_PREPARO` | Texto de preparo do exame |

**Fluxo de criação (`marcar`)** — encadeado, começa pelo paciente:
`cadweb50?url=/cgi-bin/marcar` (acha o paciente) → `marcar` (campos `pa`,
`cid10`, `cpfprofsol`/`nomeprofsol`, `ret` = retorno, `upsexec`) → `ProximaEtapa()`
decide: se o código do procedimento termina em `000` (é GRUPO) vai para
`LST_ITENS_PA` (escolher o item), senão vai direto para `LST_VAGAS`, que lista
as vagas disponíveis — a gravação acontece a partir dessa tela.

**Escalas (`cons_escalas`) é a tela-chave para "editar agenda"** de verdade:
mesmo modelo `ups`→`cpf`→`pa` do `cons_agendas`, com `EXPORTAR_ESCALAS` (export
da grade!) e um `editarEscala()` no JS. É onde vivem as vagas, não os pacientes.

⚠️ O perfil da credencial atual é **EXECUTANTE/SOLICITANTE** — ele enxerga essas
telas, mas **não** foi testado se tem permissão efetiva de gravar em cada uma.
Confirmar antes de qualquer plano de automação de escrita.

## 👤 Consulta de paciente por CPF/CNS — `cadweb50` (CADSUS) ✅
Menu **"CNS"** → `/cgi-bin/cadweb50?standalone=1`. Título "CONSULTA AO CADASTRO DE
PACIENTES SUS". **Um único campo aceita CPF OU CNS.** Muito útil para resolver a
identidade SISREG/CNS a partir do CPF (amarra com o hub FHIR / Patient).

**POST `/cgi-bin/cadweb50?standalone=1`** (form auto-submete via `pesquisarPaciente()`):

| Campo | Uso |
|-------|-----|
| `nu_cns` | **CPF ou CNS** (campo "CPF/CNS") |
| `nome_paciente`, `nome_mae`, `dt_nascimento` | filtros alternativos |
| `uf_nasc`/`mun_nasc`, `uf_res`/`mun_res`, `sexo` | filtros |
| `standalone` | `1` |
| `etapa` | **`LISTAR`** (lista resultados) \| **`DETALHAR`** (abre a ficha completa) |
| `url` | vazio no modo standalone |

✅ **Validado ao vivo (2026-07-01):** para buscar por CNS exato, **usar `etapa=DETALHAR`**
direto com `nu_cns` → devolve a ficha completa (7,7 KB). `etapa=LISTAR` devolve a
**lista** de resultados (20 KB, radios `nu_cns`), exigindo um 2º POST `DETALHAR`.
Atalho: `DETALHAR` + `nu_cns` num único POST resolve.

⚠️ **Sessão única — página de "sessão finalizada":** quando o operador loga em outra
estação, o POST **não** volta a tela de login; volta um **"Erro de Sistema — Este
operador efetuou logon em outra esta&ccedil;&atilde;o de trabalho. Sua sess&#227;o foi
finalizada pelo servidor."**. A detecção de sessão-caída precisa cobrir isso (markers
sem acento: `logon em outra`, `foi finalizada`), senão o relogin automático não dispara.

Botões: `btn_pesquisar` → `pesquisarPaciente()` (seta `etapa=LISTAR`); `btn_limpar` → `resetForm()`.

**Comportamento (validado 2026-07-01 com CPF do próprio usuário):** busca por
CPF/CNS com match único vai **direto para a ficha completa** (DETALHAR, pula a
LISTA). Estrutura da ficha retornada (→ mapeia direto para **FHIR Patient**):

- **DADOS PESSOAIS:** CNS, Nome, Nome Social/Apelido, Nome da Mãe, Nome do Pai,
  Sexo, Raça, Data de Nascimento (+idade), Tipo Sanguíneo, Nacionalidade, Município de Nascimento.
- **ENDEREÇO:** Tipo Logradouro, Logradouro, Complemento, Número, Bairro, CEP,
  País de Residência, Município de Residência.
- **CONTATOS:** Telefone(s) — Tipo, DDD, Número.
- **DOCUMENTOS:** CPF.

Ou seja, `cadweb50` resolve **CPF → CNS + demografia completa** — ideal para
casar/enriquecer a identidade no hub FHIR (Patient: name, mother, gender,
birthDate, address, telecom, identifiers CNS+CPF). Por isso é uma tela-chave.

⚠️ **PII pesado / LGPD:** essa tela retorna dados pessoais completos do cidadão.
No motor, tratar como dado sensível (não logar em claro, respeitar consentimento —
ver [[project_consentimento_lgpd_cidadao]]). Não commitar capturas dessa tela.

## 🏥 `cons_unidade` — resolver NOME → CNES (e enriquecer cadastro) ✅
Menu **Unidades**. `POST /cgi-bin/cons_unidade`, **`etapa=LISTAR_UNIDADES_IMPORTADAS`**.
Filtros: **`no_fantasia`** (nome → CNES) ou **`co_cnes_ups`** (CNES → nome); `tp_unidade`,
`st_gestao_propria`, `qtd_itens_pag` (10/20/50/100), `pagina` (0-based).

Colunas retornadas (`table_listagem`): **Código CNES | Nome Fantasia | Município Origem (c/ IBGE) |
Central Reguladora Gestora | Telefone | Solicita Para Outra Central | Tipo** (AMBAS/EXECUTANTE/
SOLICITANTE). Validado 2026-07-01: CDT→**3132358**, RADIOCENTER→**5833841**, DIMAGEM→**2285134**
(todos MARICA 330270).

→ Serve para (a) **backfill de CNES** nas unidades já cadastradas (que hoje só casam por nome) e
(b) **auto-criar** unidade com cadastro decente (nome oficial UPPERCASE + telefone + município/IBGE).
Na importação o CNES já vem na ficha do agendamento; `cons_unidade` **enriquece**. Mesma fonte,
sem API externa.

## 🔒 Escopo do perfil (RESOLVIDO 2026-08-26)
A credencial de teste antiga (perfil **EXECUTANTE/SOLICITANTE**, operador 022-ADRIANA)
enxergava **uma única unidade** (CDT 3132358). **Resolvido:** a credencial
**`PROGRAMADOR-BERNARDO`** (no `.env` do `Automais.SISCAN`) tem **perfil amplo e enxerga as
42 unidades da rede** — CDT, CMI, UPA, todas as USF, etc. O select `ups` do `cons_agendas`
lista as 42. → **Um perfil amplo cobre a secretaria inteira; não precisa de credencial por
unidade.** (É a credencial que a produção deve usar no robô — sessão dedicada.)

## ⚡ Botão "Importar" PONTUAL — caminho `cons_agendas` por dia (validado 2026-08-26)
O botão "Importar" por médico×especialidade é **pontual**, para quando **não dá para esperar as
15h** (o `expo_solicitacoes`/CSV é bloqueado 08h–15h — **confirmado ao vivo:** devolve
`alert('Aplicativo bloqueado para uso de 8 as 15 horas.')`). Então a fonte do botão é o
**`cons_agendas` (paginado, SEM bloqueio de horário)**, escopado a **um dia** para custar ~1
requisição.

**Medição real (26/08/2026, CMI × Renato Roque × USG Gestantes):** 1 requisição → **20
agendamentos** numa página. Alvos confirmados:
- **CMI** = CNES **2930242** (CENTRO MATERNO INFANTIL)
- **RENATO ROQUE DE ANDRADE** = CPF **08675605765**
- **Ultrassom Gestantes** = `pa` **0229000** (`GRUPO - ULTRASSONOGRAFIA GESTANTES`) — código de
  GRUPO que expande sozinho nas linhas: USG MORFOLOGICO / TRANSLUCENCIA NUCAL / OBSTETRICA /
  TRANSVAGINAL - GESTANTE.

Cada registro do `cons_agendas` traz o suficiente para o pipeline de import ordinário:
`co_solicitacao` (idempotência), `cns` (→ resolve paciente por cadweb50), `paciente`,
`telefones`, `data`/`hora`, `unidade_solicitante`+`cnes_solicitante`, `cid10`,
`vaga_consumida`, `situacao`, `procedimentos` (nome — que é o eixo do procedimento).
**Falta vs as 38 colunas do CSV:** SIGTAP por linha, endereço, mãe, médico solicitante — mas
paciente é enriquecido no downstream (CNS→CADSUS) e o eixo é o nome. `expo_solicitacoes`/CSV
continua sendo a fonte de **carga em massa** (após as 15h).

## 📅 `cons_escalas` — GRADE DE HORÁRIOS DA REDE INTEIRA ✅ (2026-09-04)

**Uma requisição traz toda a agenda ofertada do município.** É a fonte da estrutura de
"horários" da plataforma: profissional × unidade executante × procedimento × dia da semana
× faixa de horário × nº de vagas. **Não tem paciente** — são as VAGAS, não os agendamentos.

`GET /cgi-bin/cons_escalas` → form único `formulario`, POST para a **própria URL**.
Os selects `ups`/`cpf`/`pa` chegam vazios no HTML e são preenchidos por AJAX
(`ajax_populaUpsIbgeEHorario`, `ajax_populaProfPorUpsEHorario`) — **irrelevante para o
scraping**, porque o POST aceita os campos direto.

| Campo | Valor |
|-------|-------|
| `etapa` | `EXIBIR_ESCALAS` (lista HTML paginada) / `EXPORTAR_ESCALAS` (**CSV**) / `DETALHAR_ESCALA` |
| `ups` | CNES da unidade executante — **aceita vazio** |
| `radioFiltro` | `cpf` (profissional) ou `pa` (procedimento) — só governa qual combo a tela mostra |
| `cpf` / `pa` | filtro do profissional / procedimento — **aceitam vazio** |
| `status` | `''` todos · `A` ativas · `I` inativas · `E` expiradas · `X` excluídas |
| `dataInicial`/`dataFinal` | data da **última alteração** (não da vigência) — aceitam vazio |
| `qtd_itens_pag` | 10/20/50/100 (só afeta `EXIBIR_ESCALAS`) |
| `ibge` | `330270` (Maricá), hidden |
| `pagina`, `ordenacao`, `clas_lista`, `coluna` | paginação/ordenação |

### ⭐ Recorte SEM CRITÉRIO passa no servidor (ao contrário do `cons_agendas`)
Medição real (04/09/2026, operador `PROGRAMADOR-BERNARDO`): POST com **ups/cpf/pa/status/datas
todos vazios** → `HTTP 200 text/csv`, `filename="SISREG_ESCALAS_AMB_330270_<data>_<hora>.csv"`,
**5,9 MB / 17.469 escalas** em **uma única requisição**. `;` como separador, **UTF-8 sem BOM**,
34 colunas. Não há bloqueio de horário (diferente do `expo_solicitacoes`, travado das 8h às 15h).

Distribuição por STATUS: **1.639 ATIVAS**, 1.392 inativas, 981 excluídas, 13.457 expiradas
(o histórico é a maior parte do arquivo — filtrar por `STATUS` ou passar `status=A`).
Nas ATIVAS: **216 profissionais, 34 unidades executantes, 132 procedimentos**.

### Colunas do CSV (34)
```
00 COD. ESCALA AMBULATORIAL      ← chave natural, única por linha (idempotência)
01 COD. CENTRAL EXEC.            02 DESC. CENTRAL EXEC.        (sempre 330270/MARICA)
03 CPF PROFISSIONAL EXEC.        04 NOME PROFISSIONAL EXEC.
05 COD. CBO                      06 DESC. CBO                  ('---' quando não há)
07 COD CNES EXEC.                08 DESC. CNES EXEC.           ← unidade executante
09 COD. PROCEDIMENTO INTERNO     10 DESC. PROCEDIMENTO INTERNO ← eixo (código do SISREG)
11 COD. PROCEDIMENTO UNIFICADO   ← SIGTAP; VAZIO/'---' em 31% das linhas
12 SIGLA DIA SEMANA              (SEG..DOM — a escala é SEMANAL, não uma data)
13 QTD VAGAS PRIM. VEZ           14 QTD. MINUTOS PRIM. VEZ
15 QTD. VAGAS RETORNO            16 QTD. MINUTOS RETORNO
17 QTD. VAGAS RESERVA            18 QTD. MINUTOS RESERVA
19 QUEBRA AUTOMATICA (SIM/NAO)   20 AGENDA LOCAL (SIM/NAO)
21 DATA DE VIGENCIA INICIAL      22 DATA DE VIGENCIA FINAL
23 HORA INICIAL                  24 HORA FINAL
25 NOME OPERADOR CRIADOR         26 NOME OPERADOR MODIFICADOR
27 DATA ULTIMA ALTERACAO         28 HORA ULTIMA ALTERACAO
29 STATUS  (ATIVA/INATIVA/EXPIRADA/EXCLUIDA)
30 DATA DA INSERCAO              31 HORA DA INSERCAO
32 DATA DA ULTIMA ATIVACAO       33 HORA DA ULTIMA ATIVACAO
```

**Leitura do modelo:** cada linha é **um bloco semanal recorrente** — "toda SEX, das 11:10 às
12:00, no CNES 3132358, profissional X, procedimento 0229000, 10 vagas de primeira vez" —
válido de `VIGENCIA INICIAL` a `VIGENCIA FINAL`. O mesmo par profissional×procedimento aparece
em várias linhas (uma por dia da semana e por faixa de horário). `COD. PROCEDIMENTO INTERNO`
terminado em `000` é **GRUPO** (mesmo comportamento do `pa` no `marcar`/`cons_agendas`).

⚠️ **`COD. PROCEDIMENTO UNIFICADO` (SIGTAP) vem vazio em ~5.400 linhas** — confirma o que já
sabíamos: **o nome/código interno do procedimento é o eixo**, o SIGTAP não é confiável aqui.

### Scripts
- `recon_escalas.py` — só GET; dumpa form, campos, etapas e funções JS.
- `exportar_escalas.py --saida <fora do repo> [--status A]` — login reusado + GET + 1 POST de
  sonda (`EXIBIR_ESCALAS`, 10 itens) + 1 POST de export. **Custo: ~4 requisições** do orçamento
  anti-robô. O CSV tem CPF de profissional → nunca gravar dentro do repo.

### ⚔️ O lab BRIGA com o sincronismo de produção pela mesma credencial (2026-09-04)
Confirmado pelo operador: **a produção roda sincronismo do SISREG em paralelo** com a mesma
credencial `PROGRAMADOR-BERNARDO`. Como a sessão é ÚNICA por operador, cada lado derruba o
outro — foi exatamente o que aconteceu nesta captura (a sessão salva do lab veio morta, e o
`login()` do lab derrubou a do sincronismo). Consequências práticas:

- Rodar script do lab **interrompe o sincronismo de produção** até ele relogar sozinho.
- "Veio vazio" durante um run do lab pode ser sessão roubada, **não** ausência de dados.
- É o argumento definitivo para a pendência já registrada: **provisionar um operador dedicado
  ao lab**, separado do operador do robô de produção. Enquanto não houver, rodar o lab é uma
  ação com efeito colateral em PROD — combinar antes.

### 🐛 `esta_logado()` dava falso positivo (corrigido 2026-09-04)
Quando outro logon derruba a sessão, o SISREG responde **200 com a página `sisreg_erro.c`**
("Este operador efetuou logon em outra estação de trabalho") — não redireciona. `_login_ok`
lia isso como sessão viva e as chamadas seguintes voltavam vazias, o que se lê como "não há
dados" (conclusão errada pelo motivo certo). `_login_ok` agora reprova `logon em outra`/`sisreg_erro`.

## 🔒 Escopo antigo (histórico)
Antes de 2026-08-26 a decisão "perfil amplo × credencial por unidade" estava pendente — ver
seção acima (resolvida por `PROGRAMADOR-BERNARDO`).

## 🔁 Sessão ÚNICA por operador (reuso obrigatório)
O SISREG autentica **1 dispositivo/sessão por operador** — cada novo `login()`
derruba a sessão anterior (inclusive a do operador humano). Por isso o client
**persiste os cookies** (`capturas/.sessao.json`, gitignored) e **reaproveita o
token** (`SisregClient.conectar()`); só refaz login se `esta_logado()` falhar.
Nunca chamar `login()` em loop.

⚠️ **Implicação de arquitetura (confirmada na prática 2026-07-01):** como a sessão
é única, quando um humano loga com o mesmo operador, a sessão do robô **morre** — e
vice-versa. Sintoma sutil: chamadas ainda "funcionam" (retornam página logada) mas
**AJAX de dados vem vazio**. Por isso o motor no `SMSMais.server` **precisa de uma
credencial de operador DEDICADA**, exclusiva do robô, para não brigar com os
operadores humanos. Definir esse operador com a SMS antes do go-live.

## ⚠️ Cuidados

- **É PRODUÇÃO.** Nesta fase, só leitura/navegação. Nada de gravar/agendar/cancelar
  no SISREG sem OK explícito — mesmo padrão de PROD do resto do projeto.
- Credencial fica só no `.env` (gitignored). Nunca commitar `.env` nem `capturas/`.

## Próximos passos

- [x] Login real com credencial do `.env` (`python login_test.py`). ✅ 2026-07-01
- [x] Mapear a estrutura pós-login (frames/menus) e os endpoints CGI de cada menu. ✅
- [ ] **Avaliar a API oficial** ("Acesso a Api") antes de scraping.
- [x] Identificar a tela-alvo de agendamentos: **`cons_marcados_reg`** (autorizados/executados). ✅
- [x] Reuso de sessão (sessão única por operador). ✅
- [x] Mapear a **agenda do CDT** (executante): `cons_agendas` ups→cpf→pa. ✅ (browser 2026-07-01)
- [ ] Implementar em Python o extrator de `cons_agendas` (iterar profissional×procedimento, paginar).
- [ ] Definir escopo: perfil amplo (secretaria) **ou** 1 credencial por unidade.
- [ ] Implementar paginação completa + parser das colunas → JSON/CSV.
- [x] Mapear **`cons_escalas`** e baixar o CSV da rede inteira sem critério. ✅ 2026-09-04
- [ ] Modelar a entidade de **horários** a partir do CSV de escalas e definir a sincronização.
- [ ] Testar `expo_solicitacoes` (txt) após as 15h.
- [ ] **Provisionar credencial de operador DEDICADA** para o robô (sessão única).
- [ ] **Provisionar operador SEPARADO para o LAB** — hoje ele briga com o sincronismo de PROD.
- [ ] Portar o motor validado para `SMSMais.server`.

---

## 🧾 FILA DE ESPERA — `gerenciador_solicitacao` ✅ (2026-09-05)

**O buraco que isto fecha:** o sistema só importava solicitação **já agendada**. Medido em
05/09/2026, 22.081 de 22.101 linhas de `smsmarica.solicitacao` tinham `data_agendada` — quem
está aguardando vaga era invisível para a operação.

### ⚠️ Correção do mapa de menus acima
A entrada **"Solicitações Pendentes na Fila de Espera" → `cons_pendente_fila_`** está **errada**:
esse caminho responde **404**. Não era truncamento, era outro nome. Os nomes reais, extraídos do
frameset pós-login (`recon_menu.py`):

| Rótulo no menu | Endpoint real |
|---|---|
| Pendentes Fila de Espera | `rel_fila_espera_mun_pendentes.pl` |
| agendamento fila de espera | `rel_fila_espera_mun.pl` |
| Média de Espera em Fila | `rel_media_fila.pl` |
| Solicitações (gerenciador) | `gerenciador_solicitacao` |

### ⭐ O caminho bom é o `gerenciador_solicitacao`

`METHOD=GET`, `etapa=LISTAR_SOLICITACOES`. Regras do `validaFormulario()`: **sem** `co_solicitacao`
nem `cns_paciente`, são obrigatórios `tipo_periodo`, janela de **no máximo 31 dias** e
`cmb_situacao`. **Unidade é opcional** — dá para varrer a rede inteira de uma vez.

```
/cgi-bin/gerenciador_solicitacao
  ?etapa=LISTAR_SOLICITACOES
  &tipo_periodo=S            # S = por data de SOLICITACAO (A=agend, E=exec, P=confirm, C=cancel)
  &dt_inicial=05/08/2026&dt_final=05/09/2026     # <= 31 dias
  &cmb_situacao=1            # 1 = Solicitacao / Pendente / Regulacao  <- A FILA
  &qtd_itens_pag=0           # 0 = TODOS  <- mata a paginacao
  &pagina=0&ordenacao=2
```

**`qtd_itens_pag=0` ("TODOS") é honrado pelo servidor** — é o que derruba o custo:

| Janela | Registros | Bytes | Tempo | Requisições |
|---|---|---|---|---|
| 7 dias (recente) | 3.669 | 3,2 MB | 37 s | **1** |
| **31 dias (recente)** | **15.502** | **13,4 MB** | **75 s** | **1** |
| 31 dias (jan/2025) | 355 | 0,3 MB | 10 s | **1** |

**Não há teto silencioso** aqui (≠ `expo_solicitacoes`, que corta em 700 sem avisar): 3.669 em
7 dias × 31/7 = 16,2k ≈ 15,5k medidos em 31 dias. A aritmética fecha.

### Situações (`preencherComboSituacao`)
`1` Pendente/Regulação · `2` Pendente/Fila de Espera · `3` Cancelada · `4` Devolvida ·
`5` Reenviada · `6` Negada · `7` Agendada · `9` Agendada/Fila de Espera ·
`10` Agendamento/Cancelado · `11` Agendamento/Confirmado · `12` Agendamento/Falta

**Em Maricá a fila mora na situação `1`, não na `2`.** A situação 2 volta vazia em toda janela
testada — o município não usa o fluxo "fila de espera" do SISREG; tudo espera na regulação.
Sondar só a 2 (o nome óbvio) daria a conclusão errada de que não há fila.

### As 12 colunas da listagem
`Cód. Solicitação` · `Data da Solicitação` · `Risco` · `Paciente` · `Telefone` · `Município` ·
`Idade` · `Procedimento` · `CID` · `Unidade Solicitante` · `Unidade Executante` · `Situação`

- `Cód. Solicitação` é a **mesma chave** de `solicitacao.codigo_solicitacao` → idempotência de graça.
- `Telefone` vem em **99%** (15.336/15.502) — serve para notificar.
- `Unidade Executante` é sempre `---` e `Risco` sempre vazio: esperado para quem ainda não foi regulado.
- **Não vem CNS nem CPF.** A identidade só sai em `visualizaFicha(co_solic)`, 1 requisição por
  pessoa — inviável para 15 mil. Ver ADR-0041: entra marcado, não fica de fora.

### Cruzamento com o nosso banco (05/09/2026)
Dos **15.502** pendentes de 05/08 a 05/09, **1** já existia em `smsmarica.solicitacao`.
É dado praticamente 100% novo.

Top da fila: `GRUPO - ULTRASONOGRAFIA` 2.607 · `GRUPO - TOMOGRAFIA` 1.170 ·
`GRUPO - DIAGNOSTICO POR RADIOLOGIA` 969 · `CONSULTA EM OFTALMOLOGIA` 892 ·
`GRUPO - RESSONANCIA - ORTOPEDIA` 693 · `MAMOGRAFIA BILATERAL` 605 ·
`ECOCARDIOGRAMA - ADULTO` 535.

O Ecocardiograma aparecer com 535 na fila **e** com 481 dias de espera mediana entre os já
agendados (medido na tela de Demanda no mesmo dia) confirma o gargalo por dois caminhos
independentes.

### ⭐ A fila é um ESTADO, não um histórico (provado 2026-09-05)

**Ao ser agendada, a solicitação SAI da situação 1.** Provado sem gastar requisição: das 15.502
da fila (solicitadas 05/08–05/09) contra as 6.765 já agendadas do nosso banco **solicitadas na
mesma janela**, a interseção é **ZERO**. Conjuntos perfeitamente disjuntos.

**Consequência que engana quem lê rápido** — e enganou nesta própria investigação: consultar uma
janela antiga **não** devolve "quem esperava naquela época", devolve **quem pediu naquela época e
AINDA espera hoje**. Os 355 de janeiro/2025 não são "355 pendentes em jan/2025"; são 355 pessoas
que pediram em janeiro de 2025 e seguem na fila **20 meses depois**.

Curva medida em 05/09/2026 (situação 1, `tipo_periodo=S`, janelas de 1 mês):

| Mês do pedido | Ainda na fila hoje | Esperando há |
|---|---|---|
| jan/2024 | ~1 | 20 meses |
| jul/2024 | 0 | — |
| jan/2025 | 355 | 20 meses |
| jan/2026 | 2.688 | 8 meses |
| jun/2026 | 8.014 | 3 meses |
| ago–set/2026 (31 d) | 15.502 | ~1 mês |

Interpolando, a fila total é da ordem de **60 a 90 mil pessoas** — 3 a 4× o nosso banco inteiro de
agendados (22.101 em 05/09/2026). É estimativa de 6 pontos; a varredura completa dá o número exato.

### Custo de varrer o passado inteiro
~33 janelas de 31 dias de jan/2024 até hoje = **~33 requisições** para todo o acervo; o diário fica
em **1 requisição**. As janelas antigas encolhem sozinhas conforme as pessoas são atendidas, então
depois da carga inicial dá para revisitar o passado com folga e concentrar o diário nos meses
recentes.

### O que a importação PRECISA fazer, por causa disso
1. **Detectar saída, não só entrada.** Quem some da fila ou foi agendado ou foi cancelado/negado.
   Mesma classe de problema do "sumiu do arquivo" das escalas — mesma solução: baixar a janela
   inteira, comparar item a item, e só concluir ausência com a cobertura completa.
2. **Corrigir o viés de sobrevivência das telas de análise.** A espera mediana de 52 dias medida em
   `Core/AgendaRegulacao` é de **quem conseguiu vaga**; quem espera há 20 meses nunca teve
   `data_agendada` e não entra em conta nenhuma. Só com a fila importada dá para medir a espera de
   quem ainda não foi atendido — que é a pergunta da regulação.

### Alternativa mais barata para conferência
`rel_fila_espera_mun_pendentes.pl` (mesma trava de 31 dias) devolve o **agregado por unidade** com
resposta minúscula, e `detalha(cnes)` faz o drill-down (`abrangencia=descricao_fila`). Não serve
para importar (1 requisição por unidade), mas serve de **totalizador de conferência** sem baixar
13 MB.

### ⛔ Becos sem saída (não repetir)
- **Buscar por `co_solicitacao` sozinho não devolve nada.** O `validaFormulario()` dispensa período
  e situação quando o código está preenchido, mas o servidor respondeu vazio para 4 códigos válidos
  (2 da fila, 2 agendados do nosso banco). O campo deve esperar outro identificador que não o
  "Cód. Solicitação" exibido na listagem — provavelmente o `co_seq_solicitacao`. Fica em aberto.
- **`cmb_situacao` só respondeu na situação 1.** As situações 7 (Agendada) e 11
  (Agendamento/Confirmado) voltaram vazias em toda combinação testada, inclusive com
  `tipo_periodo=A`. Para a situação 7 em janela recente isso é *correto* — com espera mediana de
  52 dias, quase nada solicitado nos últimos 7 dias já está agendado —, mas em jan/2025 deveria
  haver resultado. Não investigado a fundo: a fila (situação 1) já resolve o objetivo.
- **`/cgi-bin/avisos` NÃO é o menu**, é o mural de comunicados (505 KB). E `/cgi-bin/index` é a
  tela de login. O menu real só sai da resposta do POST de login.

### Scripts
- `recon_menu.py` — extrai o mapa de menus real do frameset pós-login.
- `recon_fila_espera.py` — GET-only dos formulários das telas de fila.
- `sonda_fila_gerenciador.py` — sonda parametrizada (`--situacao/--de/--ate/--por-pagina`),
  com teto rígido de requisições e parada imediata em captcha.

### ⚠️ Cuidado ao portar
`qtd_itens_pag=0` num mês recente leva **75 s** e 13,4 MB. Timeout padrão de 30 s do client não
serve — foi o que deu `ReadTimeout` na primeira tentativa (situação 7). Usar timeout ≥ 180 s e
tratar a resposta em streaming.
