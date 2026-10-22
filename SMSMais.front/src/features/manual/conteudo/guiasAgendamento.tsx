import { FileText } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Guias dos agendamentos (ADR-0076). Conferido no código em 21/10/2026: no servidor,
 * `GuiasAgendamento/FilaGuiasService` (enfileirar, descartar, buscar uma por vez, idempotência pelo
 * acervo), `BaixadoresGuia` (SER/SERNIT: chave de autorização; ESUS: comprovante montado; SISREG:
 * ficha com a chave), `FilaGuiasConfiguracaoService` (ritmo, janela, impedimento, pausa) e
 * `FilaGuiasScheduler` (uma linha por sistema); `DocumentosPacienteService` (origem "Guia do
 * agendamento", sem editar/excluir). No painel, `shared/guias/FilaGuiasSecao` (nas quatro
 * configurações) e o quadro `ContatoComPaciente` (linha da guia). No app, o cartão do agendamento
 * ("Ver guia do agendamento") e Documentos.
 */
export const artigoGuiasAgendamento: Artigo = {
  slug: 'guias-agendamento',
  titulo: 'Guias dos agendamentos',
  resumo:
    'A guia de cada agendamento do SISREG, do SER, do SERNIT e do ESUS é buscada sozinha, uma vez, e fica no cadastro do paciente — na tela do pedido, na ficha (Agendamentos e Exames anexados) e no app.',
  grupo: 'regulacao',
  icone: FileText,
  publico: 'Quem regula, quem atende o paciente na unidade e quem cuida da configuração de cada sistema',
  atualizadoEm: '2026-10-22',
  palavrasChave: [
    'guia',
    'guia do agendamento',
    'comprovante',
    'comprovante de agendamento',
    'chave de autorização',
    'chave de confirmação',
    'segunda via',
    'via',
    'imprimir guia',
    'ficha do sisreg',
    'autorização de consulta',
    'fila de guias',
    'guias por hora',
    'janela',
    'pausa',
    'captcha',
    'ver guia',
    'documentos do paciente',
    'app do cidadão',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é a guia e de onde ela vem',
      busca: 'guia comprovante chave autorização ficha sisreg ser sernit esus pdf',
      conteudo: (
        <>
          <P>
            A guia é o papel que o paciente apresenta no local do atendimento. Até aqui ela só existia no
            sistema de regulação: alguém precisava entrar, emitir e imprimir. Agora a plataforma busca a guia
            de todo agendamento <strong>sozinha, uma vez</strong>, e guarda no cadastro do paciente.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'SER e SERNIT',
                descricao:
                  'O PDF "Autorização de Consulta/Exame Resumida", o mesmo do item "Emitir Chave de Autorização" da tela do pedido — com a chave de autorização e o preparo.',
              },
              {
                termo: 'ESUS de São Gonçalo',
                descricao:
                  'O ESUS não tem PDF: ele monta um cupom na hora de imprimir. A plataforma lê os mesmos dados do comprovante e monta o PDF aqui; o rodapé diz isso.',
              },
              {
                termo: 'SISREG',
                descricao:
                  'A ficha da solicitação — a mesma página que o botão Imprimir do SISREG imprime —, com a chave de confirmação em destaque. A mesma leitura guarda a chave na solicitação.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="A guia vai com a chave">
            A guia leva a chave (de autorização ou de confirmação), e o paciente vê a guia no app antes do dia
            do atendimento. Foi decisão da gestão: é o mesmo papel que ele receberia no balcão.
          </Callout>
        </>
      ),
    },
    {
      id: 'onde-aparece',
      titulo: 'Onde a guia aparece',
      busca: 'ver guia contato com o paciente exames anexados documentos app cartão agendamento ficha aba agendamentos coluna guia imprimir baixar pdf',
      conteudo: (
        <>
          <P>
            É um arquivo só, no cadastro do paciente. Todos os lugares abaixo abrem esse mesmo arquivo — não há
            cópia em lugar nenhum.
          </P>
          <Lista>
            <Item>
              <strong>Tela do pedido</strong> (detalhe do SER, do SERNIT, do ESUS e da solicitação da regulação):
              no quadro <em>Contato com o paciente</em>, a linha da guia com o botão <BotaoRef variante="outline">Ver guia</BotaoRef>.
              Enquanto ela não veio, a linha diz que está na fila; se não veio de jeito nenhum, diz por quê.
            </Item>
            <Item>
              <strong>Cadastro do paciente → Agendamentos</strong>: o ícone na coluna <em>Guia</em> da linha abre a
              guia com <BotaoRef variante="outline">Imprimir</BotaoRef> e <BotaoRef variante="outline">Baixar PDF</BotaoRef>.
              Basta a permissão de consultar pacientes. Na linha do SISREG, só aparece a guia da data em que o
              agendamento está — a de antes de uma remarcação não.
            </Item>
            <Item>
              <strong>Cadastro do paciente → Exames anexados</strong>: com a origem <em>Guia do agendamento</em>. Não
              dá para editar nem excluir — o pedido e o app apontam para ela.
            </Item>
            <Item>
              <strong>App do cidadão</strong>: o botão <em>Ver guia do agendamento</em> no cartão do próximo
              agendamento, e a guia na lista de Documentos.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'fila',
      titulo: 'A fila: ritmo, janela e por que parou',
      busca: 'fila guias por hora janela horário pausa parada impedimento configuração na fila trazidas não vieram',
      conteudo: (
        <>
          <P>
            Todo agendamento <strong>de amanhã em diante</strong> entra numa fila assim que a plataforma o vê. Cada sistema tem a sua fila, que anda
            sozinha, uma guia de cada vez, <strong>o atendimento mais perto primeiro</strong>. O ritmo é
            configurado na tela de configuração de cada sistema, no quadro <em>Guias dos agendamentos</em> — o
            mesmo quadro no SISREG, no SER, no SERNIT e no ESUS:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Buscar automaticamente', descricao: 'Liga ou desliga a fila do sistema.' },
              {
                termo: 'Guias por hora',
                descricao:
                  'Quantas guias por hora, no máximo. 60 por hora = uma por minuto. Começa em 60 (30 no SISREG).',
              },
              {
                termo: 'Das / Até',
                descricao:
                  'A janela do dia (horário de Brasília) em que a fila anda. Início igual ao fim é o dia inteiro; janela que vira o dia, como 22:00 até 06:00, vale.',
              },
            ]}
          />
          <P>
            O quadro mostra quantas estão <strong>na fila</strong>, quantas foram <strong>trazidas hoje</strong>,
            quantas <strong>não vieram</strong> (desistiu depois de 5 tentativas) e, quando a fila não está
            andando, <strong>por quê</strong>: desligada, fora da janela, a varredura do sistema rodando, outro
            processo do SISREG rodando, o limite de consultas da hora no fim ou uma pausa.
          </P>
          <Callout tipo="dica" titulo="Salvar tira a pausa">
            Se o SISREG pedir CAPTCHA, a fila do SISREG para por 24 horas — insistir só piora. Depois que alguém
            entrar no SISREG e resolver, salve o quadro: a fila volta na hora.
          </Callout>
        </>
      ),
    },
    {
      id: 'cada-sistema',
      titulo: 'O que cada sistema cobra por uma guia',
      busca: 'via segunda via terceira via comprovante impresso orçamento requisições captcha sessão varredura',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'SER e SERNIT',
                descricao:
                  'Cada guia gasta uma via no sistema (o menu passa de "2ª via" para "3ª via"). A plataforma emite uma vez só; se a unidade imprimir de novo no balcão, sai a via seguinte. A fila espera a varredura do sistema terminar — a sessão é uma só.',
              },
              {
                termo: 'ESUS',
                descricao:
                  'Só lê. Não marca "comprovante impresso" no ESUS: quem imprime lá continua vendo o pedido como não impresso.',
              },
              {
                termo: 'SISREG',
                descricao:
                  'Cada guia é uma leitura da ficha (1 a 2 consultas), do mesmo limite anti-robô dos outros processos do SISREG. A fila cede a eles e ao limite curto, e para 24 horas se o SISREG pedir CAPTCHA.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'remarcacao',
      titulo: 'Remarcação e desmarcação',
      busca: 'remarcado remarcação desmarcado cancelado guia antiga nova data descartada',
      conteudo: (
        <>
          <Lista>
            <Item>
              <strong>Remarcado</strong>: a data nova pede guia nova, que entra na fila. A guia da data antiga
              continua no cadastro do paciente, mas o pedido, a ficha e o app passam a mostrar a nova.
            </Item>
            <Item>
              <strong>Desmarcado antes de a guia vir</strong>: ela sai da fila sem ir ao sistema — no SER e no
              SERNIT, isso evita gastar uma via à toa.
            </Item>
            <Item>
              <strong>No dia do atendimento</strong>: a guia que não veio até a véspera sai da fila — no dia ela
              chega tarde, e o SER e o SERNIT já tiram o pedido de “Agendada” quando o paciente chega.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'não veio guia faltando apagar excluir guia errada paciente errado',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'A guia não aparece no pedido',
                descricao:
                  'Veja a linha da guia no quadro Contato com o paciente: ela diz se está na fila e quando tenta de novo. Se a fila do sistema estiver parada, o quadro da configuração diz o motivo.',
              },
              {
                termo: 'Posso apagar uma guia?',
                descricao:
                  'Não. Ela é do sincronismo, e o pedido e o app apontam para ela. Se o paciente do pedido está errado no sistema de regulação, o conserto é lá.',
              },
              {
                termo: 'Por que a via do SER subiu?',
                descricao:
                  'Porque a plataforma emitiu a guia — uma vez por agendamento. O número da via não diz mais quantas vezes o paciente pediu cópia.',
              },
            ]}
          />
        </>
      ),
    },
  ],
};
