# Configuração da worklist para a Cardiologia do CDT (CardioNet DICOM)

> **Para o técnico da Cardios / da unidade.** Parâmetros para o **CardioNet Client DICOM** do CDT
> puxar a **lista de trabalho (worklist)** da plataforma SMSMais: os exames de **Holter 24 horas** e
> **MAPA** que o SISREG agenda para o CDT aparecem na tela "Criar novo exame → fonte DICOM →
> Procurar...", já com nome, data de nascimento, sexo e nº do pedido.
> AE de worklist **`WORK-CARDIO-CDT`** (label `CARDIO-CDT`) criado e validado no servidor em
> **2026-10-16** — `/aets`, `/webapps`, `mwlitems` 204 e isolamento 523 = 523.

---

## 1. O que o CardioNet faz (e o que não faz)

Pela declaração de conformidade do próprio CardioNet Client DICOM (manual 01.2900.00079, Apêndice D):

- **Consulta a worklist** (Modality Worklist FIND, SCU) e faz **C-ECHO** (Verification). Só isso.
- **Não envia nada ao PACS** — nem imagem, nem laudo, nem MPPS. O exame vai para a conta do
  analista na nuvem da Cardios e o laudo volta por lá.
- Consulta **só quando o operador pede** (ao preparar o gravador), uma associação por vez, no
  máximo **1000 itens** por consulta (não configurável).
- Texto em **ISO_IR 100 (Latin-1)**. O item da nossa worklist sai sem acento, então nome com
  acento chega sem acento — não chega corrompido.

**O que ele pede de fato** (captura do C-FIND em 16/10/2026, versão instalada no CDT — a declaração
de conformidade de 2020 está incompleta): nº do pedido, **médico solicitante (0008,0090)**,
**descrição do exame (0032,1060)**, Study UID; do paciente, nome, ID, nascimento, sexo, altura, peso,
endereço, país, região, telefone e tabagismo; e na etapa agendada, Modality, Station AE e o período
de datas (do "Período" configurado até hoje). **Nenhum campo de instituição/clínica.**

---

## 2. Servidor

| Item | Valor |
|------|-------|
| Endereço (IP) | **`104.236.203.40`** |
| Hostname (DNS) | `pacs.marica.automais.cloud` |
| Porta DICOM | **`11112`** (sem TLS — o CardioNet não tem TLS) |

> Use o **IP** se a rede do CDT não resolver o DNS público.

---

## 3. Tela "Configuração DICOM" do CardioNet

| Campo no CardioNet | Valor | Observação |
|---|---|---|
| **Cliente AET** | **`CARDIO-CDT`** | o padrão de fábrica é `CARDIOS` — **trocar**. Maiúsculas, exatamente assim |
| **Servidor AEC** | **`WORK-CARDIO-CDT`** | maiúsculas, exatamente assim |
| **IP Servidor** | `104.236.203.40` | |
| **Porta Servidor** | `11112` | a mesma porta das imagens dos outros aparelhos — o que muda é o AE |

Depois de preencher, **"Testar Conexão"** tem que dar sucesso (é um C-ECHO).

### Parâmetros de busca

| Campo | Valor recomendado | Por quê |
|---|---|---|
| **Modalidade** | **`ECG`** (ou em branco) | todo exame desta worklist sai com Modality `ECG` — Holter **e** MAPA. Qualquer outro valor (`HD`, `OT`, `HOLTER`…) devolve a lista **vazia** |
| **Nome da estação** | **`CARDIO-CDT`** (ou em branco) | é o Scheduled Station AE Title dos nossos itens |
| **Período** | **`7`** dias | ver §5 — o período segura a lista enxuta |
| ID / Nome / Accession | em branco | são para busca pontual |

> **O `WORK-CARDIO-CDT` já entrega só os exames deste aparelho**, mesmo sem filtro nenhum: o servidor
> recorta pelo Worklist Label (ver [`pacs.md`](./pacs.md) §8). Os filtros acima são defesa em
> profundidade.

---

## 4. Resumo (cola rápida)

```
CardioNet Client DICOM → Criar novo exame → fonte "DICOM" → Configurações

  Cliente AET:     CARDIO-CDT
  Servidor AEC:    WORK-CARDIO-CDT
  IP Servidor:     104.236.203.40
  Porta Servidor:  11112

  Busca:  Modalidade = ECG   Nome da estação = CARDIO-CDT   Período = 7
```

---

## 5. Pontos de atenção

- **Holter × MAPA.** O CardioNet pede a descrição do exame (`0032,1060`), que vai com o nome do
  procedimento; mas quem escolhe "Holter" ou "MAPA" no programa é o operador, ao criar o exame. Se o
  mesmo paciente tiver os dois agendados, aparecem duas linhas com nº de pedido (Accession) diferente.
- **Médico solicitante e "clínica".** Ligam-se no cadastro do equipamento `CARDIO-CDT` (Exames de
  Imagem → Equipamentos → "O que vai na worklist deste aparelho"): médico na `0008,0090`, que o
  CardioNet pede; unidade com origem **executante**. Como o CardioNet não pede campo de instituição,
  a "clínica" pode não vir da worklist — se não aparecer, é cadastro local do programa.
- **O item não sai da lista sozinho.** Nos outros aparelhos, o item some quando a imagem chega ao
  PACS. O CardioNet não manda nada ao PACS, então o Holter feito continua na lista. Por isso o
  filtro de **Período**. Sem ele, a lista cresce até o teto de 1000 itens do CardioNet. (Pendência
  do nosso lado: fechar o item de quem não manda imagem — ver `pacs.md` §8, "Ciclo de vida".)
- **O exame leva o nosso nº do pedido e o Study UID** (coluna IOD da conformidade). Se um dia o
  laudo voltar como DICOM, é por eles que a plataforma casa o laudo com o pedido.

---

## 6. Rede

Liberar no firewall do CDT a **saída** TCP para `104.236.203.40:11112`. O CardioNet é sempre quem
inicia a conexão — o servidor não precisa alcançar o computador do CDT.

---

## 7. Teste

1. **Testar Conexão** na tela de configuração → sucesso.
2. **Procurar...** → aparecem os Holter/MAPA autorizados na recepção do CDT para os últimos 7 dias.
   Lista vazia com conexão OK costuma ser: (a) nenhum exame autorizado no período, ou (b) filtro de
   Modalidade diferente de `ECG`. Confirmar com a equipe Automais, que vê no servidor o que o
   CardioNet pediu de fato.
