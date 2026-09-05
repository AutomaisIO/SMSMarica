# 07 — Credenciais pessoais por usuário (incremento 5)

## Objetivo

Cada usuário guarda, no próprio perfil, suas credenciais nos sistemas de regulação (SISREG, SER, SERNIT e futuros), de forma que **ninguém — nem o servidor em repouso — consiga lê-las sem a senha dele**. O agente regulador guarda ainda a lista de credenciais SISREG por unidade (padrão para todas + específica por unidade). Com credencial configurada, o SMSMais autentica direto; sem, o modal que já existe continua pedindo a senha. A sessão de operador (hoje só em memória para o SER/SERNIT) passa a ser alimentada por esse cofre e estendida ao SISREG, com arbitragem de sessão e orçamento por login.

## Requisitos cobertos

- **R-16 (A14)** credenciais pessoais no perfil; "hasheadas junto com a senha do operador"; lista de unidades SISREG com senha padrão + específica e toggle.
- **R-17 (A14)** NAR: o agente autentica com a credencial da unidade escolhida.
- **R-11 (A9)** envio com a senha do agente.

## Decisões aplicadas

- **D-1** envelope pela senha do usuário; reset perde; nenhum job em background usa credencial pessoal.
- **D-2** modal como fallback; configurada → autentica direto.
- **D-7** aviso fixo na tela (texto literal abaixo).
- **D-9** SISREG só inclui com senha do solicitante lotado na unidade; credencial de unidade = CNES + senha.

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Sessão de operador SER/SERNIT em memória, por jti, 8h, validada no sistema antes de guardar | sim | `SMSMais.Core/Ser/Sessao/SerSessaoOperadorStore.cs` (fábrica `Func<string,string,ISerWebSessao>` injetável), `SernitSessaoOperadorStore.cs`; endpoints `regulacao/ser/sessao` |
| Hook do front "executa a ação; sem credencial abre o modal e retoma" | sim | `features/ser/lib/sessaoSer.ts` (`useSessaoSerObrigatoria`), `ModalLoginSer.tsx` / `ModalLoginSernit.tsx` |
| Hash de senha do login | sim | `PasswordHasher` (PBKDF2-SHA512) em `SMSMais.Core/Identidade/`; `IdentidadeService.LoginAsync`, `AlterarMinhaSenhaAsync` (recebe `SenhaAtual`), `AlterarSenhaAsync`/`GerarNovaSenhaAsync` (admin) |
| JWT com `jti` e expiração 8h | sim | `JwtOptions.ExpiraEmMinutos = 480`; `IUsuarioAtualAccessor.SessaoId` |
| Cifra com Data Protection (defesa em profundidade por cima do envelope) | sim | `SMSMais.Api/Auth/ProtetorSegredos.cs` (`IProtetorSegredos`) |
| Store institucional por provedor (continua para os motores de leitura) | sim | `integracao_credencial`, `IIntegracaoCredencialService` |
| Sessão SISREG indexada por login com semáforo; orçamento rolante; detecção de CAPTCHA | sim | `SMSMais.Core/Integracoes/SisregWeb/SisregWebSessao.cs`, `SisregOrcamentoRequisicoes.cs` (`CodigoCaptcha = "sisreg.captcha_exigido"`) |
| Modelo antigo de credencial SISREG por unidade | removido | migration `20260725154214_AddSisregMapeamentoECredencialUnidade` (para consulta do desenho) |
| Página "Meu perfil" e endpoints só-JWT | sim | `features/usuarios/pages/MeuPerfilPage.tsx`, `GET/PUT /identidade/me`, `PUT /identidade/me/senha` |
| Auditoria sem segredo | sim | `IAuditoriaService.RegistrarAsync` |

O que **não** existe: qualquer credencial pessoal persistida; cofre por usuário; sessão SISREG por operador humano; tela de credenciais; endpoints `me/credenciais-integracao`.

## Desenho

### Modelo criptográfico (D-1)

- No **login**, depois de `VerifyHashedPassword` dar certo, `ICofreUsuarioService.Abrir(usuarioId, senhaEmClaro, jti)`:
  - deriva `KEK = PBKDF2-SHA512(senha, sal_cofre, 600.000 iterações, 32 bytes)` — `sal_cofre` é **distinto** do sal do `SenhaHash`;
  - desembrulha `DEK = AES-256-GCM_decrypt(KEK, dek_embrulhada)` (a `dek_embrulhada` ainda passa por `IProtetorSegredos.Unprotect` — Data Protection por cima, defesa em profundidade);
  - guarda a DEK em `CofreSessaoStore` (memória, chave = jti, expira por inatividade em 8h, `Encerrar(jti)` no logout).
- Primeira vez (sem cofre): gera DEK aleatória, embrulha e cria `usuario_cofre`.
- Cada credencial: `senha_cifrada = AES-256-GCM_encrypt(DEK, senha)` (nonce + tag na coluna). Login do sistema externo em claro (não é segredo).
- **Troca de senha pelo usuário** (`AlterarMinhaSenhaAsync`, tem `SenhaAtual`): abre com a antiga, re-embrulha a DEK com a nova, `reembrulhada_em`.
- **Reset pelo admin / esqueci a senha** (`AlterarSenhaAsync`, `GerarNovaSenhaAsync`, fluxo de recuperação): `usuario_cofre.invalidada_em = agora`, `senha_cifrada = NULL` em todas as credenciais do usuário, evento de auditoria. No próximo login com a senha nova, cofre novo é criado vazio; a tela mostra "suas credenciais foram apagadas no reset; cadastre de novo".
- **Restart da API**: `CofreSessaoStore` some. O JWT continua válido, mas sem DEK. Qualquer ação que precise de credencial devolve `cofre.fechado` → o front abre um modal **"Confirme sua senha do SMSMais para destravar suas credenciais"** (uma vez por sessão), que chama `POST /identidade/me/cofre/abrir` com a senha; não é o modal do SER.
- **Vários dispositivos**: cada login deriva a DEK de novo; funciona.
- **Jobs em background**: não têm DEK; nunca usam credencial pessoal (D-1). Motores de leitura seguem na credencial institucional.

O que este modelo **não** protege: um servidor comprometido enquanto o usuário está logado (a DEK está em memória); um atacante que capture a senha do usuário. É o mesmo limite de qualquer cofre destravado por senha.

### Tabelas

**`usuario_cofre`** (1:1 com `usuario`): usuario_id PK, kdf (`"pbkdf2-sha512"`), iteracoes, sal (bytea 16), dek_embrulhada (text), versao, criado_em, reembrulhada_em, invalidada_em.

**`usuario_credencial_integracao`**

| Coluna | Nota |
|---|---|
| id, usuario_id FK | |
| provedor | `sisreg` / `ser` / `sernit` / `esus` (mesmo vocabulário de `ProvedoresIntegracao`) |
| escopo | `Pessoal=1`, `UnidadesPadrao=2`, `Unidade=3` |
| unidade_id FK NULL | obrigatório se `escopo = Unidade` |
| login | text; para SISREG por unidade, o CNES vem de `unidade.cnes` (D-9) |
| senha_cifrada | bytea NULL (NULL = invalidada pelo reset) |
| usar_padrao | bool; só `escopo = Unidade`: `true` = herda login/senha de `UnidadesPadrao` e só muda o CNES; `false` = usa login/senha desta linha |
| validada_em, validacao_resultado | teste contra o sistema |
| ultimo_uso_em, ultimo_uso_resultado, falhas_consecutivas, bloqueada_ate | CAPTCHA / senha errada; 3 falhas seguidas bloqueiam 15 min |
| criado_em, atualizado_em, invalidada_em | |

Unique `(usuario_id, provedor, escopo, coalesce(unidade_id, uuid_nil))`.

Semântica SISREG (D-9): `Pessoal` = a credencial do usuário na unidade dele (o solicitante da UBS; é a que o "Enviar" do Interno usa). `UnidadesPadrao` e `Unidade` só existem para quem tem o módulo 48 (agente): são as credenciais "de unidade" para o NAR. **A confirmar no spike b** (README §9 q.7): se uma credencial padrão realmente autentica em qualquer CNES ou se cada unidade exige login próprio; o modelo suporta os dois.

### Resolução de credencial no uso

`SessaoOperadorIntegracaoStore` (substitui `SerSessaoOperadorStore` e `SernitSessaoOperadorStore`; chave `(jti, provedor, login)`):

`Exigir(provedor, unidadeId?)`:
1. sessão viva no cache → devolve;
2. cofre fechado → `cofre.fechado` (front pede a senha do SMSMais);
3. resolve a credencial: SISREG com `unidadeId` → `Unidade` habilitada e `usar_padrao=false` → senão `UnidadesPadrao` (com o CNES da unidade) → senão erro `credencial.ausente`; SISREG sem `unidadeId` e SER/SERNIT → `Pessoal`;
4. decifra com a DEK, cria a sessão (`SerWebSessao.UsarCredencialDoOperador` / fábrica SISREG por operador), **valida com login**, cacheia; falha → incrementa `falhas_consecutivas`, registra `ultimo_uso_resultado`;
5. `credencial.ausente` → o front abre o modal que já existe (D-2), agora com a caixa "salvar no meu perfil" (grava como `Pessoal`).

`Encerrar(jti)` no logout derruba DEK e sessões.

### Sessão SISREG por operador

- `ISisregSessaoFactory.ParaOperador(login, senha, cnes?)` extraída de `SisregWebSessao`: cookie jar, `Gate` e `SisregOrcamentoRequisicoes` **por login**; a instância institucional continua com o orçamento global dos motores. Chave por login (upper), não por usuário: dois agentes com a mesma senha padrão compartilham a sessão e se serializam no gate em vez de se derrubarem.
- **Aviso na UI** antes do primeiro uso na sessão: "O SMSMais vai entrar no SISREG como `LOGIN` (unidade X). Se você estiver com o SISREG aberto no navegador, aquela sessão cairá." Confirmação guardada por jti.
- Sem relogin em ping-pong: se após o login inicial a sessão cair (`SessaoInvalida`), falhar com mensagem clara ("alguém entrou com este login no navegador"); nunca relogar dentro da mesma submissão.
- Rajada curta + **logout explícito** ao fim de uma inclusão por unidade (NAR) para devolver o SISREG ao operador humano daquela unidade; sessão `Pessoal` fica viva até 8h de inatividade.
- CAPTCHA (`CodigoCaptcha`): `bloqueada_ate = agora + 24h` na credencial, evento `FalhaEnvio(captcha)`, instrução ao usuário para resolver no navegador; outras credenciais e a institucional seguem. Se o spike b mostrar que o CAPTCHA é por IP, isso vira bloqueio global e o plano 11 muda.

### Endpoints (só JWT, como `PUT /identidade/me/senha`)

- `GET /identidade/me/credenciais-integracao` → lista sem segredo: provedor, escopo, unidade, login, `definida`, `validada_em`, `bloqueada_ate`, `usar_padrao`.
- `PUT /identidade/me/credenciais-integracao/{provedor}?escopo=&unidadeId=` body `{login, senha?, usarPadrao?}` (senha vazia preserva).
- `DELETE …/{provedor}?escopo=&unidadeId=`.
- `POST …/{provedor}/testar?escopo=&unidadeId=` → autentica e devolve o resultado; grava `validada_em`.
- `POST /identidade/me/cofre/abrir` body `{senha}` (para o caso pós-restart).
- Escopos `UnidadesPadrao`/`Unidade` só aceitos se o usuário tem 48.
- Auditoria: `credencial_integracao.definida | removida | testada | usada (solicitacao_id)` — nunca o segredo.

### Tela "Minhas credenciais" (em `MeuPerfilPage`, nova seção)

- Topo, texto fixo (D-7): **"As senhas colocadas aqui ficam criptografadas no banco, onde ninguém tem acesso sem a sua senha. Se você resetar a senha ou perder a senha, essas senhas deverão ser cadastradas novamente."**
- Um cartão por provedor (SISREG, SER, SERNIT): login, senha (write-only, mostra só "definida em dd/mm"), botões Salvar · Testar · Remover; estado (validada / falhou / bloqueada até).
- **Só para 48**, bloco "SISREG — unidades": linha "Padrão para todas as unidades" (login/senha) + dropdown de unidades ativas; para cada unidade adicionada: toggle "usar padrão / usar senha desta unidade", login/senha próprios quando o toggle está em "desta unidade", Testar.
- O modal de fallback (`ModalLoginSer`, `ModalLoginSernit` e o novo do SISREG) ganha a caixa "Salvar no meu perfil" e repete o aviso D-7.
- Tela de reset de senha do admin (`FormularioUsuario`): aviso "ao resetar, as credenciais de integração deste usuário serão apagadas e ele precisará cadastrá-las de novo".

## Tarefas

- [ ] **5.1** Entidades `UsuarioCofre`, `UsuarioCredencialIntegracao` + enum `EscopoCredencialIntegracao` + configurações + migration `CofreDeCredenciaisPessoais`; `ICofreUsuarioService` (`Abrir`, `Fechar`, `Reembrulhar`, `Invalidar`, `Cifrar`, `Decifrar`); `CofreSessaoStore`; ganchos em `IdentidadeService` (login abre; `AlterarMinhaSenhaAsync` re-embrulha; reset/admin invalida); testes de cripto (round-trip, senha errada, reset apaga).
- [ ] **5.2** `UsuarioCredencialIntegracaoService` + endpoints `me/credenciais-integracao/*` e `me/cofre/abrir`; auditoria; regra de escopo por módulo 48.
- [ ] **5.3** Front: seção "Minhas credenciais" em `MeuPerfilPage` (com o aviso D-7), bloco de unidades SISREG para 48, aviso no reset do admin.
- [ ] **5.4** `SessaoOperadorIntegracaoStore` genérico; migrar `SerSessaoOperadorStore`/`SernitSessaoOperadorStore` e os endpoints `regulacao/ser/sessao` (manter compatibilidade); `useSessaoSerObrigatoria` → `useSessaoIntegracaoObrigatoria(provedor)` com a cadeia cache → cofre → credencial → modal; caixa "salvar no meu perfil"; modal "confirme sua senha do SMSMais".
- [ ] (plano 11, inc. 7) `ISisregSessaoFactory.ParaOperador` + orçamento/CAPTCHA por login + aviso + logout.
- [ ] Testes: `Exigir` resolve Unidade → UnidadesPadrao → Pessoal; cofre fechado devolve o código certo; reset invalida e a tela recadastra; sessão por login compartilhada entre dois agentes com a mesma senha padrão.

## Dependências

Nenhum plano anterior para 5.1–5.3. O uso real vem com 12 (SER/SERNIT) e 11 (SISREG).

## Riscos e pontos a confirmar

- **Reverte a decisão de 18/08** registrada em `SerSessaoOperadorStore.cs:23-26` ("cofre de credenciais pessoais do Estado — risco que a funcionalidade não paga"). O ADR-0053 precisa dizer por que agora paga: o envelope tira do servidor a capacidade de ler; o custo operacional é o recadastro no reset.
- "Hasheadas junto com a senha do operador" na transcrição foi lido como **derivar a chave da senha**; hash puro não serviria (a senha precisa ser reversível para logar).
- O passo "confirme sua senha do SMSMais" após restart é fricção real em dia de deploy; medir quantas vezes acontece.
- Credencial SISREG por unidade **foi removida em 25/07** com a conclusão de que "a unidade nunca foi propriedade da sessão". Aqui ela volta como propriedade **do agente** para uma unidade, o que é outra coisa — registrar no ADR.

## Testes

Unitários de cripto e do store; integração: login → cofre aberto → salvar credencial SER → `Exigir(Ser)` cria sessão com `SerWebSessaoFake` sem abrir modal; trocar senha → credencial continua; reset pelo admin → `senha_cifrada` nula → `Exigir` devolve `credencial.ausente` → modal.

## Fora de escopo

Escrita nos sistemas (11, 12); credencial institucional (continua como está); login social do cidadão (outro store, ADR-0018).

---

## Especificação para execução

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Identidade/UsuarioCofre.cs`, `UsuarioCredencialIntegracao.cs` (pasta nova `Identidade/` ao lado de `Usuario.cs`; se `Usuario.cs` está na raiz de `Entities/`, colocar os dois na raiz) + `Enums/EscopoCredencialIntegracao.cs` + configurações | criar |
| `SMSMais.Data/SmsMaisDbContext.cs` | DbSets `UsuarioCofres`, `UsuarioCredenciaisIntegracao` |
| `SMSMais.Data/Migrations/<ts>_CofreDeCredenciaisPessoais.cs` | gerar |
| `SMSMais.Core/Identidade/Cofre/ICofreUsuarioService.cs`, `CofreUsuarioService.cs`, `CofreSessaoStore.cs`, `CriptografiaCofre.cs` (PBKDF2 + AES-GCM, sem dependência nova: `System.Security.Cryptography`) | criar |
| `SMSMais.Core/Identidade/Credenciais/IUsuarioCredencialIntegracaoService.cs`, `UsuarioCredencialIntegracaoService.cs`, `Dtos/UsuarioCredencialIntegracaoDtos.cs`, `Validators/SalvarCredencialIntegracaoValidator.cs` | criar |
| `SMSMais.Core/Identidade/IdentidadeService.cs` | `LoginAsync`: após `SuccessRehashNeeded`/sucesso e antes de `GerarToken`, gerar o `jti` (ver como `_tokenService.GerarToken` cria o jti; se o jti nasce lá, expor `GerarToken(usuario, out jti)` ou ler o claim do token gerado) e chamar `cofre.AbrirAsync(usuario.Id, request.Senha!, jti, ct)`; `AlterarMinhaSenhaAsync`: `cofre.ReembrulharAsync(usuarioId, request.SenhaAtual, request.SenhaNova, ct)`; `AlterarSenhaAsync`/`GerarNovaSenhaAsync`: `cofre.InvalidarAsync(usuarioId, ct)`; logout (onde `ISerSessaoOperadorStore.Encerrar` é chamado hoje): `cofre.Fechar(jti)` |
| `SMSMais.Core/Regulacao/Sessoes/ISessaoOperadorIntegracaoStore.cs`, `SessaoOperadorIntegracaoStore.cs` | criar (generaliza `SerSessaoOperadorStore`) |
| `SMSMais.Core/Ser/Sessao/SerSessaoOperadorStore.cs`, `Sernit/Sessao/SernitSessaoOperadorStore.cs` | viram adaptadores finos sobre o store genérico (mantêm a interface para não quebrar `SerEscritaService`) ou são substituídos; controllers `regulacao/ser/sessao` continuam respondendo |
| `SMSMais.Core/DependencyInjection.cs` | registrar `ICofreUsuarioService` (scoped), `CofreSessaoStore` (singleton), `IUsuarioCredencialIntegracaoService` (scoped), `ISessaoOperadorIntegracaoStore` (singleton) |
| `SMSMais.Api/Controllers/IdentidadeController.cs` (onde vivem `/identidade/me/*`) | rotas `me/credenciais-integracao/*`, `me/cofre/abrir` |
| `SMSMais.front/src/features/usuarios/pages/MeuPerfilPage.tsx` | seção "Minhas credenciais" (`components/MinhasCredenciaisSecao.tsx`, `CredencialProvedorCartao.tsx`, `CredenciaisUnidadesSisregSecao.tsx`) |
| `SMSMais.front/src/features/usuarios/api/usuariosApi.ts`, `queries.ts` | funções/hooks `me/credenciais-integracao` |
| `SMSMais.front/src/shared/regulacao/useSessaoIntegracaoObrigatoria.ts`, `ModalCredencialIntegracao.tsx`, `ModalConfirmarSenhaSmsmais.tsx` | criar (generalizam `features/ser/lib/sessaoSer.ts` + `ModalLoginSer.tsx`) |
| `SMSMais.front/src/features/usuarios/components/FormularioUsuario.tsx` | aviso no botão de reset de senha |
| `tests/SMSMais.Tests/Identidade/Cofre/*.cs`, `Identidade/Credenciais/*.cs`, `Regulacao/Sessoes/*.cs` | criar |

### B. Entidades

```csharp
public sealed class UsuarioCofre
{
    public Guid UsuarioId { get; set; }                     // PK, FK usuario Cascade
    public string Kdf { get; set; } = "pbkdf2-sha512";      // max 40
    public int Iteracoes { get; set; } = 600_000;
    public byte[] Sal { get; set; } = [];                    // 16 bytes, distinto do sal do SenhaHash
    public byte[] DekEmbrulhada { get; set; } = [];          // nonce(12) + tag(16) + cifra(32), depois IProtetorSegredos.Protect (Data Protection) → armazenar como bytea
    public int Versao { get; set; } = 1;
    public DateTime CriadoEm { get; set; }  public DateTime? ReembrulhadaEm { get; set; }  public DateTime? InvalidadaEm { get; set; }
}

public sealed class UsuarioCredencialIntegracao
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }                     // FK usuario Cascade
    public string Provedor { get; set; } = string.Empty;    // max 40: "sisreg" | "ser" | "sernit" | "esus"
    public EscopoCredencialIntegracao Escopo { get; set; }  // Pessoal=1, UnidadesPadrao=2, Unidade=3
    public Guid? UnidadeId { get; set; }                     // FK unidade SetNull; obrigatório se Escopo == Unidade
    public string? Login { get; set; }                       // max 120 (null quando UsarPadrao)
    public byte[]? SenhaCifrada { get; set; }                // AES-256-GCM sob a DEK: nonce + tag + cifra; null = invalidada
    public bool UsarPadrao { get; set; }                     // só Escopo == Unidade
    public DateTime? ValidadaEm { get; set; }  public string? ValidacaoResultado { get; set; }   // max 500
    public DateTime? UltimoUsoEm { get; set; }  public string? UltimoUsoResultado { get; set; }   // max 500
    public int FalhasConsecutivas { get; set; }  public DateTime? BloqueadaAte { get; set; }
    public DateTime CriadoEm { get; set; }  public DateTime? AtualizadoEm { get; set; }  public DateTime? InvalidadaEm { get; set; }
}
// unique ux_usuario_credencial_integracao (usuario_id, provedor, escopo, coalesce(unidade_id, '00000000-0000-0000-0000-000000000000'))  → criar via migrationBuilder.Sql (EF não expressa coalesce)
```
Enum `EscopoCredencialIntegracao { Pessoal = 1, UnidadesPadrao = 2, Unidade = 3 }`. Check: `ck_usuario_credencial_unidade`: `(escopo = 3) = (unidade_id IS NOT NULL)`.

### C. Criptografia e cofre

```csharp
public static class CriptografiaCofre
{
    public static byte[] DerivarKek(string senha, byte[] sal, int iteracoes) => Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, HashAlgorithmName.SHA512, 32);
    public static byte[] Cifrar(byte[] chave, byte[] claro);      // AES-GCM: nonce 12 aleatório | tag 16 | cifra
    public static byte[] Decifrar(byte[] chave, byte[] envelope);  // lança CryptographicException se tag inválida
}
public interface ICofreUsuarioService
{
    Task AbrirAsync(Guid usuarioId, string senha, string jti, CancellationToken ct);          // cria o cofre se não existe; senha errada → CryptographicException → ValidacaoException("identidade.credenciais_invalidas")
    Task AbrirComSenhaAsync(Guid usuarioId, string senha, string jti, CancellationToken ct);  // pós-restart: valida a senha pelo PasswordHasher antes
    void Fechar(string jti);
    Task ReembrulharAsync(Guid usuarioId, string senhaAtual, string senhaNova, CancellationToken ct);
    Task InvalidarAsync(Guid usuarioId, CancellationToken ct);   // InvalidadaEm = now; SenhaCifrada = null em todas as credenciais; auditoria
    bool EstaAberto(string jti);
    byte[] CifrarParaUsuario(string jti, string claro);          // usa a DEK da sessão; cofre fechado → ConflitoException("cofre.fechado")
    string DecifrarDaSessao(string jti, byte[] envelope);
}
```
`CofreSessaoStore` (singleton): `ConcurrentDictionary<string, (byte[] Dek, DateTime UltimoUso)>`; expira por inatividade em 8h (mesma varredura de expurgo do `SerSessaoOperadorStore`); `Fechar` zera o array (`CryptographicOperations.ZeroMemory`).

Códigos de erro que o front trata: `cofre.fechado` (409) → `ModalConfirmarSenhaSmsmais`; `credencial.ausente` (409, com `provedor` e `unidadeId` no `ProblemDetails.Extensions`) → `ModalCredencialIntegracao`; `credencial.bloqueada` (409, com `bloqueadaAte`).

### D. Credenciais — serviço e DTOs

```csharp
public interface IUsuarioCredencialIntegracaoService
{
    Task<IReadOnlyList<CredencialIntegracaoDto>> ListarMinhasAsync(CancellationToken ct);
    Task<CredencialIntegracaoDto> SalvarMinhaAsync(SalvarCredencialIntegracaoRequest req, CancellationToken ct);   // escopos 2/3 exigem módulo 48
    Task RemoverMinhaAsync(string provedor, EscopoCredencialIntegracao escopo, Guid? unidadeId, CancellationToken ct);
    Task<TestarCredencialResultadoDto> TestarMinhaAsync(string provedor, EscopoCredencialIntegracao escopo, Guid? unidadeId, CancellationToken ct);
    Task<CredencialResolvida?> ResolverAsync(Guid usuarioId, string provedor, Guid? unidadeId, string jti, CancellationToken ct);  // Unidade(!usarPadrao) → UnidadesPadrao(+CNES) → Pessoal; decifra; respeita BloqueadaAte
    Task RegistrarUsoAsync(Guid credencialId, bool sucesso, string? resultado, bool captcha, CancellationToken ct);  // falhas consecutivas ≥3 → BloqueadaAte = +15min; captcha → +24h
}
public sealed record SalvarCredencialIntegracaoRequest(string Provedor, EscopoCredencialIntegracao Escopo, Guid? UnidadeId, string? Login, string? Senha, bool? UsarPadrao);
public sealed record CredencialIntegracaoDto(Guid Id, string Provedor, EscopoCredencialIntegracao Escopo, Guid? UnidadeId, string? UnidadeNome, string? UnidadeCnes, string? Login, bool Definida, bool UsarPadrao, DateTime? ValidadaEm, string? ValidacaoResultado, DateTime? UltimoUsoEm, DateTime? BloqueadaAte);
public sealed record TestarCredencialResultadoDto(bool Ok, string Mensagem);
public sealed record CredencialResolvida(Guid CredencialId, string Provedor, string Login, string Senha, string? Cnes, EscopoCredencialIntegracao Escopo);
```
`TestarMinhaAsync`: SER/SERNIT → cria `SerWebSessao`/`SernitWebSessao` de operador e faz login (mesmo caminho de `SerSessaoOperadorStore.AutenticarAsync`); SISREG → `ISisregSessaoFactory.ParaOperador(login, senha, cnes).TestarAsync()` (plano 11; até lá, o botão Testar do SISREG devolve "teste disponível no incremento 7"). Auditoria (`IAuditoriaService.RegistrarAsync("UsuarioCredencialIntegracao", id, "definida|removida|testada|usada", null, $"{provedor}/{escopo}/{unidade}")`) — nunca o segredo.

### E. Store de sessão genérico

```csharp
public sealed record SessaoOperadorInfo(string Provedor, string Login, string? Cnes, DateTime AutenticadaEm, DateTime ExpiraEm);
public interface ISessaoOperadorIntegracaoStore
{
    Task<object> ExigirAsync(string provedor, Guid? unidadeId, CancellationToken ct);   // devolve ISerWebSessao | ISernitWebSessao | ISisregSessaoOperador conforme o provedor (métodos tipados ExigirSerAsync/ExigirSernitAsync/ExigirSisregAsync)
    Task<SessaoOperadorInfo> AutenticarManualAsync(string provedor, string login, string senha, bool salvarNoPerfil, Guid? unidadeId, CancellationToken ct);  // modal de fallback
    IReadOnlyList<SessaoOperadorInfo> Estado();
    void EncerrarSessao(string jti);
}
```
Cadeia de `ExigirSerAsync`: cache `(jti, "ser", login)` vivo → devolve; senão `cofre.EstaAberto(jti)` falso → `ConflitoException("cofre.fechado")`; `credenciais.ResolverAsync(usuario, "ser", null, jti)` nulo → `ConflitoException("credencial.ausente")`; bloqueada → `credencial.bloqueada`; cria a sessão pela fábrica (`Func<string,string,ISerWebSessao>` já injetável), login, cacheia, `RegistrarUsoAsync(true)`; falha de login → `RegistrarUsoAsync(false)` + `ValidacaoException("credencial.invalida", …)`. `SerSessaoOperadorStore.Exigir(sessaoId)` passa a delegar para o genérico (mantém `SerEscritaService` intacto).

### F. Endpoints (só JWT — sem `[RequerPermissao]`, como `PUT /identidade/me/senha`)

| Verbo | Rota | Entrada | Saída |
|---|---|---|---|
| GET | `identidade/me/credenciais-integracao` | | `CredencialIntegracaoDto[]` |
| PUT | `identidade/me/credenciais-integracao` | `SalvarCredencialIntegracaoRequest` | dto |
| DELETE | `identidade/me/credenciais-integracao?provedor=&escopo=&unidadeId=` | | 204 |
| POST | `identidade/me/credenciais-integracao/testar?provedor=&escopo=&unidadeId=` | | `TestarCredencialResultadoDto` |
| POST | `identidade/me/cofre/abrir` | `{ senha }` | 204 |
| GET | `identidade/me/cofre/estado` | | `{ aberto: bool, invalidadoEm?: date }` |
| GET | `regulacao/sessoes` · POST `regulacao/sessoes` `{provedor, login, senha, salvarNoPerfil, unidadeId?}` · DELETE `regulacao/sessoes/{provedor}` | (`Regulacao`, Consulta/Edicao) | estado / info |

### G. Front

- `MinhasCredenciaisSecao.tsx` (em `MeuPerfilPage`): banner fixo no topo com o texto D-7; `CredencialProvedorCartao` × 3 (SISREG · SER · SERNIT): login, senha (`type=password`, placeholder "definida em dd/mm" quando `definida`), botões Salvar · Testar · Remover; badge de estado (validada / falhou / bloqueada até hh:mm).
- `CredenciaisUnidadesSisregSecao.tsx` (só `useTemConsulta('RegulacaoTriagem')`): linha "Padrão para todas as unidades" (escopo `UnidadesPadrao`); `Select` de unidades ativas para adicionar; por unidade: toggle "Usar padrão / Senha desta unidade", campos login/senha quando específica, Testar, Remover; CNES exibido.
- `ModalCredencialIntegracao.tsx` (fallback D-2): props `{ provedor, unidadeId?, onAutenticado }`; campos login/senha; checkbox "Salvar no meu perfil" (marcado por padrão) com o aviso D-7 embaixo; chama `POST regulacao/sessoes`.
- `ModalConfirmarSenhaSmsmais.tsx`: "Confirme sua senha do SMSMais para destravar suas credenciais" → `POST identidade/me/cofre/abrir`.
- `useSessaoIntegracaoObrigatoria(provedor, unidadeId?)`: `executar(acao)` → tenta; em 409 `cofre.fechado` abre `ModalConfirmarSenhaSmsmais` e repete; em 409 `credencial.ausente` abre `ModalCredencialIntegracao` e repete; em `credencial.bloqueada` mostra a hora. `features/ser/lib/sessaoSer.ts` passa a ser um wrapper deste hook com `provedor = 'ser'`.
- Estado do cofre: `useEstadoCofre()` (`GET me/cofre/estado`) consultado uma vez após o login; se `invalidadoEm` recente e sem credenciais, toast "suas credenciais de integração foram apagadas no reset de senha; cadastre de novo".
- `FormularioUsuario.tsx`: ao lado de "Gerar nova senha": texto "Ao resetar, as credenciais de integração deste usuário serão apagadas e ele precisará cadastrá-las de novo".

### H. Testes

`CriptografiaCofreTests` (puro): `Round_trip`; `Tag_invalida_lanca`; `Kek_diferente_por_sal`. `CofreUsuarioServiceTests` (fixture): `Login_cria_cofre_e_abre`; `Senha_errada_nao_abre`; `Reembrulhar_preserva_credenciais`; `Invalidar_apaga_senhas_cifradas`; `Fechar_zera_dek`. `UsuarioCredencialIntegracaoServiceTests`: `Escopo_unidade_exige_48`; `Resolver_prefere_unidade_especifica_depois_padrao_depois_pessoal`; `Tres_falhas_bloqueiam_15_minutos`; `Captcha_bloqueia_24h`; `Dto_nunca_expoe_senha`. `SessaoOperadorIntegracaoStoreTests`: `Cofre_fechado_devolve_codigo`; `Credencial_ausente_devolve_codigo`; `Sessao_e_compartilhada_por_login`; `Encerrar_derruba_tudo`.

### I. Passo a passo

1. Entidades/enum/configs/DbSets → build → migration `CofreDeCredenciaisPessoais` (+ SQL do unique com `coalesce`) → build.
2. `CriptografiaCofre` + testes puros.
3. `ICofreUsuarioService` + `CofreSessaoStore` + ganchos em `IdentidadeService` (login, troca, reset, logout) + testes.
4. `IUsuarioCredencialIntegracaoService` + validador + endpoints `me/*` + auditoria + testes.
5. `ISessaoOperadorIntegracaoStore` + adaptação de `SerSessaoOperadorStore`/`Sernit…` + endpoints `regulacao/sessoes` + testes (SER e SERNIT continuam funcionando).
6. Front: hook genérico e modais → seção em `MeuPerfilPage` → bloco de unidades → aviso no reset → `npm run build`.
7. `PROGRESSO.md` 5.1–5.4.

### J. Critério de pronto

Login → salvar credencial SER → registrar follow-up numa solicitação SER sem ver o modal; trocar a própria senha → follow-up continua sem modal; admin gera nova senha → próximo login mostra o aviso e o follow-up pede a credencial pelo modal com "salvar no perfil"; dump do banco + chaves do Data Protection não revelam a senha (teste de integração que tenta decifrar sem a DEK falha).
