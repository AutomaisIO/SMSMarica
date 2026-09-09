# Configuração do PACS para o 2º ultrassom do CMI (`US02-CMI`)

> **Para o técnico do equipamento.** Parâmetros de rede DICOM para integrar o **segundo
> ultrassom do Centro Materno Infantil (CMI)** ao PACS da SMS Maricá: para onde **enviar as
> imagens** e de onde **puxar a lista de trabalho (worklist)**.
> AE de worklist `WORK-US02-CMI` criado e verificado no servidor em **2026-09-09**.

> ⚠️ **Este documento é do aparelho NOVO.** O ultrassom que já está em operação no CMI continua
> com o AE Title `US_CMI` e a worklist `WORK-CMI` — **não altere a configuração dele**. Ver
> [`pacs-us-cmi.md`](./pacs-us-cmi.md).

---

## 1. Servidor (mesmo para imagem e worklist)

| Item | Valor |
|------|-------|
| Endereço (IP) | **`104.236.203.40`** |
| Hostname (DNS) | `pacs.marica.automais.cloud` |
| Porta DICOM | **`11112`** (sem TLS) |
| Porta DICOM TLS (opcional) | `2762` |

> Use o **IP** se a rede do CMI não resolver o DNS público.

---

## 2. Identificação do equipamento (lado do ultrassom)

| Parâmetro | Valor |
|-----------|-------|
| **AE Title local** (Calling AE) | **`US02-CMI`** |
| **Nome da estação** (Station Name) | **`US02-CMI`** |
| Porta local | `104` (ou a padrão do aparelho) |

O AE Title precisa ser **exatamente** `US02-CMI` — é por ele que o servidor entrega a worklist só
para esta máquina, e é o mesmo valor cadastrado no painel da SMS (Exames de Imagem →
Equipamentos), associado ao Centro Materno Infantil.

> **Não repita o `US_CMI`.** Se os dois aparelhos usarem o mesmo AE Title, os dois recebem a mesma
> lista e o isolamento se perde — a paciente do aparelho 1 aparece no aparelho 2.

---

## 3. Envio de imagens — Storage / C-STORE

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`PACS-CDT`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** |
| AE Title do equipamento (Calling AE) | `US02-CMI` |

> O nome `PACS-CDT` é histórico — é o AE de armazenamento de **todas** as unidades, não só do CDT.

---

## 4. Lista de trabalho — Modality Worklist (MWL)

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`WORK-US02-CMI`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** &nbsp;← *a MESMA porta do envio de imagens* |
| AE Title do equipamento (Calling AE) | `US02-CMI` |
| Filtros | Modalidade = `US`; Data = dia atual; **Scheduled Station AE Title = `US02-CMI`** |

> **O `WORK-US02-CMI` já entrega só os exames deste aparelho**, mesmo que o filtro por estação não
> seja configurado: o servidor isola por Worklist Label. Ainda assim, preencha o filtro quando o
> aparelho oferecer — é defesa em profundidade.

Se a máquina suportar **MPPS**, aponte para o mesmo **`WORK-US02-CMI`** na porta `11112`.

> **Imagem e worklist usam a MESMA porta.** O que muda é só o AE Title de destino. Não configure a
> worklist para `PACS-CDT` — esse AE não responde MWL e a associação será recusada. E vice-versa.

---

## 5. Resumo (cola rápida)

```
SERVIDOR:        104.236.203.40   (pacs.marica.automais.cloud)
PORTA DICOM:     11112            (TLS opcional: 2762)

ENVIO DE IMAGEM (C-STORE)
  Called AE:     PACS-CDT
  Host:          104.236.203.40
  Porta:         11112

WORKLIST (MWL)
  Called AE:     WORK-US02-CMI
  Host:          104.236.203.40
  Porta:         11112
  Filtros:       Modality = US, data de hoje,
                 Scheduled Station AE Title = US02-CMI

MPPS (se houver):
  Called AE:     WORK-US02-CMI   Porta: 11112

EQUIPAMENTO (local)
  AE Title / Station Name:  US02-CMI
  Porta local:              104
```

---

## 6. Rede

Liberar no firewall do CMI a **saída** TCP para `104.236.203.40:11112` (e `:2762` se for usar
TLS). O ultrassom é sempre quem inicia a conexão — o PACS não precisa alcançar a porta local.

---

## 7. Teste de conectividade

1. **C-ECHO** (DICOM ping), com Calling AE `US02-CMI`:
   - `PACS-CDT@104.236.203.40:11112` → **Success (0x0000)**
   - `WORK-US02-CMI@104.236.203.40:11112` → **Success (0x0000)**
2. **Worklist**: no primeiro teste a lista pode vir **vazia** — é o esperado. Os exames só
   aparecem depois que a recepção do CMI autorizar um exame **escolhendo este aparelho** (ver §9).
   Lista vazia **não** é erro de configuração.
3. **Envio**: dispare um C-STORE de teste para `PACS-CDT`. A imagem deve aparecer no visualizador
   da plataforma logo em seguida.

> O envio de imagem costuma funcionar **antes** de a worklist estar certa — a porta aceita
> qualquer Calling AE. Ver imagem chegando **não** prova que a worklist está configurada.

---

## 8. Se o aparelho for Mindray

O ultrassom Mindray DC-28 do CMI exigiu tratamento específico no servidor para as imagens
comprimidas (RLE com YBR planar). **Já está resolvido em produção** — se as imagens aparecerem
com cores trocadas ou distorcidas, avise a equipe Automais citando este parágrafo em vez de mexer
na compressão do aparelho.

---

## 9. Como o sistema decide para qual dos dois ultrassons o exame vai

O CMI tem **dois** ultrassons ativos desde 09/09/2026. Por decisão da SMS, **a recepção escolhe o
aparelho no momento de autorizar o exame** — não há aparelho padrão. Se ninguém escolher, o envio
falha com a mensagem *"A unidade tem mais de um equipamento para esta modalidade. Selecione em
qual o exame será realizado."*

> ⚠️ **Isto muda a rotina da recepção do CMI a partir de agora.** Antes havia um só ultrassom e o
> sistema deduzia sozinho. Avisar a equipe: ao autorizar um exame de ultrassom, aparece o seletor
> de aparelho e é preciso escolher entre **Ultrassom** (`US_CMI`) e **Ultrassom 02** (`US02-CMI`).

O item de worklist sai carimbado com `ScheduledStationAETitle` igual ao AE do aparelho escolhido,
e só o AE de worklist correspondente o enxerga.

---

## 10. Estado do provisionamento (lado da SMS)

| Item | Situação |
|---|---|
| AE de worklist `WORK-US02-CMI` (label `US02-CMI`) | **criado e verificado** em 2026-09-09 |
| Web Application `WORK-US02-CMI` (`MWL_RS`) | **criada** — `/rs/mwlitems` responde 204 |
| Registro em *Unique AE Titles Registry* | **criado** |
| Equipamento no painel (Exames de Imagem → Equipamentos) | cadastrado como **`Ultrassom 02` / `US02-CMI`** |
| Ativação | **ATIVO** desde 2026-09-09 |

**Do lado da SMS está tudo pronto.** Falta apenas a configuração no aparelho, descrita nas
seções 2 a 4 deste documento.

---

*Dúvidas técnicas: equipe Automais. Documentação interna do PACS: [`pacs.md`](./pacs.md).
Aparelho 1 do CMI: [`pacs-us-cmi.md`](./pacs-us-cmi.md).*
