# ADR-0043 — Uma instância por município: whitelabel por configuração, sem tenant compartilhado

**Status:** aceito · **Data:** 2026-08-17
**Implantação:** em andamento — Fase 1 (tabela `instituicao` + endpoint público + saída dos
textos fixos do backend) implementada em 17/08/2026; branding do front, infra parametrizada e
runbook de provisionamento pendentes.

## Contexto

O sistema nasceu para a SMS de Maricá e está em produção há mais de um ano. Surgiu a demanda de
entregá-lo a uma **segunda prefeitura** (também no RJ), com a intenção de que ele vire produto
replicável para N municípios. O escopo da primeira entrega é enxuto — mensageria, cadastros e o
hub FHIR sem prontuário —, mas o prazo é de semanas.

Três desenhos foram considerados e **medidos contra o código real**, não contra a intuição:

### (a) Multi-tenant no mesmo banco (`TenantId` + filtro global)

Descartado. Os números explicam:

| Fato medido | Número |
|---|---|
| `HasQueryFilter` em `SMSMarica.server/src` | **0 ocorrências** |
| Services em `SMSMarica.Core` | **173** |
| Services que aplicam escopo de unidade | **9** (5 via `EscopoUnidade`, 4 via `UnidadeAtivaId` cru) + 2 com mecanismo próprio em Conversas |
| DbSets em `SmsMaricaDbContext` | **108** |
| DbSets com coluna de unidade | **15** |
| Índices únicos globais que colidiriam entre municípios | **~13** |
| Tabelas singleton de linha única | **7** |
| Tabelas do schema `fhir` com noção de tenant | **0** |
| Endpoints do `Automais.Fhir` protegidos por autenticação | **0** |

O filtro por unidade é **manual por decisão registrada** — `Core/Common/Unidades/EscopoUnidade.cs`
resolve *quais* unidades o usuário enxerga, mas o `.Where` fica em cada chamador, porque cada
entidade chega à unidade por um caminho diferente. Não existe, portanto, **nenhum choke point**
— nem query filter, nem middleware, nem claim de JWT — onde um `TenantId` pudesse ser plugado:
~95% dos services simplesmente não consultam escopo nenhum.

Pior, a cascata do `EscopoUnidade` tem dois bypasses que são corretos hoje e letais entre
municípios: *"sem usuário no contexto ⇒ vê tudo"* (cobre todos os jobs, workers e importadores) e
o token `X-API-Key`, que pula a verificação de permissão inteira
(`Api/Auth/RequerPermissaoAttribute.cs`).

E as colisões de unicidade não são teóricas: `usuario.cpf` é único global, e **um médico que
atende em dois municípios já quebra o cadastro**.

Somando: refatoração de meses sobre dados de saúde em produção, tendo vazamento entre municípios
como modo de falha. Custo alto, risco alto, e nada disso é necessário para chegar à segunda
instância.

### (b) Fork / cópia do repositório

Descartado. O repositório tem 866 commits, 339 só em julho/2026. Duas cópias divergem em semanas
e cada correção passa a ser feita duas vezes — com a segunda sempre atrasada.

### (c) Whitelabel por configuração, uma instância por município

Aceito. O que decidiu foi a medição do acoplamento: dos ~42.800 hits de "Maricá" no repositório,
**~37.300 (87%) estão nas 257 migrations do EF** (nomes de tipo qualificados + o schema literal) e
**~2.800 são linhas `namespace`/`using`**. Isso é ruído mecânico que nenhum cliente vê.

O acoplamento **semântico** — texto visível, domínio, CNES, número de WhatsApp, ativos de marca,
bundle ids, URN de proveniência FHIR — cabe em **~250 ocorrências em ~143 arquivos** fora de
`docs/`, mais 18 binários. É pouco, é localizável, e é exatamente o que esta decisão externaliza.

## Decisão

**Um repositório, um `main`, uma base de código. Cada município é uma instância própria** —
droplet, banco, domínio e credenciais separados —, pintada por configuração.

Três regras derivadas, que não podem ser violadas sem novo ADR:

1. **`SMSMarica` (namespace/assembly) e `smsmarica` (schema) NÃO são renomeados.** Viram o nome de
   código interno do produto, como `Automais.Fhir` já é. Renomear exigiria reescrever as 257
   migrations — violando a regra de migrations imutáveis ([ADR-0004](./0004-arquitetura-tres-projetos.md),
   regra 4 do `CLAUDE.md`) — com risco alto e ganho zero: o cliente nunca vê um namespace.

2. **Nenhum `TenantId` em lugar nenhum.** O isolamento é físico: processos e bancos separados. O
   `X-Unidade-Id` continua sendo o que sempre foi — escopo **entre unidades de um mesmo
   município** —, e não ganha um segundo nível.

3. **Nada institucional volta a nascer em código ou em migration.** Vai para a tabela
   `smsmarica.instituicao` (singleton) ou para variável de ambiente. Em particular: **seed
   institucional nunca mais entra em migration**, porque migration é imutável e roda igual em toda
   instância nova.

### A tabela `instituicao`

Singleton com PK fixa, no mesmo padrão de `laudo_configuracao`. Guarda identidade (nome da
prefeitura e da secretaria, sigla, CNPJ, código IBGE, UF, DDD, endereço, telefone), contatos
legais (contato público e DPO), marca (logo e favicon por FK para `midia`, quatro cores em hex) e
os domínios da instância.

Exposta **sem autenticação** em `GET /publico/instituicao`, porque o painel e os PWAs precisam da
marca para desenhar a própria tela de login. Consequência direta: **nenhum segredo entra nessa
tabela** — credenciais continuam cifradas nas tabelas de integração, sob Data Protection.

Duas validações de entrada não são preciosismo:

- **Cores só em `#RRGGBB`.** O valor é interpolado dentro de `<style>` numa página anônima, onde
  escapar HTML não protege. Recusar na entrada é a única defesa que não depende de quem renderiza.
- **URLs só http/https absolutas.** Viram link clicável em página pública.

E a assinatura do fornecedor (`assinatura_produto_html`) é sanitizada: é HTML renderizado na tela
de login, antes de qualquer autenticação.

### Não confundir com `Unidade`

`Unidade` é o estabelecimento de saúde — tem CNES, é o eixo durável do
[ADR-0039](./0039-unidade-de-saude-eixo-duravel.md) e existe às dezenas. `Instituicao` é o órgão
gestor da instância, e existe exatamente uma. A `Unidade` continua sem relação com o schema
`fhir`, como o ADR-0039 decidiu.

## Consequências

**Boas:**

- A segunda prefeitura não espera refatoração nenhuma: espera provisionamento.
- Isolamento de dados de saúde entre municípios passa a ser uma propriedade da infraestrutura, não
  uma invariante que 173 services precisam lembrar de respeitar.
- Cada município tem seu próprio ritmo de banco, backup e janela de manutenção.
- O caminho é reversível: se um dia houver razão real para tenant compartilhado, nada aqui atrapalha.

**Custos assumidos:**

- **N droplets e N bancos.** Custo linear de infraestrutura e de operação. É o preço do isolamento,
  e é o que a compra pública normalmente espera de qualquer forma.
- **N deploys.** Mitigado com GitHub Environments (um por instância) sobre os mesmos workflows.
- **Risco de drift entre instâncias** — uma com migration aplicada, outra não. Mitigado pelo
  runbook e pela conferência de `smsmarica.__migrations`, que já é obrigatória por causa do
  AutoMigrate que falha calado ([ADR-0021](./0021-runbook-deploy.md)).
- **O anel de Data Protection é por instância.** É ele que decifra todas as credenciais de
  integração gravadas no banco; provisionar uma instância nova inclui gerar e fazer backup do anel
  dela. Hoje `AddDataProtection()` é chamado sem `PersistKeysTo*` nem `SetApplicationName` — isso
  precisa ser corrigido antes do primeiro provisionamento.

## Pendências abertas por esta decisão

1. **`MetaSources.Base`** (`Automais.Fhir.Core/Fhir/MetaSources.cs`) é `https://smsmarica.saude.marica/source/`
   fixo em código, gravado em `meta.source` de todo recurso e reconstruído em 19 arquivos. Vira
   variável de ambiente **mantendo o valor atual como default** — mudá-lo em Maricá invalidaria a
   proveniência já gravada.
2. **`Automais.Fhir` escuta em `0.0.0.0:5081` sem autenticação alguma.** Uma segunda instância
   replicaria a exposição. Mínimo para a nova: bind em `127.0.0.1`.
3. **Termo de consentimento LGPD** (`Core/Cidadao/TermoConsentimento.cs`) tem o texto em `const` e
   o SHA-256 é gravado em cada aceite. Ao virar template, o render para Maricá tem de sair
   byte-idêntico ao texto atual, sob teste — senão os aceites existentes viram inválidos.
4. **Seeds institucionais de Maricá** (`Api/Auth/SeedCabecalhoLaudo.cs`, `Api/Auth/DbSeeder.cs`)
   rodam no startup e precisam ficar atrás de uma flag. As migrations de seed já aplicadas não
   podem ser editadas — numa base nova elas semeiam dados do CDT, que o provisionamento remove.
5. **`httpClient.ts` dos três fronts** cai em `https://api.smsmarica.online` quando
   `VITE_API_BASE_URL` não vem. Um build sem env aponta silenciosamente para Maricá; tem de falhar
   alto.

## Alternativas rejeitadas

Ver a seção de contexto: (a) tenant compartilhado e (b) fork. Ambas foram medidas, não descartadas
por gosto.
