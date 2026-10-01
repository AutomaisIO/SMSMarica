import { Inbox } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Callout } from '@/features/manual/components/Callout';
import { Passos } from '@/features/manual/components/Passos';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Suporte → Gestão de Tickets (`/app/tickets/gestao`), o lado da equipe.
 *
 * Conferido no código: `features/tickets` (GestaoTicketsPage, TicketDetalhePage com o painel de
 * Triagem e o envio ao Agente IA, ConversaTicket com nota interna) e `Core/Tickets/TicketService`
 * (concluir/negar exige retorno e levanta a bandeira do autor; comentário público também; nota
 * interna não; "novo" = nunca visto pela gestão ou com atividade depois da última vista).
 * Permissões em `TicketsController`: módulo Ticket (Consulta lê, Edição tria/responde/arquiva/
 * configura, Exclusão exclui); Agente IA (Edição) para o envio.
 */
export const artigoTicketsGestao: Artigo = {
  slug: 'tickets-gestao',
  titulo: 'Gestão de Tickets: triar e responder',
  resumo:
    'A caixa de entrada da equipe de suporte: ver todos os tickets, responder ao autor ou deixar nota interna, triar status e prioridade, concluir com retorno e encaminhar ao Agente IA.',
  grupo: 'suporte',
  icone: Inbox,
  rota: '/app/tickets/gestao',
  publico: 'Equipe de suporte (perfil com o módulo Ticket)',
  atualizadoEm: '2026-10-01',
  palavrasChave: [
    'gestão de tickets',
    'triagem',
    'triar',
    'status',
    'prioridade',
    'concluir',
    'negar',
    'feedback',
    'justificativa',
    'retorno da equipe',
    'nota interna',
    'responder',
    'novo',
    'respondido',
    'autor visualizou',
    'agente IA',
    'enviar ao agente',
    'enviado à IA',
    'arquivar',
    'excluir',
    'visibilidade',
    'quem vê os tickets',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'caixa de entrada equipe suporte todos os tickets contador menu novos',
      conteudo: (
        <>
          <P>
            É a caixa de entrada da equipe: todos os tickets abertos por qualquer usuário, de
            qualquer unidade. O número ao lado de <strong>Gestão de Tickets</strong> no menu conta
            os tickets <strong>novos</strong>: os que ninguém da gestão abriu ainda e os que tiveram
            atividade do autor depois da última vez que a equipe olhou.
          </P>
          <P>
            Do lado de quem abre o ticket, o funcionamento está em{' '}
            <Link to="/app/manual/tickets" className="font-medium text-primary-700 underline underline-offset-2">
              Meus Tickets: pedir ajuda ao suporte
            </Link>
            .
          </P>
        </>
      ),
    },
    {
      id: 'lista',
      titulo: 'A lista e os filtros',
      busca: 'buscar número título autor filtro status tipo arquivados respondido enviado à IA prioridade',
      conteudo: (
        <>
          <P>
            A busca aceita número (<code>42</code> ou <code>#42</code>), trecho do título ou nome do
            autor. Os filtros de status e de tipo se somam à busca; <strong>Arquivados</strong> mostra
            também os que a gestão arquivou. Além do status e da prioridade, a linha pode ter:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <SeloRef cor="sucesso">Respondido</SeloRef>,
                descricao:
                  'Ticket ainda Aberto ou Em análise que a equipe já respondeu ao autor. Serve para não responder duas vezes.',
              },
              {
                termo: <SeloRef cor="info">Enviado à IA</SeloRef>,
                descricao: 'Já foi encaminhado ao Agente IA (veja abaixo).',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'triagem',
      titulo: 'Triagem: status, prioridade e retorno',
      busca:
        'triagem status aberto em análise concluído negado prioridade baixa normal alta feedback justificativa obrigatório salvar triagem retorno da equipe',
      conteudo: (
        <>
          <P>Dentro do ticket, o quadro <strong>Triagem</strong> tem três campos:</P>
          <Lista>
            <Item>
              <strong>Status</strong>: <SeloRef>Aberto</SeloRef> →{' '}
              <SeloRef cor="info">Em análise</SeloRef> → <SeloRef cor="sucesso">Concluído</SeloRef>{' '}
              ou <SeloRef cor="erro">Negado</SeloRef>.
            </Item>
            <Item>
              <strong>Prioridade</strong>: Baixa, Normal ou Alta. Todo ticket nasce Normal.
            </Item>
            <Item>
              <strong>Feedback ao concluir</strong> (ou <strong>Justificativa</strong>, ao negar): o
              texto que o autor vê no quadro <strong>Retorno da equipe</strong>.
            </Item>
          </Lista>
          <P>
            Nada é gravado até clicar em <BotaoRef>Salvar triagem</BotaoRef>.
          </P>
          <Callout tipo="regra" titulo="Concluir e negar exigem retorno">
            O botão fica travado enquanto o status for Concluído ou Negado e o texto estiver vazio.
            É o que responde ao autor, semanas depois, o que foi feito ou por que não será. Ao
            salvar, o autor recebe o aviso <em>“A equipe respondeu seu ticket”</em> em qualquer
            tela em que estiver.
          </Callout>
        </>
      ),
    },
    {
      id: 'responder',
      titulo: 'Responder ao autor ou deixar nota interna',
      busca: 'responder conversa nota interna não visível ao autor comentário anexo print Ctrl+V autor visualizou a resposta',
      conteudo: (
        <>
          <P>
            Na <strong>Conversa</strong>, escreva e clique em <BotaoRef>Enviar</BotaoRef>. Anexos
            funcionam como na abertura: <BotaoRef variante="outline">Anexar imagem</BotaoRef> ou
            Ctrl+V de um print em qualquer ponto da caixa de resposta.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Resposta comum',
                descricao:
                  'O autor vê e é avisado na hora, como na conclusão. É o jeito de pedir mais informação sem mudar o status.',
              },
              {
                termo: 'Nota interna',
                descricao:
                  'Marque “Nota interna (não visível ao autor)” antes de enviar. Fica em amarelo, só a gestão vê, e o autor não é avisado. Use para o que é da equipe: hipótese de causa, onde já olharam, a quem passar.',
              },
            ]}
          />
          <P>
            Depois de uma resposta, abaixo do retorno aparece se o autor já viu:{' '}
            <em>“Autor visualizou a resposta em…”</em> ou <em>“Autor ainda não visualizou a resposta.”</em>
          </P>
        </>
      ),
    },
    {
      id: 'agente-ia',
      titulo: 'Enviar ao Agente IA',
      busca: 'agente IA enviar ao agente acompanhar no agente instrução investigar propor solução enviado à IA',
      conteudo: (
        <>
          <P>
            Quem tem permissão de edição no Agente IA vê, na Triagem, o botão{' '}
            <BotaoRef variante="outline">Enviar ao Agente IA</BotaoRef>. Ele abre o terminal do
            agente já com o número do ticket; o agente lê o ticket, investiga e propõe a solução.
          </P>
          <Passos
            itens={[
              {
                titulo: 'Se o ticket só tem o relato do autor, o sistema pergunta se você quer deixar uma instrução.',
                detalhe:
                  'Onde já olharam, hipótese de causa, prioridade real, o que não fazer. Pode enviar sem instrução.',
              },
              {
                titulo: (
                  <>
                    Depois do primeiro envio, o botão vira{' '}
                    <BotaoRef variante="outline">Acompanhar no Agente IA</BotaoRef> e a linha ganha
                    o selo <SeloRef cor="info">Enviado à IA</SeloRef>.
                  </>
                ),
              },
            ]}
          />
          <Callout tipo="atencao" titulo="O agente não fecha o ticket">
            Concluir, negar e responder ao autor continuam sendo feitos nesta tela, por uma pessoa.
          </Callout>
        </>
      ),
    },
    {
      id: 'arquivar-excluir',
      titulo: 'Arquivar e excluir',
      busca: 'arquivar desarquivar excluir apagar remover',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: <BotaoRef variante="outline">Arquivar</BotaoRef>,
              descricao:
                'Tira o ticket da lista da gestão. É independente do arquivar do autor: arquivar aqui não some com o ticket da lista dele.',
            },
            {
              termo: <BotaoRef variante="danger">Excluir</BotaoRef>,
              descricao:
                'Remove o ticket da gestão e da lista do autor. Exige a permissão de exclusão do módulo. Para ticket resolvido, prefira concluir com retorno: excluir não avisa o autor.',
            },
          ]}
        />
      ),
    },
    {
      id: 'visibilidade',
      titulo: 'Quem vê os tickets',
      busca: 'visibilidade privado por unidade público configuração quem vê os tickets',
      conteudo: (
        <>
          <P>
            No alto da página, <strong>Quem vê os tickets</strong> define o que cada usuário enxerga
            em Meus Tickets. Vale para o sistema inteiro e muda na hora:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Privado', descricao: 'Cada um vê apenas os seus. É o padrão.' },
              {
                termo: 'Por unidade',
                descricao: 'A mesma unidade vê os tickets uns dos outros.',
              },
              { termo: 'Público', descricao: 'Todos veem todos os tickets.' },
            ]}
          />
          <P>
            Em qualquer opção, só o autor responde e arquiva o próprio ticket, e notas internas
            nunca aparecem fora da gestão. Os prints anexados, porém, aparecem para quem pode ver o
            ticket.
          </P>
        </>
      ),
    },
    {
      id: 'permissoes',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo Ticket consulta edição exclusão agente IA',
      conteudo: (
        <ListaDefinicoes
          itens={[
            { termo: 'Módulo Ticket, Consulta', descricao: 'Vê a Gestão de Tickets e lê qualquer ticket, inclusive as notas internas.' },
            {
              termo: 'Módulo Ticket, Edição',
              descricao: 'Tria, responde, deixa nota interna, arquiva e muda quem vê os tickets.',
            },
            { termo: 'Módulo Ticket, Exclusão', descricao: 'Exclui tickets.' },
            { termo: 'Agente IA, Edição', descricao: 'Vê e usa o botão Enviar ao Agente IA.' },
          ]}
        />
      ),
    },
  ],
};
