import { useState } from 'react';
import { Eye, Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { descreverDias } from '@/shared/lib/diasSemana';
import { formatarDuracao } from '@/shared/lib/tempoMedio';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useEncerrarTratamento,
  useListarTratamentos,
} from '@/features/tratamentos/api/queries';
import { formatarDataBr } from '@/features/tratamentos/lib/agenda';
import type { TratamentoListItem } from '@/features/tratamentos/types';

export function TratamentosPage() {
  const navigate = useNavigate();
  const lista = useListarTratamentos();
  const encerrar = useEncerrarTratamento();
  const [paraEncerrar, setParaEncerrar] = useState<TratamentoListItem | null>(null);
  const [erroEncerrar, setErroEncerrar] = useState<string | null>(null);

  const visiveis = (lista.data ?? []).filter((t) => t.ativo);

  const colunas: Coluna<TratamentoListItem>[] = [
    { chave: 'paciente', cabecalho: 'Paciente', render: (t) => t.pacienteNome },
    {
      chave: 'tipo',
      cabecalho: 'Tipo',
      render: (t) => t.tipoTratamentoNome ?? t.descricao,
    },
    { chave: 'unidade', cabecalho: 'Unidade de atendimento', render: (t) => t.unidadeAtendimentoNome },
    { chave: 'dias', cabecalho: 'Dias', render: (t) => descreverDias(t.diasSemanaMascara) || '—' },
    { chave: 'tempo', cabecalho: 'Tempo médio', render: (t) => formatarDuracao(t.tempoMedioMinutos) },
    {
      chave: 'proxima',
      cabecalho: 'Próxima sessão',
      render: (t) => (t.proximaSessao ? formatarDataBr(t.proximaSessao) : '—'),
    },
    {
      chave: 'progresso',
      cabecalho: 'Progresso',
      render: (t) => (
        <span className="text-xs text-gray-600">
          {t.continuo ? `contínuo · ${t.sessoesRealizadas} realizadas` : `${t.sessoesRealizadas}/${t.totalSessoes}`}
        </span>
      ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (t) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => navigate(`/app/tratamentos/${t.id}`)}>
            <Eye className="h-3.5 w-3.5" /> Abrir
          </BotaoLinhaAcao>
          {t.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaEncerrar(t)}>
              <Trash2 className="h-3.5 w-3.5" /> Encerrar
            </BotaoLinhaAcao>
          ) : null}
        </div>
      ),
    },
  ];

  async function confirmar() {
    if (!paraEncerrar) return;
    setErroEncerrar(null);
    try {
      await encerrar.mutateAsync(paraEncerrar.id);
      setParaEncerrar(null);
    } catch (e) {
      setErroEncerrar(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-1.5">
            <h1 className="text-2xl font-semibold text-gray-900">Atendimentos</h1>
            <AjudaManual artigo="atendimentos-transporte" />
          </div>
          <p className="mt-1 text-sm text-gray-600">
            Quem o transporte leva, para onde e em que dias — com a condição do paciente e os acompanhantes.
          </p>
        </div>
        <Button onClick={() => navigate('/app/tratamentos/novo')}>
          <Plus className="h-4 w-4" />
          Novo atendimento
        </Button>
      </header>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela colunas={colunas} dados={visiveis} chaveLinha={(t) => t.id} carregando={lista.isLoading} />

      <ConfirmDialog
        aberto={Boolean(paraEncerrar)}
        titulo="Encerrar atendimento"
        mensagem={
          paraEncerrar
            ? `Encerrar o atendimento de "${paraEncerrar.pacienteNome}" (${paraEncerrar.descricao})? As sessões futuras que ainda não estão numa rota são canceladas, e o contínuo para de renovar.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Encerrar"
        carregando={encerrar.isPending}
        erro={erroEncerrar}
        aoConfirmar={confirmar}
        aoCancelar={() => { setParaEncerrar(null); setErroEncerrar(null); }}
      />
    </div>
  );
}
