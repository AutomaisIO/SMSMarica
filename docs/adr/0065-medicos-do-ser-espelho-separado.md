# ADR-0065 — Médicos do SER num espelho à parte do nosso cadastro; envio de médico novo ao SER só com autorização

**Status:** proposto (fases de leitura implementadas localmente em 2026-10-01; envio NÃO implementado) · **Data:** 2026-10-01
**Relacionado:** [ADR-0052](./0052-fila-pre-regulacao-e-agente-regulador.md) (solicitação de
regulação; D-11 — nada da Regulação escreve no SER) · [ADR-0007](./0007-schema-fhir-separado.md)
(profissional clínico é `Practitioner` no FHIR) · [`docs/ser-criar-solicitacao.md`](../ser-criar-solicitacao.md) §2.1.2

## Contexto

A solicitação ao SER exige o **médico solicitante**, escolhido do combo `form0:medicoResp` — a
lista dos profissionais ativos **com lotação no município** no cadastro do próprio SER
(Cadastro → Profissionais). Médico que não está lá não pode ser solicitante. Pedido do Bernardo
(01/10/2026): listar os médicos do SER, cadastrar pela nossa plataforma o que falta, e decidir se
o cadastro do SER fica separado do nosso ou se fazemos um backfill para melhorá-lo.

Medido em 01/10/2026, só leitura (`Automais.SER/probe_profissional_saude.py`):

**O cadastro do SER**
- É **estadual e único por CPF**, com N **lotações** (unidade × especialidade × CBO × ativo). A
  nossa conta só lota em "GESTOR SMS MARICA". Um profissional da amostra tinha 16 lotações.
- A pesquisa com filtro vazio devolve **930** (47 páginas de 20) — o universo ligado ao município.
- **Qualidade ruim:**
  - CPF em **4,6%**, e só 2,5% com dígito verificador válido;
  - **metade (50,4%) só tem o nome**;
  - CRM em 45,9%, em formato livre e sem UF;
  - 54 grupos de nomes duplicados;
  - 20% de nomes abreviados ou suspeitos.
  - O SER **não valida o DV**: o CPF 111.111.111-11 existe lá.
- **Não há identificador estável exposto:**
  - o link "Editar" da pesquisa responde **HTTP 500** (também no navegador);
  - o `value` de `medicoResp` e o da especialidade da lotação são **índices de view do Seam**,
    que mudam a cada abertura.
- **Formulário de inclusão:** CPF (com busca no `onblur`), nome, telefone e lotação
  (especialidade, entre 115 opções, + CBO por sugestão). Não tem CRM, documento nem tipo. A
  validação JavaScript está **comentada**; o que o servidor exige no Gravar só um Gravar revela.
- Escolher o médico na solicitação **preenche sozinho** especialidade e telefone, a partir da
  lotação no município.

**O nosso cadastro**
- 1.639 médicos (`fhir.practitioner`).
- CPF válido em 99,9%, CRM em 99,9% (78,8% com UF) e especialidade em 88,5%.

**O cruzamento**
- Só **177** do SER casam com algum nosso, e só **74 com segurança** (por CPF ou CRM); 81% não
  casam com nada.
- **1.532** médicos nossos não estão no SER.

## Decisão

1. **Espelho à parte.**
   - `smsmarica.ser_profissional` guarda o profissional **como o SER mostra**, sem corrigir nem
     completar.
   - Nada daqui entra no nosso cadastro de Médicos, e nada do nosso cadastro é copiado para cá.
   - Chave natural: o CPF quando existe; senão, documento + tipo + nome normalizado.
   - Linhas repetidas no SER viram uma só, com a contagem de ocorrências.
   - Quem sai da pesquisa fica marcado "saiu do SER" e **não é apagado**.
2. **Ligação com o nosso médico por uma pessoa.**
   - A coluna `medico_id` (Practitioner, sem FK) é preenchida **só por confirmação humana** na tela.
   - A máquina pode **sugerir** (por CPF; por CRM com e sem o prefixo "52"), nunca ligar sozinha.
     O SER tem homônimos e nomes abreviados demais para casar por nome.
3. **Sem backfill.**
   - A maior falta do SER (CPF em 95%) não se corrige pela tela: o CPF é a chave do formulário, e
     informá-lo num cadastro antigo cria **outro** cadastro.
   - Não existe edição: o "Editar" dá 500, e o formulário não tem CRM.
   - O ganho seria em no máximo 74 registros confiáveis.
4. **A solicitação guarda o NOME do médico**, nunca o `value` do combo. O envio resolve o índice
   pelo texto na hora. Especialidade e telefone não são campos nossos.
5. **Importação só lendo.**
   - "Importar do SER" percorre a pesquisa com o filtro vazio, pela sessão de sincronismo, e só
     passa pela trava de somente-leitura (`SubmeterFormAsync`).
   - Roda em fila de fundo (capacidade 1). A tela acompanha pelo resumo.
   - Leitura que volta com menos da metade do que havia é tratada como **parcial** e não altera
     nada.
6. **Envio de médico novo ao SER — desenhado, desligado.** É a primeira escrita no cadastro do
   Estado e uma exceção ao D-11 do ADR-0052. Só liga com OK explícito do Bernardo, depois de:
   - **(a)** um ensaio do Gravar autorizado, para medir o que o servidor exige;
   - **(b)** a tabela de **especialidades do SER** à parte, com rótulo e ordem (o value é
     posicional e há rótulos repetidos), e a de **CBO**;
   - **(c)** o fluxo:
     1. buscar o CPF;
     2. se o profissional existe no Estado, só **acrescentar a lotação** de Maricá;
     3. se não existe, nome + telefone + lotação;
     4. Gravar.
     - Ao final, **reler a ficha** para provar o resultado; "salvo com sucesso" não é prova.
   - Usa `SubmeterEscritaAsync` com a credencial do operador (autoria no SER), nunca a de
     sincronismo.

SISREG e ESUS ficam de fora por ora:
- no SISREG, os profissionais vêm do CNES por unidade (`PROFISSIONAIS_POR_UPS`) e não há
  cadastro pelo município;
- no ESUS de São Gonçalo não achamos cadastro de profissional entre os endpoints mapeados.

## Consequências

- **Nova tela, Regulação → SER → Médicos** (módulo RegulacaoSer): resumo, filtros, "Importar do
  SER", "Ligar a médico nosso" / "Desligar". O artigo do manual é `ser-medicos`.
- O lixo do SER fica contido no espelho e na lista do formulário. O nosso cadastro não muda.
- **"Médico não está na lista"** continua se resolvendo no SER, até o envio ser liberado.
- **Quando liberar o envio**, a ligação confirmada evita duplicar no Estado um médico que já
  está lá.

## Complemento — 02/10/2026: médico pedido na abertura fica PENDENTE

Decisão do Bernardo. Substitui a linha "Médico não está na lista continua se resolvendo no SER".

- **Quem pode abrir solicitação pode procurar e pedir médico.** Na Nova Solicitação, quando o
  médico não está na lista do destino (SER ou SERNIT), "Incluir médico" abre o modal com os
  mesmos campos do modal do SER: nome (gravado em MAIÚSCULAS), tipo de documento (CRM, CNS, RG,
  CPF, PMM, RMS), número e especialidade. Só o nome é obrigatório.
- **Antes de pedir, "Já existe?".** A busca compara palavra a palavra, entendendo abreviação e
  inicial ("ANDRADE" = "A.") e sobrenome a mais. Medido em 01/10/2026: "LAURA BEATRIZ ANDRADE
  RODRIGUES" ia ser cadastrada de novo, e já existia como "LAURA BEATRIZ A. RODRIGUES VILELA". A
  regra está em `SemelhancaNome` e só **sugere** — quem escolhe é a pessoa.
- **O pedido não escreve no SER.** Fica em `smsmarica.regulacao_medico_pendente`, e a solicitação
  guarda o médico como `pendente:{id}`. Outra unidade que procurar o mesmo médico encontra o
  pedido e o reaproveita.
- **Quem cadastra no sistema é o técnico da regulação**, pela tela do próprio SER (ícone "Adicionar
  médico", que no modal da solicitação TEM tipo e número de documento — diferente do formulário
  de Cadastro → Profissionais medido acima). No detalhe da solicitação ele resolve:
  **Cadastrei**, **Já existia** (a solicitação passa a usar o nome de lá) ou **Recusado** (com
  motivo para a unidade). O "Registrar envio" fica barrado enquanto o médico estiver pendente.
- A decisão 6 (envio automático de médico ao SER) continua desligada: a escrita no cadastro do
  Estado segue sendo de uma pessoa.

## Complemento — 08/10/2026: a decisão 6 liga, com o "Autorizo" do regulador a cada médico

Decisão do Bernardo, depois de ver o cartão "Médico novo a cadastrar no SER" no meio do envio. Muda
a última linha do complemento de 02/10: a escrita no cadastro do Estado continua sendo de uma
pessoa, mas quem digita é a plataforma, com a autorização expressa dessa pessoa.

- **Onde:** no próprio envio automático (ADR-0069). A **prévia** deixa de barrar o médico pendente.
  Ela segue preenchendo a tela e devolve o bloco "Médico não cadastrado", que traz:
  - o que a unidade pediu;
  - os **nomes parecidos do combo "Médico responsável" de hoje**, por `SemelhancaNome`, mais o
    documento igual no espelho `ser_profissional` (só quem está no combo);
  - as especialidades do modal do sistema, com uma sugestão ("ONCOLOGISTA" → "ONCOLOGIA").

  O **envio** continua barrando enquanto o médico não estiver resolvido.
- **"É este"** resolve como **Já existia** (a solicitação passa a usar o nome de lá).
- **"Autorizo cadastrar"** (`POST regulacao/solicitacoes/{id}/envio-automatico/medico`, com
  `autorizo: true` obrigatório):
  1. reabre a tela de criação;
  2. se o nome já está no combo, **não grava** e resolve como Já existia;
  3. passa o pendente para **`CadastroIncerto`** numa atualização condicional (é a trava contra
     duplo clique e contra dois reguladores);
  4. clica o ícone `form0:addMedico` (navegação, pela trava de somente-leitura);
  5. preenche `formModalAdicionarMedico`: nome, tipo e número do documento, e a especialidade
     escolhida **pelo rótulo depois de abrir o modal** (o `value` é índice de view; há rótulos
     repetidos). O CPF fica vazio;
  6. aciona o **Gravar do modal** por `SubmeterEscritaAsync`, com a sessão do operador e a
     operação nomeada no log;
  7. **confere se o nome aparece no combo**; se a resposta não re-renderizou o combo, reabre a tela.
- **Desfechos:**
  - **nome na lista** → `Cadastrado`, com o nome de lá, e troca nas solicitações;
  - **mensagem de validação e nome fora** → volta a `Pendente` (nada criado);
  - **qualquer outro caso depois do Gravar** → fica `CadastroIncerto`. Ninguém tenta de novo
    sozinho, porque o SER não apaga e repetir duplica. O regulador confere no sistema e resolve
    no cartão: **Já existia** (escolhe o cadastro) ou **Não entrou** (volta a pendente).
    "Cadastrei" não é aceito nesse estado.
- Vale para **SER e SERNIT**: o modal é o mesmo nas duas instâncias.
- **O ensaio do Gravar (6a) é o primeiro uso real.** Não existe ensaio sem escrita: o SER não
  apaga. O modal foi lido da captura do laboratório (`Automais.SER/capturas/criar_aba_editar.html`;
  no SERNIT, `Automais.SERNIT/capturas/nova.html`). O que o servidor exige no Gravar só o primeiro
  cadastro mostra — e a conferência pelo combo é o que impede tomar silêncio por sucesso.
- `SituacaoMedicoPendente.CadastroIncerto = 5`, guardado como inteiro, **sem migration**.

### Ajuste — 09/10/2026: o regulador só autoriza; quem cadastra é o envio

O combinado era: o técnico confere os nomes parecidos e **autoriza**, e o cadastro acontece **no
processo de inserção** do SER. O que foi ao ar em 08/10 tinha um botão "Cadastrar no SER" à parte
(endpoint `envio-automatico/medico`), e o cartão da solicitação ainda pedia "Cadastrei no SER".
Corrigido:

- **O endpoint `POST .../envio-automatico/medico` sai.** O "Autorizo" (nome, documento,
  especialidade, `autorizo: true`) vai em `medicoNovo`, no **próprio** `POST .../envio-automatico/enviar`.
- **No envio**, antes de a solicitação mudar de estado: abre a tela de nova solicitação, faz os
  passos 2 a 7 acima no "Adicionar Médico" e, com o nome conferido na lista, relê a solicitação (que
  passou a ter o nome de lá) e segue o envio de sempre. Se o médico não der certo, **a solicitação
  não é enviada** e continua em análise; os desfechos do médico são os mesmos.
- Sem `medicoNovo`, médico fora da lista barra o envio com a pergunta ("escolha um parecido ou
  autorize").
- **Cartão do médico na análise** (solicitação com envio automático): sem "Cadastrei" nem "Já
  existia"; fica o aviso de que o médico se resolve no envio e o **Recusar**. O "Cadastro a conferir"
  continua com "Já existia" e "Não entrou". A fila de pedidos de cadastro segue como estava.
- O clique no ícone e o Gravar do modal mandam `AJAXREQUEST=_viewRoot`, o que o navegador manda
  (conferido no `framework.pack.js`; `docs/ser.md` §3.1).
