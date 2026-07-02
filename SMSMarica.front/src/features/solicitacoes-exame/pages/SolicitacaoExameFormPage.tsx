import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstanteData, paraInputLocalDeUtc, paraUtcDeLocal } from '@/shared/lib/datas';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { useTipoExamePorId } from '@/features/tipos-exame/api/queries';
import {
  useAtualizarSolicitacao,
  useCadastrarSolicitacao,
  useSolicitacaoPorId,
  useSolicitacoesRecentesPaciente,
} from '@/features/solicitacoes-exame/api/queries';
import { SeletorTipoExame } from '@/features/solicitacoes-exame/components/SeletorTipoExame';
import type { PrioridadeSolicitacao } from '@/features/solicitacoes-exame/types';

type EstadoForm = {
  pacienteId: string;
  pacienteNome: string;
  tipoExameId: string;
  unidadeId: string;
  unidadeSolicitanteId: string;
  solicitanteNome: string;
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
  unidadeSolicitanteId: '',
  solicitanteNome: '',
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
  const location = useLocation();
  const { id } = useParams<{ id: string }>();
  const ehNovo = !id || id === 'novo';
  const podeCadastrarPaciente = usePermissao('Pacientes', 'Inclusao');

  const detalhe = useSolicitacaoPorId(ehNovo ? null : id ?? null);
  const cadastrar = useCadastrarSolicitacao();
  const atualizar = useAtualizarSolicitacao();
  const unidades = useListarUnidades();

  const [estado, setEstado] = useState<EstadoForm>(ESTADO_INICIAL);
  const [erro, setErro] = useState<string | null>(null);
  const [confirmarDuplicata, setConfirmarDuplicata] = useState(false);

  // Aviso de duplicada: solicitações deste paciente nos últimos 10 dias (exclui
  // canceladas). Pede confirmação antes de criar outra.
  const dataInicial10d = useMemo(() => {
    const d = new Date();
    d.setDate(d.getDate() - 10);
    return d.toISOString().slice(0, 10);
  }, []);
  const recentes = useSolicitacoesRecentesPaciente(
    ehNovo ? estado.pacienteId || null : null,
    dataInicial10d,
  );
  const duplicatas = useMemo(
    () => (recentes.data ?? []).filter((s) => s.status !== 'Cancelada'),
    [recentes.data],
  );

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
        unidadeSolicitanteId: s.unidadeSolicitanteId ?? '',
        solicitanteNome: s.solicitanteNome,
        codigoSolicitacao: s.codigoSolicitacao ?? '',
        chaveConfirmacao: s.chaveConfirmacao ?? '',
        justificativa: s.justificativa ?? '',
        prioridade: s.prioridade,
        observacoes: s.observacoes ?? '',
        dataAgendada: paraInputLocalDeUtc(s.dataAgendada),
      });
    }
  }, [detalhe.data]);

  // Paciente recém-cadastrado vindo da tela de cadastro (fluxo "Cadastrar
  // paciente" → "Criar solicitação"): pré-seleciona sem precisar buscar de novo.
  useEffect(() => {
    const st = (location.state as { pacienteCriado?: { id: string; nomeCompleto: string } } | null) ?? null;
    if (ehNovo && st?.pacienteCriado) {
      setEstado((s) => ({
        ...s,
        pacienteId: st.pacienteCriado!.id,
        pacienteNome: st.pacienteCriado!.nomeCompleto,
      }));
      // Limpa o state para um refresh não re-aplicar.
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [ehNovo, location.state, location.pathname, navigate]);

  function up<K extends keyof EstadoForm>(k: K, v: EstadoForm[K]) {
    setEstado((s) => ({ ...s, [k]: v }));
  }

  async function salvar(forcarDuplicata = false) {
    setErro(null);
    if (!estado.pacienteId) return setErro('Selecione o paciente.');
    if (!estado.tipoExameId) return setErro('Selecione o tipo de exame.');
    if (!estado.unidadeId) return setErro('Selecione a unidade executora.');
    if (!estado.solicitanteNome.trim()) return setErro('Informe o solicitante.');
    if (!regulacaoValida(estado.codigoSolicitacao))
      return setErro('Código de Solicitação inválido: use 0000 (emergência extra-SUS) ou um número a partir de 9999.');
    if (!regulacaoValida(estado.chaveConfirmacao))
      return setErro('Chave de Confirmação inválida: use 0000 (emergência extra-SUS) ou um número a partir de 9999.');

    // Já existe solicitação recente para este paciente → confirma antes de criar outra.
    if (ehNovo && !forcarDuplicata && duplicatas.length > 0) {
      setConfirmarDuplicata(true);
      return;
    }

    const payload = {
      tipoExameId: estado.tipoExameId,
      unidadeId: estado.unidadeId,
      unidadeSolicitanteId: estado.unidadeSolicitanteId || null,
      solicitanteNome: estado.solicitanteNome.trim(),
      codigoSolicitacao: estado.codigoSolicitacao.trim() || null,
      chaveConfirmacao: estado.chaveConfirmacao.trim() || null,
      justificativa: estado.justificativa.trim() || null,
      prioridade: estado.prioridade,
      observacoes: estado.observacoes.trim() || null,
      dataAgendada: paraUtcDeLocal(estado.dataAgendada),
    };

    try {
      if (ehNovo) {
        await cadastrar.mutateAsync({
          ...payload,
          pacienteId: estado.pacienteId,
        });
        notificar('Solicitação criada com sucesso.');
        navigate('/app/solicitacoes-exame', { replace: true });
      } else if (id) {
        await atualizar.mutateAsync({ id, payload });
        notificar('Solicitação atualizada com sucesso.');
        navigate(`/app/solicitacoes-exame/${id}`, { replace: true });
      }
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const salvando = cadastrar.isPending || atualizar.isPending;

  const mensagemDuplicata =
    duplicatas.length > 0
      ? `Este paciente já tem ${duplicatas.length} solicitação(ões) nos últimos 10 dias — a mais recente: ` +
        `${duplicatas[0].tipoExameNome} em ${formatarInstanteData(duplicatas[0].criadoEm)} ` +
        `(${duplicatas[0].status}). Deseja criar outra mesmo assim?`
      : '';

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
        <Button onClick={() => salvar()} disabled={salvando}>
          {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          {ehNovo ? 'Criar Solicitação' : 'Salvar'}
        </Button>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {/* 1) Paciente */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">1. Paciente</h2>
        {estado.pacienteId ? (
          <div className="space-y-2">
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
            {ehNovo && duplicatas.length > 0 ? (
              <div className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                ⚠ Este paciente já tem {duplicatas.length} solicitação(ões) nos últimos 10 dias. Ao salvar, será
                pedida confirmação.
              </div>
            ) : null}
          </div>
        ) : (
          <BuscaPaciente
            aoSelecionar={(p) => {
              up('pacienteId', p.id);
              up('pacienteNome', p.nomeCompleto);
            }}
            aoCadastrarPaciente={
              podeCadastrarPaciente
                ? (termo) =>
                    navigate('/app/pacientes/novo', { state: { termo, origem: 'solicitacao' } })
                : undefined
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

          <Campo label="Unidade solicitante (opcional)" htmlFor="unidade-solic" className="sm:col-span-2">
            <Select
              id="unidade-solic"
              value={estado.unidadeSolicitanteId}
              onChange={(e) => up('unidadeSolicitanteId', e.target.value)}
              disabled={unidades.isPending}
            >
              <option value="">— Nenhuma —</option>
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

      {/* 3) Solicitante (texto livre) */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">3. Solicitante</h2>
        <Campo label="Solicitante" htmlFor="solicitante" required>
          <Input
            id="solicitante"
            value={estado.solicitanteNome}
            onChange={(e) => setEstado((s) => ({ ...s, solicitanteNome: e.target.value }))}
            placeholder="Nome do profissional/unidade solicitante"
            maxLength={200}
          />
        </Campo>
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

      <ConfirmDialog
        aberto={confirmarDuplicata}
        titulo="Solicitação recente para este paciente"
        mensagem={mensagemDuplicata}
        rotuloConfirmar="Criar mesmo assim"
        rotuloCancelar="Cancelar"
        aoConfirmar={() => {
          setConfirmarDuplicata(false);
          salvar(true);
        }}
        aoCancelar={() => setConfirmarDuplicata(false)}
      />
    </div>
  );
}
