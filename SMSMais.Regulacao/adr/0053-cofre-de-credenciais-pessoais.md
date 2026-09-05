# ADR-0053 — Credenciais pessoais dos sistemas de regulação ficam num cofre por usuário, cifradas por uma chave derivada da senha dele; o servidor não as lê em repouso

- **Status:** rascunho (promover no incremento 5)
- **Data:** 04/09/2026
- **Contexto:** decisão de 18/08/2026 registrada em `SMSMais.Core/Ser/Sessao/SerSessaoOperadorStore.cs` (credencial pessoal só em memória); [ADR-0006](../../docs/adr/0006-papel-derivado-e-auditoria-explicita.md) (auditoria); remoção de `sisreg_credencial_unidade` em 25/07/2026.
- **Documento de apoio:** plano 07.

## Contexto

Em 18/08 decidimos **não persistir** a senha pessoal do operador no SER: "guardar em banco criaria um cofre de credenciais pessoais do Estado — risco que a funcionalidade não paga". A senha vive em memória, por sessão, e o operador a digita a cada plantão.

O módulo de solicitações muda a conta. Todo usuário solicitante precisa de credencial SISREG para incluir a própria solicitação (D-8); o agente regulador precisa da credencial pessoal no SER/SERNIT e de credenciais **de unidade** no SISREG para o NAR (D-9). Pedir tudo isso a cada sessão inviabiliza o uso. A transcrição pede explicitamente que cada usuário cadastre suas senhas no perfil, "hasheadas junto com a senha do operador para evitar vazamento".

## Decisão

### 1. Envelope pela senha do usuário

No login, depois de verificar a senha, deriva-se uma chave (PBKDF2-SHA512, sal próprio, distinto do sal do hash de login) que desembrulha a chave de dados do usuário (DEK). A DEK fica só em memória, por sessão (jti), até 8h de inatividade ou logout. Cada credencial é cifrada com AES-256-GCM sob a DEK. Por cima, Data Protection do servidor (defesa em profundidade). **Em repouso, nem o servidor lê.**

### 2. O que acontece com a senha do SMSMais

- Troca pelo próprio usuário (tem a senha atual): re-embrulha a DEK; nada se perde.
- Reset pelo admin ou "esqueci a senha": a DEK não pode ser destravada; as credenciais são **apagadas** e o usuário recadastra. A tela avisa (texto do Bernardo, D-7) e o admin é avisado ao resetar.
- Restart da API: a DEK some da memória; o usuário confirma a senha do SMSMais uma vez para destravar (não é o modal do SER).

### 3. Nenhum job em background usa credencial pessoal

Toda escrita em sistema externo é disparada por um humano logado. Motores de leitura continuam na credencial institucional (`integracao_credencial`).

### 4. Credencial SISREG por unidade pertence ao agente, não à unidade

`sisreg_credencial_unidade` foi removida porque "a unidade nunca foi propriedade da sessão". Aqui o que volta é diferente: a credencial que **um agente** tem para incluir em nome de **uma unidade** (CNES + senha padrão ou específica), guardada no cofre dele. Escopos: `Pessoal`, `UnidadesPadrao`, `Unidade` (com `usar_padrao`).

### 5. O modal continua como fallback

Sem credencial configurada, o modal que já existe pede a senha e agora oferece "salvar no meu perfil". Com credencial, autentica direto (D-2).

## Consequências

- Reverte conscientemente a decisão de 18/08: o risco que ela evitava (um cofre legível) não existe neste desenho; o custo que ela evitava (recadastro) volta só no reset.
- Fricção real em dia de deploy (confirmar a senha do SMSMais). Medir.
- Não protege contra servidor comprometido enquanto o usuário está logado nem contra roubo da senha do usuário — o mesmo limite de qualquer cofre destravado por senha. Dizer isso na tela e na doc.
- `SerSessaoOperadorStore` e `SernitSessaoOperadorStore` viram um store genérico alimentado pelo cofre.
- Auditoria registra definida/removida/testada/usada — nunca o segredo.

## Alternativas consideradas

- **Data Protection do servidor** (mesmo esquema de `integracao_credencial`). Nada se perde no reset e funciona sem o usuário presente; mas quem tem a chave do servidor lê todas as senhas pessoais. Rejeitado pelo Bernardo em 04/09 depois de ouvir o custo do reset.
- **Manter só em memória** (status quo). Rejeitado: inviabiliza o Interno pelo solicitante e o NAR.
- **Cópia de recuperação embrulhada pelo servidor** (híbrido). Rejeitado: reintroduz a cópia legível; é a alternativa 1 com mais peças.

## Verificação

Dump do banco + chaves do Data Protection não revelam nenhuma senha pessoal. Reset pelo admin apaga as credenciais e o próximo login pede recadastro. Troca de senha pelo usuário preserva. `Exigir(provedor)` autentica sem modal quando há credencial.
