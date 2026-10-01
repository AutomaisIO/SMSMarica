import { LifeBuoy } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Passos } from '@/features/manual/components/Passos';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import { SimulacaoAbrirTicket } from '@/features/manual/simulacoes/SimulacaoAbrirTicket';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Suporte → Meus Tickets (`/app/tickets`), o lado de quem abre o ticket.
 *
 * Conferido no código: `features/tickets` (MeusTicketsPage, AbrirTicketModal, AnexosInput com o
 * Ctrl+V, TicketDetalhePage, ConversaTicket, ModalTicketRespondido) e `Core/Tickets/TicketService`
 * (visibilidade Privado/PorUnidade/Publico, só o autor comenta e arquiva, abrir o ticket reconhece
 * a resposta). Limite de anexo: `MidiasService` (5 MB; PNG, JPEG, GIF, WEBP, SVG).
 * A gestão (triagem, resposta, nota interna) está em `ticketsGestao.tsx`.
 */
export const artigoTickets: Artigo = {
  slug: 'tickets',
  titulo: 'Meus Tickets: pedir ajuda ao suporte',
  resumo:
    'Como abrir um ticket (bug, mudança, sugestão ou dúvida), anexar um print colando com Ctrl+V, acompanhar a resposta da equipe e conversar no ticket.',
  grupo: 'suporte',
  icone: LifeBuoy,
  rota: '/app/tickets',
  publico: 'Todo mundo que usa o painel',
  atualizadoEm: '2026-10-01',
  palavrasChave: [
    'ticket',
    'chamado',
    'suporte',
    'abrir ticket',
    'abrir chamado',
    'bug',
    'erro',
    'mudança',
    'sugestão',
    'dúvida',
    'print',
    'captura de tela',
    'anexo',
    'anexar imagem',
    'colar',
    'Ctrl+V',
    'Win+Shift+S',
    'Print Screen',
    'área de transferência',
    'respondido',
    'reconhecer',
    'marcar como visto',
    'retorno da equipe',
    'arquivar',
    'meus tickets',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'ticket chamado suporte bug mudança sugestão dúvida tipo o que você quer fazer',
      conteudo: (
        <>
          <P>
            O ticket é o canal para falar com a equipe que mantém o sistema. Fica registrado, tem
            número (<strong>#42</strong>) e a resposta chega para você no próprio painel. Ao abrir,
            você escolhe o que quer:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Bug', descricao: 'Algo está com erro ou se comportando de forma errada.' },
              { termo: 'Mudança', descricao: 'Ajuste em algo que já existe.' },
              { termo: 'Sugestão', descricao: 'Ideia de melhoria ou funcionalidade nova.' },
              { termo: 'Dúvida', descricao: 'Dúvida sobre como usar o sistema.' },
            ]}
          />
          <P>
            Antes de abrir uma dúvida, vale procurar aqui no Manual: muitas telas têm um{' '}
            <strong>?</strong> ao lado do título que leva direto ao artigo delas.
          </P>
        </>
      ),
    },
    {
      id: 'abrir',
      titulo: 'Abrir um ticket',
      busca: 'abrir ticket título descrição o que fez o que esperava abrir outro ver ticket',
      conteudo: (
        <>
          <Passos
            itens={[
              { titulo: <>Em Suporte → Meus Tickets, clique em <BotaoRef>Abrir ticket</BotaoRef>.</> },
              { titulo: 'Escolha o tipo: Bug, Mudança, Sugestão ou Dúvida.' },
              {
                titulo: 'Escreva um título que resuma o caso em uma frase.',
                detalhe: 'É o que a equipe lê primeiro na lista. “Botão Salvar da ficha não responde” ajuda; “Erro” não.',
              },
              {
                titulo: 'Na descrição, conte o que você fez, o que esperava e o que aconteceu.',
                detalhe:
                  'Diga a tela, o paciente ou registro de exemplo (sem expor mais dados do que o necessário) e, se apareceu, o código ERRO-XXXXXX.',
              },
              { titulo: 'Anexe um print, se puder (veja a próxima seção).' },
              { titulo: <>Clique em <BotaoRef>Abrir ticket</BotaoRef>.</> },
            ]}
          />
          <P>
            O ticket nasce com status <SeloRef>Aberto</SeloRef>. Depois de abrir, a janela oferece{' '}
            <BotaoRef variante="ghost">Ver ticket</BotaoRef> e <BotaoRef variante="outline">Abrir outro</BotaoRef>.
          </P>
        </>
      ),
    },
    {
      id: 'anexar-print',
      titulo: 'Anexar um print: Ctrl+V',
      busca:
        'print captura de tela colar Ctrl+V Win+Shift+S Print Screen área de transferência anexar imagem recorte 5 MB PNG JPEG Word Excel',
      conteudo: (
        <>
          <P>
            Um print mostra em um segundo o que levaria um parágrafo para explicar. O jeito mais
            rápido é colar:
          </P>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Tire o print com <strong>Win+Shift+S</strong> e selecione a parte da tela que
                    interessa.
                  </>
                ),
                detalhe: 'A tecla Print Screen também serve, mas copia a tela inteira.',
              },
              {
                titulo: (
                  <>
                    Volte para o ticket e aperte <strong>Ctrl+V</strong>.
                  </>
                ),
                detalhe:
                  'Funciona em qualquer ponto do formulário, até com o cursor na descrição. O anexo aparece como miniatura logo abaixo.',
              },
            ]}
          />
          <P>
            Também dá para anexar um arquivo de imagem pelo botão{' '}
            <BotaoRef variante="outline">Anexar imagem</BotaoRef>, inclusive vários de uma vez. O{' '}
            <strong>×</strong> na miniatura remove o anexo antes de enviar. O mesmo vale na resposta
            dentro do ticket.
          </P>
          <Lista>
            <Item>Cada imagem pode ter até <strong>5 MB</strong>, em PNG, JPEG, GIF, WEBP ou SVG.</Item>
            <Item>O print colado ganha um nome com a data e a hora, por exemplo <code>print-2026-10-01-143005.png</code>.</Item>
            <Item>
              Ao copiar de dentro do Word ou do Excel, vêm texto e imagem juntos. Com o cursor num
              campo de texto, o Ctrl+V cola <strong>o texto</strong>, que é o que se espera ali.
              Para anexar como imagem, clique fora dos campos (num espaço vazio da janela) e cole.
            </Item>
          </Lista>
          <Callout tipo="lgpd" titulo="Recorte o print">
            Mostre só a parte da tela que explica o problema. Dependendo de como o sistema está
            configurado, outras pessoas podem ver o seu ticket, e os prints vão junto (veja a seção
            <strong> Quem vê o seu ticket</strong>, mais abaixo).
          </Callout>
        </>
      ),
    },
    {
      id: 'simulacao',
      titulo: 'Experimente',
      busca: 'simulação treinar colar print',
      conteudo: (
        <>
          <P>
            Tire um print com Win+Shift+S, clique dentro da simulação e aperte Ctrl+V. A imagem
            aparece só aqui: nada é enviado e nenhum ticket é aberto de verdade.
          </P>
          <SimulacaoAbrirTicket />
        </>
      ),
    },
    {
      id: 'acompanhar',
      titulo: 'Acompanhar: quando a equipe responde',
      busca:
        'status aberto em análise concluído negado respondido reconhecer marcar como visto ver ticket inteiro retorno da equipe contador menu aviso',
      conteudo: (
        <>
          <P>A lista mostra os seus tickets com número, tipo, título, status e a data da última atualização. Os status:</P>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef>Aberto</SeloRef>, descricao: 'Recebido, ainda não analisado.' },
              { termo: <SeloRef cor="info">Em análise</SeloRef>, descricao: 'A equipe está trabalhando nele.' },
              {
                termo: <SeloRef cor="sucesso">Concluído</SeloRef>,
                descricao: 'Resolvido. Vem sempre com um retorno da equipe dizendo o que foi feito.',
              },
              {
                termo: <SeloRef cor="erro">Negado</SeloRef>,
                descricao: 'Não será feito. Vem sempre com a justificativa.',
              },
            ]}
          />
          <P>Quando a equipe responde, ou conclui ou nega o ticket, você fica sabendo de três jeitos:</P>
          <Lista>
            <Item>
              Abre no centro da tela, em qualquer página do painel, a janela{' '}
              <strong>“A equipe respondeu seu ticket”</strong>, com o retorno. Ela tem{' '}
              <BotaoRef variante="outline">Ver ticket inteiro</BotaoRef> e{' '}
              <BotaoRef>Marcar como visto</BotaoRef>. Fechar no X só adia: ela volta no próximo
              acesso enquanto você não marcar.
            </Item>
            <Item>Um número aparece ao lado de <strong>Meus Tickets</strong> no menu.</Item>
            <Item>
              Na lista, o ticket ganha o selo <SeloRef cor="erro">Respondido</SeloRef> e o botão{' '}
              <BotaoRef variante="outline">Reconhecer</BotaoRef>.
            </Item>
          </Lista>
          <P>
            Abrir o ticket já conta como “visto”: o aviso e o número do menu somem sozinhos. O
            texto que a equipe deixou ao concluir ou negar fica no quadro verde{' '}
            <strong>Retorno da equipe</strong>, logo abaixo da descrição.
          </P>
        </>
      ),
    },
    {
      id: 'conversa',
      titulo: 'Conversar no ticket',
      busca: 'conversa responder resposta comentar comentário anexar enviar reabrir',
      conteudo: (
        <>
          <P>
            Embaixo de cada ticket há a <strong>Conversa</strong>: a equipe pergunta, você responde,
            com anexos se precisar (o Ctrl+V vale aqui também). Responda no mesmo ticket em vez de
            abrir outro: o histórico inteiro fica num lugar só.
          </P>
          <Lista>
            <Item>Só quem abriu o ticket responde nele.</Item>
            <Item>
              Responder não muda o status. Se o problema voltou num ticket já concluído, escreva na
              conversa: a equipe vê a atividade nova e decide se reabre.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'arquivar',
      titulo: 'Arquivar',
      busca: 'arquivar desarquivar mostrar arquivados limpar lista',
      conteudo: (
        <P>
          <BotaoRef variante="outline">Arquivar</BotaoRef>, dentro do ticket, tira ele da sua
          lista. Não apaga nada e não muda nada para a equipe. Para ver de novo, marque{' '}
          <strong>Mostrar arquivados</strong> na lista; dentro do ticket,{' '}
          <BotaoRef variante="outline">Desarquivar</BotaoRef> devolve ele à lista.
        </P>
      ),
    },
    {
      id: 'quem-ve',
      titulo: 'Quem vê o seu ticket',
      busca: 'visibilidade privado por unidade público quem vê outros tickets da unidade',
      conteudo: (
        <>
          <P>
            A equipe de suporte vê todos os tickets. Entre os usuários, depende de uma configuração
            única do sistema, feita na Gestão de Tickets:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Privado', descricao: 'Cada um vê apenas os seus. É o padrão.' },
              {
                termo: 'Por unidade',
                descricao:
                  'Você vê também os tickets abertos por outras pessoas na unidade em que você está trabalhando.',
              },
              { termo: 'Público', descricao: 'Todos veem todos os tickets.' },
            ]}
          />
          <P>
            Quando um ticket de outra pessoa aparece na sua lista, você pode ler, mas só o autor
            responde e arquiva. Isso evita abrir o mesmo pedido duas vezes. Notas internas da equipe
            nunca aparecem para quem não é da gestão.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'Ctrl+V não funciona não anexa imagem grande demais excluir ticket apagar',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Apertei Ctrl+V e nada aconteceu.',
              descricao:
                'Confira se o print foi mesmo copiado: depois do Win+Shift+S é preciso selecionar a área. Se você copiou de dentro do Word ou do Excel com o cursor num campo de texto, o que colou foi o texto; clique fora dos campos e cole de novo.',
            },
            {
              termo: '“Arquivo excede o limite de 5 MB.”',
              descricao:
                'Recorte uma área menor da tela com Win+Shift+S; um print de tela inteira em monitor grande pode passar do limite.',
            },
            {
              termo: 'Abri o ticket errado. Consigo apagar?',
              descricao:
                'Você pode arquivar, para tirar da sua lista. Excluir é só com a equipe de suporte: escreva na conversa pedindo.',
            },
          ]}
        />
      ),
    },
  ],
};
