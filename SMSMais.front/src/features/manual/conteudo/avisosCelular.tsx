import { BellRing } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Sistema → Avisos no celular (`/app/avisos-celular`, permissão Erros).
 *
 * Conferido no código: `features/alertas-plataforma/pages/AvisosCelularPage.tsx` (lista única,
 * freio, template `erro_plataforma`, teste) e, no back, `Core/Alertas` (janela de 23 h, teto
 * diário). O canal do Agente IA pelo WhatsApp é a chave "Conversa com o Agente IA" de cada
 * telefone — o detalhe do uso está no artigo do Agente IA.
 */
export const artigoAvisosCelular: Artigo = {
  slug: 'avisos-no-celular',
  titulo: 'Avisos no celular',
  resumo:
    'Quem recebe no WhatsApp os erros da plataforma, o que é reportado, o freio contra repetição — e o telefone que conversa com o Agente IA.',
  grupo: 'sistema',
  icone: BellRing,
  rota: '/app/avisos-celular',
  publico: 'Quem cuida da plataforma (permissão Erros)',
  atualizadoEm: '2026-10-06',
  palavrasChave: [
    'avisos no celular',
    'aviso de erro',
    'whatsapp',
    'erro_plataforma',
    'template',
    'destinatário',
    'telefone',
    'silenciar',
    'freio',
    'teto diário',
    'enviar teste',
    'agente ia',
    'agente pelo whatsapp',
    'conversa com o agente ia',
    'reiniciar',
    'parar',
    'status',
    'responder citando o aviso',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'erro plataforma whatsapp robô sincronismo erro 500 log',
      conteudo: (
        <>
          <P>
            Todo erro da plataforma — robô que parou de responder, crédito da IA, sincronismo que falhou, erro 500
            novo e qualquer erro que um motor grave no log — vira uma mensagem de WhatsApp para os telefones desta
            tela. É a <strong>única</strong> lista de avisos de erro: não existe outra configuração em nenhuma tela.
          </P>
          <P>
            Se a pessoa escreveu para o número da Secretaria nas últimas 23 horas, o aviso chega como mensagem
            comum, com o detalhe inteiro. Fora disso vai pelo template <code>erro_plataforma</code>, aprovado pela
            Meta.
          </P>
        </>
      ),
    },
    {
      id: 'quem-recebe',
      titulo: 'Quem recebe',
      busca: 'telefone adicionar remover recebe desligar enviar teste agora',
      conteudo: (
        <Lista>
          <Item>Adicione o celular com DDD e, se quiser, um nome. Ele passa a receber na hora.</Item>
          <Item>
            Desmarcar <strong>recebe</strong> mantém o telefone na lista sem mandar nada (férias, troca de plantão).
          </Item>
          <Item>
            <strong>Enviar teste agora</strong> dispara um aviso de verdade e mostra a resposta da Meta — é o jeito
            de saber se está chegando.
          </Item>
        </Lista>
      ),
    },
    {
      id: 'freio',
      titulo: 'O freio contra repetição',
      busca: 'freio silenciar 30 minutos 16 horas segurada ocorrências teto',
      conteudo: (
        <P>
          A mesma fonte não avisa de novo antes de 30 minutos; se continua falhando, o intervalo dobra (1 h, 2 h… até
          16 h) e o aviso seguinte diz quantas ocorrências foram seguradas. <strong>Silenciar</strong> uma fonte
          mantém a contagem e só para de mandar.
        </P>
      ),
    },
    {
      id: 'agente-ia',
      titulo: 'Conversa com o Agente IA pelo WhatsApp',
      busca:
        'agente ia agente pelo whatsapp conversa com o agente ia robô ícone usuário representa reiniciar parar status responder citando o aviso duas etapas desligar acesso',
      conteudo: (
        <>
          <P>
            Pelo ícone do robô, ao lado de cada telefone, liga-se a chave <strong>Conversa com o Agente IA</strong> e
            escolhe-se o <strong>usuário</strong> que aquele telefone representa. A partir daí, quem escrever desse
            número no WhatsApp conversa com o Agente IA do servidor, com o acesso desse usuário — acesso total se
            ele for administrador do agente. O telefone ganha o selo <strong>Agente IA</strong> na lista.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'reiniciar', descricao: 'Fecha a sessão atual e começa uma nova, do zero.' },
              { termo: 'parar', descricao: 'Interrompe o trabalho que está em andamento.' },
              { termo: 'status', descricao: 'Diz se há trabalho em andamento.' },
            ]}
          />
          <P>
            Para pedir que ele resolva um erro, <strong>responda citando o aviso</strong> no WhatsApp: o texto do
            aviso vai junto com o pedido. As mensagens desse telefone somem do módulo Conversas; as sessões ficam em
            Agente IA, aba WhatsApp.
          </P>
          <Callout tipo="atencao" titulo="Acesso total pelo celular">
            Quem tomar esse WhatsApp ganha o mesmo acesso. Ligue a confirmação em duas etapas do WhatsApp no celular.
            Desmarcar a chave corta o acesso na hora — é o botão de emergência se o celular for perdido.
          </Callout>
        </>
      ),
    },
  ],
};
