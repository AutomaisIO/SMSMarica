# Pendência — cadastrar equipamento pela tela deveria provisionar o AE no dcm4chee

**Status:** ideia aprovada, sem implementação. Registrada em 2026-09-04.
**Relacionado:** [`../pacs.md`](../pacs.md) §7, §8 · [`pacs-whitelist-dinamica-por-login.md`](./pacs-whitelist-dinamica-por-login.md)
· [ADR-0045](../adr/0045-criterio-para-extrair-servico.md) (critério para extrair serviço)

## O problema

O CRUD de equipamento já existe inteiro — entidade `smsmarica.equipamento`, `EquipamentosController`,
`ModuloPermissao.Equipamentos = 26`, a página `/app/equipamentos` e a aba Equipamentos dentro da
unidade. Cadastrar um aparelho ali faz o `ConstrutorMwlItem` carimbar todo item de worklist com
`WorklistLabel (0074,1202) = IdentificadorDicom` do equipamento.

**Mas nenhum AE do dcm4chee consulta esse label enquanto alguém não criar o Archive AE
correspondente à mão**, por SSH + `ldapmodify` no host do PACS. Sem esse passo o aparelho não recebe
nada.

O que torna isso perigoso é o **silêncio dos dois lados**: o cadastro salva com sucesso, o item de
worklist é criado com sucesso, o dcm4chee responde normalmente — e o equipamento simplesmente nunca
enxerga a lista. Não há erro em log nenhum, porque do ponto de vista de cada componente isolado nada
falhou.

Hoje o procedimento não existe em prosa em lugar nenhum: sobrevive como histórico de comandos em
`.claude/settings.local.json`.

## A proposta — `Automais.PacsAgent`

Um serviço pequeno (systemd) **no próprio host do PACS**, chamado pelo backend por HTTP:

```
GET    /aes                 lista AEs + dcmMWLWorklistLabel  (alimenta o diff/status na tela)
POST   /aes                 cria AE de worklist: clone da subárvore + label   (idempotente)
DELETE /aes/{ae}            remove — recusa os canônicos, os legados e os referenciados
POST   /reload              POST /dcm4chee-arc/ctrl/reload
GET    /backup              slapcat + GET /devices/dcm4chee-arc, gravado no host
GET    /health
```

Depois, sem mudar o desenho, o mesmo agente atende mais dois casos que hoje pediriam serviço próprio:
`POST /calling-aes` (a allowlist `dcmAcceptedCallingAETitle`, planejada em `../pacs.md` §14) e
`POST /whitelist` (a [whitelist dinâmica por login](./pacs-whitelist-dinamica-por-login.md), já
aprovada). **Um agente serve aos três.**

### Por que passa no ADR-0045

O ADR-0045 exige **dono externo** para extrair serviço. O caso 2 do critério é "um host ou operador de
terceiro" — precedente aprovado ali: o PABX rodando dentro do servidor VOIP da FalarMais. O dcm4chee é
software de terceiro, em outro host, com a configuração num slapd local.

E o teste que importa: **nenhum domínio nosso é extraído.** Nenhuma entidade, tabela ou regra de
negócio migra. O agente é o braço local de operações que o backend não consegue fazer bem de fora.

### Por que não o backend falando LDAP direto

1. **A senha do slapd nunca sai do host.** No outro desenho ela teria que viajar para o env do backend,
   em outro droplet; aqui o backend guarda só um token.
2. **Permite fechar a `:389`** até para o backend (hoje liberada para `146.190.65.73`), deixando o
   slapd só em localhost.
3. **O clone de subárvore é um pipe de shell que já funcionou em produção**
   (`ldapsearch -b <DN do AE> | sed | ldapadd`). Reimplementá-lo por biblioteca LDAP no .NET — ler N
   entradas, reescrever DNs, gravar na ordem certa — é mais código e mais risco.

### Quatro condições não-negociáveis

Tiradas do que o ADR-0045 **mediu** nas extrações anteriores deste repositório: `Automais.Fhir` com
zero endpoints autenticados escutando em `0.0.0.0`; `Automais.Assinador` com mais YAML de deploy que
código; Telefonia com `unidades.csv` duplicado que divergiu e reverte dados a cada boot.

1. **Auth e bind restrito no primeiro commit**, não como "próximo passo": escuta só em `127.0.0.1` e
   `10.35.0.16` (VPN), token compartilhado no header **e** restrição por origem (`10.35.0.10` /
   `146.190.65.73`).
2. **Zero estado próprio** — sem banco, sem arquivo de dados, sem cópia de cadastro. A verdade continua
   em `smsmarica.equipamento`.
3. **Não conhece o domínio.** Recebe "crie o AE `X` com label `Y`"; não sabe o que é equipamento,
   unidade ou exame.
4. **Deploy é uma unit systemd**, sem workflow de CI novo.

### Fronteira

**Dados continuam indo direto** ao dcm4chee (DICOMweb, proxy de imagem, `mwlitems` na `:8080`, como
hoje). **Só configuração** passa pelo agente.

### Escrita sempre por LDAP

Nunca pelo `PUT /devices/dcm4chee-arc`: ele **descarta `dcmMWLWorklistLabel` em silêncio** (responde
204 e não grava — é o que torna o passo manual inevitável hoje) e reescreve o device inteiro.

### Stack

Python 3.11 + FastAPI sob systemd — o que a equipe já usa nos labs e o host já tem `python3`.
Ressalva: a VM tem 2 GB de RAM e só saiu da zona de OOM depois de remover o `pegaph` e criar 4 GB de
swap. Um `uvicorn` de 1 worker (~50 MB) cabe; se apertar, um binário Go estático (~10 MB) é o plano B.

## O que o cadastro ganha junto

Campos que hoje não existem e vivem só na cabeça de quem configurou o aparelho: o `WORK-*`
correspondente, fabricante/modelo/nº de série, suporta MWL?, suporta MPPS?, IP/porta local,
`(0040,0008)` (o código de protocolo do caso Fuji/JJ1017), e status/data de provisionamento.

E duas correções de integridade:

- **Índice único global em `identificador_dicom`** (filtrado por `excluido_em IS NULL`). Hoje só existe
  `ux_equipamento_unidade_nome` — **nada impede dois equipamentos com o mesmo AE Title**, o que
  quebraria o isolamento da worklist sem sinal nenhum.
- **A dica do campo AE Title está errada.** `ModalEquipamento.tsx:164` afirma que "em branco, os exames
  desta unidade caem no AE padrão do sistema". É falso: **não existe AE de fallback** — o envio falha
  com `worklist.sem_equipamento`. O texto induz o administrador a deixar o campo vazio achando que
  funciona.

## Ficha técnica do aparelho

Hoje é um `.md` escrito à mão por equipamento (`../pacs-cdt-mamografo.md`, `../pacs-us-cmi.md`), sem
índice, e que **não cobre** `US01-CDT`, `US02-CDT` nem `DO-CDT`.

Proposta: tabela `smsmarica.equipamento_ficha` com o conteúdo **congelado por emissão**
(`equipamento_id`, `versao`, `conteudo`, `emitida_em`, `emitida_por`), export PDF por QuestPDF (já no
projeto). Assim há prova do que foi entregue ao técnico e quando, e o histórico não se perde quando o
cadastro muda.

## Divergências reais encontradas no servidor (2026-09-04)

Levantadas ao preparar o RX do CDT. **São exatamente o tipo de coisa que o `GET /aes` do agente
mostraria na tela como diff** — e a razão de o provisionamento manual não escalar:

| Achado | Observação |
|---|---|
| Existem **5** AEs de worklist com label — `WORK-CDT`/`FDR-MAMO`, `WORK-CMI`/`US_CMI`, `WORK-US01-CDT`/`US01-CDT`, `WORK-US02-CDT`/`US02-CDT`, `WORK-DO-CDT`/`DO-CDT` | `../pacs.md` §8 documenta só **2** |
| Existe um AE `PACS-MARICA` (storage compartilhado) | não documentado em `../pacs.md` |
| **`WORK-DO-CDT` não está no `cn=Unique AE Titles Registry`** | quem o criou pulou o passo; funciona, mas é inconsistente |
| `NEW_AET_A` no registry | resquício de teste, sem AE correspondente |
| Nenhum dos AEs criados depois do scaffold tem `dcmWebApp` | o REST funciona mesmo assim (`/aets/WORK-DO-CDT/rs/mwlitems` → HTTP 200); o WebApp só afeta a listagem na UI Arc Light |

## Pendência de segurança relacionada (tratar antes)

**`.claude/settings.local.json` está rastreado no git** (não há entrada no `.gitignore`) e carrega, em
texto claro, a senha do **LDAP admin do dcm4chee** e a de **SSH root** do host — é a credencial que o
agente vai usar.

- `git rm --cached .claude/settings.local.json` + entrada no `.gitignore`;
- **rotacionar a senha do slapd**, lembrando que ela vive em **três** pontos no host (`olcRootPW`, o
  `userPassword` da entrada `cn=admin` e o `ldap.properties` que o dcm4chee lê no boot) — trocar só um
  não revoga nada;
- o histórico do git continua com a senha antiga: **quem resolve é a rotação**, não o `git rm`.
