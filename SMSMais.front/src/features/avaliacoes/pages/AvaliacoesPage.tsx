import { useState } from 'react';
import { Pencil, Plus, Star, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDeletarAvaliacao,
  useListarAvaliacoes,
} from '@/features/avaliacoes/api/queries';
import { FormularioAvaliacao } from '@/features/avaliacoes/components/FormularioAvaliacao';
import type { AvaliacaoListItem } from '@/features/avaliacoes/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

/** "2026-10-16" → "16/10/2026". */
function formatarDia(iso: string | null): string {
  const m = iso?.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : '—';
}

function Estrelas({ nota }: { nota: number }) {
  return (
    <span className="inline-flex items-center gap-0.5 text-amber-500">
      {Array.from({ length: 5 }).map((_, i) => (
        <Star
          key={i}
          className={`w-3.5 h-3.5 ${i < nota ? 'fill-current' : 'fill-none text-gray-300'}`}
        />
      ))}
      <span className="ml-1 text-xs text-gray-600">{nota}</span>
    </span>
  );
}

export function AvaliacoesPage() {
  const lista = useListarAvaliacoes();
  const deletar = useDeletarAvaliacao();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraDeletar, setParaDeletar] = useState<AvaliacaoListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<AvaliacaoListItem>[] = [
    {
      chave: 'viagem',
      cabecalho: 'Viagem',
      render: (a) => (
        <div>
          <div className="text-sm text-gray-900">{a.pacienteNome ?? 'Paciente'}</div>
          <div className="text-xs text-gray-500">
            {formatarDia(a.dataViagem)} · {a.trecho === 'Resgate' ? 'resgate (volta para casa)' : 'ida'}
          </div>
        </div>
      ),
    },
    {
      chave: 'quem-levou',
      cabecalho: 'Motorista · veículo do trecho',
      render: (a) => (
        <span className="text-sm text-gray-700">
          {a.motoristaNome ?? '—'}
          {a.veiculoPlaca ? <span className="text-gray-500"> · {a.veiculoPlaca}</span> : null}
        </span>
      ),
    },
    { chave: 'nota', cabecalho: 'Nota', render: (a) => <Estrelas nota={a.nota} /> },
    {
      chave: 'comentario',
      cabecalho: 'Comentário',
      render: (a) => (
        <span className="line-clamp-2 max-w-xs text-sm text-gray-700" title={a.comentario ?? undefined}>
          {a.comentario ?? '—'}
        </span>
      ),
    },
    {
      chave: 'canal',
      cabecalho: 'Por onde',
      render: (a) => (
        <span className="text-xs text-gray-600">{a.canal === 'AppCidadao' ? 'App do paciente' : 'Painel (equipe)'}</span>
      ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (a) => (
        <div className="flex items-center justify-end gap-1">
          {a.canal === 'AppCidadao' ? null : (
            <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: a.id })}>
              <Pencil className="w-3.5 h-3.5" /> Editar
            </BotaoLinhaAcao>
          )}
          <BotaoLinhaAcao tom="perigo" onClick={() => setParaDeletar(a)}>
            <Trash2 className="w-3.5 h-3.5" /> Excluir
          </BotaoLinhaAcao>
        </div>
      ),
    },
  ];

  async function confirmarDeletar() {
    if (!paraDeletar) return;
    setErroAcao(null);
    try {
      await deletar.mutateAsync(paraDeletar.id);
      setParaDeletar(null);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-1.5">
            <h1 className="text-2xl font-semibold text-gray-900">Avaliações</h1>
            <AjudaManual artigo="avaliacoes" />
          </div>
          <p className="mt-1 text-sm text-gray-600">
            Notas que os pacientes dão pelo app depois da viagem, com quem levou e em qual veículo.
          </p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Registrar avaliação
        </Button>
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(a) => a.id}
        carregando={lista.isLoading}
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Registrar avaliação' : 'Editar avaliação'}
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioAvaliacao
            modo={estado.tipo}
            idAvaliacao={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDeletar)}
        titulo="Excluir avaliação"
        mensagem="Esta ação remove a avaliação permanentemente."
        destrutivo
        rotuloConfirmar="Excluir"
        carregando={deletar.isPending}
        aoConfirmar={confirmarDeletar}
        aoCancelar={() => {
          setParaDeletar(null);
          setErroAcao(null);
        }}
      />

      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}
    </div>
  );
}
