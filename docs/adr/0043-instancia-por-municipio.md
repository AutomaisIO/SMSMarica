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

> **Revisado em 25/09/2026, item por item.** Quatro das cinco já estavam resolvidas e a lista não
> dizia — o que é perigoso nos dois sentidos: lista que não se atualiza deixa de ser lida, e foi
> justamente assim que o item 2 ficou valendo em **produção** por meses (ver abaixo). Ao fechar
> um item, marcar aqui.

1. **ABERTA — `MetaSources.Base`** (`Automais.Fhir.Core/Fhir/MetaSources.cs`) é
   `https://smsmarica.saude.marica/source/` fixo em código, gravado em `meta.source` de todo
   recurso e reconstruído em 19 arquivos. Vira variável de ambiente **mantendo o valor atual como
   default** — mudá-lo em Maricá invalidaria a proveniência já gravada.
   *Conferido em 25/09/2026: segue `private const string Base` fixo.*

2. ~~**`Automais.Fhir` escuta em `0.0.0.0:5081` sem autenticação alguma.**~~
   **RESOLVIDA em 24/09/2026 — e não era hipótese: estava ABERTA NA INTERNET.** Medido naquele
   dia: `ufw` inativo, serviço em `0.0.0.0:5081`, e `GET http://<ip>:5081/fhir/Patient` respondendo
   **HTTP 200 sem token** de fora — 378.188 pacientes com CPF/CNS/endereço/telefone e todo o
   clínico, com `POST`/`PUT`/`DELETE` disponíveis. O host está sob varredura automatizada contínua
   (scanners de CVE e `GET /.env` no log do nginx). Corrigido: bind em `127.0.0.1` no unit **e** no
   `deploy-fhir.yml` (que tinha `0.0.0.0` fixo e reabriria no próximo deploy).
   Detalhes em `Automais.prime/docs/APRENDIZADOS.md` §33.

3. ~~**Termo de consentimento LGPD** com o texto em `const`.~~ **RESOLVIDA.** Virou
   `record TermoVigente(Texto, Hash)` montado por instância, com o texto de Maricá saindo
   byte-idêntico ao 1.0 — os hashes já gravados seguem válidos.

4. ~~**Seeds institucionais de Maricá** rodam no startup.~~ **RESOLVIDA.** Atrás da flag
   `Seeds:ConteudoMarica` (padrão `false`), lida em `Program.cs` e passada como
   `incluirConteudoMarica` ao `DbSeeder`.

5. ~~**`httpClient.ts` cai em `api.smsmarica.online` sem a env.**~~ **RESOLVIDA.** Agora
   **lança** em build de produção sem `VITE_API_BASE_URL`, em vez de subir calado apontando para
   Maricá.

### Achado adjacente, fora do escopo original deste ADR
`smsmarica-server` escuta em **`0.0.0.0:5080`** no servidor de Maricá (conferido 25/09/2026),
embora **todos** os workflows de deploy já especifiquem `127.0.0.1`. O unit é antigo: o deploy só
o escreve `if [ ! -f "$UNIT_FILE" ]`, então correções de unit nos workflows **nunca alcançam
servidor já provisionado**. Menos grave que o item 2 — a autenticação funciona (`/pacientes` →
401) e o único bypass é o TLS, já que o nginx do `api.` não tem rate limit nem WAF —, mas é HTTP
puro numa porta pública. Corrigir alinhando o unit ao que o workflow já manda.

## Alternativas rejeitadas

Ver a seção de contexto: (a) tenant compartilhado e (b) fork. Ambas foram medidas, não descartadas
por gosto.
