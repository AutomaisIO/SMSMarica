import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Megaphone, Pencil, Plus, RotateCcw, Send, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, paraInputLocalDeUtc, paraUtcDeLocal } from '@/shared/lib/datas';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { listarUnidades } from '@/features/unidades/api/unidadesApi';
import {
  useAlcanceCampanha,
  useCampanhas,
  useEnviarCampanha,
  useExcluirCampanha,
  useSalvarCampanha,
} from '@/features/mensageria/api/campanhasApi';
import { CLASSE_RESPOSTA, CLASSE_STATUS, ROTULO_RESPOSTA, ROTULO_STATUS, rotuloCanal } from '@/features/mensageria/lib/rotulos';
import type {
  Campanha,
  CampanhaAlcanceItem,
  ModoEnvioCampanha,
  SalvarCampanha,
  StatusConfirmacao,
  StatusNotificacao,
} from '@/features/mensageria/types';

/**
 * Campanhas (ADR-0062). Um período em que o que a regulação agenda para uma unidade do SISREG é
 * atendido em outro lugar (ex.: unidade móvel agendada como Secretaria). O paciente recebe o local
 * e o endereço da campanha; a conferência de CPF e nascimento é opcional; o envio sai pelo botão
 * e o alcance é acompanhado aqui, com reenvio para quem não respondeu.
 */
export function AbaCampanhas() {
  const podeIncluir = usePermissao('NotificacoesAgendamento', 'Inclusao');
  const podeEditar = usePermissao('NotificacoesAgendamento', 'Edicao');
  const campanhas = useCampanhas();
  const [selecionadaId, setSelecionadaId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<{ aberta: boolean; campanha: Campanha | null }>({ aberta: false, campanha: null });

  // Abre a campanha mais recente por padrão — na prática, a que está rodando.
  useEffect(() => {
    if (!selecionadaId && campanhas.data && campanhas.data.length > 0) setSelecionadaId(campanhas.data[0].id);
  }, [campanhas.data, selecionadaId]);

  const selecionada = campanhas.data?.find((c) => c.id === selecionadaId) ?? null;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-3xl text-sm text-gray-600">
          Durante a campanha, todo agendamento da unidade no período recebe o <strong>local e o endereço da campanha</strong>
          {' '}no lugar dos da unidade do SISREG — na mensagem, no app e nas respostas do robô.
        </p>
        {podeIncluir ? (
          <Button tamanho="sm" onClick={() => setEdicao({ aberta: true, campanha: null })}>
            <Plus className="mr-1.5 h-4 w-4" /> Nova campanha
          </Button>
        ) : null}
      </div>

      {campanhas.isLoading ? <p className="text-sm text-gray-500">Carregando…</p> : null}
      {campanhas.data && campanhas.data.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 bg-white p-8 text-center text-sm text-gray-500">
          Nenhuma campanha cadastrada.
        </div>
      ) : null}

      {campanhas.data && campanhas.data.length > 0 ? (
        <div className="flex flex-wrap gap-2">
          {campanhas.data.map((c) => (
            <button
              key={c.id}
              type="button"
              onClick={() => setSelecionadaId(c.id)}
              className={`rounded-lg border px-3 py-2 text-left text-sm transition ${
                c.id === selecionadaId ? 'border-primary-500 bg-primary-50' : 'border-gray-200 bg-white hover:border-gray-300'
              }`}
            >
              <span className="flex items-center gap-2 font-medium text-gray-900">
                <Megaphone className="h-4 w-4 text-primary-600" /> {c.nome}
                {!c.ativa ? <span className="badge badge-gray">desligada</span> : null}
              </span>
              <span className="block text-xs text-gray-500">
                {formatarInstante(c.inicioEm)} → {formatarInstante(c.fimEm)}
              </span>
            </button>
          ))}
        </div>
      ) : null}

      {selecionada ? (
        <DetalheCampanha
          campanha={selecionada}
          podeEditar={podeEditar}
          aoEditar={() => setEdicao({ aberta: true, campanha: selecionada })}
          aoExcluir={() => setSelecionadaId(null)}
        />
      ) : null}

      <ModalCampanha
        aberto={edicao.aberta}
        campanha={edicao.campanha}
        aoFechar={() => setEdicao({ aberta: false, campanha: null })}
        aoSalvar={(id) => {
          setSelecionadaId(id);
          setEdicao({ aberta: false, campanha: null });
        }}
      />
    </div>
  );
}

type Filtro = 'todos' | 'semResposta' | 'confirmados' | 'naoVao' | 'problemas' | 'semMensagem';

const FILTROS: { id: Filtro; rotulo: string; aplica: (i: CampanhaAlcanceItem) => boolean }[] = [
  { id: 'todos', rotulo: 'Todos', aplica: () => true },
  { id: 'semResposta', rotulo: 'Sem resposta', aplica: (i) => !!i.enviadoEm && i.statusConfirmacao === 'Pendente' },
  { id: 'confirmados', rotulo: 'Confirmaram', aplica: (i) => i.statusConfirmacao === 'Confirmada' },
  { id: 'naoVao', rotulo: 'Não vão', aplica: (i) => i.statusConfirmacao === 'Cancelada' },
  {
    id: 'problemas',
    rotulo: 'Falha / sem celular',
    aplica: (i) => ['Falha', 'SemTelefoneValido', 'AguardandoCorrecaoContato'].includes(i.statusComunicacao ?? ''),
  },
  { id: 'semMensagem', rotulo: 'Sem mensagem', aplica: (i) => !i.comunicacaoId },
];

function DetalheCampanha({
  campanha,
  podeEditar,
  aoEditar,
  aoExcluir,
}: {
  campanha: Campanha;
  podeEditar: boolean;
  aoEditar: () => void;
  aoExcluir: () => void;
}) {
  const podeExcluir = usePermissao('NotificacoesAgendamento', 'Exclusao');
  const alcance = useAlcanceCampanha(campanha.id);
  const enviar = useEnviarCampanha();
  const excluir = useExcluirCampanha();
  const [filtro, setFiltro] = useState<Filtro>('todos');
  const [confirmarEnvio, setConfirmarEnvio] = useState<ModoEnvioCampanha | null>(null);
  const [resultadoEnvio, setResultadoEnvio] = useState<string | null>(null);

  const t = alcance.data?.totais;
  const itens = useMemo(
    () => (alcance.data?.itens ?? []).filter(FILTROS.find((f) => f.id === filtro)!.aplica),
    [alcance.data, filtro],
  );

  const colunas: Coluna<CampanhaAlcanceItem>[] = [
    { chave: 'data', cabecalho: 'Agendado para', render: (i) => formatarInstante(i.dataAgendada) },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (i) => (
        <div>
          <div className="font-medium text-gray-900">{i.pacienteNome ?? '—'}</div>
          <div className="text-xs text-gray-500">{i.codigoSolicitacao ? `SISREG ${i.codigoSolicitacao}` : ''}</div>
        </div>
      ),
    },
    { chave: 'procedimento', cabecalho: 'Procedimento', render: (i) => i.procedimento ?? '—' },
    { chave: 'telefone', cabecalho: 'Telefone', render: (i) => i.telefone ?? '—' },
    {
      chave: 'mensagem',
      cabecalho: 'Mensagem',
      render: (i) =>
        i.statusComunicacao ? (
          <span
            className={`badge ${CLASSE_STATUS[i.statusComunicacao as StatusNotificacao] ?? 'badge-gray'}`}
            title={i.motivoFalha ?? undefined}
          >
            {ROTULO_STATUS[i.statusComunicacao as StatusNotificacao] ?? i.statusComunicacao}
          </span>
        ) : (
          <span className="text-xs text-gray-400">nenhuma</span>
        ),
    },
    {
      chave: 'resposta',
      cabecalho: 'Resposta',
      render: (i) => (
        <span className={`badge ${CLASSE_RESPOSTA[i.statusConfirmacao as StatusConfirmacao] ?? 'badge-gray'}`}>
          {ROTULO_RESPOSTA[i.statusConfirmacao as StatusConfirmacao] ?? i.statusConfirmacao}
          {i.confirmadoCanal ? ` · ${rotuloCanal(i.confirmadoCanal)}` : ''}
        </span>
      ),
    },
  ];

  function dispararEnvio(modo: ModoEnvioCampanha) {
    setResultadoEnvio(null);
    enviar.mutate(
      { id: campanha.id, modo },
      {
        onSuccess: (r) => {
          setConfirmarEnvio(null);
          setResultadoEnvio(
            r.enfileirados === 0
              ? 'Ninguém se encaixava — nenhuma mensagem nova.'
              : `${r.enfileirados} mensagem(ns) na fila. Saem no ritmo configurado; acompanhe abaixo.`,
          );
        },
      },
    );
  }

  return (
    <section className="space-y-4 rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1 text-sm">
          <h2 className="text-base font-semibold text-gray-900">{campanha.nome}</h2>
          <p className="text-gray-600">
            Agendados no SISREG para <strong>{campanha.unidadeNome ?? 'a unidade'}</strong> de{' '}
            {formatarInstante(campanha.inicioEm)} a {formatarInstante(campanha.fimEm)}
          </p>
          <p className="text-gray-600">
            O paciente vê: <strong>{campanha.localNome}</strong> — {campanha.localEndereco}
          </p>
          <p className="flex flex-wrap gap-2 pt-1">
            <span className={`badge ${campanha.ativa ? 'badge-success' : 'badge-gray'}`}>
              {campanha.ativa ? 'Ativa' : 'Desligada'}
            </span>
            <span className={`badge ${campanha.exigirConferenciaCadastral ? 'badge-info' : 'badge-warning'}`}>
              {campanha.exigirConferenciaCadastral ? 'Confere CPF e nascimento' : 'Entrega direta (sem conferência)'}
            </span>
            <span className="badge badge-gray">
              {campanha.envioAutomatico ? 'Envio automático ao importar' : 'Envio só pelo botão'}
            </span>
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {podeEditar ? (
            <>
              <Button tamanho="sm" disabled={!campanha.ativa} onClick={() => setConfirmarEnvio('NaoEnviados')}>
                <Send className="mr-1.5 h-4 w-4" /> Enviar
              </Button>
              <Button
                tamanho="sm"
                variante="outline"
                disabled={!campanha.ativa || !t || t.semResposta === 0}
                onClick={() => setConfirmarEnvio('NaoRespondidos')}
              >
                <RotateCcw className="mr-1.5 h-4 w-4" /> Reenviar a quem não respondeu
              </Button>
              <Button tamanho="sm" variante="ghost" onClick={aoEditar}>
                <Pencil className="mr-1.5 h-4 w-4" /> Editar
              </Button>
            </>
          ) : null}
          {podeExcluir ? (
            <Button
              tamanho="sm"
              variante="ghost"
              disabled={excluir.isPending}
              onClick={() => excluir.mutate(campanha.id, { onSuccess: aoExcluir })}
              title="Encerra a campanha: os agendamentos voltam a mostrar a unidade do SISREG"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          ) : null}
        </div>
      </div>

      {resultadoEnvio ? <p className="text-sm text-emerald-700">{resultadoEnvio}</p> : null}

      {t ? (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8">
          <Contador rotulo="Agendados" valor={t.agendados} />
          <Contador rotulo="Sem mensagem" valor={t.semMensagem} />
          <Contador rotulo="Na fila" valor={t.naFila} />
          <Contador rotulo="Enviadas" valor={t.enviados} />
          <Contador rotulo="Entregues" valor={t.entregues} />
          <Contador rotulo="Lidas" valor={t.lidos} />
          <Contador rotulo="Confirmaram" valor={t.confirmados} destaque="text-emerald-700" />
          <Contador rotulo="Não vão" valor={t.naoVao} destaque="text-amber-700" />
          <Contador rotulo="Sem resposta" valor={t.semResposta} />
          <Contador rotulo="Falhas" valor={t.falhas} destaque="text-red-700" />
          <Contador rotulo="Sem celular" valor={t.semTelefone} destaque="text-red-700" />
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {FILTROS.map((f) => (
          <button
            key={f.id}
            type="button"
            onClick={() => setFiltro(f.id)}
            className={`rounded-full border px-3 py-1 text-xs ${
              filtro === f.id ? 'border-primary-500 bg-primary-50 text-primary-700' : 'border-gray-200 text-gray-600'
            }`}
          >
            {f.rotulo} ({(alcance.data?.itens ?? []).filter(f.aplica).length})
          </button>
        ))}
      </div>

      <Tabela
        colunas={colunas}
        dados={itens}
        chaveLinha={(i) => i.solicitacaoId}
        carregando={alcance.isLoading}
        vazio="Nenhum agendamento nesta situação. Se a campanha acabou de ser criada, confira se a agenda da unidade já foi importada do SISREG."
      />

      <Modal
        aberto={confirmarEnvio !== null}
        aoFechar={() => setConfirmarEnvio(null)}
        titulo={confirmarEnvio === 'NaoRespondidos' ? 'Reenviar a quem não respondeu' : 'Enviar a mensagem da campanha'}
        largura="sm"
      >
        <div className="space-y-3 text-sm text-gray-700">
          {confirmarEnvio === 'NaoRespondidos' ? (
            <p>
              A mensagem é refeita para os <strong>{t?.semResposta ?? 0}</strong> pacientes que receberam e não
              responderam. O link da mensagem anterior deixa de valer.
            </p>
          ) : (
            <p>
              Sai agora, mesmo fora do horário de envio, para os agendamentos futuros da campanha que ainda
              não receberam nada ({t?.semMensagem ?? 0} sem mensagem, {t?.naFila ?? 0} esperando na fila).
            </p>
          )}
          {!campanha.exigirConferenciaCadastral ? (
            <p className="rounded bg-amber-50 p-2 text-amber-800">
              Entrega direta: a mensagem já leva data, local e endereço, sem conferir CPF. O link só confirma a
              presença — não abre o app de quem não tem o número verificado.
            </p>
          ) : null}
          {enviar.isError ? <p className="text-red-700">{extrairMensagemDeErro(enviar.error)}</p> : null}
          <div className="flex justify-end gap-2">
            <Button variante="ghost" onClick={() => setConfirmarEnvio(null)}>Cancelar</Button>
            <Button disabled={enviar.isPending} onClick={() => confirmarEnvio && dispararEnvio(confirmarEnvio)}>
              {enviar.isPending ? 'Enviando…' : 'Confirmar envio'}
            </Button>
          </div>
        </div>
      </Modal>
    </section>
  );
}

function Contador({ rotulo, valor, destaque }: { rotulo: string; valor: number; destaque?: string }) {
  return (
    <div className="rounded-md border border-gray-100 bg-gray-50 px-3 py-2">
      <div className="text-xs text-gray-500">{rotulo}</div>
      <div className={`text-lg font-semibold ${destaque ?? 'text-gray-900'}`}>{valor}</div>
    </div>
  );
}

const VAZIA: SalvarCampanha = {
  nome: '',
  unidadeId: '',
  inicioEm: '',
  fimEm: '',
  localNome: '',
  localEndereco: '',
  exigirConferenciaCadastral: false,
  envioAutomatico: true,
  ativa: true,
};

function ModalCampanha({
  aberto,
  campanha,
  aoFechar,
  aoSalvar,
}: {
  aberto: boolean;
  campanha: Campanha | null;
  aoFechar: () => void;
  aoSalvar: (id: string) => void;
}) {
  const salvar = useSalvarCampanha();
  const unidades = useQuery({ queryKey: ['unidades', 'lista'], queryFn: listarUnidades, enabled: aberto });
  const [f, setF] = useState<SalvarCampanha>(VAZIA);
  // Datas em hora de Brasília no formulário; a API recebe UTC.
  const [inicio, setInicio] = useState('');
  const [fim, setFim] = useState('');

  useEffect(() => {
    if (!aberto) return;
    salvar.reset();
    if (campanha) {
      setF({ ...campanha });
      setInicio(paraInputLocalDeUtc(campanha.inicioEm));
      setFim(paraInputLocalDeUtc(campanha.fimEm));
    } else {
      setF(VAZIA);
      setInicio('');
      setFim('');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aberto, campanha]);

  const set = <K extends keyof SalvarCampanha>(k: K, v: SalvarCampanha[K]) => setF((x) => ({ ...x, [k]: v }));

  function enviarFormulario() {
    salvar.mutate(
      { id: campanha?.id ?? null, dados: { ...f, inicioEm: paraUtcDeLocal(inicio) ?? '', fimEm: paraUtcDeLocal(fim) ?? '' } },
      { onSuccess: (id) => aoSalvar(id) },
    );
  }

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo={campanha ? 'Editar campanha' : 'Nova campanha'} largura="lg">
      <div className="space-y-4 text-sm">
        <label className="flex flex-col gap-1">
          <span className="font-medium text-gray-700">Nome da campanha</span>
          <Input value={f.nome} onChange={(e) => set('nome', e.target.value)} placeholder="ex.: Outubro Rosa 2026" />
        </label>

        <label className="flex flex-col gap-1">
          <span className="font-medium text-gray-700">Unidade em que é agendada no SISREG</span>
          <Select value={f.unidadeId} onChange={(e) => set('unidadeId', e.target.value)}>
            <option value="">Selecione…</option>
            {(unidades.data ?? [])
              .filter((u) => u.ativo || u.id === f.unidadeId)
              .sort((a, b) => a.nome.localeCompare(b.nome))
              .map((u) => (
                <option key={u.id} value={u.id}>{u.nome}</option>
              ))}
          </Select>
        </label>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <label className="flex flex-col gap-1">
            <span className="font-medium text-gray-700">Início</span>
            <Input type="datetime-local" value={inicio} onChange={(e) => setInicio(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="font-medium text-gray-700">Fim</span>
            <Input type="datetime-local" value={fim} onChange={(e) => setFim(e.target.value)} />
          </label>
        </div>

        <label className="flex flex-col gap-1">
          <span className="font-medium text-gray-700">Nome do local (o que o paciente vê)</span>
          <Input value={f.localNome} onChange={(e) => set('localNome', e.target.value)} placeholder="ex.: Carreta da Mulher" />
        </label>
        <label className="flex flex-col gap-1">
          <span className="font-medium text-gray-700">Endereço do local</span>
          <Input
            value={f.localEndereco}
            onChange={(e) => set('localEndereco', e.target.value)}
            placeholder="Rua, nº – Bairro, Cidade/UF"
          />
        </label>

        <div className="space-y-2 rounded-md border border-gray-100 bg-gray-50 p-3">
          <Chave
            ligada={f.exigirConferenciaCadastral}
            aoMudar={(v) => set('exigirConferenciaCadastral', v)}
            rotulo="Conferir CPF e data de nascimento antes de mostrar o agendamento"
            ajuda={
              f.exigirConferenciaCadastral
                ? 'Primeiro sai a mensagem curta; os dados só vão depois que a pessoa se identificar.'
                : 'A mensagem já sai com data, local e endereço. O link só confirma presença — só abre o app se o número for o verificado do paciente.'
            }
          />
          <Chave
            ligada={f.envioAutomatico}
            aoMudar={(v) => set('envioAutomatico', v)}
            rotulo="Avisar sozinho quem for importado do SISREG no período"
            ajuda="Desligado, a mensagem só sai quando alguém clicar em Enviar."
          />
          <Chave ligada={f.ativa} aoMudar={(v) => set('ativa', v)} rotulo="Campanha ativa" />
        </div>

        {salvar.isError ? <p className="text-red-700">{extrairMensagemDeErro(salvar.error)}</p> : null}
        <div className="flex justify-end gap-2">
          <Button variante="ghost" onClick={aoFechar}>Cancelar</Button>
          <Button disabled={salvar.isPending} onClick={enviarFormulario}>
            {salvar.isPending ? 'Salvando…' : 'Salvar'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

function Chave({ ligada, aoMudar, rotulo, ajuda }: { ligada: boolean; aoMudar: (v: boolean) => void; rotulo: string; ajuda?: string }) {
  return (
    <label className="flex cursor-pointer items-start gap-2">
      <input type="checkbox" className="mt-0.5" checked={ligada} onChange={(e) => aoMudar(e.target.checked)} />
      <span>
        <span className="font-medium text-gray-800">{rotulo}</span>
        {ajuda ? <span className="block text-xs text-gray-500">{ajuda}</span> : null}
      </span>
    </label>
  );
}
