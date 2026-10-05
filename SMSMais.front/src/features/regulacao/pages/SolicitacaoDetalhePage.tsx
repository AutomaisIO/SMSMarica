import { useState } from 'react';
import { ArrowLeft, ClipboardCheck, PencilLine } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';

import { usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';

import { AnexosSolicitacao } from '../components/AnexosSolicitacao';
import { CabecalhoSolicitacao } from '../components/CabecalhoSolicitacao';
import { LinhaDoTempo } from '../components/LinhaDoTempo';
import { MedicoPendenteCard } from '../components/MedicoPendenteCard';
import { ModalMotivo } from '../components/ModalMotivo';
import { ParaLancarNoSistema } from '../components/ParaLancarNoSistema';
import { RespostasRegras } from '../components/RespostasRegras';
import {
  useCancelarSolicitacao,
  useEventosSolicitacao,
  useSolicitacao,
} from '../api/solicitacoesQueries';

/**
 * O caso inteiro numa tela, do lado de <b>quem pediu</b>: cabeçalho, o que foi preenchido e
 * anexado, e a linha do tempo.
 *
 * <p><b>Aqui só há as ações da unidade</b> — continuar o rascunho, corrigir o que foi devolvido e
 * cancelar o que ainda é dela. Assumir, aceitar, devolver e recusar são da regulação e moram na
 * tela de análise (Gestão de fila, módulo 48). Antes era uma tela só, com os botões decididos pela
 * permissão: quem regulava via os comandos da regulação até na fila da própria unidade.</p>
 *
 * <p>Quem tem o 48 e chega por aqui (pela "Minha fila", por uma notificação, pela Ouvidoria)
 * ganha um atalho para a análise, em vez dos botões.</p>
 */
export function SolicitacaoDetalhePage() {
  const { id = '' } = useParams();
  const navegar = useNavigate();
  const ehAgente = useTemConsulta('RegulacaoTriagem');
  const podeEditarModulo = usePermissao('Regulacao', 'Edicao');

  const solicitacao = useSolicitacao(id);
  const eventos = useEventosSolicitacao(id);
  const cancelar = useCancelarSolicitacao();

  const [cancelando, setCancelando] = useState(false);

  const s = solicitacao.data;

  if (solicitacao.isLoading) return <p className="text-sm text-slate-500">Carregando…</p>;
  if (!s) return <p className="text-sm text-slate-500">Solicitação não encontrada.</p>;

  const podeCancelar = ['Rascunho', 'PendenteRegulacao'].includes(s.status);
  // Enquanto é da unidade, ela edita tudo — procedimento, destino, paciente, formulário e anexos.
  const podeEditar = podeEditarModulo && ['Rascunho', 'Devolvida'].includes(s.status);
  // Rascunho ainda não chegou à regulação: não há o que analisar.
  const podeAnalisar = ehAgente && s.status !== 'Rascunho';

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <button
        type="button"
        onClick={() => navegar(-1)}
        className="flex items-center gap-1 text-sm text-slate-500 hover:text-slate-800"
      >
        <ArrowLeft className="size-4" /> Voltar
      </button>

      <CabecalhoSolicitacao s={s} />

      {(podeEditar || podeCancelar || podeAnalisar) && (
        <div className="flex flex-wrap gap-2">
          {podeEditar && (
            <Button onClick={() => navegar(`/app/regulacao/solicitacoes/${s.id}/editar`)}>
              <PencilLine className="size-4" />
              {s.status === 'Devolvida' ? 'Corrigir e reenviar' : 'Continuar rascunho'}
            </Button>
          )}

          {podeCancelar && (
            <Button variante="secundaria" onClick={() => setCancelando(true)}>
              Cancelar solicitação
            </Button>
          )}

          {podeAnalisar && (
            <Button
              variante="outline"
              className="ml-auto"
              onClick={() => navegar(`/app/regulacao/gestao-fila/${s.id}`)}
            >
              <ClipboardCheck className="size-4" />
              Abrir na Gestão de fila
            </Button>
          )}
        </div>
      )}

      {/* Só leitura aqui: quem cadastra o médico no sistema e resolve o pedido é a regulação. */}
      <MedicoPendenteCard valorMedico={s.formulario?.medico_solicitante} podeResolver={false} />

      <ParaLancarNoSistema s={s} />

      <RespostasRegras solicitacaoId={id} />

      <AnexosSolicitacao solicitacaoId={id} />

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h2 className="mb-3 text-sm font-semibold text-slate-900">Linha do tempo</h2>
        <LinhaDoTempo eventos={eventos.data} carregando={eventos.isLoading} />
      </section>

      {cancelando && (
        <ModalMotivo
          titulo="Cancelar solicitação"
          pergunta="Motivo do cancelamento"
          rotuloConfirmar="Cancelar solicitação"
          desfecho="Solicitação cancelada. Ela fica em Encerradas, com o motivo registrado."
          perigo
          aoConfirmar={(motivo) => cancelar.mutateAsync({ id, motivo })}
          aoFechar={() => setCancelando(false)}
        />
      )}
    </div>
  );
}
