import { useEffect, useMemo, useState } from 'react';
import { Loader2, Plus, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import {
  useAtualizarAssunto,
  useCatalogoComandos,
  useCriarAssunto,
  useObterAssunto,
} from '@/features/robo-atendimento/api/queries';
import type {
  ComandoRobo,
  RoboAssuntoCondicao,
  RoboAssuntoTreino,
  SalvarRoboAssuntoPayload,
  TipoCondicaoRobo,
  TipoTreinoRobo,
} from '@/features/robo-atendimento/types';

const CAMPO_CLASSE =
  'w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-primary-500 focus:outline-none focus:ring-1 focus:ring-primary-500';

const TIPOS_CONDICAO: { valor: TipoCondicaoRobo; rotulo: string }[] = [
  { valor: 'PalavraChave', rotulo: 'Palavra-chave' },
  { valor: 'Frase', rotulo: 'Frase' },
  { valor: 'Regex', rotulo: 'Regex' },
];

const TIPOS_TREINO: { valor: TipoTreinoRobo; rotulo: string }[] = [
  { valor: 'Instrucao', rotulo: 'Instrução' },
  { valor: 'Exemplo', rotulo: 'Exemplo' },
  { valor: 'Glossario', rotulo: 'Glossário' },
  { valor: 'Do', rotulo: 'Deve fazer' },
  { valor: 'Dont', rotulo: 'Não deve fazer' },
];

const DIAS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

type FormAssunto = {
  nome: string;
  descricao: string;
  instrucoesPersona: string;
  modelo: string;
  ativo: boolean;
  todosDias: boolean;
  diasSemana: number;
  horarioInicio: string;
  horarioFim: string;
  maxInteracoesSemResolver: number;
  limiarConfianca: number;
  ordem: number;
  condicoes: RoboAssuntoCondicao[];
  treinos: RoboAssuntoTreino[];
  comandos: ComandoRobo[];
};

const FORM_VAZIO: FormAssunto = {
  nome: '',
  descricao: '',
  instrucoesPersona: '',
  modelo: '',
  ativo: true,
  todosDias: true,
  diasSemana: 0,
  horarioInicio: '',
  horarioFim: '',
  maxInteracoesSemResolver: 5,
  limiarConfianca: 0.6,
  ordem: 0,
  condicoes: [],
  treinos: [],
  comandos: [],
};

type Props = { assuntoId: string | null; aberto: boolean; aoFechar: () => void };

export function EditorAssunto({ assuntoId, aberto, aoFechar }: Props) {
  const detalhe = useObterAssunto(aberto ? assuntoId : null);
  const catalogo = useCatalogoComandos();
  const criar = useCriarAssunto();
  const atualizar = useAtualizarAssunto();

  const [form, setForm] = useState<FormAssunto>(FORM_VAZIO);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!aberto) return;
    setErro(null);
    if (!assuntoId) {
      setForm(FORM_VAZIO);
      return;
    }
    const a = detalhe.data;
    if (!a) return;
    setForm({
      nome: a.nome,
      descricao: a.descricao ?? '',
      instrucoesPersona: a.instrucoesPersona,
      modelo: a.modelo ?? '',
      ativo: a.ativo,
      todosDias: a.diasSemana == null,
      diasSemana: a.diasSemana ?? 0,
      horarioInicio: a.horarioInicio ? a.horarioInicio.slice(0, 5) : '',
      horarioFim: a.horarioFim ? a.horarioFim.slice(0, 5) : '',
      maxInteracoesSemResolver: a.maxInteracoesSemResolver,
      limiarConfianca: a.limiarConfianca,
      ordem: a.ordem,
      condicoes: a.condicoes.map((c) => ({ ...c })),
      treinos: a.treinos.map((t) => ({ ...t })),
      comandos: [...a.comandos],
    });
  }, [aberto, assuntoId, detalhe.data]);

  const salvando = criar.isPending || atualizar.isPending;
  const carregandoDetalhe = !!assuntoId && detalhe.isPending;

  function set<K extends keyof FormAssunto>(chave: K, valor: FormAssunto[K]) {
    setForm((f) => ({ ...f, [chave]: valor }));
  }

  function alternarDia(bit: number) {
    setForm((f) => ({ ...f, diasSemana: f.diasSemana ^ (1 << bit) }));
  }

  function alternarComando(cmd: ComandoRobo) {
    setForm((f) => ({
      ...f,
      comandos: f.comandos.includes(cmd) ? f.comandos.filter((c) => c !== cmd) : [...f.comandos, cmd],
    }));
  }

  function aoSalvar(ev: React.FormEvent) {
    ev.preventDefault();
    setErro(null);
    const payload: SalvarRoboAssuntoPayload = {
      nome: form.nome.trim(),
      descricao: form.descricao.trim() || null,
      instrucoesPersona: form.instrucoesPersona.trim(),
      modelo: form.modelo.trim() || null,
      ativo: form.ativo,
      horarioInicio: form.horarioInicio ? `${form.horarioInicio}:00` : null,
      horarioFim: form.horarioFim ? `${form.horarioFim}:00` : null,
      diasSemana: form.todosDias ? null : form.diasSemana,
      maxInteracoesSemResolver: form.maxInteracoesSemResolver,
      limiarConfianca: form.limiarConfianca,
      escalonamentoUnidadeId: null,
      ordem: form.ordem,
      condicoes: form.condicoes.map((c, i) => ({ ...c, valor: c.valor.trim(), ordem: i })),
      treinos: form.treinos.map((t, i) => ({
        ...t,
        titulo: t.titulo?.trim() || null,
        conteudo: t.conteudo.trim(),
        ordem: i,
      })),
      comandos: form.comandos,
    };
    const opcoes = {
      onSuccess: () => aoFechar(),
      onError: (err: unknown) => setErro(extrairMensagemDeErro(err)),
    };
    if (assuntoId) atualizar.mutate({ id: assuntoId, payload }, opcoes);
    else criar.mutate(payload, opcoes);
  }

  const comandosCatalogo = useMemo(() => catalogo.data ?? [], [catalogo.data]);

  return (
    <Modal
      aberto={aberto}
      aoFechar={aoFechar}
      titulo={assuntoId ? 'Editar assunto' : 'Novo assunto'}
      descricao="Como o robô atende este assunto: persona, treinos, condições de ativação e comandos liberados."
      largura="lg"
    >
      {carregandoDetalhe ? (
        <div className="flex items-center gap-2 py-10 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
        </div>
      ) : (
        <form onSubmit={aoSalvar} className="space-y-6">
          {/* Geral */}
          <section className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <Campo label="Nome do assunto" htmlFor="assunto-nome" required>
                <Input
                  id="assunto-nome"
                  value={form.nome}
                  onChange={(e) => set('nome', e.target.value)}
                  placeholder="Ex.: Confirmação de agendamento"
                  autoFocus
                  required
                />
              </Campo>
              <Campo label="Modelo de IA" htmlFor="assunto-modelo" dica="Vazio = usa o padrão global (Haiku).">
                <Input
                  id="assunto-modelo"
                  value={form.modelo}
                  onChange={(e) => set('modelo', e.target.value)}
                  placeholder="claude-haiku-4-5-20251001"
                />
              </Campo>
            </div>

            <Campo label="Descrição" htmlFor="assunto-descricao" dica="Ajuda a classificação a escolher o assunto certo.">
              <Input
                id="assunto-descricao"
                value={form.descricao}
                onChange={(e) => set('descricao', e.target.value)}
                placeholder="Do que trata este assunto"
              />
            </Campo>

            <Campo label="Instruções / persona" htmlFor="assunto-persona" required
              dica="Como o robô deve se comportar neste assunto (tom, limites, o que pode dizer).">
              <textarea
                id="assunto-persona"
                className={CAMPO_CLASSE}
                rows={4}
                value={form.instrucoesPersona}
                onChange={(e) => set('instrucoesPersona', e.target.value)}
                required
              />
            </Campo>

            <div className="grid gap-4 sm:grid-cols-3">
              <Campo label="Interações antes do humano" htmlFor="assunto-max">
                <Input
                  id="assunto-max"
                  type="number"
                  min={1}
                  value={form.maxInteracoesSemResolver}
                  onChange={(e) => set('maxInteracoesSemResolver', Number(e.target.value))}
                />
              </Campo>
              <Campo label="Confiança mínima (0–1)" htmlFor="assunto-limiar"
                dica="Abaixo disso, encaminha ao humano.">
                <Input
                  id="assunto-limiar"
                  type="number"
                  min={0}
                  max={1}
                  step={0.05}
                  value={form.limiarConfianca}
                  onChange={(e) => set('limiarConfianca', Number(e.target.value))}
                />
              </Campo>
              <Campo label="Ordem na classificação" htmlFor="assunto-ordem" dica="Menor primeiro.">
                <Input
                  id="assunto-ordem"
                  type="number"
                  value={form.ordem}
                  onChange={(e) => set('ordem', Number(e.target.value))}
                />
              </Campo>
            </div>

            {/* Horário de atendimento */}
            <div className="rounded-lg border border-gray-200 p-3">
              <p className="mb-2 text-sm font-medium text-gray-700">Horário de atendimento</p>
              <div className="grid gap-4 sm:grid-cols-2">
                <Campo label="Início" htmlFor="assunto-hi" dica="Vazio = sem restrição de horário.">
                  <Input id="assunto-hi" type="time" value={form.horarioInicio}
                    onChange={(e) => set('horarioInicio', e.target.value)} />
                </Campo>
                <Campo label="Fim" htmlFor="assunto-hf">
                  <Input id="assunto-hf" type="time" value={form.horarioFim}
                    onChange={(e) => set('horarioFim', e.target.value)} />
                </Campo>
              </div>
              <label className="mt-3 flex items-center gap-2 text-sm text-gray-700">
                <input type="checkbox" checked={form.todosDias}
                  onChange={(e) => set('todosDias', e.target.checked)} />
                Atende todos os dias
              </label>
              {!form.todosDias ? (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {DIAS.map((dia, bit) => {
                    const marcado = (form.diasSemana & (1 << bit)) !== 0;
                    return (
                      <button
                        type="button"
                        key={dia}
                        onClick={() => alternarDia(bit)}
                        className={
                          'rounded-md border px-2.5 py-1 text-xs font-medium ' +
                          (marcado
                            ? 'border-primary-300 bg-primary-50 text-primary-700'
                            : 'border-gray-300 bg-white text-gray-500 hover:bg-gray-50')
                        }
                      >
                        {dia}
                      </button>
                    );
                  })}
                </div>
              ) : null}
            </div>

            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input type="checkbox" checked={form.ativo} onChange={(e) => set('ativo', e.target.checked)} />
              Assunto ativo
            </label>
          </section>

          {/* Comandos liberados */}
          <section>
            <h3 className="mb-1 text-sm font-semibold text-gray-900">Comandos liberados</h3>
            <p className="mb-3 text-xs text-gray-500">
              O robô só executa os comandos marcados aqui — nunca sai deste conjunto.
            </p>
            <div className="space-y-2">
              {comandosCatalogo.map((c) => (
                <label key={c.comando} className="flex items-start gap-2 rounded-md border border-gray-200 p-2.5">
                  <input
                    type="checkbox"
                    className="mt-0.5"
                    checked={form.comandos.includes(c.comando)}
                    onChange={() => alternarComando(c.comando)}
                  />
                  <span className="text-sm">
                    <span className="font-medium text-gray-900">{c.rotulo}</span>
                    {c.escrita ? (
                      <span className="ml-2 rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700">
                        altera dado
                      </span>
                    ) : null}
                    <span className="block text-xs text-gray-500">{c.descricao}</span>
                  </span>
                </label>
              ))}
            </div>
          </section>

          {/* Condições de ativação */}
          <ListaCondicoes
            condicoes={form.condicoes}
            aoMudar={(condicoes) => set('condicoes', condicoes)}
          />

          {/* Treinos */}
          <ListaTreinos treinos={form.treinos} aoMudar={(treinos) => set('treinos', treinos)} />

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          <div className="flex items-center justify-end gap-3 border-t border-gray-100 pt-4">
            <Button type="button" variante="secundaria" onClick={aoFechar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={salvando || !form.nome.trim() || !form.instrucoesPersona.trim()}>
              {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Salvar
            </Button>
          </div>
        </form>
      )}
    </Modal>
  );
}

function ListaCondicoes({
  condicoes,
  aoMudar,
}: {
  condicoes: RoboAssuntoCondicao[];
  aoMudar: (c: RoboAssuntoCondicao[]) => void;
}) {
  function adicionar() {
    aoMudar([...condicoes, { tipo: 'PalavraChave', valor: '', ativo: true, ordem: condicoes.length }]);
  }
  function atualizar(i: number, patch: Partial<RoboAssuntoCondicao>) {
    aoMudar(condicoes.map((c, idx) => (idx === i ? { ...c, ...patch } : c)));
  }
  function remover(i: number) {
    aoMudar(condicoes.filter((_, idx) => idx !== i));
  }
  return (
    <section>
      <div className="mb-2 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold text-gray-900">Condições de ativação</h3>
          <p className="text-xs text-gray-500">Casam a mensagem do cidadão a este assunto (pré-classificação).</p>
        </div>
        <Button type="button" variante="secundaria" onClick={adicionar}>
          <Plus className="mr-1 h-3.5 w-3.5" /> Condição
        </Button>
      </div>
      {condicoes.length === 0 ? (
        <p className="text-xs italic text-gray-400">Nenhuma condição — a classificação ficará por conta da IA.</p>
      ) : (
        <div className="space-y-2">
          {condicoes.map((c, i) => (
            <div key={i} className="flex items-center gap-2">
              <select
                className={CAMPO_CLASSE + ' w-40'}
                value={c.tipo}
                onChange={(e) => atualizar(i, { tipo: e.target.value as TipoCondicaoRobo })}
              >
                {TIPOS_CONDICAO.map((t) => (
                  <option key={t.valor} value={t.valor}>
                    {t.rotulo}
                  </option>
                ))}
              </select>
              <input
                className={CAMPO_CLASSE}
                value={c.valor}
                onChange={(e) => atualizar(i, { valor: e.target.value })}
                placeholder="Ex.: confirmar consulta"
              />
              <label className="flex shrink-0 items-center gap-1 text-xs text-gray-600">
                <input type="checkbox" checked={c.ativo} onChange={(e) => atualizar(i, { ativo: e.target.checked })} />
                Ativa
              </label>
              <button type="button" onClick={() => remover(i)} className="shrink-0 p-1 text-red-600 hover:text-red-800">
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function ListaTreinos({
  treinos,
  aoMudar,
}: {
  treinos: RoboAssuntoTreino[];
  aoMudar: (t: RoboAssuntoTreino[]) => void;
}) {
  function adicionar() {
    aoMudar([...treinos, { tipo: 'Instrucao', titulo: '', conteudo: '', ordem: treinos.length, ativo: true }]);
  }
  function atualizar(i: number, patch: Partial<RoboAssuntoTreino>) {
    aoMudar(treinos.map((t, idx) => (idx === i ? { ...t, ...patch } : t)));
  }
  function remover(i: number) {
    aoMudar(treinos.filter((_, idx) => idx !== i));
  }
  return (
    <section>
      <div className="mb-2 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold text-gray-900">Treinos</h3>
          <p className="text-xs text-gray-500">Instruções, exemplos e regras que refinam o atendimento do assunto.</p>
        </div>
        <Button type="button" variante="secundaria" onClick={adicionar}>
          <Plus className="mr-1 h-3.5 w-3.5" /> Treino
        </Button>
      </div>
      {treinos.length === 0 ? (
        <p className="text-xs italic text-gray-400">Nenhum treino ainda.</p>
      ) : (
        <div className="space-y-3">
          {treinos.map((t, i) => (
            <div key={i} className="rounded-lg border border-gray-200 p-3">
              <div className="mb-2 flex items-center gap-2">
                <select
                  className={CAMPO_CLASSE + ' w-44'}
                  value={t.tipo}
                  onChange={(e) => atualizar(i, { tipo: e.target.value as TipoTreinoRobo })}
                >
                  {TIPOS_TREINO.map((op) => (
                    <option key={op.valor} value={op.valor}>
                      {op.rotulo}
                    </option>
                  ))}
                </select>
                <input
                  className={CAMPO_CLASSE}
                  value={t.titulo ?? ''}
                  onChange={(e) => atualizar(i, { titulo: e.target.value })}
                  placeholder="Título (opcional)"
                />
                <label className="flex shrink-0 items-center gap-1 text-xs text-gray-600">
                  <input type="checkbox" checked={t.ativo} onChange={(e) => atualizar(i, { ativo: e.target.checked })} />
                  Ativo
                </label>
                <button type="button" onClick={() => remover(i)} className="shrink-0 p-1 text-red-600 hover:text-red-800">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
              <textarea
                className={CAMPO_CLASSE}
                rows={2}
                value={t.conteudo}
                onChange={(e) => atualizar(i, { conteudo: e.target.value })}
                placeholder="Conteúdo do treino"
              />
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
