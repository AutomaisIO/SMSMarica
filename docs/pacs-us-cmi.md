# Configuração do PACS para o ultrassom do CMI

> **Para o técnico do equipamento.** Parâmetros de rede DICOM para integrar o
> ultrassom do **Centro Materno Infantil (CMI)** ao PACS da SMS Maricá: para onde
> **enviar as imagens** e de onde **puxar a lista de trabalho (worklist)**.
> Validado por C-ECHO em **2026-07-22** (AE de worklist do CMI: `WORK-CMI`).

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
| **AE Title local** (Calling AE) | **`US_CMI`** |
| **Nome da estação** (Station Name) | **`US_CMI`** |
| Porta local | `104` |

O AE Title precisa ser **exatamente** `US_CMI` — é por ele que o sistema entrega a
worklist só para esta máquina, e é o mesmo valor cadastrado no painel da SMS
(Exames de Imagem → Equipamentos), associado ao Centro Materno Infantil.

---

## 3. Envio de imagens — Storage / C-STORE

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`PACS-CDT`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** |
| AE Title do equipamento (Calling AE) | `US_CMI` |

---

## 4. Lista de trabalho — Modality Worklist (MWL)

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`WORK-CMI`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** &nbsp;← *a MESMA porta do envio de imagens* |
| AE Title do equipamento (Calling AE) | `US_CMI` |
| Filtros | Modalidade = `US`; Data = dia atual; **Scheduled Station AE Title = `US_CMI`** |

> **O `WORK-CMI` já entrega só os exames do CMI**, mesmo que o filtro por estação não
> seja configurado: o servidor isola por Worklist Label (ver `pacs.md`). Ainda assim,
> preencha o filtro quando o aparelho oferecer — é defesa em profundidade.

Se a máquina suportar **MPPS**, aponte para o mesmo **`WORK-CMI`** na porta `11112`.

> **Imagem e worklist usam a MESMA porta.** O que muda é só o AE Title de destino.
> Não configure a worklist para `PACS-CDT` — esse AE não responde MWL e a
> associação será recusada. E vice-versa.

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
  Called AE:     WORK-CMI
  Host:          104.236.203.40
  Porta:         11112
  Filtros:       Modality = US, data de hoje,
                 Scheduled Station AE Title = US_CMI   <- obrigatorio

MPPS (se houver):
  Called AE:     WORK-CMI   Porta: 11112

EQUIPAMENTO (local)
  AE Title / Station Name:  US_CMI
  Porta local:              104
```

---

## 6. Rede

Liberar no firewall do CMI a **saída** TCP para `104.236.203.40:11112`
(e `:2762` se for usar TLS). O ultrassom é sempre quem inicia a conexão — o PACS
não precisa alcançar a porta local `104`.

---

## 7. Teste de conectividade

1. **C-ECHO** (DICOM ping), com Calling AE `US_CMI`:
   - `PACS-CDT@104.236.203.40:11112` → **Success (0x0000)**
   - `WORK-CMI@104.236.203.40:11112` → **Success (0x0000)**
   - *(Ambos validados em 2026-07-21 pela equipe Automais, a partir da internet.)*
2. **Worklist**: os exames aparecem conforme forem agendados na plataforma
   SMSMais **com o CMI como unidade executante**. Lista vazia costuma ser
   ausência de exame agendado para a data — confirmar com a equipe Automais.
3. **Envio**: dispare um C-STORE de teste para `PACS-CDT`. A imagem deve aparecer
   no visualizador da plataforma logo em seguida.

---

## 8. Como o sistema usa esses nomes

O item de worklist sai carimbado com `ScheduledStationAETitle = US_CMI` — valor
tirado do cadastro **Exames de Imagem → Equipamentos** (equipamento do CMI,
modalidade US). Se a unidade executante não tiver equipamento cadastrado na
modalidade do exame, o envio à worklist falha com "Sem equipamento configurado" e
o exame mostra o erro — não existe AE de fallback.

A separação de "Exames de Imagem" por unidade **não** depende do AE: ela vem da
unidade executante da solicitação.

---

*Dúvidas técnicas: equipe Automais. Documentação interna do PACS: [`pacs.md`](./pacs.md).
Equivalente do mamógrafo: [`pacs-cdt-mamografo.md`](./pacs-cdt-mamografo.md).*
