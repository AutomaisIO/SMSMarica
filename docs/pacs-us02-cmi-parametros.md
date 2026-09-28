# Ultrassom 02 — CMI

**Parâmetros DICOM · PACS SMS Maricá**

## Servidor

| | |
|---|---|
| IP | **104.236.203.40** |
| Hostname | pacs.marica.automais.cloud |
| Porta DICOM | **11112** |
| Porta TLS (opcional) | 2762 |

## Aparelho (local)

| | |
|---|---|
| AE Title (Calling AE) | **US02-CMI** |
| Station Name | **US02-CMI** |
| Modalidade | **US** |

## Envio de imagem — C-STORE

| | |
|---|---|
| Called AE | **PACS-CDT** |
| Host | 104.236.203.40 |
| Porta | **11112** |

## Worklist — MWL

| | |
|---|---|
| Called AE | **WORK-US02-CMI** |
| Host | 104.236.203.40 |
| Porta | **11112** |
| Filtro | Modality = US · Scheduled Station AE Title = US02-CMI |

## MPPS (se houver)

| | |
|---|---|
| Called AE | **WORK-US02-CMI** · porta **11112** |

---

Imagem e worklist usam a **mesma porta**. O que muda é o **Called AE**.

Liberar saída TCP para `104.236.203.40:11112`.

*Automais · 09/09/2026*
