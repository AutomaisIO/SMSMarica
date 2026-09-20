import { CalendarCheck2 } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import { Passos } from '@/features/manual/components/Passos';
import { FluxoConfirmacao } from '@/features/manual/simulacoes/FluxoConfirmacao';
import { SimulacaoConfirmacoes } from '@/features/manual/simulacoes/SimulacaoConfirmacoes';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Primeiro artigo do manual — e o modelo dos próximos.
 *
 * A ordem das seções segue a dúvida de quem está aprendendo, não a arrumação da tela: de onde vem
 * essa lista → o que são as filas → como se lê um card → de quem é a ficha → agora faça → os quatro
 * desfechos → os casos chatos → quem pode o quê.
 */
export const artigoConfirmacoes: Artigo = {
  slug: 'confirmacoes',
  titulo: 'Confirmações de agendamento',
  resumo:
    'A fila de quem tem consulta ou exame marcado e ainda não confirmou. Como pegar a ficha, ligar e dar o desfecho sem pisar no trabalho da colega.',
  grupo: 'atendimento',
  icone: CalendarCheck2,
  rota: '/app/confirmacoes',
  publico: 'Quem confirma agendamento por telefone, na unidade ou na regulação',
  atualizadoEm: '2026-09-20',
  palavrasChave: [
    'confirmação',
    'confirmar presença',
    'agendamento',
    'sisreg',
    'whatsapp',
    'faltas',
    'absenteísmo',
    'cancelar',
    'contato errado',
    'número negado',
    'pendente',
    'telefone comprometido',
    'fila de cancelamento',
    'pedido de cancelamento',
    'atendente',
    'fila',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve esta tela',
      busca: 'o que é confirmações de onde vem a lista importação sisreg lembrete',
      conteudo: (
        <div className="space-y-4">
          <P>
            Cada consulta ou exame marcado e não avisado é uma vaga em risco: o paciente esquece, não aparece, e a vaga
            se perde num dia em que outra pessoa esperava por ela. Esta tela junta, num lugar só, todos os agendamentos
            futuros que ainda precisam de uma resposta — e o que já foi respondido, para quem quiser conferir.
          </P>
          <P>
            Ela <strong>não marca nem remarca</strong> nada. Quem agenda continua sendo o SISREG. Aqui se trabalha o que
            já está agendado: avisar, confirmar, cancelar quando a pessoa avisa que não vai.
          </P>
          <FluxoConfirmacao />
          <Callout tipo="dica" titulo="A conta que importa">
            Confirmar não é burocracia: é a diferença entre remanejar a vaga com antecedência e descobrir a falta no dia.
            Um cancelamento avisado com três dias vira atendimento para outra pessoa.
          </Callout>
        </div>
      ),
    },
    {
      id: 'abas',
      titulo: 'As seis filas',
      busca: 'abas não confirmados confirmados contato errado pendentes telefone comprometido cancelamento significado',
      conteudo: (
        <div className="space-y-4">
          <P>
            As abas não são categorias que alguém preenche à mão: elas são <strong>deduzidas</strong> do que já
            aconteceu com cada agendamento. Uma ficha muda de aba sozinha quando o fato muda — não existe botão de
            "mover para".
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <AbaRef>Não confirmados</AbaRef>,
                descricao:
                  'Ninguém respondeu ainda, por nenhum caminho. É a fila de trabalho: aparece do agendamento mais próximo para o mais distante, porque o de amanhã é o que não pode esperar.',
              },
              {
                termo: <AbaRef>Confirmados</AbaRef>,
                descricao:
                  'Quem já disse que vem — pelo link, pelo botão do WhatsApp, pelo robô, pelo app, na recepção ou pela mão de uma atendente. Serve de conferência, e ainda dá para cancelar daqui se a pessoa mudar de ideia.',
              },
              {
                termo: <AbaRef>Contato errado</AbaRef>,
                descricao:
                  'Quem atendeu o telefone disse que não é o paciente. O número fica marcado como negado e nada automático sai mais para ele — até alguém corrigir e verificar o telefone.',
              },
              {
                termo: <AbaRef>Pendentes</AbaRef>,
                descricao:
                  'Fichas estacionadas por uma atendente com um motivo ("não atendeu", "ligar depois"). É a sua lista de retorno, não um limbo: ninguém mais vai ligar para essas enquanto estiverem aqui.',
              },
              {
                termo: <AbaRef>Cancelamento</AbaRef>,
                descricao:
                  'Alguém pediu para cancelar e ninguém tratou ainda. Vem de qualquer caminho — botão do WhatsApp, robô, app, recepção: o que importa é que a pessoa disse que não vem e a vaga continua de pé. O pedido fica com as palavras dela, e dá para abrir a conversa inteira antes de decidir.',
              },
              {
                termo: <AbaRef>Telefone comprometido</AbaRef>,
                descricao:
                  'O canal não alcança a pessoa: ou o cadastro não tem celular, ou o número não está no WhatsApp. Não é contato errado — o número pode ser do paciente e atender ligação. Aqui o caminho é telefonar e arrumar o cadastro.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="O que NÃO entra em nenhuma fila">
            Agendamento de data passada, agendamento já cancelado e (quando a instalação está configurada assim) o que
            não veio do SISREG. Se você procura alguém e não acha, confira primeiro a data.
          </Callout>
        </div>
      ),
    },
    {
      id: 'card',
      titulo: 'Como ler um card',
      busca: 'selo badge enviada entregue lida falhou na fila número negado verificado respondeu no zap',
      conteudo: (
        <div className="space-y-4">
          <P>
            Cada card responde três perguntas antes de você ligar: <strong>quem é</strong> (nome, CPF, telefone),{' '}
            <strong>o que está marcado</strong> (procedimento, unidade, data — a de hoje aparece destacada) e{' '}
            <strong>o que já foi tentado</strong> (os selos à direita).
          </P>
          <Sub>Os selos do envio automático</Sub>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef cor="gray">Na fila</SeloRef>, descricao: 'A mensagem está para sair. Fora da janela de horário, espera.' },
              { termo: <SeloRef cor="gray">Enviada ✓</SeloRef>, descricao: 'Saiu daqui e foi aceita pelo WhatsApp.' },
              { termo: <SeloRef cor="gray">Entregue ✓✓</SeloRef>, descricao: 'Chegou no aparelho do paciente.' },
              { termo: <SeloRef cor="sucesso">Lida ✓✓</SeloRef>, descricao: 'A pessoa abriu a conversa. Leu e não respondeu é um bom motivo para ligar.' },
              { termo: <SeloRef cor="erro">Falhou</SeloRef>, descricao: 'A entrega foi recusada. Passe o mouse no selo: a dica diz o motivo.' },
              { termo: <SeloRef cor="erro">Número negado</SeloRef>, descricao: 'Alguém já disse que aquele telefone não é do paciente.' },
              { termo: <SeloRef cor="gray">Atendida por pessoa</SeloRef>, descricao: 'Uma atendente assumiu a ficha, então o automático foi encerrado.' },
            ]}
          />
          <Sub>As marcas ao lado do telefone</Sub>
          <Lista>
            <Item>
              <strong>✔ verificado</strong> — o paciente já provou que o número é dele (leu um código enviado para o
              aparelho). É o que libera o envio de coisa sensível.
            </Item>
            <Item>
              <strong>respondeu no zap</strong> — ele escreveu nas últimas 24 horas; a conversa está aberta e dá para
              falar por lá sem depender de modelo aprovado.
            </Item>
            <Item>
              <strong>sem celular</strong> / <strong>não é WhatsApp</strong> — por que o automático não alcança essa
              pessoa. Aparece com o número de tentativas perdidas quando já houve mais de uma.
            </Item>
          </Lista>
        </div>
      ),
    },
    {
      id: 'posse',
      titulo: 'A ficha tem dono: Atender, Assumir, Transferir, Liberar',
      busca: 'posse atender assumir transferir liberar duas atendentes ligam para o mesmo paciente',
      conteudo: (
        <div className="space-y-4">
          <P>
            O problema que essa parte resolve é velho e conhecido: duas pessoas ligando para o mesmo paciente, e um
            terceiro que ninguém ligou. Por isso a ficha tem dono enquanto está sendo trabalhada.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <BotaoRef>Atender</BotaoRef>,
                descricao:
                  'Pega a ficha para você. O card ganha borda destacada e, para as colegas, fica esmaecido com o seu nome e a hora em que você começou.',
              },
              {
                termo: <BotaoRef variante="outline">Assumir atendimento</BotaoRef>,
                descricao:
                  'Tira a ficha de quem está com ela. Existe porque a vida acontece (alguém saiu, o turno virou) — e fica registrado que foi você.',
              },
              {
                termo: <BotaoRef variante="ghost">Transferir</BotaoRef>,
                descricao: 'Entrega a ficha a uma colega específica, com um recado. Use quando o caso é dela, não seu.',
              },
              {
                termo: <BotaoRef variante="ghost">Liberar</BotaoRef>,
                descricao: 'Devolve a ficha para a fila sem desfecho. É o "não era para eu ter pegado esta".',
              },
              {
                termo: <BotaoRef variante="outline">Retomar</BotaoRef>,
                descricao: 'Volta a atender uma ficha que estava estacionada em Pendentes ou em Contato errado.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Pessoa entrou, robô sai">
            No instante em que você pega a ficha, o sistema <strong>encerra o envio automático daquele agendamento</strong>{' '}
            (o selo vira <SeloRef cor="gray">Atendida por pessoa</SeloRef>). Ninguém vai receber uma mensagem automática
            enquanto você está falando com ele — nem depois, para aquele agendamento.
          </Callout>
        </div>
      ),
    },
    {
      id: 'simulacao',
      titulo: 'Agora faça você: simulação',
      busca: 'simulação treinar praticar exemplo clique aqui para simular',
      conteudo: (
        <div className="space-y-4">
          <P>
            Abaixo está a tela de verdade, com pacientes de mentira. Siga o roteiro para fazer o caminho completo de uma
            confirmação — e depois saia do roteiro e experimente o resto. Nada aqui manda mensagem, cancela vaga ou toca
            em cadastro: é tudo dentro do seu navegador.
          </P>
          <SimulacaoConfirmacoes />
        </div>
      ),
    },
    {
      id: 'desfechos',
      titulo: 'Os quatro desfechos',
      busca: 'confirmar presença enviar para pendente contato errado cancelar agendamento motivo meio',
      conteudo: (
        <div className="space-y-4">
          <P>Toda ficha que você pega termina em um destes quatro — ou volta para a fila pelo Liberar.</P>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    <BotaoRef>Confirmar</BotaoRef> — ela vai comparecer
                  </>
                ),
                detalhe:
                  'Escolha como falou (ligação, WhatsApp, presencial, outro) e, se quiser, deixe uma observação ("confirmou com a filha"). A ficha vai para Confirmados com o seu nome.',
              },
              {
                titulo: (
                  <>
                    <BotaoRef variante="outline">Enviar para pendente</BotaoRef> — não deu para falar agora
                  </>
                ),
                detalhe:
                  'Motivo obrigatório, com sugestões prontas: não atendeu, caixa postal, pediu para ligar depois. A ficha sai da fila geral e fica na aba Pendentes, esperando o seu retorno.',
              },
              {
                titulo: (
                  <>
                    <BotaoRef variante="outline">Contato errado</BotaoRef> — não é o paciente
                  </>
                ),
                detalhe:
                  'Marca o número como negado para essa pessoa e retém o que sairia para ele. Use só quando quem atendeu disse que não conhece o paciente — não quando simplesmente não atenderam.',
              },
              {
                titulo: (
                  <>
                    <BotaoRef variante="danger">Cancelar agendamento</BotaoRef> — ela não vai
                  </>
                ),
                detalhe:
                  'Motivo obrigatório. A vaga volta a contar aqui na hora e o paciente sai das filas — mas ainda falta o SISREG (leia a seção seguinte).',
              },
            ]}
          />
          <Callout tipo="dica" titulo="Por que o sistema insiste no motivo e no meio">
            Não é para controlar você. É o que responde, semanas depois, por que aquela vaga foi cancelada e de onde saiu
            a confirmação de quem faltou mesmo assim.
          </Callout>
        </div>
      ),
    },
    {
      id: 'cancelamento-sisreg',
      titulo: 'Cancelar: o que este sistema faz e o que falta fazer no SISREG',
      busca: 'cancelar sisreg extensão navegador conciliação vaga',
      conteudo: (
        <div className="space-y-4">
          <P>Este é o ponto que mais gera confusão, então vale ler com calma. Ao cancelar aqui, na mesma hora:</P>
          <Lista>
            <Item>o agendamento é cancelado neste sistema e a vaga volta a contar;</Item>
            <Item>o paciente sai de todas as filas de confirmação;</Item>
            <Item>o que ainda ia ser enviado para ele sobre esse agendamento é encerrado;</Item>
            <Item>os links de confirmação que ele já recebeu deixam de funcionar.</Item>
          </Lista>
          <Callout tipo="atencao" titulo="O SISREG não é cancelado sozinho">
            Hoje o cancelamento <strong>não é escrito no SISREG</strong>. Depois de cancelar aqui, cancele lá pelo
            navegador, como você sempre fez. Com a extensão instalada, o sistema percebe o cancelamento no SISREG e
            concilia sozinho — você não precisa voltar aqui para avisar. O aviso amarelo que aparece na tela é exatamente
            esse lembrete.
          </Callout>
        </div>
      ),
    },
    {
      id: 'fila-de-cancelamento',
      titulo: 'A fila de Cancelamento: por que ninguém cancela por texto',
      busca: 'cancelamento fila robô whatsapp app pedido motivo conversa contexto triagem',
      conteudo: (
        <div className="space-y-4">
          <P>
            Quando alguém diz que não vai — pelo botão do WhatsApp, escrevendo para o robô, pelo app ou na recepção —
            o sistema <strong>registra o pedido</strong> e põe a ficha nesta fila. Ele não cancela sozinho, de
            propósito.
          </P>
          <Callout tipo="atencao" titulo="Por que o sistema não cancela sozinho">
            Pedido de cancelamento chega em texto livre, e texto livre engana. Na varredura das conversas reais, no meio
            dos <q>quero cancelar</q> vinham <q>não quero cancelar</q>, <q>não pretendo cancelar nenhum exame</q> e
            <q>qual o motivo do cancelamento?</q>. Cancelar por interpretação tiraria a vaga de quem queria ir.
          </Callout>
          <P>
            Por isso cada ficha mostra <strong>as palavras da própria pessoa</strong> e um link para abrir a conversa
            inteira. Leia antes de clicar: o motivo já vem preenchido no cancelamento, com o que ela escreveu, e é isso
            que vai responder, semanas depois, por que aquela vaga caiu.
          </P>
          <Callout tipo="dica" titulo="A fila é de vagas, não de mensagens">
            Cada ficha parada aqui é um horário que continua bloqueado para alguém que não vai aparecer. Trabalhar esta
            fila é o que devolve a vaga para a fila de espera a tempo.
          </Callout>
        </div>
      ),
    },
    {
      id: 'telefone-comprometido',
      titulo: 'Quando o WhatsApp não alcança a pessoa',
      busca: 'sem celular não é whatsapp 131026 corrigir contato código otp verificar telefone',
      conteudo: (
        <div className="space-y-4">
          <P>
            A aba <AbaRef>Telefone comprometido</AbaRef> existe porque essas fichas não podem ficar misturadas com as
            outras: ali, mandar mensagem não adianta — o caminho é ligar, ou pegar o número na próxima passagem pela
            unidade. O painel no topo da aba diz quantas estão paradas por cada motivo e, ao lado, quantas{' '}
            <strong>pessoas</strong> são — porque cada pessoa é um telefonema, mesmo que tenha três exames marcados.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Sem celular no cadastro',
                descricao: 'Não há número para tentar. Só se resolve com a pessoa: peça o celular e cadastre.',
              },
              {
                termo: 'Número não é WhatsApp',
                descricao:
                  'O WhatsApp recusou a entrega dizendo que aquele número não existe por lá. O número pode estar certo e atender ligação — é comum em telefone fixo e em linha só de chamada.',
              },
            ]}
          />
          <Sub>Corrigir o contato (e por que tem código)</Sub>
          <P>
            Na aba <AbaRef>Contato errado</AbaRef>, o botão <BotaoRef variante="outline">Corrigir contato</BotaoRef> abre
            o fluxo de verificação: você digita o número novo, o sistema manda um código de 6 dígitos para ele e o
            paciente lê o código para você. Só então o número entra como <strong>verificado</strong>, a pendência é
            fechada e o agendamento volta para a fila automática.
          </P>
          <Callout tipo="lgpd" titulo="O código não é frescura">
            É o que garante que a informação de saúde vai para o dono do telefone, e não para quem digitou errado três
            meses atrás. Sem essa prova, o sistema não volta a mandar nada para o número.
          </Callout>
        </div>
      ),
    },
    {
      id: 'filtros',
      titulo: 'Achar uma ficha no meio de milhares',
      busca: 'busca filtro unidade envio automático paginação nome cpf cns número sisreg',
      conteudo: (
        <div className="space-y-4">
          <Lista>
            <Item>
              <strong>Campo de busca</strong> — nome, CPF, CNS ou o número da solicitação no SISREG. Pode digitar
              parcial; a lista se refaz sozinha enquanto você digita.
            </Item>
            <Item>
              <strong>Unidade</strong> — filtra pela unidade que vai <em>executar</em> o procedimento. É por ela que se
              divide o trabalho entre equipes.
            </Item>
            <Item>
              <strong>Envio automático</strong> — mostra só quem está num certo estado de envio. O par mais útil no dia a
              dia: <SeloRef cor="sucesso">Lida</SeloRef> sem resposta (leu e ignorou — ligar) e{' '}
              <SeloRef cor="erro">Falhou</SeloRef> (a mensagem nem chegou).
            </Item>
            <Item>
              <strong>Quantidade por página</strong> — 25, 50 ou 100, no rodapé. Em fila grande, 100 rende mais.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="Divida a fila com as colegas">
            Combine por unidade ou por dia de agendamento e cada uma filtra o seu pedaço. Como a ficha tem dono, mesmo se
            duas caírem na mesma lista ninguém liga em duplicidade.
          </Callout>
        </div>
      ),
    },
    {
      id: 'permissoes',
      titulo: 'Quem pode o quê',
      busca: 'permissão consulta edição exclusão perfil não aparece botão cancelar',
      conteudo: (
        <div className="space-y-3">
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Vê as filas e os cards, sem nenhum botão de ação. Serve para acompanhar.' },
              {
                termo: 'Edição',
                descricao: 'Atende, assume, transfere, libera, confirma, envia para pendente e registra contato errado.',
              },
              { termo: 'Exclusão', descricao: 'Além de tudo acima, pode cancelar agendamento.' },
            ]}
          />
          <P>
            Se um botão que este manual cita não aparece para você, quase sempre é permissão — não é defeito. Peça ao
            responsável pelo seu perfil, ou abra um ticket no menu Suporte.
          </P>
        </div>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas que sempre aparecem',
      busca: 'faq dúvidas frequentes por que sumiu não aparece paciente lista',
      conteudo: (
        <div className="space-y-3">
          <ListaDefinicoes
            itens={[
              {
                termo: 'Confirmei e a ficha sumiu da lista',
                descricao: 'Ela mudou de fila: está na aba Confirmados. Nenhuma ação apaga ficha nesta tela.',
              },
              {
                termo: 'O paciente confirmou pelo link, ainda preciso ligar?',
                descricao: 'Não. Ele já está em Confirmados, com o canal registrado. A fila de trabalho é a de Não confirmados.',
              },
              {
                termo: 'Marquei contato errado por engano',
                descricao:
                  'Vá à aba Contato errado, use Corrigir contato e verifique o número certo — isso reabre o caminho automático. Se o número estava certo, informe o mesmo número na verificação.',
              },
              {
                termo: 'Cancelei aqui e continua marcado no SISREG',
                descricao: 'É o esperado nesta fase: o cancelamento no SISREG ainda é feito por você, pelo navegador.',
              },
              {
                termo: 'A colega está com a ficha e ela saiu do plantão',
                descricao: 'Use Assumir atendimento. Fica registrado que a ficha mudou de mão e quando.',
              },
              {
                termo: 'Quero ver o que já foi enviado, os lotes e as regras',
                descricao:
                  'Isso é a tela Mensageria (menu Atendimento). Confirmações é o atendimento pessoa a pessoa; Mensageria é a gestão dos envios.',
              },
            ]}
          />
        </div>
      ),
    },
  ],
};
