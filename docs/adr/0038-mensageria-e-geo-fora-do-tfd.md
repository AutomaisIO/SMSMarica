# ADR-0038 — Mensageria WhatsApp e geocodificação são infraestrutura compartilhada, não submódulos do TFD

**Status:** aceito · **Data:** 2026-07-31
**Implantação:** não iniciada *(migration pronta, ainda não aplicada em produção)*

## Contexto

O canal de WhatsApp e a geocodificação nasceram dentro do TFD, porque o TFD foi o primeiro
a precisar deles. Ficaram com o prefixo `tfd_` e nunca saíram de lá — mesmo depois de
virarem infraestrutura de todo o sistema (Central de Atendimento, comunicações ao paciente,
OTP do app do cidadão, painel do secretário).

Isso não é cosmético. Enquanto a mensageria se chamar `tfd_*`, quem lê o schema — pessoa ou
IA — conclui que o WhatsApp do município pertence ao transporte sanitário; e quem for mexer
no TFD acredita que pode mexer nas 26 mil mensagens.

### A prova

`tfd_mensagem_whatsapp` tinha uma FK `sessao_id` apontando para `sessao_de_tratamento`
(a sessão de TFD). Medido em produção em 31/07/2026:

| Coluna | Preenchidas |
|---|---|
| `sessao_id` — **o vínculo com o TFD** | **0** de 26.055 |
| `conversa_id` — Central de Atendimento | 22.928 |
| `autor_usuario_id` — operador humano | 11.850 |

**A tabela chamada `tfd_` nunca carregou uma única mensagem de TFD.** O `sessao_id` era o
cordão umbilical do nascimento, vazio desde sempre.

### Por que agora

O TFD **permanece no escopo do produto** — o que falta nele é definição, não decisão de
manter. Mas seu domínio está **vazio** (`avaliacao` 0, `rastreamento_*` 0, `veiculo` 1,
`tratamento` 3). Corrigir a fronteira hoje custa um rename; corrigir depois de o TFD entrar
em operação custa migração de dados com o sistema no ar.

## Decisão

**1. Mensageria e geo saem de baixo do TFD.** O prefixo da tabela declara o dono real:

| Antes | Depois | Dono |
|---|---|---|
| `tfd_mensagem_whatsapp` | `whatsapp_mensagem` | canal WhatsApp do município |
| `tfd_config_whatsapp` | `whatsapp_configuracao` | credenciais Meta Cloud API |
| `tfd_geocodigo` | `geo_endereco` | cache de geocodificação |
| `tfd_config_google` | `geo_configuracao` | chave Google Maps Platform |
| `tfd_config_faturamento` | `tfd_configuracao` | **continua TFD** (só adota o sufixo pt-BR) |
| `tfd_registro_faturamento` | *(inalterada)* | **continua TFD** |

Entidades acompanham: `Data/Entities/Notificacoes/` e `Data/Entities/Geo/`.
`TfdConfigWhatsApp` → `WhatsAppConfiguracao`, `TfdConfigGoogle` → `GeoConfiguracao`,
`TfdConfigFaturamento` → `TfdConfiguracao`, `Geocodigo` → `GeoEndereco`.

**2. A dependência aponta numa direção só: domínio → mensageria.** A coluna
`sessao_id` foi removida da mensagem, junto com o parâmetro `sessaoId` que atravessava
toda a interface `IWhatsAppCliente`. Quem precisa amarrar uma mensagem a um evento de
domínio **referencia a mensagem** — como `comunicacao_paciente.mensagem_whatsapp_id` já
faz — e não o contrário. O `WhatsAppNotificador` do TFD continua sabendo da sessão; a
mensagem é que não sabe mais do TFD.

**3. Convenção de nome, derivada do schema real** (e não do `database.md`, que estava
desatualizado): prefixo = quem é dono; `snake_case` singular; configuração singleton com
sufixo `_configuracao` em pt-BR — como `ia_configuracao`, `laudo_configuracao`,
`sisreg_configuracao`, `ticket_configuracao`. As três `tfd_config_*` eram a única exceção
do banco, e erravam duas vezes: `config` em inglês e dono errado.

## Consequências

### O rename é feito com `ALTER TABLE ... RENAME`, nunca com copiar-e-dropar

O scaffolder do EF **gerou `DropTable` + `CreateTable`** para esta mudança — ele não sabe
inferir rename, enxerga "tabela sumiu, tabela nova apareceu". Aquilo teria apagado as 26 mil
mensagens. **A migration foi reescrita à mão** e o SQL gerado foi conferido: 10 `RENAME TO`,
zero `DROP TABLE`, zero `CREATE TABLE`, zero `DELETE`, zero `TRUNCATE`, e um único
`DROP COLUMN` (a coluna morta).

Rename no PostgreSQL é operação de catálogo: instantânea, não move dado, e as FKs continuam
válidas sozinhas porque a constraint referencia a tabela por **OID**, não por nome. Só os
**nomes** de índices e constraints precisam ser acertados — senão sobra
`PK_tfd_mensagem_whatsapp` numa tabela `whatsapp_mensagem`, e o próximo `migrations add`
gera diff fantasma.

> **Regra que fica:** se esta migration precisar ser regerada, **não aceitar o scaffold cru**.

### O compilador não cobre tudo

Havia 5 SQLs crus em `EstatisticasService.cs` montando `FROM smsmarica.tfd_mensagem_whatsapp`
em string interpolada — invisíveis ao compilador, quebrariam em runtime no dashboard.
Foram corrigidos. Antes do rename foi verificado que **nada mais** depende dos nomes:
0 indicadores com SQL citando `tfd_`, 0 documentos/chunks de conhecimento da IA, 0 views.

### Não há passo irreversível

O rollback primário é outro `RENAME` — que preserva inclusive as mensagens chegadas depois
da ida. A única remoção (`sessao_id`) tinha 0 de 26.055 linhas preenchidas, então recriar a
coluna restaura o estado idêntico. Backup CSV das 6 tabelas + manifesto (linhas, bytes, md5)
foi tirado antes, e há verificação pós-migração id-a-id em `scripts/verificar-rename-tfd.py`
— comparação por **id**, não por contagem, porque o WhatsApp grava durante a janela e a
contagem cresce legitimamente.

### Janela de indisponibilidade

Rename é *breaking*: entre a migration rodar e o serviço reiniciar, o código antigo quebra.
São segundos, mas com a Central de Atendimento ativa. **Aplicar fora do horário de atendimento.**

## Pendências que este ADR abre (não fazem parte desta entrega)

1. **Chaves de configuração ainda dizem `Tfd:`** — `Tfd:Otp:ModoTeste`,
   `Tfd:Otp:WhatsAppTemplate`, `Tfd:WhatsApp:Simular`, `Tfd:Cidadao:SessaoDias`. Mesmo
   vazamento, mas trocar exige atualizar variável de ambiente no servidor **no mesmo
   instante**, senão a chave nova lê o default em silêncio. Fica para um passo próprio.
2. **`TfdConfigService`/`TfdConfigController` empacotam três configurações de domínios
   diferentes** (WhatsApp, Google Maps, faturamento). Quebrar em três mexe nas rotas
   `/tfd/config/*` que o front consome em `features/integracoes` — exige deploy coordenado
   back+front, por isso ficou fora desta entrega.
3. **`WhatsAppNotificador` mora em `Core/Notificacoes/WhatsApp/`, mas é código do TFD**
   (fala de `SessaoDeTratamento`). O lugar dele é `Core/Tfd/`. Cosmético, sem risco.

## Alternativas descartadas

- **Criar tabelas novas, copiar os dados e dropar as antigas.** O webhook do WhatsApp grava
  continuamente: mensagem chegando durante a cópia se perderia ou duplicaria. Além disso
  exigiria dropar e revalidar a FK de `comunicacao_paciente` (1.548 linhas). Copiar só se
  justifica quando a *forma* muda — aqui só muda o nome.
- **Deixar como está e documentar.** Foi o que aconteceu por meses: o XML doc de
  `MensagemWhatsApp` já dizia "hoje transversal a todo o SMSMarica (não só TFD)" e o nome
  continuou mentindo. Comentário não corrige schema.
