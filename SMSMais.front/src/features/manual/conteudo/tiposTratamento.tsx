import { ClipboardList } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo dos Tipos de tratamento do transporte e do tempo médio (que saiu do atendimento).
 *
 * Conferido no código: `features/tiposTratamento` (TiposTratamentoPage, FormularioTipoTratamento),
 * `shared/ui/CampoTempoMedio`, `features/tratamentos` (TratamentoFormPage avisa tipo sem tempo) e no
 * backend (`TiposTratamentoService` — código normalizado e único, exclusão = desativar —,
 * `TiposTratamentoValidators`, `TratamentosService.GarantirTipoAsync`).
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
  atualizadoEm: '2026-09-30',
  palavrasChave: [
    'tipo de tratamento',
    'catálogo',
    'tempo médio',
    'duração',
    'hemodiálise',
    'radioterapia',
    'quimioterapia',
    'fisioterapia',
    'retorno',
    'volta',
    'código',
    'não informado',
    'excluir',
    'desativar',
    'transporte',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'tipo de tratamento catálogo atendimento obrigatório tempo médio volta retorno horário',
      conteudo: (
        <>
          <P>
            É a lista de tratamentos que o transporte atende — hemodiálise, radioterapia,
            quimioterapia, fisioterapia… Todo atendimento escolhe um tipo, e o tipo é obrigatório.
          </P>
          <P>
            O tipo guarda o <strong>tempo médio</strong> que o paciente fica no tratamento, da
            chegada à liberação (ex.: hemodiálise ≈ 4h00). É desse número que sai quanto tempo depois
            da chegada o paciente vai precisar voltar, e ele vale para todo atendimento daquele tipo
            — o atendimento não tem tempo próprio.
          </P>
          <Callout tipo="atencao" titulo="A previsão automática da volta ainda está chegando">
            Por enquanto o sistema guarda o tempo médio e mostra no atendimento; o horário de volta
            ainda não é calculado sozinho na rota. Quem monta a rota usa o número.
          </Callout>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar ou editar um tipo',
      busca: 'novo tipo cadastrar editar nome código tempo médio horas minutos situação ativo',
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
              {
                titulo: 'Escreva o nome e um código curto.',
                detalhe:
                  'O código vira minúsculas, com espaço trocado por “_” (ex.: hemodialise), e não pode repetir o de outro tipo.',
              },
              {
                titulo: 'Informe o tempo médio em horas e minutos.',
                detalhe: 'Obrigatório, de 1 minuto a 24 horas.',
              },
              {
                titulo: (
                  <>
                    Clique em <BotaoRef>Cadastrar</BotaoRef> (ou <BotaoRef>Salvar alterações</BotaoRef>).
                  </>
                ),
                detalhe: 'Na edição, a caixa “Ativo” reativa um tipo que foi excluído.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'lista',
      titulo: 'Como ler a lista',
      busca: 'lista nome código tempo médio não informado status ativo inativo',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Nome e código', descricao: 'Como o tipo aparece no atendimento, e o identificador curto.' },
              {
                termo: 'Tempo médio',
                descricao: (
                  <>
                    Em horas e minutos. <SeloRef cor="alerta">não informado</SeloRef> aparece nos tipos
                    que já existiam antes do campo: edite e preencha.
                  </>
                ),
              },
              { termo: 'Status', descricao: 'Ativo ou inativo. Só os ativos aparecem no atendimento novo.' },
            ]}
          />
          <Callout tipo="dica" titulo="Tipo sem tempo médio">
            O atendimento deixa escolher um tipo sem tempo médio, mas avisa em vermelho embaixo do
            campo. Vale preencher logo: é o número que quem monta a rota vai usar.
          </Callout>
        </>
      ),
    },
    {
      id: 'excluir',
      titulo: 'Excluir',
      busca: 'excluir desativar tipo atendimentos existentes reativar',
      conteudo: (
        <P>
          <BotaoRef variante="ghost">Excluir</BotaoRef> desativa o tipo: ele some das opções do
          atendimento novo, mas os atendimentos que já apontam para ele continuam com ele. Para
          voltar, edite e marque “Ativo”.
        </P>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil tipos de tratamento consulta inclusão edição exclusão',
      conteudo: (
        <>
          <P>
            A permissão é <strong>Tipos de tratamento</strong>, na tela de Perfis. Quem só cadastra
            atendimento não precisa dela: a lista de tipos do formulário vem com a permissão de
            Atendimentos.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver a lista de tipos.' },
              { termo: 'Inclusão', descricao: 'Cadastrar tipo.' },
              { termo: 'Edição', descricao: 'Editar nome, código e tempo médio; reativar.' },
              { termo: 'Exclusão', descricao: 'Desativar.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'código repetido tipo não aparece atendimento tempo médio onde',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: '“Já existe tipo de tratamento com este código.”',
              descricao: 'Outro tipo usa o mesmo código (maiúsculas e espaços não contam como diferença). Escolha outro.',
            },
            {
              termo: 'O tipo não aparece no atendimento.',
              descricao: 'Ele está inativo. Edite e marque “Ativo”.',
            },
            {
              termo: 'Posso ter um tempo diferente para um paciente?',
              descricao: 'Não: o tempo é do tipo. Se um grupo de pacientes demora bem diferente, crie outro tipo.',
            },
          ]}
        />
      ),
    },
  ],
};
