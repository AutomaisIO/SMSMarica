import { WifiOff } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo transversal (não tem tela própria): os avisos que aparecem em qualquer tela quando uma
 * requisição falha — faixa de conexão, aviso de ação sem conexão e erro com código de referência.
 *
 * Conferido no código: `shared/api/httpClient.ts` (interceptor), `shared/api/conexao.ts`
 * (verificação pelo /health/live, limiar de 15s), `shared/ui/FaixaConexao.tsx`, o reconectar do
 * tempo real em `features/conversas/hooks/useChatHub.ts` e o 503 do nginx do vhost da API.
 */
export const artigoAvisosConexao: Artigo = {
  slug: 'avisos-e-conexao',
  titulo: 'Avisos de erro e de conexão',
  resumo:
    'O que significam a faixa amarela de conexão, o aviso de ação que pode não ter sido gravada e o erro com código ERRO-XXXXXX — e o que fazer em cada caso.',
  grupo: 'sistema',
  icone: WifiOff,
  publico: 'Todo mundo que usa o painel',
  atualizadoEm: '2026-09-30',
  palavrasChave: [
    'erro',
    'aviso',
    'conexão',
    'internet',
    'servidor',
    'não foi possível conectar',
    'sem contato com o servidor',
    'reconectando',
    'reconectar',
    'sistema sendo atualizado',
    'atualização',
    'deploy',
    'fora do ar',
    'caiu',
    'lento',
    'faixa amarela',
    'tentar agora',
    'código de referência',
    'ERRO-',
    'erro já reportado',
    'ticket',
    'F5',
    'recarregar',
  ],
  secoes: () => [
    {
      id: 'tres-avisos',
      titulo: 'Os três avisos',
      busca: 'aviso erro faixa amarela vermelho canto código ERRO ação gravada conexão',
      conteudo: (
        <>
          <P>
            Quando alguma coisa dá errado entre a sua tela e o servidor, o painel mostra um de três
            avisos. Cada um pede uma atitude diferente:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Faixa amarela no topo',
                descricao:
                  'O painel está sem contato com o servidor há mais de 15 segundos. Não precisa fazer nada: ela some sozinha quando a conexão volta.',
              },
              {
                termo: '“A ação pode não ter sido gravada”',
                descricao:
                  'Você clicou em salvar, enviar ou excluir e a conexão falhou nesse momento. Confira se a ação aconteceu antes de repetir.',
              },
              {
                termo: 'Erro com código (ERRO-XXXXXX)',
                descricao:
                  'O servidor recebeu o pedido e falhou ao processar. É defeito do sistema, não da sua conexão: anote o código.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'faixa-de-conexao',
      titulo: 'A faixa amarela: sem contato com o servidor',
      busca: 'faixa amarela sem contato servidor reconectando sistema sendo atualizado sem internet tentar agora 15 segundos some sozinha F5 recarregar',
      conteudo: (
        <>
          <P>
            Uma falha de rede isolada não gera aviso nenhum. Antes de avisar, o painel pergunta ao
            servidor de novo, algumas vezes, por até <strong>15 segundos</strong>. Se ele responde
            nesse intervalo, você nem fica sabendo: as telas buscam os dados de novo sozinhas. A
            faixa só aparece quando a queda se confirma, e diz o motivo:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: '“O sistema está sendo atualizado”',
                descricao:
                  'Uma versão nova está entrando no ar. Leva alguns segundos; aguarde.',
              },
              {
                termo: '“Sem internet neste computador”',
                descricao:
                  'O próprio computador perdeu a rede (cabo, Wi-Fi, link da unidade). O painel volta assim que a conexão voltar.',
              },
              {
                termo: '“Sem contato com o servidor. Reconectando…”',
                descricao:
                  'A internet do computador está funcionando, mas o servidor não responde. O painel continua tentando a cada poucos segundos.',
              },
            ]}
          />
          <P>
            O botão <BotaoRef>Tentar agora</BotaoRef> pergunta na hora, sem esperar a próxima
            tentativa. Quando o servidor volta, a faixa some e a tela que você está olhando
            <strong> se atualiza sozinha</strong>, sem precisar de F5. As conversas em tempo real
            (bipe e alerta de mensagem nova) também reconectam sozinhas, por mais longa que tenha
            sido a queda.
          </P>
          <Callout tipo="dica" titulo="Faixa que não some">
            Se a faixa ficar mais de alguns minutos, teste outro site no mesmo computador. Se
            nenhum abre, o problema é a rede da unidade. Se os outros abrem, avise o suporte.
          </Callout>
        </>
      ),
    },
    {
      id: 'acao-sem-conexao',
      titulo: 'A ação pode não ter sido gravada',
      busca: 'salvar enviar excluir ação não gravada conferir repetir duplicar sem conexão',
      conteudo: (
        <>
          <P>
            Salvar, enviar, excluir e assinar continuam avisando <strong>na hora</strong> quando a
            conexão falha. Nesses casos você precisa saber que talvez não tenha dado certo, e o
            painel não repete a ação por conta própria.
          </P>
          <Lista>
            <Item>
              <strong>Confira antes de repetir.</strong> Às vezes o pedido chegou ao servidor e só a
              resposta se perdeu. Repetir sem olhar pode duplicar um cadastro ou uma mensagem ao
              paciente.
            </Item>
            <Item>
              Se a faixa amarela estiver na tela, espere ela sumir e só então tente de novo.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'erro-com-codigo',
      titulo: 'Erro com código de referência',
      busca: 'código de referência ERRO erro já reportado ticket suporte defeito servidor falhou',
      conteudo: (
        <>
          <P>
            Um aviso vermelho com um código como <strong>ERRO-4F9C2A</strong> significa que o
            servidor recebeu o pedido e falhou ao processar. O código aponta para o registro técnico
            do que aconteceu, para a equipe achar a causa sem pedir que você repita tudo.
          </P>
          <P>
            Anote o código e abra um ticket em <strong>Suporte → Meus Tickets</strong> dizendo o que
            você estava fazendo. Quando o aviso começa com <strong>“Erro já reportado”</strong>, o
            mesmo erro já tinha sido registrado antes: a equipe já sabe dele, mas o ticket ainda
            ajuda a medir quantas pessoas ele atrapalha.
          </P>
        </>
      ),
    },
  ],
};
