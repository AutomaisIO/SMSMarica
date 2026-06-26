import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { useTipoExamePorId } from '@/features/tipos-exame/api/queries';
import {
  useAtualizarSolicitacao,
  useCadastrarSolicitacao,
  useSolicitacaoPorId,
} from '@/features/solicitacoes-exame/api/queries';
import { SeletorMedicoSolicitante } from '@/features/solicitacoes-exame/components/SeletorMedicoSolicitante';
import { SeletorTipoExame } from '@/features/solicitacoes-exame/components/SeletorTipoExame';
import type { PrioridadeSolicitacao } from '@/features/solicitacoes-exame/types';

type EstadoForm = {
  pacienteId: string;
  pacienteNome: string;
  tipoExameId: string;
  unidadeId: string;
  solicitanteUsuarioId: string | null;
  solicitanteNome: string;
  solicitanteCrm: string;
  solicitanteUfCrm: string;
  codigoSolicitacao: string;
  chaveConfirmacao: string;
  justificativa: string;
  prioridade: PrioridadeSolicitacao;
  observacoes: string;
  dataAgendada: string;
};

const ESTADO_INICIAL: EstadoForm = {
  pacienteId: '',
  pacienteNome: '',
  tipoExameId: '',
  unidadeId: '',
  solicitanteUsuarioId: null,
  solicitanteNome: '',
  solicitanteCrm: '',
  solicitanteUfCrm: '',
  codigoSolicitacao: '',
  chaveConfirmacao: '',
  justificativa: '',
  prioridade: 'Eletiva',
  observacoes: '',
  dataAgendada: '',
};

/**
 * Régua do Código de Solicitação / Chave de Confirmação (espelha o backend):
 * válido = sentinela "0000" (exame emergencial extra-SUS) OU número a partir de 9999.
 */
function regulacaoValida(v: string): boolean {
  const t = v.trim();
  if (t.length === 0) return false;
  if (t === '0000') return true;
  return /^\d+$/.test(t) && Number(t) >= 9999;
}

export function SolicitacaoExameFormPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const ehNovo = !id || id === 'novo';
  const podeCadastrarPaciente = usePermissao('Pacientes', 'Inclusao');

  const detalhe = useSolicitacaoPorId(ehNovo ? null : id ?? null);
  const cadastrar = useCadastrarSolicitacao();
  const atualizar = useAtualizarSolicitacao();
  const unidades = useListarUnidades();

  const [estado, setEstado] = useState<EstadoForm>(ESTADO_INICIAL);
  const [erro, setErro] = useState<string | null>(null);

  // Preview do tipo de exame escolhido (puxa modalidade + tempo).
  const tipoSelecionado = useTipoExamePorId(estado.tipoExameId || null);

  useEffect(() => {
    if (detalhe.data) {
      const s = detalhe.data;
      setEstado({
        pacienteId: s.pacienteId,
        pacienteNome: s.pacienteNome,
        tipoExameId: s.tipoExameId,
        unidadeId: s.unidadeId,
        solicitanteUsuarioId: s.solicitanteUsuarioId,
        solicitanteNome: s.solicitanteNome,
        solicitanteCrm: s.solicitanteCrm,
        solicitanteUfCrm: s.solicitanteUfCrm,
        codigoSolicitacao: s.codigoSolicitacao ?? '',
        chaveConfirmacao: s.chaveConfirmacao ?? '',
        justificativa: s.justificativa ?? '',
        prioridade: s.prioridade,
        observacoes: s.observacoes ?? '',
        dataAgendada: s.dataAgendada ? s.dataAgendada.slice(0, 16) : '',
      });
    }
  }, [detalhe.data]);

  function up<K extends keyof EstadoForm>(k: K, v: EstadoForm[K]) {
    setEstado((s) => ({ ...s, [k]: v }));
  }

  async function salvar() {
    setErro(null);
    if (!estado.pacienteId) return setErro('Selecione o paciente.');
    if (!estado.tipoExameId) return setErro('Selecione o tipo de exame.');
    if (!estado.unidadeId) return setErro('Selecione a unidade executora.');
    if (!estado.solicitanteNome.trim()) return setErro('Informe o médico solicitante.');
    if (!estado.solicitanteCrm.trim() || !estado.solicitanteUfCrm.trim())
      return setErro('Informe CRM e UF do solicitante.');
    if (!regulacaoValida(estado.codigoSolicitacao))
      return setErro('Código de Solicitação inválido: use 0000 (emergência extra-SUS) ou um número a partir de 9999.');
    if (!regulacaoValida(estado.chaveConfirmacao))
      return setErro('Chave de Confirmação inválida: use 0000 (emergência extra-SUS) ou um número a partir de 9999.');

    const payload = {
      tipoExameId: estado.tipoExameId,
      unidadeId: estado.unidadeId,
      solicitanteUsuarioId: estado.solicitanteUsuarioId,
      solicitanteNome: estado.solicitanteNome.trim(),
      solicitanteCrm: estado.solicitanteCrm.trim(),
      solicitanteUfCrm: estado.solicitanteUfCrm.trim(),
      codigoSolicitacao: estado.codigoSolicitacao.trim() || null,
      chaveConfirmacao: estado.chaveConfirmacao.trim() || null,
      justificativa: estado.justificativa.trim() || null,
      prioridade: estado.prioridade,
      observacoes: estado.observacoes.trim() || null,
      dataAgendada: estado.dataAgendada
        ? new Date(estado.dataAgendada).toISOString()
        : null,
    };

    try {
      if (ehNovo) {
        const novoId = await cadastrar.mutateAsync({
          ...payload,
          pacienteId: estado.pacienteId,
        });
        navigate(`/app/solicitacoes-exame/${novoId}`, { replace: true });
      } else if (id) {
        await atualizar.mutateAsync({ id, payload });
        navigate(`/app/solicitacoes-exame/${id}`, { replace: true });
      }
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const salvando = cadastrar.isPending || atualizar.isPending;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <button
            type="button"
            onClick={() => navigate(-1)}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" />
            Voltar
          </button>
          <h1 className="mt-1 text-2xl font-semibold text-gray-900">
            {ehNovo ? 'Nova solicitação de exame' : 'Editar solicitação'}
          </h1>
        </div>
        <Button onClick={salvar} disabled={salvando}>
          {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          {ehNovo ? 'Criar e gerar worklist' : 'Salvar'}
        </Button>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {/* 1) Paciente */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">1. Paciente</h2>
        {estado.pacienteId ? (
          <div className="flex items-center justify-between gap-3 rounded-md border border-gray-200 bg-gray-50 px-3 py-2">
            <div>
              <div className="font-medium text-gray-900">{estado.pacienteNome}</div>
              <div className="text-xs text-gray-500 font-mono">ID {estado.pacienteId}</div>
            </div>
            {ehNovo ? (
              <Button variante="outline" tamanho="sm" onClick={() => { up('pacienteId', ''); up('pacienteNome', ''); }}>
                Trocar
              </Button>
            ) : null}
          </div>
        ) : (
          <BuscaPaciente
            aoSelecionar={(p) => {
              up('pacienteId', p.id);
              up('pacienteNome', p.nomeCompleto);
            }}
            aoCadastrarPaciente={
              podeCadastrarPaciente ? () => navigate('/app/pacientes/novo') : undefined
            }
          />
        )}
      </section>

      {/* 2) Exame + Unidade */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">2. Exame</h2>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <Campo label="Tipo de exame" htmlFor="tipo" className="sm:col-span-2">
            <SeletorTipoExame id="tipo" valor={estado.tipoExameId} aoMudar={(id) => up('tipoExameId', id)} />
          </Campo>
          <Campo label="Unidade executora" htmlFor="unidade">
            <Select
              id="unidade"
              value={estado.unidadeId}
              onChange={(e) => up('unidadeId', e.target.value)}
              disabled={unidades.isPending}
            >
              <option value="">— Selecione —</option>
              {(unidades.data ?? []).filter((u) => u.ativo).map((u) => (
                <option key={u.id} value={u.id}>
                  {u.nome}
                </option>
              ))}
            </Select>
          </Campo>

          {tipoSelecionado.data ? (
            <div className="sm:col-span-3 text-xs text-gray-500">
              Modalidade DICOM: <strong className="text-gray-700">{tipoSelecionado.data.modalidadeDicom}</strong>
              {tipoSelecionado.data.tempoEstimadoMinutos
                ? ` · Tempo estimado: ${tipoSelecionado.data.tempoEstimadoMinutos} min`
                : ''}
              {' · SIGTAP '}
              <span className="font-mono">{tipoSelecionado.data.procedimentoSigtapCodigo}</span>
            </div>
          ) : null}

          <Campo label="Data/hora agendada (opcional)" htmlFor="data" className="sm:col-span-2">
            <Input
              id="data"
              type="datetime-local"
              value={estado.dataAgendada}
              onChange={(e) => up('dataAgendada', e.target.value)}
            />
          </Campo>
          <Campo label="Prioridade" htmlFor="prioridade">
            <Select
              id="prioridade"
              value={estado.prioridade}
              onChange={(e) => up('prioridade', e.target.value as PrioridadeSolicitacao)}
            >
              <option value="Eletiva">Eletiva</option>
              <option value="Prioritaria">Prioritária</option>
              <option value="Urgente">Urgente</option>
            </Select>
          </Campo>
        </div>
      </section>

      {/* 3) Médico solicitante */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">3. Médico solicitante</h2>
        <SeletorMedicoSolicitante
          valor={{
            solicitanteUsuarioId: estado.solicitanteUsuarioId,
            solicitanteNome: estado.solicitanteNome,
            solicitanteCrm: estado.solicitanteCrm,
            solicitanteUfCrm: estado.solicitanteUfCrm,
          }}
          aoMudar={(v) => setEstado((s) => ({ ...s, ...v }))}
        />
      </section>

      {/* 4) Regulação + observações */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">
          4. Regulação e observações
        </h2>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Campo
            label="Código de Solicitação"
            htmlFor="cod"
            erro={
              estado.codigoSolicitacao.trim() && !regulacaoValida(estado.codigoSolicitacao)
                ? 'Use 0000 (emergência extra-SUS) ou um número a partir de 9999.'
                : undefined
            }
          >
            <Input
              id="cod"
              value={estado.codigoSolicitacao}
              onChange={(e) => up('codigoSolicitacao', e.target.value)}
              inputMode="numeric"
              placeholder="Ex.: 12345 — ou 0000 (emergência)"
            />
          </Campo>
          <Campo
            label="Chave de Confirmação"
            htmlFor="chave"
            erro={
              estado.chaveConfirmacao.trim() && !regulacaoValida(estado.chaveConfirmacao)
                ? 'Use 0000 (emergência extra-SUS) ou um número a partir de 9999.'
                : undefined
            }
          >
            <Input
              id="chave"
              value={estado.chaveConfirmacao}
              onChange={(e) => up('chaveConfirmacao', e.target.value)}
              inputMode="numeric"
              placeholder="Ex.: 67890 — ou 0000 (emergência)"
            />
          </Campo>
          <Campo label="Justificativa clínica" htmlFor="just">
            <Input
              id="just"
              value={estado.justificativa}
              onChange={(e) => up('justificativa', e.target.value)}
              placeholder="Hipótese diagnóstica, motivo do exame"
            />
          </Campo>
          <Campo label="Observações" htmlFor="obs" className="sm:col-span-2">
            <Input
              id="obs"
              value={estado.observacoes}
              onChange={(e) => up('observacoes', e.target.value)}
              placeholder="Informações adicionais para o operador do equipamento"
            />
          </Campo>
        </div>
      </section>
    </div>
  );
}
