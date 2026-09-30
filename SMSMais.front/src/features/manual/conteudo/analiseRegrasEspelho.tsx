import { ListChecks } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da análise automática das regras de elegibilidade sobre os pedidos que chegam do SER, do
 * SERNIT e do ESUS de São Gonçalo (ADR-0063 §4).
 *
 * Conferido no código: `shared/regulacao/analiseRegras` (BadgeAnaliseRegras, FiltroVeredito,
 * PainelAnaliseRegras, tipos), as filas e os detalhes de `features/ser`, `features/sernit` e
 * `features/esussg`, e no backend (`AnaliseRegrasEspelhoService`, `AnaliseRegrasEspelhoWorker` —
 * passada a cada 10 min, só pedidos em aberto, reanálise por hash —, enum `VereditoAnaliseRegras`
 * e as rotas `…/{id}/analise/reanalisar`).
 */
export const artigoAnaliseRegrasEspelho: Artigo = {
  slug: 'analise-regras-espelho',
  titulo: 'Análise automática das regras (SER, SERNIT e ESUS SG)',
  resumo:
    'O parecer que o sistema dá, sozinho, sobre cada pedido que chega do SER, do SERNIT e do ESUS São Gonçalo, com as mesmas regras de elegibilidade do assistente de Nova Solicitação.',
  grupo: 'regulacao',
  icone: ListChecks,
  publico: 'Quem regula ou acompanha as filas do SER, do SERNIT e do ESUS São Gonçalo',
  atualizadoEm: '2026-09-30',
  palavrasChave: [
    'análise',
    'análise das regras',
    'análise automática',
    'regras de elegibilidade',
    'elegibilidade',
    'regra',
    'parecer',
    'veredito',
    'bloqueado',
    'a conferir',
    'conferir',
    'com ressalva',
    'ressalva',
    'apto',
    'sem regras',
    'sem procedimento',
    'catálogo canônico',
    'pareamento',
    'pergunta',
    'documento',
    'idade',
    'sexo',
    'CPF',
    'CID',
    'reanalisar',
    'reanálise',
    'SER',
    'SERNIT',
    'ESUS',
    'ESUS SG',
    'São Gonçalo',
    'espelho',
    'fila',
    'Nova Solicitação',
    'assistente',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca:
        'para que serve pedido incluído direto no sistema externo nunca conferido mesmas regras assistente nova solicitação parecer ao lado nada escrito',
      conteudo: (
        <>
          <P>
            As regras de elegibilidade — idade mínima, sexo, CID exigido, documento obrigatório —
            sempre valeram no assistente de <strong>Nova Solicitação</strong>. Mas o pedido que alguém
            inclui <strong>direto</strong> no SER, no SERNIT ou no ESUS de São Gonçalo nunca passava
            por elas. A análise automática fecha esse buraco: cada pedido que chega por esses espelhos
            recebe um <strong>parecer</strong>, com as mesmas regras e o mesmo avaliador do
            assistente.
          </P>
          <Callout tipo="regra" titulo="É um parecer, não uma decisão">
            A análise não muda o pedido e não escreve nada no sistema externo. Ela fica ao lado do
            pedido, para quem regula olhar e decidir.
          </Callout>
        </>
      ),
    },
    {
      id: 'o-que-ela-sabe',
      titulo: 'Com o que a análise trabalha',
      busca: 'dados idade nascimento sexo cpf cid espelho pergunta documento não responde sozinho',
      conteudo: (
        <>
          <P>A análise usa só o que o espelho do pedido tem:</P>
          <Lista>
            <Item>a data de nascimento (para a idade) e o sexo;</Item>
            <Item>o CPF;</Item>
            <Item>o CID do pedido, quando o sistema de origem o informa.</Item>
          </Lista>
          <P>
            Regras que dependem de uma <strong>pergunta</strong> (“o paciente já fez tal exame?”) ou
            de um <strong>documento</strong> (laudo, exame anterior) não têm resposta aqui — isso é
            juízo de pessoa. Quando uma dessas regras pode travar o pedido, ele fica{' '}
            <strong>A conferir</strong>.
          </P>
        </>
      ),
    },
    {
      id: 'vereditos',
      titulo: 'Os vereditos',
      busca:
        'veredito bloqueado a conferir com ressalva apto sem regras sem procedimento cor vermelho âmbar amarelo verde cinza tracejado significado',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: <SeloRef cor="erro">Bloqueado</SeloRef>,
              descricao:
                'Uma regra bloqueia este sistema com o dado que o pedido já tem (idade, sexo, CPF, CID). Ex.: exame só a partir de 40 anos, paciente com 35.',
            },
            {
              termo: <SeloRef cor="alerta">A conferir</SeloRef>,
              descricao:
                'Uma regra que bloqueia depende de pergunta ou documento que só uma pessoa responde. A máquina não conclui — alguém precisa conferir.',
            },
            {
              termo: <SeloRef cor="alerta">Com ressalva</SeloRef>,
              descricao: 'Alguma regra fez ressalva. O pedido passa, marcado; quem regula decide.',
            },
            {
              termo: <SeloRef cor="sucesso">Apto</SeloRef>,
              descricao: 'Nenhuma regra bloqueou nem fez ressalva, e nada que trave ficou em aberto.',
            },
            {
              termo: <SeloRef>Sem regras</SeloRef>,
              descricao: 'O procedimento não tem regra ativa que valha para este sistema. Não é “aprovado” — é que não há régua.',
            },
            {
              termo: <SeloRef>Sem procedimento</SeloRef>,
              descricao:
                'O procedimento do pedido não está ligado a nenhum procedimento do catálogo canônico — sem isso, não há como saber quais regras valem. Resolve-se pareando o procedimento no catálogo.',
            },
          ]}
        />
      ),
    },
    {
      id: 'onde-aparece',
      titulo: 'Onde aparece',
      busca: 'fila coluna análise das regras chip filtro veredito detalhe painel regras avaliadas motivo perguntas documentos analisado em tooltip',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Na fila',
                descricao:
                  'A coluna “Análise das regras” mostra o veredito de cada pedido; passe o mouse para ler o motivo. Os chips acima da tabela e o campo “Análise das regras” filtram por veredito. No ESUS SG os chips mostram quantos pedidos em aberto há em cada veredito.',
              },
              {
                termo: 'No detalhe do pedido',
                descricao:
                  'O painel mostra o veredito, a frase que o decidiu, as contagens (bloqueios, ressalvas, perguntas e documentos em aberto), o procedimento do catálogo canônico, cada regra avaliada com o resultado (Atende, Bloqueia, Ressalva, Indefinido) e o motivo, as perguntas e os documentos pendentes, e quando foi analisado.',
              },
            ]}
          />
          <P>
            “Indefinido” numa regra quer dizer que falta dado para decidir — sem data de nascimento,
            sem CID, pergunta sem resposta.
          </P>
        </>
      ),
    },
    {
      id: 'quando-roda',
      titulo: 'Quando a análise roda (e o Reanalisar)',
      busca:
        'quando roda automático 10 minutos pedidos em aberto em fila pendentes regra editada reanálise automática hash reanalisar botão agora agendado fica guardada',
      conteudo: (
        <>
          <P>
            A análise passa sozinha a cada <strong>10 minutos</strong> nos pedidos{' '}
            <strong>em aberto</strong> (em fila ou pendentes) dos três espelhos. Ela só refaz o que
            mudou: se o pedido mudou, ou se <strong>as regras do procedimento mudaram</strong>, o
            pedido é reanalisado na passada seguinte — sem ninguém precisar pedir. Quando o pedido é
            agendado ou fecha, a última análise fica guardada.
          </P>
          <P>
            O botão <BotaoRef variante="outline">Reanalisar</BotaoRef>, no detalhe, refaz a análise
            daquele pedido na hora — útil logo depois de editar uma regra. Ele pede só a permissão de
            consulta do sistema, porque não escreve nada fora.
          </P>
        </>
      ),
    },
    {
      id: 'editar-regras',
      titulo: 'Onde as regras são editadas',
      busca: 'editar regras regulação regras de elegibilidade curadoria importação catálogo pareamento procedimento canônico configuração',
      conteudo: (
        <>
          <P>
            As regras moram em <strong>Regulação → Solicitações → Regras de elegibilidade</strong>
            (permissão Regulação — Configuração). É a mesma régua do assistente de Nova Solicitação:
            editou lá, vale aqui na passada seguinte.
          </P>
          <P>
            O veredito <strong>Sem procedimento</strong> se resolve no catálogo canônico: parear o
            procedimento do sistema externo com o procedimento canônico (Regulação → SER →
            Configuração, aba Catálogo de procedimentos). No ESUS SG, o botão “Sincronizar catálogo” da
            configuração traz os procedimentos novos para o catálogo.
          </P>
        </>
      ),
    },
  ],
};
