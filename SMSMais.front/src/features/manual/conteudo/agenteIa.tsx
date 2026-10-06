import { Bot } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Inteligência → Agente IA (`/app/agente-ia`, módulo AgenteIa).
 *
 * Conferido no código: `features/agente-ia/pages/AgenteIaPage.tsx` (abas Painel × WhatsApp,
 * somente leitura no WhatsApp, turno roda no servidor) e `components/ListaSessoes.tsx`. O canal
 * WhatsApp nasce da chave "Conversa com o Agente IA" em Avisos no celular.
 */
export const artigoAgenteIa: Artigo = {
  slug: 'agente-ia',
  titulo: 'Agente IA',
  resumo:
    'O agente que trabalha no servidor — pelo painel ou pelo WhatsApp do telefone de avisos — e como acompanhar as sessões de cada canal.',
  grupo: 'sistema',
  icone: Bot,
  rota: '/app/agente-ia',
  publico: 'Administradores da plataforma (módulo Agente IA)',
  atualizadoEm: '2026-10-06',
  palavrasChave: [
    'agente ia',
    'gestão ia',
    'claude',
    'servidor',
    'sessão',
    'conversa',
    'ticket',
    'arquivar',
    'agente pelo whatsapp',
    'whatsapp',
    'celular',
    'reiniciar',
    'parar',
    'status',
    'responder citando o aviso',
    'somente leitura',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'diagnóstico ticket correção código servidor turno',
      conteudo: (
        <>
          <P>
            O Agente IA trabalha no servidor da plataforma: lê código e logs, consulta o banco, investiga tickets e,
            com autorização, corrige. O trabalho roda <strong>lá</strong> — fechar a aba ou trocar de conversa não
            interrompe nada; ao voltar, o histórico está inteiro.
          </P>
          <P>
            Só os administradores do agente podem pedir mudança (código, deploy, escrita no servidor). Os demais
            ficam em modo somente leitura e, quando o pedido exige mudança, o agente oferece abrir um ticket.
          </P>
        </>
      ),
    },
    {
      id: 'abas',
      titulo: 'As duas abas: Painel e WhatsApp',
      busca: 'aba painel whatsapp sessão celular somente leitura gestão ia arquivada',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Painel',
              descricao:
                'Conversas abertas por aqui. “Nova conversa” abre outra; arquivar guarda o histórico e tira da lista.',
            },
            {
              termo: 'WhatsApp',
              descricao:
                'Sessões conduzidas pelo celular cadastrado em Avisos no celular — uma por telefone, com o número no cartão. Aqui elas são SOMENTE LEITURA: não há caixa de envio, quem conduz é o celular.',
            },
          ]}
        />
      ),
    },
    {
      id: 'pelo-whatsapp',
      titulo: 'Usar o agente pelo WhatsApp',
      busca:
        'agente pelo whatsapp reiniciar parar status responder citando o aviso andamento 30 segundos resposta final sessão não expira',
      conteudo: (
        <>
          <P>
            Ligue a chave <strong>Conversa com o Agente IA</strong> no telefone, em Sistema → Avisos no celular, e
            escolha o usuário que ele representa. Daí em diante, escrever desse celular para o número da Secretaria é
            falar com o agente.
          </P>
          <Lista>
            <Item>
              A sessão <strong>não expira</strong>: o agente lembra da conversa de dias atrás. Ela só muda quando
              você manda <strong>reiniciar</strong>.
            </Item>
            <Item>
              Enquanto ele trabalha, o andamento chega a cada ~30 segundos; a resposta final chega ao terminar.
            </Item>
            <Item>
              Para pedir que resolva um erro, <strong>responda citando o aviso</strong> no WhatsApp — o texto do
              aviso vai junto com o pedido.
            </Item>
            <Item>Antes de mexer em produção, ele pergunta; basta responder “sim” ou “não” pelo WhatsApp.</Item>
          </Lista>
          <ListaDefinicoes
            itens={[
              { termo: 'reiniciar', descricao: 'Fecha a sessão atual e começa uma nova, do zero.' },
              { termo: 'parar', descricao: 'Interrompe o trabalho que está em andamento.' },
              { termo: 'status', descricao: 'Diz se há trabalho em andamento.' },
            ]}
          />
          <Callout tipo="atencao" titulo="Acesso pelo celular">
            Quem tomar esse WhatsApp ganha o mesmo acesso. Ligue a confirmação em duas etapas do WhatsApp no
            celular; desmarcar a chave em Avisos no celular corta o acesso na hora.
          </Callout>
        </>
      ),
    },
  ],
};
