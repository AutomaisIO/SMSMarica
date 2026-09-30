import { ClipboardList } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo dos Tipos de tratamento do transporte e do tempo médio (que saiu do atendimento).
 *
 * Conferido no código: `features/tiposTratamento` (TiposTratamentoPage, FormularioTipoTratamento),
 * `shared/ui/CampoTempoMedio` e no backend (`TiposTratamentoService`, `TiposTratamentoValidators`).
 */
export const artigoTiposTratamento: Artigo = {
  slug: 'tipos-tratamento',
  titulo: 'Tipos de tratamento e tempo médio',
  resumo:
    'O catálogo de tratamentos do transporte (hemodiálise, radioterapia…) e o tempo médio que o paciente fica em cada um.',
  grupo: 'transporte',
  icone: ClipboardList,
  rota: '/app/tipos-tratamento',
  publico: 'Quem organiza o transporte de pacientes',
  atualizadoEm: '2026-09-29',
  palavrasChave: [
    'tipo de tratamento',
    'catálogo',
    'tempo médio',
    'duração',
    'hemodiálise',
    'radioterapia',
    'quimioterapia',
    'retorno',
    'volta',
    'código',
    'excluir',
    'transporte',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'tipo de tratamento catálogo atendimento obrigatório tempo médio volta retorno',
      conteudo: (
        <>
          <P>
            É a lista de tratamentos que o transporte atende — hemodiálise, radioterapia,
            quimioterapia… Todo atendimento escolhe um tipo.
          </P>
          <P>
            O tipo guarda o <strong>tempo médio</strong> que o paciente fica no tratamento, da chegada
            à liberação (ex.: hemodiálise ≈ 4h00). É a base para prever a volta na rota, e vale para
            todo atendimento daquele tipo — o atendimento não tem tempo próprio.
          </P>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar ou editar um tipo',
      busca: 'novo tipo cadastrar editar nome código tempo médio horas minutos',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Em <strong>Transporte Pacientes → Tipos de tratamento</strong>, clique em{' '}
                    <BotaoRef>Novo tipo</BotaoRef> (ou em <BotaoRef variante="ghost">Editar</BotaoRef> na linha).
                  </>
                ),
              },
              { titulo: 'Escreva o nome e um código curto, único (ex.: hemodialise).' },
              {
                titulo: 'Informe o tempo médio em horas e minutos.',
                detalhe: 'Obrigatório, de 1 minuto a 24 horas.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Tipos antigos sem tempo médio">
            Os tipos que já existiam antes do campo aparecem como <strong>não informado</strong>. O
            atendimento deixa escolher, mas avisa: edite o tipo e preencha.
          </Callout>
        </>
      ),
    },
    {
      id: 'excluir',
      titulo: 'Excluir',
      busca: 'excluir desativar tipo atendimentos existentes',
      conteudo: (
        <P>
          <BotaoRef variante="ghost">Excluir</BotaoRef> desativa o tipo: ele some das opções do
          atendimento novo, mas os atendimentos que já apontam para ele continuam com ele.
        </P>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil tipos de tratamento consulta inclusão edição exclusão',
      conteudo: (
        <ListaDefinicoes
          itens={[
            { termo: 'Consulta', descricao: 'Ver a lista de tipos.' },
            { termo: 'Inclusão', descricao: 'Cadastrar tipo.' },
            { termo: 'Edição', descricao: 'Editar nome, código e tempo médio; reativar.' },
            { termo: 'Exclusão', descricao: 'Desativar.' },
          ]}
        />
      ),
    },
  ],
};
