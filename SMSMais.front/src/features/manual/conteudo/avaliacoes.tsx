import { Star } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo das Avaliações do transporte (etapa 5): a nota que o paciente dá pelo app depois da viagem.
 *
 * Conferido no código: `features/avaliacoes` (AvaliacoesPage — colunas Viagem, Motorista · veículo,
 * Nota, Comentário, Por onde; Editar só para as do painel), o app/PWA do cidadão
 * (`SMSMais.cidadao.pwa` AvaliarViagens e `SMSMais.cidadao.app` avaliar_viagens) e o backend
 * (`AvaliacoesService` — viagem realizada, 30 dias, uma por viagem, nota 1 a 5, comentário até 500 no
 * app; guarda motorista, veículo e rota; a do app não se edita; `CidadaoController` — pelo link do
 * WhatsApp só lê e a equipe "entrando como o paciente" não avalia).
 */
export const artigoAvaliacoes: Artigo = {
  slug: 'avaliacoes',
  titulo: 'Avaliações do transporte',
  resumo: 'As notas que os pacientes dão pelo app depois da viagem, com quem levou e em qual veículo.',
  grupo: 'transporte',
  icone: Star,
  rota: '/app/avaliacoes',
  publico: 'Quem acompanha a qualidade do transporte de pacientes',
  atualizadoEm: '2026-10-16',
  palavrasChave: [
    'avaliação',
    'nota',
    'estrelas',
    'comentário',
    'feedback',
    'paciente',
    'app do cidadão',
    'motorista',
    'veículo',
    'translado',
    'qualidade',
  ],
  secoes: () => [
    {
      id: 'de-onde-vem',
      titulo: 'De onde vêm as notas',
      busca: 'app do cidadão transporte avalie suas viagens nota 1 a 5 estrelas comentário 30 dias uma vez',
      conteudo: (
        <>
          <P>
            Depois que a viagem acontece, o paciente vê, no app, em <strong>Transporte</strong>, o bloco{' '}
            <strong>Avalie suas viagens</strong>: escolhe de 1 a 5 estrelas e, se quiser, escreve um
            comentário. A nota chega aqui com o dia, o motorista e o veículo daquela viagem.
          </P>
          <Lista>
            <Item>Só viagem que aconteceu (o paciente foi levado) — quem faltou não avalia.</Item>
            <Item>Uma vez por viagem, até 30 dias depois.</Item>
            <Item>
              Pelo link do WhatsApp o app só mostra; para avaliar, o paciente entra com o código. E quem
              entra “como o paciente” pelo Sandbox não avalia por ele.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'lista',
      titulo: 'Como ler a lista',
      busca: 'viagem motorista veículo nota comentário por onde app do paciente painel equipe',
      conteudo: (
        <ListaDefinicoes
          itens={[
            { termo: 'Viagem', descricao: 'Quem avaliou e o dia da viagem.' },
            {
              termo: 'Motorista · veículo',
              descricao: 'Quem levou e em qual veículo, como estavam na hora da avaliação.',
            },
            { termo: 'Nota e comentário', descricao: 'De 1 (muito ruim) a 5 (muito boa).' },
            {
              termo: 'Por onde',
              descricao: '“App do paciente” — o próprio paciente; “Painel (equipe)” — registrada aqui.',
            },
          ]}
        />
      ),
    },
    {
      id: 'registrar-corrigir',
      titulo: 'Registrar, corrigir e excluir',
      busca: 'registrar avaliação equipe telefone editar excluir comentário ofensivo não se edita',
      conteudo: (
        <>
          <P>
            <BotaoRef>Registrar avaliação</BotaoRef> é para quando o paciente dá a nota por outro meio
            (por telefone, por exemplo).
          </P>
          <Callout tipo="regra" titulo="A nota do paciente é dele">
            A avaliação que veio do app não se edita: a equipe pode <strong>excluir</strong> (um
            comentário ofensivo, por exemplo), mas não reescrever.
          </Callout>
        </>
      ),
    },
  ],
};
