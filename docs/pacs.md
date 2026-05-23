# PACS — Imagens DICOM (dcm4chee-arc)

Documentação operacional do servidor de imagens médicas que abastece o visualizador
`SMSMarica.front > /features/pacs`. Documenta o que está em produção hoje
(2026-05-23): topologia, configuração, integrações, problemas conhecidos e ações
recomendadas.

## 1. Visão geral

O backend de imagens é um **dcm4chee-arc-light 5.32.0** (open-source DICOM
archive sobre Wildfly/Java) hospedado em DigitalOcean. O frontend `SMSMarica.front`
fala **DICOMweb (QIDO-RS + WADO-RS)** com ele através do proxy `/pacs/rs/*` do
backend `SMSMarica.Api`, que apenas encaminha as requisições HTTP sem reescrever
payload — ver [`PacsProxyService`](../SMSMarica.server/src/SMSMarica.Core/Pacs/PacsProxyService.cs).

```mermaid
flowchart LR
  Front[SMSMarica.front<br/>Cornerstone3D + WADO-RS]
  Api[SMSMarica.Api<br/>/pacs/rs/* proxy]
  Arc[dcm4chee-arc 5.32.0<br/>Wildfly :8080]
  PG[(PostgreSQL 14<br/>dcmdb)]
  LDAP[(OpenLDAP<br/>cn=admin,dc=dcm4che,dc=org)]
  S3[(DigitalOcean Spaces<br/>images.pacs.marica.automais.tec.br<br/>nyc3.digitaloceanspaces.com)]
  Mod[Equipamentos<br/>Fuji FDR-3000AWS, Oehm und Rehbein, ...]

  Front -->|HTTPS| Api -->|HTTP :8080| Arc
  Arc --> PG
  Arc --> LDAP
  Arc -->|s3fs-fuse<br/>/mnt/s3images| S3
  Mod -->|C-STORE :11112| Arc
```

## 2. Host

| Item | Valor |
|------|-------|
| Hostname | `pacs.marica.automais.cloud` |
| IP | `104.236.203.40` (DigitalOcean Droplet) |
| SO | Ubuntu 22.04.4 LTS (kernel 5.15) |
| Acesso | SSH `root@pacs.marica.automais.cloud:22` (OpenSSH 8.9p1) — credenciais fora deste repositório |
| Host key | `ssh-ed25519 SHA256:qma4J9CyGhuOHPoGwXtspfXncTN+nhCFDPmSh4RvXCo` |

> Não documentar senhas neste repositório. SSH é por chave/senha mantidas no
> cofre da equipe.

### Outros serviços no mesmo host (não relacionados ao PACS)

| Porta | Processo | Domínio | Finalidade |
|-------|----------|---------|------------|
| 5000 | `dotnet` | `api.pegaph.automais.app` (via nginx) | Outro produto (não SMSMarica). |
| 80/443 | nginx | `pegaph.automais.app` / `api.pegaph.automais.app` | Frontend e API do produto acima. |

O PACS **não** está atrás do nginx local — fica direto em `:8080` (plain HTTP).
Ver §10 (problemas conhecidos).

## 3. dcm4chee — instalação

| Item | Valor |
|------|-------|
| Versão | `dcm4chee-arc-ear-5.32.0-psql` (build `24e182d`, 2024-04-22) |
| Container | Wildfly nativo (sem Docker), config `dcm4chee-arc.xml` |
| Service | `systemd: dcm4chee.service` (User=root, ExecStart=`/opt/wildfly/bin/standalone.sh -c dcm4chee-arc.xml -b 0.0.0.0`) |
| Dir base | `/opt/wildfly/` (owner `dcm:dcm`) |
| Config dir | `/opt/wildfly/standalone/configuration/dcm4chee-arc/ldap.properties` |
| Deploy mode | **unsecure** (`dcm4chee-arc-war-5.32.0-unsecure.war` — Keycloak instalado como módulo mas **não ativo**) |
| Logs | `/opt/wildfly/standalone/log/server.log` (rotação diária) |
| UI admin | http://pacs.marica.automais.cloud:8080/dcm4chee-arc/ui2/ |

### Portas expostas

| Porta | Protocolo | Finalidade |
|-------|-----------|------------|
| `8080` | HTTP | DICOMweb (QIDO-RS, WADO-RS, STOW-RS) + UI Arc Light — **plain, internet-facing** |
| `8443` | HTTPS | Mesmo conteúdo do 8080 sobre TLS (cert self-signed) |
| `9990` | HTTP | Wildfly mgmt console (bind `127.0.0.1` apenas) |
| `11112` | DICOM | C-STORE/C-FIND/C-MOVE plain — entrada dos equipamentos |
| `2762` | DICOM TLS | Mesmo do 11112 sobre TLS |
| `2575` | HL7 | MLLP plain |
| `12575` | HL7 TLS | MLLP sobre TLS |

## 4. Storage

| Item | Valor |
|------|-------|
| `dcmStorageID` | `fs1` |
| URI | `file:///mnt/s3images/` |
| Backing | DigitalOcean Spaces (S3-compatível) via **s3fs-fuse** |
| Bucket | `images.pacs.marica.automais.tec.br` |
| Endpoint | `https://nyc3.digitaloceanspaces.com` |
| Path format | `{now,date,yyyy/MM/dd}/{0020000D,hash}/{0020000E,hash}/{00080018,hash}` |
| `dcmCheckMountFilePath` | `NO_MOUNT` (não valida a montagem) |
| `dcmDigestAlgorithm` | `MD5` |
| Credenciais | `/root/.passwd-s3fs` (chmod 600, AWS-style `key:secret`) |

s3fs mount (linha em `mount` no host):

```
s3fs images.pacs.marica.automais.tec.br /mnt/s3images
  -o rw,allow_other,passwd_file=/root/.passwd-s3fs,
     url=https://nyc3.digitaloceanspaces.com,use_path_request_style,
     uid=1000,gid=1000,umask=0022,dev,suid
```

> **Não há `MetadataStorageID` configurado.** Toda chamada `/metadata` precisa
> abrir o DICOM original no S3 — se o objeto sumir, o endpoint volta 500
> (ver §10.1).

## 5. Banco PostgreSQL

| Item | Valor |
|------|-------|
| Cluster | PostgreSQL 14 (Ubuntu, bind `127.0.0.1:5432`) |
| Database | `dcmdb` (owner `dcmadmin`) |
| Migrations | gerenciadas pelo próprio dcm4chee (EAR já traz scripts) |

**Volume atual (2026-05-23):**

| Tabela | Linhas |
|--------|--------|
| `patient` | 2.491 |
| `study` | 2.493 |
| `series` | 10.968 |
| `instance` | 10.968 |
| `location` | 10.968 (todas em `storage_id='fs1'`) |

## 6. LDAP (configuração do dcm4chee)

O dcm4chee-arc guarda **toda a configuração** (devices, AEs, storage, regras de
exportação, retenção, etc.) num diretório LDAP — não em arquivo XML.

| Item | Valor |
|------|-------|
| Daemon | `slapd` (`systemd: slapd.service`) |
| URL | `ldap://localhost:389/dc=dcm4che,dc=org` |
| DN admin | `cn=admin,dc=dcm4che,dc=org` |
| Senha | **padrão de instalação `dcmsecret`** — ver §10.4 |
| Cliente CLI | `ldapsearch -x -D 'cn=admin,dc=dcm4che,dc=org' -W -b 'dc=dcm4che,dc=org'` |

Também é possível editar tudo via REST do dcm4chee (`/dcm4chee-arc/devices/...`)
ou pela UI Arc Light (`Configuration > Devices`).

## 7. AE titles (Application Entities)

| AE Title | Descrição |
|----------|-----------|
| **`DCM4CHEE`** | AE principal — esconde instâncias rejeitadas. **Esta é a usada pelo SMSMarica.** |
| `AS_RECEIVED` | Retrieve instâncias exatamente como recebidas (sem filtro de rejeição). |
| `IOCM_EXPIRED` | Mostra só instâncias rejeitadas por *Data Retention Expired*. |
| `IOCM_PAT_SAFETY` | Mostra só instâncias rejeitadas por *Patient Safety*. |
| `IOCM_QUALITY` | Mostra só instâncias rejeitadas por *Quality Reasons*. |
| `IOCM_REGULAR_USE` | Mostra inclusive instâncias rejeitadas por *Quality*. |
| `IOCM_WRONG_MWL` | Mostra só instâncias rejeitadas por *Incorrect MWL Entry*. |
| `WORKLIST` | Modality Worklist (MWL) + Unified Worklist (UPS). |

A regra `dcmAllowDeleteStudyPermanently = REJECTED` está em todas — só estudos
explicitamente rejeitados podem ser apagados permanentemente.

### Conexões DICOM (`dicomNetworkConnection`)

| `cn` | host | porta | TLS |
|------|------|-------|-----|
| `dicom` | localhost | 11112 | — |
| `dicom-tls` | localhost | 2762 | sim |
| `hl7` | localhost | 2575 | — |
| `hl7-tls` | localhost | 12575 | sim |
| `syslog` | localhost | — | — |
| `syslog-tls` | localhost | — | sim |

## 8. Equipamentos emissores (modalidades)

Inferido dos `Manufacturer` / `ManufacturerModelName` dos estudos armazenados:

| Fabricante | Modelo | Modalidade observada | PixelSpacing típico |
|-----------|--------|----------------------|----------------------|
| FUJIFILM Corporation | FDR-3000AWS | MG (mamografia digital) | `0.05 mm` (50 µm) |
| Oehm und Rehbein GmbH | (sem `ManufacturerModelName`) | MG | (estudos de 2024 — arquivo sumiu) |

Equipamentos enviam via **C-STORE para `DCM4CHEE@pacs.marica.automais.cloud:11112`**
(plain). Lembrar: a porta 11112 está aberta para internet — qualquer modalidade
configurada pode pushar.

## 9. Endpoints DICOMweb (consumidos pelo SMSMarica)

Base RS: `http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/DCM4CHEE/rs/`

Configurado em [`appsettings.json`](../SMSMarica.server/src/SMSMarica.Api/appsettings.json)
sob `Pacs.Dcm4chee.RsBaseUrl`, sobrescritível por `Pacs__Dcm4chee__RsBaseUrl` em prod.

| Operação | Caminho |
|----------|---------|
| QIDO — estudos | `GET /studies` (params: `PatientName`, `StudyDate`, `limit`, `includefield=all`, `fuzzymatching=true`, `orderby`) |
| QIDO — séries | `GET /studies/{StudyInstanceUID}/series?includefield=all` |
| QIDO — instâncias | `GET /studies/{study}/series/{series}/instances?includefield=all` |
| WADO — metadados | `GET /studies/{study}/series/{series}/metadata` (`Accept: application/dicom+json`) ⚠ atualmente quebrado para estudos de 2024 — ver §10.1 |
| WADO — instância (metadata) | `GET /studies/{study}/series/{series}/instances/{sop}/metadata` |
| WADO — pixel frame | `GET /studies/{study}/series/{series}/instances/{sop}/frames/{n}` (`Accept: multipart/related; type="application/octet-stream"; transfer-syntax=*`) — chamado pelo Cornerstone `dicom-image-loader` |

Header obrigatório nos QIDO/metadata: `Accept: application/dicom+json`. STOW-RS
(upload via HTTP) também existe mas hoje não é usado pelo SMSMarica — os
equipamentos enviam via DICOM C-STORE direto.

### Integração no `SMSMarica.front`

Cornerstone3D (`@cornerstonejs/core` + `tools` + `dicom-image-loader`) consome
o WADO-RS via os helpers em [`features/pacs/lib/cornerstone.ts`](../SMSMarica.front/src/features/pacs/lib/cornerstone.ts):

- `wadoRsRoot()` aponta para `${baseURL}/pacs/rs` (passa pelo proxy).
- `construirImageId(study, series, sop, frame)` monta `wadors:.../frames/1`.
- `registrarMetadados(imageId, dicomJsonDataset)` registra em
  `wadors.metaDataManager` — é daí que o Cornerstone deriva `PixelSpacing` para
  a `LengthTool` mostrar mm em vez de px.

Resumo do fluxo no `PacsViewerPage.tsx`:

```
1. Usuário busca estudo (PacsBuscaModal) → QIDO /studies
2. Sidebar lista séries → QIDO /studies/{}/series
3. Selecionar série → /studies/{}/series/{}/metadata (todas instâncias)
   → registrarMetadados(imageId, inst) para cada uma
   → setStack(imageIds) no viewport
4. LengthTool mede em mm se PixelSpacing chegou no metadado
```

## 10. Problemas conhecidos e ações recomendadas

### 10.1. **Arquivos de 2024 sumiram do bucket S3 — `/metadata` retorna 500**

`/mnt/s3images/` hoje só tem `2025/` e `2026/`. **Toda a árvore `2024/`
desapareceu.** O Postgres ainda referencia 10.968 instâncias em `storage_id=fs1`,
e ~25–30% delas têm `storage_path` começando com `2024/...` — os equipamentos
de mamografia "Oehm und Rehbein" colocaram tudo entre 2024-07 e 2024-08.

Stack trace típico no `server.log` ao tentar abrir um estudo de 2024:

```
WARN [org.dcm4chee.arc.wado.WadoRS] Response Internal Server Error caused by
java.nio.file.NoSuchFileException: /mnt/s3images/2024/08/12/D073D656/A464541B/32286057
  at org.dcm4chee.arc.storage.filesystem.FileSystemStorage.openInputStreamA(FileSystemStorage.java:209)
  at ...RetrieveServiceImpl.loadMetadataFromDicomFile(RetrieveServiceImpl.java:1094)
```

QIDO funciona (catálogo no Postgres), mas `/metadata` precisa abrir o arquivo
físico → 500.

**Investigar urgente:**
- Os objetos foram realmente apagados do bucket no DigitalOcean Spaces? (Console
  Spaces → bucket → procurar prefixo `2024/`).
- Existe lifecycle policy no bucket? Versioning está habilitado? Se sim, dá
  pra restaurar.
- Há backup externo dos exames de 2024? Mamografia tem requisito legal de
  retenção (CFM/CRM exige 20 anos).

**Mitigação imediata no frontend** (não resolve o sumiço, mas evita o crash):
- Marcar série como indisponível quando `/metadata` retorna 500.
- Mensagem clara para o usuário: "Imagens deste exame não estão mais disponíveis
  no storage".

### 10.2. **Sem `MetadataStorageID` configurado**

Sem cache de metadados, `/metadata` sempre vai ao arquivo DICOM cru. Isso:

1. Torna o sistema refém do S3 (latência + falha mata o endpoint).
2. Faz com que sumiço de arquivo derrube o catálogo, mesmo que só o
   metadado bastasse para o visualizador.

Recomendado: configurar um **segundo storage** (`dcmStorageID=meta`) só com
JSON de metadados (ex.: outro bucket ou disco local), e setar
`dcmMetadataStorageID=meta` no `dcmArchiveDevice`. dcm4chee passa a gravar
um JSON por instância nesse storage no momento do C-STORE, e o `/metadata`
serve do JSON sem tocar o arquivo DICOM. Doc: `https://dcm4che.atlassian.net/wiki/spaces/ee2/pages/4292935747/Metadata+Storage`.

### 10.3. **Régua em pixels para estudos antigos** (origem da investigação)

Causa direta: como `/metadata` falha em estudos de 2024 (§10.1), o frontend
não consegue registrar `PixelSpacing` no `wadors.metaDataManager` → a
`LengthTool` cai em pixels. Para estudos de 2025/2026 (FUJIFILM), o
`PixelSpacing = 0.05 mm` chega corretamente e a régua mostra mm.

Mesmo assim vale defender o frontend contra duas variantes:

a) **Modalidade DX/CR/MG sem `PixelSpacing` (00280030) mas com
`ImagerPixelSpacing` (00181164)** — promover `ImagerPixelSpacing` para
`PixelSpacing` em `lib/dicomJson.ts` antes do `registrarMetadados`. Para
mamografia (placa colada na mama), os dois são equivalentes
(`EstimatedRadiographicMagnificationFactor=1`).

b) **Imagens sem nenhum spacing** (US, SC) — adicionar
`CalibrationLineTool` do Cornerstone como fallback (usuário calibra
clicando em dois pontos com distância conhecida).

### 10.4. **Segurança — deploy `unsecure`**

- `dcm4chee-arc-war-5.32.0-unsecure.war`: **sem autenticação** em nenhum
  endpoint REST. Keycloak está instalado como módulo do Wildfly mas não
  está em uso.
- LDAP admin com senha **`dcmsecret`** (default público da imagem upstream).
  Qualquer pessoa com acesso ao host pode reconfigurar AEs, storage,
  exportar/apagar estudos.
- `:8080` plain HTTP **exposto à internet pública** (não está atrás do
  nginx). Tráfego DICOMweb inclui dados de paciente em claro.
- `:11112` (C-STORE) também exposto sem TLS — qualquer modalidade
  configurada pode pushar para `DCM4CHEE`.

**Recomendado para LGPD/CFM:**
1. Trocar `dcmsecret` (LDAP) por senha forte; atualizar `ldap.properties` e
   reiniciar `dcm4chee.service`.
2. Colocar o `:8080` atrás do nginx local com TLS (Certbot) e fechar 8080
   no firewall do Droplet — manter só `127.0.0.1:8080` ouvido.
3. Avaliar habilitar Keycloak (deploy `secure`) ou ao menos basic-auth no
   nginx para os endpoints REST.
4. Restringir `:11112` por firewall às modalidades conhecidas (allowlist
   de IP da unidade).

### 10.5. **`MahatmaFS` em `/mnt/s3images/` retorna I/O error**

Arquivo `MahatmaFS` (15 bytes) na raiz do bucket dá `Input/output error` ao
ser lido. Provavelmente um artefato deixado por alguma migração antiga.
Inofensivo, mas convém limpar (`rm /mnt/s3images/MahatmaFS` via s3fs ou
direto pelo console do Spaces).

## 11. Operação — receitas curtas

> Todos os comandos assumem SSH como `root@pacs.marica.automais.cloud`.

```bash
# Status do PACS
systemctl status dcm4chee

# Logs
tail -f /opt/wildfly/standalone/log/server.log
journalctl -u dcm4chee -f

# Reiniciar PACS (~40s de downtime)
systemctl restart dcm4chee

# Storage — checar montagem
df -h /mnt/s3images && mount | grep s3fs
ls /mnt/s3images/ | head -5

# Postgres
sudo -u postgres psql dcmdb -c "SELECT count(*) FROM study;"

# UI Arc Light (admin)
# http://pacs.marica.automais.cloud:8080/dcm4chee-arc/ui2/
```

## 12. Referências

- dcm4chee-arc-light docs: https://dcm4che.atlassian.net/wiki/spaces/ee2/
- DICOMweb specs: https://www.dicomstandard.org/dicomweb
- Cornerstone3D + WADO-RS: https://www.cornerstonejs.org/docs/concepts/cornerstone-core/imageId
- s3fs-fuse: https://github.com/s3fs-fuse/s3fs-fuse
- ADR-0004 (arquitetura 3-projetos do backend): [`adr/0004-arquitetura-tres-projetos.md`](./adr/0004-arquitetura-tres-projetos.md)
