# Configuração do PACS para o equipamento (mamógrafo do CDT)

> **Para o técnico do equipamento.** Estes são os parâmetros de rede DICOM para
> integrar o mamógrafo (e qualquer modalidade) ao PACS da SMS Maricá: para onde
> **enviar as imagens** e de onde **puxar a lista de trabalho (worklist)**.
> Atualizado em **2026-06-16**.

---

## 1. Servidor (mesmo para imagem e worklist)

| Item | Valor |
|------|-------|
| Endereço (IP) | **`104.236.203.40`** |
| Hostname (DNS) | `pacs.marica.automais.cloud` |
| Porta DICOM | **`11112`** (sem TLS) |
| Porta DICOM TLS (opcional) | `2762` |

> Use o **IP** se a rede do CDT não resolver o DNS público.

---

## 2. Envio de imagens — Storage / C-STORE

Configure o destino de **armazenamento de imagens** (Storage SCP / "PACS de envio"):

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`PACS-CDT`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** |
| AE Title do equipamento (Calling AE / AE local) | o do próprio mamógrafo — ex.: **`FDR-MAMO`** |

---

## 3. Lista de trabalho — Modality Worklist (MWL)

Configure a **consulta de worklist** (Modality Worklist SCP):

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`WORK-CDT`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** &nbsp;← *a MESMA porta do envio de imagens* |
| AE Title do equipamento (Calling AE / AE local) | o do próprio mamógrafo — ex.: **`FDR-MAMO`** |
| Filtros recomendados | Modalidade = `MG`; Data = dia atual |

---

## 4. ⚠️ A worklist roda em porta diferente? **NÃO.**

**Imagem e worklist usam a MESMA porta: `11112`.** O que muda entre as duas
funções é **apenas o AE Title de destino**:

| Função | AE Title de destino | Porta |
|--------|--------------------|-------|
| **Enviar imagem** (C-STORE) | **`PACS-CDT`** | **11112** |
| **Puxar worklist** (MWL) | **`WORK-CDT`** | **11112** |

Não configure o worklist para o AE de imagens (`PACS-CDT`) — esse AE **não**
responde MWL e a associação será recusada. E vice-versa.

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
  Called AE:     WORK-CDT
  Host:          104.236.203.40
  Porta:         11112

AE do equipamento (Calling AE / local):  FDR-MAMO   (ou o nome já usado na máquina)
```

---

## 6. Teste de conectividade

1. **DICOM ping (C-ECHO)** para confirmar que a máquina alcança o servidor:
   - C-ECHO em `PACS-CDT@104.236.203.40:11112` → deve retornar **Success (0x0000)**.
   - C-ECHO em `WORK-CDT@104.236.203.40:11112` → deve retornar **Success (0x0000)**.
   - *(Ambos validados em 2026-06-16 pela equipe Automais.)*
2. **Envio de imagem**: dispare um C-STORE de teste para `PACS-CDT`. A imagem deve
   aparecer no visualizador da plataforma SMSMarica em seguida.
3. **Worklist**: configure o nó MWL para `WORK-CDT` e atualize a lista. Os exames
   aparecem conforme forem **agendados na plataforma SMSMarica** — se a lista vier
   vazia, confirme com a equipe Automais se há exame agendado para a data/modalidade.

---

## 7. Observações

- O AE Title do **equipamento** (Calling AE) pode ser o que a máquina já usa
  (ex.: `FDR-MAMO`). O servidor não exige cadastro prévio dele hoje, mas use um
  nome **fixo e único** por equipamento — facilita rastrear e, futuramente,
  restringir por allowlist.
- Se a máquina suportar **MPPS** (Modality Performed Procedure Step), aponte para o
  mesmo AE de worklist **`WORK-CDT`** (porta `11112`).
- Firewall do lado do CDT: liberar saída TCP para `104.236.203.40:11112`
  (e `:2762` se for usar TLS).

---

*Dúvidas técnicas: equipe Automais. Documentação interna do PACS: `docs/pacs.md`.*
