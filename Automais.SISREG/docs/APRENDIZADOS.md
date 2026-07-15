# SISREG III — aprendizados da integração

> Laboratório isolado (Python) para mapear o SISREG antes de portar o motor
> para o `SMSMarica.server`. Fonte: `https://sisregiii.saude.gov.br`.

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
| Solicitações Pendentes na Fila de Espera | `cons_pendente_fila_` |
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

### `expo_solicitacoes` — "Arquivo Agendamento (txt)"
⚠️ **Bloqueado das 08h às 15h** (`alert('Aplicativo bloqueado para uso de 8 as 15
horas.')` → redireciona p/ `/cgi-bin/avisos`). Export em massa provavelmente só
fora do horário comercial. **Avaliar após 15h** — pode ser o caminho ideal (txt
estruturado em vez de raspar HTML paginado).

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

## 🔒 Escopo do perfil (IMPORTANTE p/ "toda a secretaria")
A credencial de teste (perfil **EXECUTANTE/SOLICITANTE**) enxerga **uma única
unidade**: o select `ups`/hidden `unidade` só traz **CNES 3132358 (CDT ALBERTO
LUIS MACHADO BORGES)**. Para cobrir **toda a secretaria** precisaremos de:
- (a) um perfil mais amplo (gestor/regulador central que liste várias unidades), **ou**
- (b) **uma credencial por unidade**, iterando cada uma.
→ Decisão pendente com o usuário.

## 🔁 Sessão ÚNICA por operador (reuso obrigatório)
O SISREG autentica **1 dispositivo/sessão por operador** — cada novo `login()`
derruba a sessão anterior (inclusive a do operador humano). Por isso o client
**persiste os cookies** (`capturas/.sessao.json`, gitignored) e **reaproveita o
token** (`SisregClient.conectar()`); só refaz login se `esta_logado()` falhar.
Nunca chamar `login()` em loop.

⚠️ **Implicação de arquitetura (confirmada na prática 2026-07-01):** como a sessão
é única, quando um humano loga com o mesmo operador, a sessão do robô **morre** — e
vice-versa. Sintoma sutil: chamadas ainda "funcionam" (retornam página logada) mas
**AJAX de dados vem vazio**. Por isso o motor no `SMSMarica.server` **precisa de uma
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
- [ ] Testar `expo_solicitacoes` (txt) após as 15h.
- [ ] **Provisionar credencial de operador DEDICADA** para o robô (sessão única).
- [ ] Portar o motor validado para `SMSMarica.server`.
