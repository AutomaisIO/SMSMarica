---
name: criar-equipamento-imagem
description: Cria um equipamento de imagem novo (ultrassom, raio-X, mamógrafo, densitômetro) ponta a ponta — cadastro no painel, AE de worklist no dcm4chee (AE + dcmWebApp + registro de AE único), verificação de isolamento e o documento de parâmetros para o técnico. Use SEMPRE que o pedido for "vai entrar um aparelho novo", "criar equipamento", "criar a worklist do aparelho X", "gerar o documento para o técnico configurar", "o aparelho não recebe worklist", "o AE não aparece na web do PACS". NÃO improvisar pelo docs/pacs.md — são quatro objetos e esquecer um falha EM SILÊNCIO dos dois lados.
---

# Criar um equipamento de imagem novo

Esta skill existe porque o procedimento **falha em silêncio**, e já foi esquecido mais de uma vez
(`WORK-DO-CDT` em 05/08/2026, `WORK-RX-CDT` em 04/09/2026). O AE entra, o C-ECHO responde
Success, a REST `/aets` lista o AE — **e mesmo assim a worklist não chega ao aparelho e o AE não
aparece na web do PACS**. Nenhum dos dois lados dá erro.

## A regra que se esquece toda vez

Um AE de worklist são **DUAS entradas LDAP irmãs** (não pai/filho), ambas filhas de
`dicomDeviceName=dcm4chee-arc`:

| Entrada | O que faz | Sintoma se faltar |
|---|---|---|
| `dicomAETitle=WORK-XXX,...` | responde C-ECHO/C-FIND na 11112; carrega o `dcmMWLWorklistLabel`; tem ~12 filhos | o aparelho não acha o AE |
| `dcmWebAppName=WORK-XXX,...` | expõe `MWL_RS` (`/aets/WORK-XXX/rs/mwlitems`) e é o que a **web UI lista** | `404 No Web Application with MWL_RS service class found...` e web UI vazia |

**Clonar a subárvore do AE copia só a primeira.** O sintoma engana porque `/dcm4chee-arc/aets`
lista o AE assim mesmo — quem denuncia a falta é `/dcm4chee-arc/webapps`.

Mais dois objetos completam: o **registro em `cn=Unique AE Titles Registry`** (controle de
colisão) e o **cadastro do equipamento no painel** — sem ele o item de worklist sai carimbado com
um label que nenhum AE consulta.

## Antes de tudo: decidir o AE Title

**É a decisão cara.** O AE Title viaja para o `dcmMWLWorklistLabel`, para o
`ScheduledStationAETitle (0040,0001)` e para **dentro de cada estudo armazenado**. Trocar depois
obriga o técnico a reconfigurar o aparelho e deixa o nome antigo gravado no acervo.

Convenção em uso: `<MODALIDADE><NN>-<UNIDADE>` para o aparelho (`US02-CMI`, `RX-CDT`,
`US01-CDT`) e `WORK-<AE do aparelho>` para o AE de worklist (`WORK-US02-CMI`). Nomes legados sem
número (`US_CMI`) ficam como estão — **não renomear aparelho que já funciona só por simetria.**

**Padrão para TODO aparelho novo (definido pelo Bernardo em 14/09/2026):**
`WORK-<MODALIDADE><NN>-<UNIDADE>` para a worklist e `<MODALIDADE><NN>-<UNIDADE>` para o aparelho,
com o índice de dois dígitos sempre, mesmo quando só existe um aparelho daquela modalidade na
unidade (`US01-XXX`, não `US-XXX`). O próximo ultrassom do CMI é `US03-CMI` / `WORK-US03-CMI`.
**Exceção conhecida, que não deve ser "corrigida":** o ultrassom 1 do CMI, um dos primeiros
aparelhos. Continua com o AE `US_CMI`, e a worklist dele é `WORK-US-CMI` desde 14/09/2026 (antes
era `WORK-CMI`, e o aparelho foi reconfigurado para o nome novo). O label continua `US_CMI`.

Confirmar o nome com o operador e conferir que está livre:

```bash
ldapsearch -x -LLL -H ldap://pacs.marica.automais.cloud:389 -D "cn=admin,dc=dcm4che,dc=org" -w "$W" \
  -b "cn=DICOM Configuration,dc=dcm4che,dc=org" "(dicomAETitle=<AE>)" dn
```

## Onde rodar

O host do PACS é `pacs.marica.automais.cloud` (`104.236.203.40`). Se o SSH direto não estiver à
mão, **o LDAP (389) é alcançável do droplet `smsmarica.online`**, direto e pela VPN — lá já tem
`ldap-utils` (instalado em 09/09/2026). Credenciais fora deste repositório.

## Passo a passo

### 1. Backup, sempre

```bash
ldapsearch -x -LLL -o ldif-wrap=no -H $H -D "$D" -w "$W" -b "dc=dcm4che,dc=org" > antes.ldif
```

### 2. Clonar um par que já funciona

Modelo **da mesma modalidade** (`WORK-US02-CDT` para ultrassom, `WORK-RX-CDT` para raio-X). O
`sed` de `s/<label-antigo>/<label-novo>/g` resolve o AE **e** o label de uma vez, porque o nome do
AE contém o label. Trocar também a `dicomDescription`, que o sed do label não pega.

```bash
troca() { sed -e 's/US02-CDT/US02-CMI/g' -e "s#^dicomDescription:.*#dicomDescription: $DESC#"; }
ldapsearch ... -b "dicomAETitle=WORK-US02-CDT,$BASE" | troca > ae.ldif
ldapsearch ... -b "dcmWebAppName=WORK-US02-CDT,$BASE" | troca > webapp.ldif   # <-- o esquecido
```

**Conferir antes de aplicar:** `grep -c "<label-antigo>" ae.ldif webapp.ldif` tem que dar **0**.

As conexões (`cn=dicom` 11112, `cn=dicom-tls` 2762) são herdadas por referência: **aparelho novo
não ganha porta nova.** O que separa imagem de worklist é o AE Title, não a porta.

> **Bônus de clonar:** as regras de coerção de atributo ficam **por AE**
> (`SupplementIssuerCPF*`, `SupplementIssuerOfPatientID*`). Um AE clonado herda as regras certas;
> um AE montado à mão nasceria sem elas e os estudos daquele aparelho ficariam fora da régua de
> identidade.

### 3. Registro de AE único

```
dn: dicomAETitle=WORK-XXX,cn=Unique AE Titles Registry,cn=DICOM Configuration,dc=dcm4che,dc=org
objectClass: dicomUniqueAETitle
dicomAETitle: WORK-XXX
```

### 4. Aplicar e recarregar

```bash
cat ae.ldif webapp.ldif registry.ldif | ldapadd -x -H $H -D "$D" -w "$W"
curl -s -X POST http://pacs.marica.automais.cloud:8080/dcm4chee-arc/ctrl/reload   # 204
```

**Nunca** por `PUT /devices/dcm4chee-arc`: descarta o `dcmMWLWorklistLabel` em silêncio (204 sem
gravar) e reescreve o device inteiro. `systemctl restart` também não — custa ~40 s de
indisponibilidade e o `reload` basta.

### 5. Verificar — as quatro provas

1. `GET /dcm4chee-arc/aets` lista o AE novo.
2. `GET /dcm4chee-arc/webapps` lista a Web Application — **é esta que denuncia o passo 2 esquecido.**
3. `GET /aets/<AE>/rs/mwlitems` responde **204** (lista vazia é o correto para AE recém-criado).
   `404` = falta o `dcmWebApp`.
4. **Isolamento:** a soma dos itens de todos os AEs com label tem que bater **exatamente** com o
   total do `WORKLIST` (que não tem label e enxerga tudo). Não bateu = algum item está carimbado
   com label que ninguém lê.

### 6. Cadastrar o equipamento no painel

Exames de Imagem → Equipamentos. `identificador_dicom` = o AE Title do **aparelho** (não o
`WORK-`). Dois campos que importam:

- **`descricao_max_caracteres`**: deixar o padrão **64** (teto do VR `LO`). Só baixar se o console
  daquele aparelho falhar com descrição longa — é o caso do Fuji FDR-3000AWS, que fica em 16.
- **`ativo`**: **perguntar ao operador**, não decidir sozinho. Cadastrar **INATIVO** é o default
  seguro quando o aparelho ainda não chegou — um equipamento ativo a mais na mesma modalidade
  torna a dedução ambígua e **todo envio daquela unidade passa a exigir escolha da recepção**
  antes de a máquina existir. Mas ativar desde já é uma escolha legítima (feita para o `US02-CMI`
  em 09/09/2026), e aí **a recepção da unidade precisa ser avisada no mesmo dia**: a tela passa a
  exigir a escolha do aparelho, o que antes o sistema deduzia sozinho.

### 7. Escopo do exame por unidade (ADR-0056)

Com **um** aparelho na modalidade, a dedução acha candidato único e tudo flui. Com **dois**, há
duas configurações válidas — e a escolha é do operador, não sua:

| | Efeito |
|---|---|
| escopo com equipamento fixo | nada quebra; sem escolha, tudo cai no aparelho fixado |
| escopo sem equipamento | a recepção **precisa** escolher ao autorizar; quem não escolher vê `worklist.equipamento_ambiguo` |

A tela de autorização suporta as duas (`temEscolhaEquip` aparece quando há mais de um candidato).

> **Ativou um segundo aparelho numa unidade que só tinha um?** Isso muda a rotina da recepção
> **na hora**. Avisar a unidade faz parte da tarefa, não é acessório.

### 8. Documento para o técnico

Criar `docs/pacs-<aparelho>.md` no molde de `docs/pacs-us-cmi.md` ou `docs/pacs-rx-cdt.md`, com
servidor, porta, Called AE de imagem (`PACS-CDT`), Called AE de worklist (`WORK-XXX`), Calling AE
do aparelho, filtros e a cola rápida. **Gerar também a cópia `.html`** (`node WiFi/md2html.mjs`) —
é a regra de documento para humanos. **Se o operador pedir PDF**, não há ferramenta no repo:
gerar do HTML com o Chrome headless —
`chrome.exe --headless=new --disable-gpu --no-pdf-header-footer --print-to-pdf=<saida.pdf> "file:///<caminho>.html"`.
Conferir antes de enviar que o documento reflete o estado **final** (ativo × inativo) — o PDF é o
que vai para a mão do técnico.

Acrescentar o aparelho às tabelas de `docs/pacs.md` (§8 e §9.1).

## Armadilhas já pagas

- **Imagem funciona antes de a worklist existir.** A `11112` aceita qualquer Calling AE, então o
  C-STORE passa a funcionar assim que o técnico aponta — e isso dá falsa sensação de que está
  tudo pronto. A worklist é a metade que falta.
- **`StationName` do aparelho ≠ AE Title**, e não tem problema: o isolamento é pelo Calling AE e
  pelo Worklist Label. O Konica emite `ImagePilot.` (com ponto).
- **Item sem label é devolvido a todos os AEs.** O nosso carimba sempre; item criado por sistema
  externo apareceria em todo aparelho.
- **Não repontar o backend** para um AE com label: `Pacs:Dcm4chee:WorklistBaseUrl` tem que
  continuar no `WORKLIST` sem label, que enxerga tudo.
- **Receber a worklist não garante iniciar o exame.** O Fuji falha com erro 31027
  (`JJ1017V3CodeMapping` vazia). Console que não mostra a descrição do estudo costuma precisar de
  `(0040,0008) ScheduledProtocolCodeSequence`, que hoje não emitimos — medido no Konica
  ImagePilot em 09/09/2026, que descarta `(0032,1060)` e `(0040,0007)`.
- **Instalar o aparelho 2 pode desconfigurar o aparelho 1.** Em 11/09/2026, na visita para
  instalar o `US02-CMI`, o técnico mexeu também no `US_CMI` que já funcionava: o Called AE da
  worklist virou `WORK-US-CMI` (antes tentou `WORK-US01-CMI` e `WORK-USG-CMI`, e por um tempo o
  Calling AE ficou `AE`). Nenhum desses existe — o PACS recusou tudo com
  `called-AE-title-not-recognized` e o aparelho ficou sem worklist dias, enquanto a imagem seguia
  chegando (exame digitado no console, Patient ID `AAAAMMDD_hhmmss_...`). **Depois de toda visita
  técnica, conferir no log os DOIS aparelhos da unidade**, não só o novo.

## Diagnóstico: "o aparelho não recebe a worklist"

Antes de mexer em LDAP, **ver o que o aparelho está pedindo de verdade** — o log do dcm4chee dá
o Called e o Calling AE de cada associação. No host do PACS:

```bash
LOGD=/opt/wildfly/standalone/log; IP=<IP público da unidade>
# associações do IP por dia x "CALLED<-CALLING"
grep -h "addr=$IP" $LOGD/server.log* | grep "close Socket" \
  | awk '{for(i=1;i<=NF;i++) if($i ~ /<-/){n=$i; sub(/\(.*$/,"",n); print $1, n}}' | sort | uniq -c
# recusas por nome errado (associação recusada não tem "close Socket" com o IP)
grep -h "A-ASSOCIATE-RJ" $LOGD/server.log | grep -v "ANY-SCP<-" | cut -c1-230 | tail
```

| Sintoma no log | Causa |
|---|---|
| `WORK-XXX<-...` com `A-ASSOCIATE-RJ ... called-AE-title-not-recognized` | Called AE digitado errado no aparelho |
| `WORK-XXX<-<outro nome>` aceito, mas lista vazia | Calling AE errado não importa (o recorte é pelo label); ver datas dos itens |
| nenhuma associação `WORK-*` do IP | aparelho não está consultando (config desligada, rede, ou só consulta ao abrir a tela) |
| C-FIND aceito (`status=ff00H` = item devolvido) e nada na tela | filtro do console (data, modalidade, estação) |

Depois conferir o lado do servidor: `GET /aets/<AE>/rs/mwlitems` e as **datas** dos itens
(`00400002`) — console filtrando "hoje" mostra vazio se só há itens de outros dias. `ANY-SCP<-ECHOSCU`,
`<-CENSYS`, `<-FINDSCU` são varreduras da internet, ruído.

## Memórias relacionadas

`reference_dcm4chee_ae_webapp_par` · `project_worklist_estacao_por_equipamento` ·
`reference_pacs_server` · `project_rx_cdt_konica_worklist` ·
`project_identidade_paciente_pacs` · `feedback_docs_md_copia_html`
