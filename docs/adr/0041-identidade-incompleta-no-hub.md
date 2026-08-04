# ADR-0041 — Paciente sem CPF entra no hub, marcado; a incerteza é declarada, não escondida

**Status:** aceito · **Data:** 2026-08-03 · **Adendo 04/08:** a régua é CPF **VÁLIDO**
(dígito verificador), não "11 dígitos" — a auditoria adversarial mostrou que `00000000000`
(preenchimento clássico de campo obrigatório) passaria como chave nacional e **fundiria duas
pessoas** num único Patient. CPF inválido = identidade incompleta: entra marcado, pela chave
local, e não vira identifier de CPF.
**Implantação:** **em produção desde 03/08/2026** (Salux) — conector Klinikos com a mesma regra,
ainda não ligado.

## Contexto

O hub deduplica cidadão por chave nacional: CPF, e secundariamente CNS
([ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md)). Isso funciona bem — até o dia em
que a origem não tem nenhuma das duas.

Medido nas bases reais:

| Base | Pacientes | Sem CPF **e** sem CNS | % |
|---|---:|---:|---:|
| Salux (HMCML) | 370.226 | **63.324** | 17,1% |
| Klinikos UPA Maricá | 83.878 | **14.152** | 16,9% |

Não é lixo de cadastro. No Salux, esses 63.324 respondem por **127.145 boletins** e **8.216
internações — das quais 6.697 são recém-nascidos** (o HMCML é maternidade). Na UPA, **todos os
14.152 têm pelo menos um boletim**: são pessoas que foram atendidas.

Tentativas de resgate na UPA, medidas: o CNS capturado no boletim recupera **863** (6,1%); o CNS
provisório, **859** — e os dois conjuntos quase coincidem. **Não existe resgate em massa.** O
dado não foi coletado no atendimento, e não há de onde tirá-lo.

Ou seja: um sexto do cidadão atendido na rede não tem identificador nacional na origem. Não é
uma exceção a tratar depois; é uma característica permanente do dado de urgência — recém-nascido
sem registro, indigente, paciente que chega inconsciente.

### As duas saídas ruins

**Deixar de fora.** Foi a decisão inicial, e é defensável: sem CPF não há como afirmar que o
"João da Silva" de uma base é o mesmo de outra, e uma dedup errada **funde dois prontuários** —
o pior desfecho possível num repositório clínico. Mas o preço é o hub afirmar, por omissão, que
aquele atendimento não existiu. Um recém-nascido que passou 12 dias internado simplesmente não
consta. Para um repositório longitudinal, silêncio e ausência são indistinguíveis.

**Deduplicar por nome + nascimento.** É o caminho mais curto para fundir dois pacientes
distintos. Homônimos com a mesma data de nascimento existem, e são mais comuns do que a
intuição sugere. Descartado.

## Decisão

**O paciente sem CPF entra no hub, identificado apenas pela chave interna da base de origem, e
marcado como de identidade incompleta.**

1. **Identifier local, nunca vazio.** O recurso carrega `urn:<pep>:paciente` com o código da
   origem prefixado pelo slug da base. **Não** carrega um `Identifier` de CPF com `value` em
   branco — o hub rejeita o recurso inteiro com 400.

2. **A marca é `meta.tag`**, não um campo nosso:
   `urn:smsmarica:qualidade|identidade-incompleta`. É o mecanismo do próprio FHIR para
   qualificar um registro sem sujar o dado clínico, e é buscável — `GET
   /fhir/Patient?_tag=urn:smsmarica:qualidade|identidade-incompleta` responde "quantos
   registros incertos existem" a qualquer momento, **sem contador paralelo para desincronizar**.

3. **Uma tag só para todo o hub.** Salux e Klinikos usam exatamente o mesmo system e code. A
   pergunta "quantos registros incertos temos?" tem de ter uma resposta, não uma por base.

4. **Fora do merge canônico.** Esses pacientes nunca entram no caminho de dedup por CPF. Cada
   base tem o seu; se a mesma pessoa aparecer em duas, serão dois recursos. **É o resultado
   correto**: é melhor ter dois registros que sabemos separados do que um que achamos unificado.

5. **A marca some sozinha.** No dia em que a origem ganhar o CPF, o paciente passa pelo caminho
   canônico e sobe sem a tag. Não há processo de limpeza a manter.

6. **Visível na operação.** O painel mostra o selo "sem CPF" ao lado do nome na busca de
   pacientes. Quem atende precisa saber que aquele cadastro não é confiável para cruzar
   informação — a incerteza tem de aparecer onde a decisão é tomada, não só num relatório.

7. **CNS provisório não vale.** Os 17.361 CNS provisórios do Klinikos **não** são identificador
   nacional e não viram `identifier` de system CNS. Ficam de fora até a operação dizer o que
   significam.

## Consequências

**A favor**

- O hub deixa de mentir por omissão: o atendimento existe, com o aviso de que a identidade não
  foi confirmada.
- A contagem de dados ruins é uma query, não uma estimativa. E é a mesma query para sempre.
- O risco de fusão indevida não aumenta — esses recursos ficam explicitamente fora do merge.

**Contra, e assumido**

- **O hub passa a conter duplicatas conhecidas.** A mesma pessoa atendida no Conde e na UPA sem
  documento vira dois Patients. Isso é deliberado; a alternativa é fundir sem base.
- Qualquer consumidor que conte pacientes distintos precisa saber da tag. Contagem populacional
  sobre o hub tem de excluí-los ou tratá-los à parte.
- A quantidade só cresce enquanto a origem não coletar documento. O número é um **indicador de
  qualidade da recepção**, não um bug a corrigir no hub — e é aí que ele deve ser cobrado.

**Não decidido aqui**

- O que fazer com os já existentes: se um dia houver identificação retroativa (por prontuário
  físico, por exemplo), o merge desses recursos precisa de desenho próprio — e de um caminho de
  desfazimento, porque merge errado em prontuário é dano difícil de reverter.
