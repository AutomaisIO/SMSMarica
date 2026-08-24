# ADR-0045 — Só se extrai um serviço que tenha dono externo

**Status:** aceito · **Data:** 2026-08-17
**Relacionado:** [ADR-0004](./0004-arquitetura-tres-projetos.md) (3 projetos, supersede o Modular
Monolith), [ADR-0010](./0010-servico-fhir-autonomo.md) (Automais.Fhir),
[ADR-0015](./0015-assinatura-laudos-agente-itext.md) (Automais.Assinador),
[ADR-0043](./0043-instancia-por-municipio.md), [ADR-0044](./0044-app-meta-unico-e-roteador-whatsapp.md).

## Contexto

Com o produto virando multi-município, voltou a pergunta: *"eu não deveria ter uma API e um banco
para mensageria, outra para telefonia, outra para motoristas?"*

Ela já apareceu antes, em outra forma. O scaffold inicial seguiu o [ADR-0002](./0002-modular-monolith.md)
— Modular Monolith com 44 projetos (9 módulos × 4 camadas) — e foi desfeito pelo
[ADR-0004](./0004-arquitetura-tres-projetos.md), que registra: *"rejeitada após uso real"*. O que
se propõe agora é a versão mais cara daquilo: não só projetos separados, mas processos e bancos.

Desta vez a decisão foi tomada com medição, em 2026-08-17, sobre o código e o histórico do
repositório. Os números estão abaixo porque são o valor deste documento — sem eles, a pergunta
volta em três meses e é respondida por intuição de novo.

### O que as separações já feitas custaram

Este repositório já tem três serviços fora do monolito. Medindo commits que tocam cada um **e
também** `SMSMarica.server` no mesmo commit:

| Serviço | commits | também mexem no monolito | |
|---|---:|---:|---|
| `Automais.Fhir` | 31 | **16 (51,6%)** | fronteira que não separou mudança |
| `Automais.Assinador` | 7 | **4 (57,1%)** | idem |
| `Telefonia/PABX` | 4 | **0 (0%)** | a única fronteira real |

O ADR-0010 prometia "deployável e versionável de forma independente". O que se materializou foi
**deploy** independente, não **mudança** independente.

**`Automais.Fhir` — o caso mais instrutivo:**

- O código no monolito que existe *só* para falar com ele — clientes HTTP + mappers FHIR — soma
  **4.119 linhas**. O serviço inteiro, sem migrations, tem **4.214**. *O lado cliente da fronteira
  tem o tamanho do serviço.*
- **Zero endpoints com autenticação ou autorização**, contra 442 `[RequerPermissao]` no monolito.
  Auth "própria" era o próximo passo do ADR-0010; nunca chegou. Escuta em `0.0.0.0:5081`.
- A fronteira **vaza**: `Automais.Fhir.Api/Program.cs:19-23` põe `smsmarica` no `search_path`
  porque a busca de paciente precisa do `unaccent()` instalado por migration do monolito — no
  mesmo arquivo em que `FhirDbContext.cs:9` afirma "este DbContext não conhece o schema smsmarica".
- `Fhir:BaseUrl` **não está configurado em lugar nenhum** — produção roda no default
  `http://localhost:5081/`. Os "dois serviços" são co-residentes conversando por loopback.
- Constantes de `identifier.system` foram centralizadas no hub "para nunca duplicar string" e
  estão **redeclaradas 8 vezes** no monolito; `PacienteFhirMapper.cs:29` admite: *"espelham
  FhirSystems do hub"*.

**`Automais.Assinador`:** **3 workflows / 14.929 bytes de YAML** para **548 linhas** de serviço —
mais YAML que o `deploy-server.yml` do monolito inteiro. O CI não roda os testes dele.

**`Telefonia/PABX`** é a única com 0% de sobreposição — e mesmo ela pagou o preço da cópia que a
separação exigiu: duplicou `Telefonia/registro/unidades.csv` para dentro do serviço, **as duas
cópias já divergiram nas 30 linhas de dados**, e como o `UnidadeSeeder` faz upsert a **todo boot**,
um restart do PABX reverte o status das unidades para o estado de julho. É um defeito de dado
vivo, causado pela duplicação.

### Os dois candidatos, medidos

**Mensageria** parece a melhor candidata pelo grafo de dados e é a pior na prática:

| | |
|---|---|
| FKs saindo / entrando | 8 / **0** |
| Services de domínio que dependem dela | 8 |
| Controllers/hubs que dependem dela | 12 |
| Arquivos de fora acessando `db.ComunicacoesPaciente` direto | **11** (15 pontos) |
| Cadeias `.Include` cruzando a fronteira | 12 |
| Fluxos hoje em transação única que virariam 2 chamadas de rede | **7** |
| `BeginTransactionAsync` explícita cruzando | 1 (`CorrecaoIdentidadeExameService.cs:121,190`) |

E o impedimento decisivo: **`usuario_unidade` mora em `Data/Entities/Conversas/`**, mas é a
tabela que `Core/Common/Unidades/EscopoUnidade.cs:59` lê para escopar exames, consultas, laudos,
estatísticas e painel — e o faz **fail-closed** (quem não tem vínculo não vê nada; a inversão de
2026-07-30 está documentada no XML-doc do próprio arquivo, que a atribui ao "ADR-0037" — decisão
citada no código mas **sem arquivo em `docs/adr/`**).
Mandá-la para outro banco significa que **uma indisponibilidade da API de mensageria faz todo
usuário não ver nada.**

**Transporte/TFD** tem a fronteira de dados mais limpa do sistema — **2 FKs saindo, 0 entrando,
0 services de fora o injetam**, só 2 arquivos de fora tocam seus DbSets — e mesmo assim não deve
ser extraído: é o módulo **menos entregue**. O endpoint da PWA do cidadão é um stub
(`CidadaoController.cs:53` devolve `Array.Empty`), o app do motorista é mock salvo por um
endpoint de GPS, e o `RastreamentoHub` não tem cliente. Extrair um módulo inacabado é otimizar a
coisa errada.

### E a dor que motivou a pergunta é menor do que parece

"Deploy tudo ou nada" já é falso: são **8 workflows com filtro de `paths`** — mexer na telefonia
não redeploya a API de saúde. Os deploys do server levam **3m30 a 6m40**, todos verdes. O que é
monolítico é só o interior de `SMSMarica.server`.

## Decisão

**Só se extrai para um serviço próprio aquilo que tem um DONO EXTERNO impondo a fronteira.**

Dono externo é uma destas três coisas:

1. **Uma restrição jurídica** — ex.: o iText é AGPL e precisa ficar isolado do produto fechado
   (ADR-0015). A razão é legal, não técnica.
2. **Um host ou operador de terceiro** — ex.: o PABX roda dentro do servidor VOIP da FalarMais,
   que atende outros clientes, e não toca dado de paciente.
3. **Um recurso compartilhado entre clientes** — ex.: o App único da Meta, cujo webhook é por App
   e precisa ser distribuído entre instâncias (ADR-0044).

**Não contam como justificativa:** tamanho do domínio, seção de menu, "está grande demais",
"quero deployar separado", ou desejo de fronteira arquitetural. Para isso existem pastas dentro
dos 3 projetos, que é o que o ADR-0004 decidiu.

Aplicando o critério:

| Candidato | Dono externo | Decisão |
|---|---|---|
| Assinatura de laudo | iText AGPL + certificado na máquina do médico | já extraído, correto |
| Telefonia | servidor VOIP de terceiro, zero dado de paciente | já extraído, correto |
| WhatsApp (**roteador**) | webhook por App da Meta, N clientes | **extrair** — ADR-0044 |
| Mensageria (domínio) | — | manter no monolito |
| Transporte / TFD | — | manter no monolito |
| Hub FHIR | — (extraído por outra razão, ADR-0010) | manter como está; ver pendências |

## Consequências

- Uma pergunta recorrente passa a ter resposta com evidência, e não com preferência.
- O monolito continua crescendo por pastas. Quando um módulo *ganhar* um dono externo — um
  cliente que compre só telefonia, um regulador que exija isolamento — o critério dispara e a
  extração é justificada por escrito.
- Nada disso impede fronteiras internas melhores; ao contrário, ver "Trabalho que substitui a
  extração".

## Trabalho que substitui a extração

Três itens atacam a dor real por uma fração do custo:

1. **Ligar/desligar módulo por instância — hoje não existe.** Busca por `FeatureFlag`,
   `Modulos:`, `ModuloHabilitado`: **zero resultados**. E os **15 `AddHostedService` de
   `Core/DependencyInjection.cs` sobem incondicionalmente** (linhas 158, 161, 167, 180, 290, 291,
   328, 334, 358, 368, 385, 440, 455, 456, 459). Tirar o módulo do perfil some com a tela e **não
   para o worker** — `EscopoUnidade.cs:29-40` documenta que job sem `HttpContext` "vê tudo". Isto
   é pré-requisito do produto multi-município, não melhoria.
2. **Mover `UsuarioUnidade` para fora de `Entities/Conversas/`.** É a tabela de escopo de 5
   módulos, não de conversas. Só o código muda de pasta — a tabela `usuario_unidade` não se mexe,
   sem migration.
3. **Fechar o acesso direto a `comunicacao_paciente`.** 11 arquivos de fora leem/escrevem o DbSet
   sem passar por `IComunicacaoPacienteService`, que já existe. É a fronteira que importa — e a
   que teria que existir de qualquer forma antes de qualquer extração futura.

## Pendências abertas

Levantadas pelas mesmas medições, sem decisão ainda:

1. **`automais-fhir` escuta em `0.0.0.0:5081` sem autenticação alguma**, incluindo
   `GET /fhir/_estatisticas` (10 `COUNT` sobre ~10,5M linhas). Mínimo: bind em `127.0.0.1`.
2. **`AddDataProtection()` sem `PersistKeysToFileSystem` nem `SetApplicationName`**
   (`Api/Program.cs:85`) — é esse anel que decifra **todas** as credenciais de integração do banco.
3. **Telefonia: `unidades.csv` duplicado e divergido**, revertendo status a cada boot.
4. **`CLAUDE.md` descreve arquitetura que não existe mais.** A regra 1 afirma "um único
   `SmsMaricaDbContext`" e "FKs cross-schema `smsmarica → fhir`" — falso desde o cutover do
   ADR-0010 (`grep 'schema: "fhir"'` em `SMSMarica.Data` devolve 0). A regra 8 e o
   [ADR-0007](./0007-schema-fhir-separado.md) descrevem `ck_usuario_papel_unico` como valendo:
   **essa constraint não existe no banco** — a única `HasCheckConstraint` do modelo é
   `ck_agenda_recurso_por_finalidade`, e as colunas `patient_id`/`practitioner_id`/`motorista_id`
   em `usuario` também não existem (a FK real é `motorista.usuario_id`). É o primeiro arquivo que
   devs e agentes leem.
5. **Decisões citadas no código sem ADR escrito.** `EscopoUnidade.cs` atribui a inversão
   fail-closed ao "ADR-0037" e a extração da cascata ao "ADR-0033"; não há arquivo para nenhum
   dos dois em `docs/adr/` (a numeração salta de 0023 para 0036). Uma regra de segurança cuja
   justificativa só existe num comentário é uma regra que a próxima pessoa desfaz sem saber.
