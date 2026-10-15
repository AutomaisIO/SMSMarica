import { Siren } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da central de Ocorrências dos veículos (botão REPORT do tablet).
 *
 * Conferido no código: `features/ocorrencias` (OcorrenciasPage — abas Em aberto / Encerradas 30 dias,
 * Atender, Encerrar com observação; AlertaOcorrencias — faixa vermelha global + alarme + notificação
 * pelo hub /hubs/rastreamento; contador do menu), `features/rastreamento/pages/MapaFrotaPage`
 * (veículo vermelho com ocorrência em aberto), o app do tablet (`SMSMais.agente.app`,
 * features/ocorrencia — tipos, "Outro" exige texto, fila sem rede) e o backend
 * (`OcorrenciaVeiculoService` — idLocal único, posição opcional, Aberta → EmAtendimento → Encerrada,
 * encerrada não reabre; `OcorrenciasVeiculoController` — Rastreamento Consulta/Edição).
 */
export const artigoOcorrencias: Artigo = {
  slug: 'ocorrencias',
  titulo: 'Ocorrências dos veículos',
  resumo:
    'Os avisos que os motoristas mandam pelo botão REPORT do tablet do carro — acidente, pane, paciente que passou mal — e como a central atende e encerra.',
  grupo: 'transporte',
  icone: Siren,
  rota: '/app/rastreamento/ocorrencias',
  publico: 'Quem acompanha a frota do transporte de pacientes (central)',
  atualizadoEm: '2026-10-15',
  palavrasChave: [
    'ocorrência',
    'report',
    'acidente',
    'pane',
    'quebrou',
    'pneu furado',
    'paciente passou mal',
    'emergência',
    'alerta',
    'aviso vermelho',
    'alarme',
    'central',
    'tablet',
    'motorista',
    'atender',
    'encerrar',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'report tablet motorista avisar central acidente pane paciente passou mal outro',
      conteudo: (
        <>
          <P>
            No tablet preso no carro há um botão vermelho <strong>REPORT</strong>, sempre à mão. Com
            ele o motorista avisa a central de que algo aconteceu no caminho, escolhendo entre{' '}
            <strong>Acidente</strong>, <strong>Pane no veículo</strong>,{' '}
            <strong>Paciente passou mal</strong> ou <strong>Outro</strong> (neste caso ele escreve o
            que houve). O aviso chega aqui com o veículo, a hora e o ponto no mapa onde o carro
            estava.
          </P>
          <Callout tipo="regra" titulo="Não substitui ligar para a emergência">
            O REPORT avisa a central. Em acidente com ferido ou paciente em risco, o motorista
            continua devendo acionar a emergência (192/193) — a central acompanha e apoia.
          </Callout>
        </>
      ),
    },
    {
      id: 'como-chega',
      titulo: 'Como a ocorrência chega',
      busca: 'faixa vermelha alarme som notificação navegador contador menu mapa da frota vermelho tempo real',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Faixa vermelha',
              descricao:
                'Aparece no topo de qualquer tela do painel, na hora, com um alarme sonoro. Fica até você clicar em Ver ocorrências ou fechá-la (fechar só tira o aviso; a ocorrência continua na lista).',
            },
            {
              termo: 'Notificação do navegador',
              descricao:
                'Se o navegador estiver com notificações permitidas para o sistema, também aparece fora da aba.',
            },
            {
              termo: 'Contador no menu',
              descricao: 'O item Ocorrências mostra quantas estão em aberto (abertas + em atendimento).',
            },
            {
              termo: 'Mapa da frota',
              descricao:
                'O veículo com ocorrência em aberto fica vermelho, com um anel pulsando, e o balão dele mostra o tipo da ocorrência.',
            },
          ]}
        />
      ),
    },
    {
      id: 'atender-encerrar',
      titulo: 'Atender e encerrar',
      busca: 'atender em atendimento encerrar observação o que foi feito quem atendeu encerradas 30 dias',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: 'Clique em Atender assim que alguém da central assumir o caso.',
                detalhe:
                  'A ocorrência passa para "Em atendimento" e fica registrado quem atendeu e quando — os outros operadores veem na hora que já tem gente cuidando.',
              },
              {
                titulo: 'Resolvido, clique em Encerrar e conte em poucas palavras o que foi feito.',
                detalhe:
                  'Ex.: guincho acionado, paciente levado à UPA, motorista seguiu viagem. A observação é opcional, mas é o que explica o caso depois.',
              },
            ]}
          />
          <P>
            Encerrada, ela sai de <AbaRef>Em aberto</AbaRef> e vai para{' '}
            <AbaRef>Encerradas (30 dias)</AbaRef>. Dá para encerrar direto, sem passar por{' '}
            <BotaoRef>Atender</BotaoRef>. Uma ocorrência encerrada não reabre: se o problema voltou, o
            motorista manda um REPORT novo.
          </P>
        </>
      ),
    },
    {
      id: 'no-tablet',
      titulo: 'O que o motorista faz no tablet',
      busca: 'tablet report escolher tipo enviar para a central sem rede guardado central avisada vinculado',
      conteudo: (
        <>
          <Passos
            itens={[
              { titulo: 'Toca em REPORT (na tela do mapa, na Rota do dia ou durante a navegação).' },
              {
                titulo: 'Escolhe o tipo e, se quiser, escreve um detalhe.',
                detalhe: 'Em "Outro" o texto é obrigatório. Tocar em REPORT sem querer não envia nada.',
              },
              { titulo: 'Toca em Enviar para a central e vê "Central avisada às HH:MM".' },
            ]}
          />
          <Callout tipo="dica" titulo="Sem internet no carro">
            O aviso fica guardado no tablet e sai sozinho assim que a rede voltar — o motorista vê
            "Sem conexão agora" e o botão REPORT mostra quantos avisos estão esperando. A central
            recebe uma vez só, mesmo que o tablet tente de novo.
          </Callout>
        </>
      ),
    },
    {
      id: 'permissoes',
      titulo: 'Quem pode o quê',
      busca: 'permissão rastreamento consulta edição ver atender encerrar',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Rastreamento — Consulta',
              descricao: 'Vê a lista, recebe a faixa vermelha e o contador do menu.',
            },
            {
              termo: 'Rastreamento — Edição',
              descricao: 'Também atende e encerra.',
            },
          ]}
        />
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'sem posição gps duas vezes duplicada horário tablet sumiu aviso',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: '"Sem posição"',
              descricao:
                'O GPS do tablet ainda não tinha sinal no momento do REPORT (ex.: logo depois de ligar). A ocorrência entra mesmo assim — vale ligar para o motorista.',
            },
            {
              termo: 'Pode chegar duplicada?',
              descricao:
                'Não. Cada aviso sai do tablet com um identificador próprio; se a rede falhar e o tablet reenviar, a central continua vendo uma ocorrência só.',
            },
            {
              termo: 'Fechei a faixa sem querer',
              descricao: 'A ocorrência continua em Ocorrências → Em aberto e no contador do menu.',
            },
          ]}
        />
      ),
    },
  ],
};
