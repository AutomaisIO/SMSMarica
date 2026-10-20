# Configuração da worklist da MAPA do CDT (CardioNet MAPAS DICOM)

> **Para o técnico da Cardios / da unidade.** Parâmetros para o **MAPAS DICOM** da CardioNet do CDT
> puxar a **lista de trabalho (worklist)** da plataforma SMSMais: os exames de **MAPA**
> (monitorização ambulatorial da pressão arterial) que o SISREG agenda para o CDT aparecem na busca
> DICOM do programa, já com nome, data de nascimento, sexo e nº do pedido.
> AE de worklist **`WORK-MAPAS-CDT`** (label `MAPAS-CDT`) criado e validado no servidor em
> **2026-10-19** — `/aets`, `/webapps`, `mwlitems` 204 e isolamento 543 = 543.

A MAPA tinha saído, até 19/10/2026, pelo CardioNet da cardiologia (`WORK-CARDIO-CDT`, modalidade
`ECG`), junto com o Holter. Desde então ela tem aparelho e worklist próprios — o CardioNet de
Holter/ECG ([`pacs-cardio-cdt.md`](./pacs-cardio-cdt.md)) não recebe mais MAPA nova.

---

## 1. O que o MAPAS DICOM faz (e o que não faz)

O CardioNet Client DICOM, do mesmo fabricante, **só consulta a worklist** (Modality Worklist FIND,
SCU) e faz **C-ECHO** — não envia imagem, laudo nem MPPS ao PACS, e traz no máximo **1000 itens**
por consulta. **Conferir na declaração de conformidade do MAPAS DICOM** se é o mesmo; se ele também
não envia nada ao PACS, vale o §5 ("o item não sai da lista sozinho").

Os itens da nossa worklist saem **sem acento** (nome com acento chega sem acento, não corrompido).

---

## 2. Servidor

| Item | Valor |
|------|-------|
| Endereço (IP) | **`104.236.203.40`** |
| Hostname (DNS) | `pacs.marica.automais.cloud` |
| Porta DICOM | **`11112`** (sem TLS) |

> Use o **IP** se a rede do CDT não resolver o DNS público.

---

## 3. Configuração DICOM do MAPAS

| Campo | Valor | Observação |
|---|---|---|
| **AE do cliente** (Calling AE / "Cliente AET") | **`MAPAS-CDT`** | maiúsculas, exatamente assim — não usar o padrão de fábrica nem o `CARDIO-CDT` do Holter |
| **AE do servidor** (Called AE / "Servidor AEC") | **`WORK-MAPAS-CDT`** | maiúsculas, exatamente assim |
| **IP do servidor** | `104.236.203.40` | |
| **Porta do servidor** | `11112` | a mesma porta dos outros aparelhos — o que muda é o AE |

Depois de preencher, **"Testar Conexão"** tem que dar sucesso (é um C-ECHO).

### Parâmetros de busca

| Campo | Valor recomendado | Por quê |
|---|---|---|
| **Modalidade** | **`OT`** (ou em branco) | todo exame desta worklist sai com Modality `OT`. Qualquer outro valor (`ECG`, `HD`, `MAPA`…) devolve a lista **vazia** |
| **Nome da estação** | **`MAPAS-CDT`** (ou em branco) | é o Scheduled Station AE Title dos nossos itens |
| **Período** | **`7`** dias | ver §5 — o período segura a lista enxuta |
| ID / Nome / Accession | em branco | são para busca pontual |

> **O `WORK-MAPAS-CDT` já entrega só a MAPA**, mesmo sem filtro nenhum: o servidor recorta pelo
> Worklist Label (ver [`pacs.md`](./pacs.md) §8). Os filtros acima são defesa em profundidade.

---

## 4. Resumo (cola rápida)

```
CardioNet MAPAS DICOM → Configuração DICOM

  AE do cliente:   MAPAS-CDT
  AE do servidor:  WORK-MAPAS-CDT
  IP do servidor:  104.236.203.40
  Porta:           11112

  Busca:  Modalidade = OT   Nome da estação = MAPAS-CDT   Período = 7
```

---

## 5. Pontos de atenção

- **Só MAPA.** Holter e ECG continuam no CardioNet da cardiologia (`CARDIO-CDT` / `WORK-CARDIO-CDT`).
  Paciente com MAPA e Holter no mesmo dia aparece uma vez em cada programa, cada um com o seu nº de
  pedido (Accession).
- **Densitometria também é `OT`** no CDT (aparelho `DO-CDT`), mas não aparece aqui: quem separa é o
  AE (`WORK-MAPAS-CDT` × `WORK-DO-CDT`), não a modalidade.
- **O item não sai da lista sozinho.** Nos aparelhos de imagem, o item some quando a imagem chega ao
  PACS. Se o MAPAS não manda nada ao PACS, a MAPA feita continua na lista — por isso o filtro de
  **Período**. (Pendência do nosso lado: fechar o item de quem não manda imagem — ver `pacs.md` §8,
  "Ciclo de vida".)
- **Médico solicitante.** Liga-se no cadastro do equipamento `MAPAS-CDT` (Exames de Imagem →
  Equipamentos → "O que vai na worklist deste aparelho"), na tag que o programa pedir. Nasce
  desligado — o CardioNet de Holter pede o `0008,0090`; conferir o que o MAPAS pede antes de ligar.
- **O exame só entra na lista depois que a recepção do CDT autoriza** o pedido na plataforma.

---

## 6. Rede

Liberar no firewall do CDT a **saída** TCP para `104.236.203.40:11112`. O MAPAS é sempre quem
inicia a conexão — o servidor não precisa alcançar o computador do CDT.

---

## 7. Teste

1. **Testar Conexão** → sucesso.
2. **Procurar** → aparecem as MAPAs autorizadas na recepção do CDT nos últimos 7 dias. Lista vazia
   com conexão OK costuma ser: (a) nenhuma MAPA autorizada no período, ou (b) filtro de Modalidade
   diferente de `OT`. Confirmar com a equipe Automais, que vê no servidor o que o programa pediu de
   fato.
