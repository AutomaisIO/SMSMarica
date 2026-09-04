# Configuração do PACS para o Raio-X do CDT

> **Para o técnico do equipamento.** Parâmetros de rede DICOM para integrar o **aparelho de
> Raio-X do CDT** ao PACS da SMS Maricá: para onde **enviar as imagens** e de onde **puxar a
> lista de trabalho (worklist)**.
> AE de worklist `WORK-RX-CDT` criado e validado no servidor em **2026-09-04**.

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

## 2. Identificação do equipamento (lado do Raio-X)

| Parâmetro | Valor |
|-----------|-------|
| **AE Title local** (Calling AE) | **`RX-CDT`** |
| **Nome da estação** (Station Name) | **`RX-CDT`** |

O AE Title precisa ser **exatamente** `RX-CDT` — é por ele que o servidor entrega a worklist só
para esta máquina, e é o mesmo valor cadastrado no painel da SMS (Exames de Imagem →
Equipamentos), associado ao CDT.

---

## 3. Envio de imagens — Storage / C-STORE

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`PACS-CDT`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** |
| AE Title do equipamento (Calling AE) | `RX-CDT` |

---

## 4. Lista de trabalho — Modality Worklist (MWL)

| Parâmetro | Valor |
|-----------|-------|
| **AE Title de destino** (Called AE) | **`WORK-RX-CDT`** |
| Host / IP | `104.236.203.40` |
| **Porta** | **`11112`** &nbsp;← *a MESMA porta do envio de imagens* |
| AE Title do equipamento (Calling AE) | `RX-CDT` |
| Filtros | Modalidade = `DX`; Data = dia atual; **Scheduled Station AE Title = `RX-CDT`** |

> **O `WORK-RX-CDT` já entrega só os exames deste aparelho**, mesmo que o filtro por estação não
> seja configurado: o servidor isola por Worklist Label (ver [`pacs.md`](./pacs.md) §8). Ainda
> assim, preencha o filtro quando o aparelho oferecer — é defesa em profundidade.

Se a máquina suportar **MPPS**, aponte para o mesmo **`WORK-RX-CDT`** na porta `11112`.

> **Imagem e worklist usam a MESMA porta.** O que muda é só o AE Title de destino. Não configure
> a worklist para `PACS-CDT` — esse AE **não** responde MWL e a associação será recusada. E
> vice-versa: não mande imagem para `WORK-RX-CDT`.

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
  Called AE:     WORK-RX-CDT
  Host:          104.236.203.40
  Porta:         11112
  Filtros:       Modality = DX, data de hoje,
                 Scheduled Station AE Title = RX-CDT

MPPS (se houver):
  Called AE:     WORK-RX-CDT   Porta: 11112

EQUIPAMENTO (local)
  AE Title / Station Name:  RX-CDT
```

---

## 6. Rede

Liberar no firewall do CDT a **saída** TCP para `104.236.203.40:11112` (e `:2762` se for usar
TLS). O Raio-X é sempre quem inicia a conexão — o PACS não precisa alcançar a porta local do
aparelho.

---

## 7. Teste de conectividade

1. **C-ECHO** (DICOM ping), com Calling AE `RX-CDT`:
   - `PACS-CDT@104.236.203.40:11112` → **Success (0x0000)**
   - `WORK-RX-CDT@104.236.203.40:11112` → **Success (0x0000)**
2. **Worklist**: os exames aparecem conforme forem autorizados na plataforma SMSMais **com o CDT
   como unidade executante**. Lista vazia costuma ser ausência de exame autorizado para a data —
   confirmar com a equipe Automais.
3. **Envio**: dispare um C-STORE de teste para `PACS-CDT`. A imagem deve aparecer no visualizador
   da plataforma logo em seguida.

---

## 8. Duas perguntas para o fabricante (antes de ligar em produção)

1. **O console exige `(0040,0008) Scheduled Protocol Code Sequence`** para resolver o menu de
   exposição a partir da worklist? No mamógrafo Fuji do CDT, a worklist chegava e o exame **não
   iniciava** por causa disso (tabela de tradução vazia, erro 31027 — ver
   [`PACS/diagnostico-worklist-fuji.md`](./PACS/diagnostico-worklist-fuji.md)). Hoje o SMSMais
   **não** emite essa tag; se este aparelho precisar, combinamos o código antes.
2. **A modalidade emitida é `DX` ou `CR`?** O cadastro está como **`DX`**. Se o aparelho emitir
   `CR`, avise — é um ajuste de um campo no painel, mas sem ele a worklist não casa.

---

## 9. Como o sistema usa esses nomes

O item de worklist sai carimbado com `ScheduledStationAETitle = RX-CDT` e
`WorklistLabel (0074,1202) = RX-CDT` — valor tirado do cadastro **Exames de Imagem →
Equipamentos** (equipamento do CDT, modalidade DX). Se a unidade executante não tiver equipamento
cadastrado na modalidade do exame, o envio falha com "Sem equipamento configurado" e o exame
mostra o erro — **não existe AE de fallback**.

Sem MPPS, quem tira o item da lista do aparelho é o próprio SMSMais: quando o exame chega ao PACS
e a conciliação o promove a *Realizada*, o item é removido da worklist.

---

*Dúvidas técnicas: equipe Automais. Documentação interna do PACS: [`pacs.md`](./pacs.md).
Equivalentes: [`pacs-cdt-mamografo.md`](./pacs-cdt-mamografo.md), [`pacs-us-cmi.md`](./pacs-us-cmi.md).*
