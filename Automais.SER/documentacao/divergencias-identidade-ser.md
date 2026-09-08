# Divergências de identidade no SER — achado de implantação

**Data:** 07/09/2026 · **Origem:** resolução em massa CPF → CNS contra o SER, fase de implantação
do histórico do SISREG.

> Este documento **não contém dado pessoal**. Os casos, com nome e CPF, estão em
> `Automais.SER/capturas/divergencias_identidade.csv`, que é gitignored.

---

## O que foi encontrado

Ao perguntar ao SER o cadastro de um CPF, **ele às vezes devolve a ficha de outra pessoa**.

Em 20.636 consultas, **10 casos** (0,05%). Todos foram retidos — nenhum entrou como resolvido.

| Veredito | Casos | Decidido por |
|---|---|---|
| O CPF que pedimos é válido, o devolvido não é | 8 | dígito verificador |
| São **pessoas diferentes** na Receita | 2 | Hub do Desenvolvedor |

## Por que isso é grave

O caso mais ilustrativo: pedimos o CPF de uma paciente nascida em 2007 e o SER devolveu a ficha
de outra pessoa **com o mesmo sobrenome, nascida em 1987** — pelo padrão dos dados, a mãe.

Não é um erro que salta aos olhos. É um cadastro coerente, de alguém da mesma família, que teria
entrado no hub com aparência perfeitamente normal. Sem conferência automática, ninguém
desconfiaria — e o dado errado só apareceria meses depois, quando alguém fosse atendido com a
identidade de outro.

## Como foi detectado

Uma guarda no resolvedor: **o CPF devolvido pelo painel tem de ser o CPF perguntado**.

Isso não estava lá no começo. Foi acrescentado depois de a documentação do
`PreCargaCadastroSerService` (que já roda em produção) nomear exatamente este risco:

> *"nenhuma recebeu o paciente da outra, que era o risco que mataria a ideia — velocidade que
> troca identidade de paciente é o pior defeito possível aqui"*

O isolamento por sessão (cookie jar próprio por worker) estava correto e **não houve cruzamento
entre sessões** — verificado: nenhum dos CPFs devolvidos havia sido consultado antes na mesma
corrida. A troca vem da fonte, não da nossa paralelização.

**Falso positivo conhecido:** ficha sem CPF preenchido, ou com `000.000.000-00`, não é divergência
— são 25 em 1.936 na primeira medição. A guarda só acusa CPF real e diferente.

## Cascata de desempate

Do mais barato ao mais caro. A ordem importa: o desempate barato resolveu 80% dos casos.

1. **Dígito verificador** — local, custo zero, ilimitado. CPF com DV inválido não é de ninguém.
2. **Receita Federal** (`nome_cpf/` do Hub do Desenvolvedor) — 10 créditos, só quando os dois CPFs
   são aritmeticamente válidos e a conta não decide.

Gasto até aqui: **40 créditos de 10.500**.

## O que precisa de decisão

1. **Os 8 casos com CPF inválido vindo do SER** — o cadastro do SER está errado nesses pacientes.
   Vale reportar ao gestor do SER?
2. **Os 2 casos de pessoa trocada** — são os mais sérios. Se o SER devolve ficha de outra pessoa
   para um CPF válido, qualquer sistema que consulte o SER por CPF está exposto ao mesmo erro,
   inclusive o nosso motor de produção.
3. **Nenhum dos 10 pode ser conciliado automaticamente.** Precisam de tratamento manual ou de
   regra explícita antes da importação do histórico.

## Onde isso vive

- `resolver_identidades.py` — a guarda (`ALERTA_IDENTIDADE`) e a retenção da evidência
- `relatorio_divergencias.py` — gera o CSV e fecha o veredito de cada caso
- `capturas/divergencias_identidade.csv` — os casos com PII (gitignored)

---

## PENDÊNCIA — levar a guarda para a produção

**Ainda não foi feito.** Registrado em 07/09/2026 para não se perder.

### Onde

`SMSMais.server/src/SMSMais.Core/Integracoes/Cadastro/CadastroPacienteRoteador.cs`

É o ponto **único** por onde a produção consulta cadastro de paciente: roteia para SISREG ou SER,
por CNS ou por CPF, conforme `sisreg_configuracao.fonte_cadastro_paciente`. Tudo passa por ali.

### O quê

> **O que voltou tem de trazer a chave que foi perguntada.** Perguntou por CNS, o CNS de volta tem
> de ser o mesmo; perguntou por CPF, idem. Quando não for, lançar `NaoEncontradoException` com
> trilha — nunca aceitar em silêncio.

O roteador é melhor lugar que o `SerCadastroPacienteService` por dois motivos: cobre **as duas
fontes** (o SISREG pode ter o mesmo defeito e nunca foi testado) e **as duas chaves**.

**Exceção conhecida:** CPF vazio ou `000.000.000-00` não é divergência — é ficha sem CPF preenchido
na origem (25 em 1.936 na primeira medição). Só CPF real e diferente acusa.

### Por que importa mais do que 13 casos sugerem

O mecanismo não é "a fonte erra a busca". É **ficha com nome de uma pessoa e documento de outra**:
num dos casos, a ficha exibe o nome de uma paciente de 2007 com o CPF e o nascimento de outra
pessoa de 1987, mesmo sobrenome. **Nome não pega. Nascimento não pega. Só a chave pega.**

A varredura diária consulta o CADSUS a cada paciente novo, hoje sem essa conferência. A 0,04%, com
o volume diário atual, é questão de tempo até um cadastro trocado entrar — e entra com aparência
perfeitamente normal.

### Aprendizado irmão: CNS provisório × definitivo

O SISREG traz o CNS **provisório** e o SER o **definitivo**, para a mesma pessoa. Casar por CNS
puro erra nesses casos. A conciliação — e a importação — precisam tratar os dois como a mesma
pessoa. Não há sinal legível na tela do SER para isso: o balão de aviso e o destaque amarelo do
campo estão sempre presentes no HTML. A divergência só se apura comparando os dois valores.

### Quando

**Depois** de a implantação fechar. Misturar mudança de produção com carga em andamento foi o que
causou os incidentes de 06/09 — e esta mudança merece teste próprio, como as outras.
