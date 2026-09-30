import { useState } from 'react';
import { ArrowLeft, Check, Loader2 } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { hojeSP } from '@/shared/lib/datas';
import { descreverDias } from '@/shared/lib/diasSemana';
import { formatarDuracao } from '@/shared/lib/tempoMedio';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { usePermissao } from '@/shared/auth/authStore';
import { ListaAcompanhantes } from '@/features/acompanhantes/components/ListaAcompanhantes';
import {
  useCadastrarTratamento,
  useOpcoesUnidadesAtendimento,
  usePreviaAgenda,
  useTiposTratamento,
} from '@/features/tratamentos/api/queries';
import { CampoLimiteAcompanhantes } from '@/features/tratamentos/components/CampoLimiteAcompanhantes';
import { CamposAgenda } from '@/features/tratamentos/components/CamposAgenda';
import { CamposNecessidades } from '@/features/tratamentos/components/CamposNecessidades';
import { ChipsNecessidades } from '@/features/tratamentos/components/ChipsNecessidades';
import { formatarDataBr, paraAgendaPayload, type EstadoAgenda } from '@/features/tratamentos/lib/agenda';
import { necessidadesValidas, paraNecessidadesPayload } from '@/features/tratamentos/lib/necessidades';
import {
  SEM_NECESSIDADES,
  type Necessidades,
  type RegraAcompanhantesPayload,
} from '@/features/tratamentos/types';
import type { PacienteListItem } from '@/features/pacientes/types';

type Passo = 'paciente' | 'dados' | 'condicao' | 'agenda' | 'revisao';

export function TratamentoFormPage() {
  const navigate = useNavigate();
  const destinos = useOpcoesUnidadesAtendimento();
  const podeCadastrarDestino = usePermissao('UnidadesAtendimento', 'Inclusao');
  const podeEditarAcompanhantes = usePermissao('Tratamentos', 'Edicao');
  const tipos = useTiposTratamento();
  const cadastrar = useCadastrarTratamento();

  const [passo, setPasso] = useState<Passo>('paciente');
  const [paciente, setPaciente] = useState<PacienteListItem | null>(null);
  const [dados, setDados] = useState({
    tipoTratamentoId: '',
    unidadeAtendimentoId: '',
    descricao: '',
    observacoes: '',
  });
  const [necessidades, setNecessidades] = useState<Necessidades>(SEM_NECESSIDADES);
  const [regraAcompanhantes, setRegraAcompanhantes] = useState<RegraAcompanhantesPayload>({
    quantidade: 1,
    justificativaSegundo: null,
  });
  const [agenda, setAgenda] = useState<EstadoAgenda>({
    dataInicio: hojeSP(),
    diasSemanaMascara: (1 << 1) | (1 << 3) | (1 << 5), // Seg, Qua e Sex
    continuo: false,
    quantidade: '12',
  });
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);

  const agendaPayload = paraAgendaPayload(agenda);
  const previa = usePreviaAgenda(agendaPayload);
  const totalPrevisto = agendaPayload ? (previa.data?.datas.length ?? 0) : 0;

  const opcoesDestino = destinos.data ?? [];
  const destinoEscolhido = opcoesDestino.find((u) => u.id === dados.unidadeAtendimentoId);
  const tipoEscolhido = (tipos.data ?? []).find((t) => t.id === dados.tipoTratamentoId);

  const podeAvancarDados =
    Boolean(paciente) &&
    Boolean(dados.tipoTratamentoId) &&
    Boolean(dados.unidadeAtendimentoId) &&
    dados.descricao.trim().length > 0;
  const condicaoOk =
    necessidadesValidas(necessidades) &&
    (regraAcompanhantes.quantidade === 1 || Boolean(regraAcompanhantes.justificativaSegundo?.trim()));
  const agendaOk = agendaPayload !== null && totalPrevisto > 0;
  const podeConfirmar = podeAvancarDados && condicaoOk && agendaOk;

  async function confirmar() {
    if (!paciente || !agendaPayload) return;
    setErroGlobal(null);
    try {
      const id = await cadastrar.mutateAsync({
        pacienteId: paciente.id,
        unidadeAtendimentoId: dados.unidadeAtendimentoId,
        tipoTratamentoId: dados.tipoTratamentoId,
        descricao: dados.descricao.trim(),
        observacoes: dados.observacoes.trim() || null,
        agenda: agendaPayload,
        necessidades: paraNecessidadesPayload(necessidades),
        acompanhantes: regraAcompanhantes,
      });
      navigate(`/app/tratamentos/${id}`, { replace: true });
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <button
          type="button"
          onClick={() => navigate('/app/tratamentos')}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
          aria-label="Voltar"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <div className="flex items-center gap-1.5">
            <h1 className="text-2xl font-semibold text-gray-900">Novo atendimento</h1>
            <AjudaManual artigo="atendimentos-transporte" secao="cadastrar" />
          </div>
          <p className="text-sm text-gray-600">
            {paciente ? `Paciente: ${paciente.nomeCompleto}` : 'Selecione o paciente para começar.'}
          </p>
        </div>
      </header>

      <BarraPassos passo={passo} />

      {passo === 'paciente' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-2 text-base font-medium text-gray-900">1. Paciente</h2>
          <p className="mb-4 text-sm text-gray-600">
            Busque por nome (qualquer parte) ou CPF. Resultados limitados a 20.
          </p>
          <BuscaPaciente
            aoSelecionar={(p) => {
              setPaciente(p);
              setPasso('dados');
            }}
          />
        </div>
      ) : null}

      {passo === 'dados' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-medium text-gray-900">2. Dados do atendimento</h2>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Campo
              label="Tipo de tratamento"
              htmlFor="tipo"
              required
              erro={
                tipoEscolhido && tipoEscolhido.tempoMedioMinutos == null
                  ? 'Este tipo ainda não tem tempo médio. Dá para seguir, mas peça a quem cuida de Tipos de tratamento para preencher.'
                  : undefined
              }
              dica={
                tipoEscolhido?.tempoMedioMinutos != null
                  ? `Tempo médio do tipo: ${formatarDuracao(tipoEscolhido.tempoMedioMinutos)} (da chegada à liberação).`
                  : 'O tempo médio vem do tipo de tratamento.'
              }
            >
              <Select
                id="tipo"
                value={dados.tipoTratamentoId}
                onChange={(e) => setDados((d) => ({ ...d, tipoTratamentoId: e.target.value }))}
              >
                <option value="">— Selecione —</option>
                {(tipos.data ?? []).map((t) => (
                  <option key={t.id} value={t.id}>{t.nome}</option>
                ))}
              </Select>
            </Campo>
            <Campo
              label="Unidade de atendimento (destino)"
              htmlFor="unidade"
              required
              erro={
                destinoEscolhido && !destinoEscolhido.temCoordenada
                  ? 'Esta unidade está sem ponto no mapa — a rota não terá destino até alguém marcar.'
                  : undefined
              }
              dica={
                !destinos.isLoading && opcoesDestino.length === 0 ? (
                  <>
                    Nenhuma unidade de atendimento cadastrada.{' '}
                    {podeCadastrarDestino ? (
                      <Link to="/app/unidades-atendimento/novo" className="text-red-700 hover:underline">
                        Cadastrar agora
                      </Link>
                    ) : (
                      'Peça a quem cuida do cadastro de destinos.'
                    )}
                  </>
                ) : (
                  'Onde o paciente é atendido — o fim da rota da van.'
                )
              }
            >
              <Select
                id="unidade"
                value={dados.unidadeAtendimentoId}
                onChange={(e) => setDados((d) => ({ ...d, unidadeAtendimentoId: e.target.value }))}
              >
                <option value="">{destinos.isLoading ? 'Carregando…' : '— Selecione —'}</option>
                {opcoesDestino.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.nome}
                    {u.cidade ? ` — ${u.cidade}${u.uf ? `/${u.uf}` : ''}` : ''}
                  </option>
                ))}
              </Select>
            </Campo>
            <Campo label="Descrição" htmlFor="descricao" required className="md:col-span-2"
              dica="Texto curto que identifica esse atendimento (aparece nas listagens).">
              <Input
                id="descricao"
                value={dados.descricao}
                maxLength={500}
                onChange={(e) => setDados((d) => ({ ...d, descricao: e.target.value }))}
                placeholder="Ex: Hemodiálise — 3x/sem"
              />
            </Campo>
            <Campo label="Observações" htmlFor="obs" className="md:col-span-2">
              <textarea
                id="obs"
                className="input min-h-[80px]"
                value={dados.observacoes}
                onChange={(e) => setDados((d) => ({ ...d, observacoes: e.target.value }))}
              />
            </Campo>
          </div>
          <div className="mt-6 flex justify-between">
            <Button variante="ghost" onClick={() => setPasso('paciente')}>Voltar</Button>
            <Button disabled={!podeAvancarDados} onClick={() => setPasso('condicao')}>Avançar</Button>
          </div>
        </div>
      ) : null}

      {passo === 'condicao' && paciente ? (
        <div className="space-y-6 rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <div>
            <h2 className="mb-1 text-base font-medium text-gray-900">3. Condição do paciente e acompanhantes</h2>
            <p className="mb-4 text-sm text-gray-600">
              Para quem monta a rota escolher o veículo e os lugares. Marque só o que se aplica.
            </p>
            <CamposNecessidades valor={necessidades} aoMudar={setNecessidades} />
          </div>
          <div className="border-t border-gray-100 pt-5">
            <CampoLimiteAcompanhantes valor={regraAcompanhantes} aoMudar={setRegraAcompanhantes} />
          </div>
          <div className="border-t border-gray-100 pt-5">
            <ListaAcompanhantes pacienteId={paciente.id} podeEditar={podeEditarAcompanhantes} />
          </div>
          <div className="flex justify-between">
            <Button variante="ghost" onClick={() => setPasso('dados')}>Voltar</Button>
            <Button disabled={!condicaoOk} onClick={() => setPasso('agenda')}>Avançar</Button>
          </div>
        </div>
      ) : null}

      {passo === 'agenda' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-medium text-gray-900">4. Agenda</h2>
          <CamposAgenda valor={agenda} aoMudar={setAgenda} />
          <div className="mt-6 flex justify-between">
            <Button variante="ghost" onClick={() => setPasso('condicao')}>Voltar</Button>
            <Button onClick={() => setPasso('revisao')} disabled={!agendaOk}>Avançar</Button>
          </div>
        </div>
      ) : null}

      {passo === 'revisao' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-medium text-gray-900">5. Confirmação</h2>
          <dl className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Item rotulo="Paciente" valor={paciente?.nomeCompleto ?? '—'} />
            <Item rotulo="Unidade de atendimento" valor={destinoEscolhido?.nome ?? '—'} />
            <Item rotulo="Tipo" valor={tipoEscolhido?.nome ?? '—'} />
            <Item rotulo="Tempo médio (do tipo)" valor={formatarDuracao(tipoEscolhido?.tempoMedioMinutos)} />
            <Item rotulo="Descrição" valor={dados.descricao} />
            <Item
              rotulo="Agenda"
              valor={`${descreverDias(agenda.diasSemanaMascara)} a partir de ${formatarDataBr(agenda.dataInicio)} · ${
                agenda.continuo ? 'contínuo (renova todo mês)' : `${agenda.quantidade} sessões`
              }`}
            />
            <Item rotulo="Sessões criadas agora" valor={String(totalPrevisto)} />
            <Item
              rotulo="Acompanhantes por viagem"
              valor={regraAcompanhantes.quantidade === 2 ? '2 (liberado com justificativa)' : '1'}
            />
            <div className="md:col-span-2">
              <dt className="text-xs uppercase tracking-wide text-gray-500">Condição do paciente</dt>
              <dd className="mt-1">
                <ChipsNecessidades necessidades={necessidades} mostrarVazio />
              </dd>
            </div>
          </dl>

          {erroGlobal ? (
            <div role="alert" className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erroGlobal}
            </div>
          ) : null}

          <div className="mt-6 flex justify-between">
            <Button variante="ghost" onClick={() => setPasso('agenda')}>Voltar</Button>
            <Button onClick={confirmar} disabled={!podeConfirmar || cadastrar.isPending}>
              {cadastrar.isPending ? (
                <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Salvando…</>
              ) : (
                <><Check className="mr-2 h-4 w-4" /> Cadastrar atendimento</>
              )}
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

const PASSOS: { id: Passo; label: string }[] = [
  { id: 'paciente', label: 'Paciente' },
  { id: 'dados', label: 'Dados' },
  { id: 'condicao', label: 'Condição e acompanhantes' },
  { id: 'agenda', label: 'Agenda' },
  { id: 'revisao', label: 'Confirmação' },
];

function BarraPassos({ passo }: { passo: Passo }) {
  const atual = PASSOS.findIndex((p) => p.id === passo);
  return (
    <ol className="flex flex-wrap items-center gap-4 text-sm">
      {PASSOS.map((e, i) => (
        <li key={e.id} className="flex items-center gap-2">
          <span
            className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-semibold ${
              i === atual ? 'bg-red-600 text-white' : i < atual ? 'bg-green-600 text-white' : 'bg-gray-200 text-gray-600'
            }`}
          >
            {i + 1}
          </span>
          <span className={i === atual ? 'font-medium text-gray-900' : 'text-gray-500'}>{e.label}</span>
          {i < PASSOS.length - 1 ? <span className="text-gray-300">›</span> : null}
        </li>
      ))}
    </ol>
  );
}

function Item({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</dt>
      <dd className="text-sm font-medium text-gray-900">{valor}</dd>
    </div>
  );
}
