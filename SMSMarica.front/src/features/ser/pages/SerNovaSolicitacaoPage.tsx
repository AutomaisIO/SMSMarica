import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Check,
  FilePlus2,
  HardDriveDownload,
  Loader2,
  Paperclip,
  Save,
  Send,
  Trash2,
} from 'lucide-react';

import {
  useAnexarRascunhoSer,
  useCamposCatalogoSer,
  useFormularioCatalogoSer,
  useMarcarRascunhoPronto,
  useRascunhoSer,
  useRascunhosSer,
  useRemoverAnexoRascunhoSer,
  useSalvarRascunhoSer,
} from '@/features/ser/api/queries';
import type {
  CampoDinamicoSer,
  CatalogoRecursoSer,
  OpcaoSer,
  RascunhoSerLista,
  StatusRascunhoSer,
  TipoRecursoSer,
} from '@/features/ser/types';
import { SeletorRecursoSer } from '@/features/ser/components/SeletorRecursoSer';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { formatarInstante } from '@/shared/lib/datas';

/**
 * Regulação → Nova solicitação.
 *
 * <p>O formulário é montado do NOSSO catálogo espelhado — offline e instantâneo. O pedido é
 * guardado aqui como rascunho, com anexos, e só vai ao SER quando o envio for autorizado.</p>
 *
 * <p>Os campos do bloco dinâmico mudam por recurso: 203 recursos produzem 21 formulários
 * distintos. A tela desenha o que o catálogo disser que aquele recurso exige.</p>
 */

const CLASSE_STATUS: Record<StatusRascunhoSer, string> = {
  Rascunho: 'bg-slate-100 text-slate-700',
  Pronto: 'bg-emerald-100 text-emerald-800',
  Enviado: 'bg-sky-100 text-sky-800',
  Falhou: 'bg-red-100 text-red-800',
};

export function SerNovaSolicitacaoPage() {
  const [rascunhoId, setRascunhoId] = useState<string | null>(null);
  const [tipo, setTipo] = useState<TipoRecursoSer | ''>('');
  const [recurso, setRecurso] = useState<CatalogoRecursoSer | null>(null);
  const [cns, setCns] = useState('');
  const [paciente, setPaciente] = useState('');
  const [hipotese, setHipotese] = useState('');
  const [campos, setCampos] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const catalogo = useFormularioCatalogoSer();
  const dinamicos = useCamposCatalogoSer(tipo || undefined, recurso?.valor);
  const lista = useRascunhosSer();
  const carregado = useRascunhoSer(rascunhoId);

  const salvar = useSalvarRascunhoSer();
  const marcarPronto = useMarcarRascunhoPronto();
  const anexar = useAnexarRascunhoSer();
  const removerAnexo = useRemoverAnexoRascunhoSer();

  // Ao abrir um rascunho existente, a tela é repovoada a partir dele.
  useEffect(() => {
    const r = carregado.data;
    if (!r || r.id !== rascunhoId) return;
    setTipo(r.tipo ?? '');
    setCns(r.cns ?? '');
    setPaciente(r.pacienteNome ?? '');
    setHipotese(r.hipotese ?? '');
    setCampos(r.campos ?? {});
    setRecurso(
      r.recursoValor
        ? { tipo: r.tipo ?? 'Consulta', valor: r.recursoValor, rotulo: r.recursoRotulo ?? r.recursoValor, camposLidos: true }
        : null,
    );
  }, [carregado.data, rascunhoId]);

  const recursosDoTipo = useMemo(
    () => (catalogo.data?.recursos ?? []).filter((r) => r.tipo === tipo),
    [catalogo.data, tipo],
  );

  const listaCampos = dinamicos.data ?? [];
  const somenteLeitura = carregado.data?.status === 'Enviado';

  function corpo() {
    return {
      tipo: (tipo || undefined) as TipoRecursoSer | undefined,
      recursoValor: recurso?.valor,
      recursoRotulo: recurso?.rotulo,
      cns: cns.trim() || undefined,
      pacienteNome: paciente.trim() || undefined,
      hipotese: hipotese.trim() || undefined,
      campos,
    };
  }

  async function aoSalvar() {
    setErro(null);
    setAviso(null);
    try {
      const r = await salvar.mutateAsync({ id: rascunhoId, corpo: corpo() });
      setRascunhoId(r.id);
      setAviso('Rascunho salvo. Ele fica guardado aqui até você mandar para o SER.');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoMarcarPronto() {
    setErro(null);
    setAviso(null);
    try {
      const salvo = await salvar.mutateAsync({ id: rascunhoId, corpo: corpo() });
      setRascunhoId(salvo.id);
      await marcarPronto.mutateAsync(salvo.id);
      setAviso('Marcado como pronto — completo e esperando autorização de envio.');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoAnexar(arquivo: File) {
    setErro(null);
    try {
      // Anexo pertence a um rascunho: se ainda não existe, salva antes — senão o arquivo não
      // teria a quem pertencer.
      let id = rascunhoId;
      if (!id) {
        const r = await salvar.mutateAsync({ id: null, corpo: corpo() });
        id = r.id;
        setRascunhoId(id);
      }
      await anexar.mutateAsync({ id, arquivo });
      setAviso(`"${arquivo.name}" anexado. Fica guardado aqui e sobe junto no envio.`);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  function novo() {
    setRascunhoId(null);
    setTipo('');
    setRecurso(null);
    setCns('');
    setPaciente('');
    setHipotese('');
    setCampos({});
    setErro(null);
    setAviso(null);
  }

  const semCatalogo = (catalogo.data?.recursos.length ?? 0) === 0;

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center gap-3">
        <FilePlus2 className="size-6 text-red-700" />
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Nova solicitação (SER)</h1>
          <p className="text-sm text-slate-600">
            Monte o pedido e guarde aqui. O envio ao SER é um passo separado.
          </p>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Button variante="secundaria" onClick={novo}>Novo</Button>
        </div>
      </header>

      {semCatalogo && !catalogo.isLoading && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          <HardDriveDownload className="mt-0.5 size-4 shrink-0" />
          <p>
            <strong>O catálogo ainda não foi copiado.</strong> Sem ele a tela não tem os recursos
            nem os campos de cada um. Vá em <em>Regulação → Configuração → SER</em> e rode a cópia
            do catálogo (leva ~15 min e é retomável).
          </p>
        </div>
      )}

      {catalogo.data && catalogo.data.recursosSemCampos > 0 && (
        <p className="text-xs text-amber-700">
          {catalogo.data.recursosSemCampos} recurso(s) ainda sem os campos copiados — rode a cópia
          do catálogo de novo para completar.
        </p>
      )}

      <div className="flex items-start gap-2 rounded-lg border border-slate-300 bg-slate-50 p-3 text-sm text-slate-700">
        <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-600" />
        <p>
          <strong>Envio ao SER ainda desligado.</strong> Tudo aqui é guardado na nossa base —
          inclusive os anexos. Quando o envio for ligado, os rascunhos marcados como
          <em> Pronto</em> são os que vão.
        </p>
      </div>

      {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-800">{erro}</p>}
      {aviso && <p className="rounded bg-emerald-50 p-3 text-sm text-emerald-800">{aviso}</p>}

      <div className="grid gap-4 lg:grid-cols-[1fr_20rem]">
        <div className="space-y-5 rounded-lg border border-slate-200 bg-white p-4">
          {carregado.data && (
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <span className={`rounded px-2 py-0.5 font-medium ${CLASSE_STATUS[carregado.data.status]}`}>
                {carregado.data.status}
              </span>
              {carregado.data.idSerGerado && (
                <span className="text-slate-600">nº no SER: {carregado.data.idSerGerado}</span>
              )}
              {carregado.data.mensagemErro && (
                <span className="text-red-700">{carregado.data.mensagemErro}</span>
              )}
            </div>
          )}

          <section>
            <h2 className="mb-2 font-semibold text-slate-800">O que está sendo pedido</h2>
            <div className="flex flex-wrap items-end gap-3">
              <Campo label="Tipo *" htmlFor="ns-tipo" className="w-44">
                <Select
                  id="ns-tipo"
                  value={tipo}
                  disabled={somenteLeitura}
                  onChange={(e) => {
                    setTipo(e.target.value as TipoRecursoSer | '');
                    // Recurso e campos são do tipo anterior: manter montaria um pedido com
                    // campos que o novo recurso nem tem.
                    setRecurso(null);
                    setCampos({});
                  }}
                >
                  <option value="">Selecione…</option>
                  <option value="Consulta">Consulta</option>
                  <option value="Exame">Exame</option>
                </Select>
              </Campo>

              <Campo label="Recurso *" htmlFor="ns-recurso" className="min-w-96 flex-1">
                <SeletorRecursoSer
                  recursos={recursosDoTipo}
                  valor={recurso?.valor ?? ''}
                  desabilitado={!tipo || somenteLeitura}
                  onChange={(r) => {
                    setRecurso(r);
                    setCampos({});
                  }}
                />
              </Campo>
            </div>
            {tipo && (
              <p className="mt-1 text-xs text-slate-500">
                {recursosDoTipo.length} recursos em {tipo} — digite para filtrar.
              </p>
            )}
          </section>

          <section>
            <h2 className="mb-2 font-semibold text-slate-800">Paciente</h2>
            <div className="flex flex-wrap items-end gap-3">
              <Campo label="CNS *" htmlFor="ns-cns" className="w-56">
                <Input id="ns-cns" value={cns} disabled={somenteLeitura}
                  onChange={(e) => setCns(e.target.value)} />
              </Campo>
              <Campo label="Nome" htmlFor="ns-pac" className="min-w-72 flex-1">
                <Input id="ns-pac" value={paciente} disabled={somenteLeitura}
                  onChange={(e) => setPaciente(e.target.value)} />
              </Campo>
            </div>
          </section>

          <section>
            <h2 className="mb-2 font-semibold text-slate-800">Classificação</h2>
            <div className="flex flex-wrap items-end gap-3">
              <Campo label="Classificação de risco *" htmlFor="ns-risco" className="w-56">
                <Select
                  id="ns-risco"
                  value={campos['form0:classificacao_risco'] ?? ''}
                  disabled={somenteLeitura}
                  onChange={(e) => setCampos({ ...campos, 'form0:classificacao_risco': e.target.value })}
                >
                  <option value="">Selecione…</option>
                  {(catalogo.data?.classificacoesRisco ?? []).map((o: OpcaoSer) => (
                    <option key={o.valor} value={o.valor}>{o.rotulo}</option>
                  ))}
                </Select>
              </Campo>

              <Campo label="Médico responsável" htmlFor="ns-medico" className="min-w-72 flex-1">
                <Select
                  id="ns-medico"
                  value={campos['form0:medicoResp'] ?? ''}
                  disabled={somenteLeitura}
                  onChange={(e) => setCampos({ ...campos, 'form0:medicoResp': e.target.value })}
                >
                  <option value="">Selecione…</option>
                  {(catalogo.data?.medicos ?? []).map((o: OpcaoSer) => (
                    <option key={o.valor} value={o.valor}>{o.rotulo}</option>
                  ))}
                </Select>
              </Campo>

              <Campo label="Hipótese *" htmlFor="ns-hip" className="min-w-72 flex-1">
                <Input id="ns-hip" value={hipotese} disabled={somenteLeitura}
                  onChange={(e) => setHipotese(e.target.value)} />
              </Campo>
            </div>
          </section>

          <section>
            <h2 className="mb-1 font-semibold text-slate-800">Campos do recurso</h2>
            {!recurso && <p className="text-sm text-slate-500">Escolha o recurso.</p>}
            {dinamicos.isLoading && (
              <p className="flex items-center gap-2 text-sm text-slate-500">
                <Loader2 className="size-4 animate-spin" /> carregando…
              </p>
            )}
            {recurso && !dinamicos.isLoading && listaCampos.length === 0 && (
              <p className="text-sm text-amber-700">
                Este recurso ainda não teve os campos copiados do SER.
              </p>
            )}
            {listaCampos.length > 0 && (
              <>
                <p className="mb-3 text-xs text-slate-500">
                  {listaCampos.length} campo(s), {listaCampos.filter((c) => c.obrigatorio).length}{' '}
                  obrigatório(s).
                </p>
                <div className="flex flex-wrap gap-3">
                  {listaCampos.map((c) => (
                    <CampoDinamico
                      key={c.campo}
                      c={c}
                      valor={campos[c.campo] ?? ''}
                      desabilitado={somenteLeitura}
                      onChange={(v) => setCampos({ ...campos, [c.campo]: v })}
                    />
                  ))}
                </div>
              </>
            )}
          </section>

          <section className="border-t border-slate-200 pt-4">
            <h2 className="mb-2 flex items-center gap-2 font-semibold text-slate-800">
              <Paperclip className="size-4" /> Anexos
            </h2>

            <ul className="mb-2 space-y-1">
              {(carregado.data?.anexos ?? []).map((a) => (
                <li key={a.id} className="flex items-center gap-2 text-sm">
                  <span className="min-w-0 flex-1 truncate">{a.nomeArquivo}</span>
                  <span className="shrink-0 text-xs text-slate-500">
                    {(a.tamanho / 1024).toFixed(0)} KB
                  </span>
                  {a.enviadoEm ? (
                    <span className="shrink-0 text-xs text-emerald-700">no SER</span>
                  ) : (
                    !somenteLeitura && (
                      <button
                        type="button"
                        title="Remover anexo"
                        onClick={() =>
                          rascunhoId && removerAnexo.mutate({ id: rascunhoId, anexoId: a.id })
                        }
                        className="shrink-0 text-slate-400 hover:text-red-700"
                      >
                        <Trash2 className="size-4" />
                      </button>
                    )
                  )}
                </li>
              ))}
            </ul>

            {!somenteLeitura && (
              <label className="inline-flex cursor-pointer items-center gap-2 rounded border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-50">
                {anexar.isPending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Paperclip className="size-4" />
                )}
                Anexar arquivo
                <input
                  type="file"
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) void aoAnexar(f);
                    e.target.value = '';
                  }}
                />
              </label>
            )}
            <p className="mt-1 text-xs text-slate-500">
              Até 10 MB por arquivo. Fica guardado na nossa base e sobe junto quando o pedido for
              enviado.
            </p>
          </section>

          {!somenteLeitura && (
            <section className="flex flex-wrap items-center gap-2 border-t border-slate-200 pt-4">
              <Button onClick={aoSalvar} disabled={salvar.isPending}>
                {salvar.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
                Salvar rascunho
              </Button>
              <Button variante="secundaria" onClick={aoMarcarPronto} disabled={marcarPronto.isPending}>
                <Check className="size-4" />
                Marcar pronto para envio
              </Button>
              <Button variante="secundaria" disabled title="O envio ao SER ainda não está habilitado">
                <Send className="size-4" />
                Enviar ao SER (desligado)
              </Button>
            </section>
          )}
        </div>

        <aside className="space-y-2">
          <h2 className="font-semibold text-slate-800">Rascunhos</h2>
          {(lista.data ?? []).length === 0 && (
            <p className="text-sm text-slate-500">Nenhum ainda.</p>
          )}
          {(lista.data ?? []).map((r: RascunhoSerLista) => (
            <button
              key={r.id}
              type="button"
              onClick={() => setRascunhoId(r.id)}
              className={`w-full rounded-lg border p-2 text-left text-sm ${
                r.id === rascunhoId ? 'border-red-400 bg-red-50' : 'border-slate-200 hover:bg-slate-50'
              }`}
            >
              <div className="flex items-center gap-2">
                <span className={`rounded px-1.5 py-0.5 text-[11px] ${CLASSE_STATUS[r.status]}`}>
                  {r.status}
                </span>
                {r.anexos > 0 && (
                  <span className="text-[11px] text-slate-500">{r.anexos} anexo(s)</span>
                )}
              </div>
              <div className="truncate font-medium text-slate-800">
                {r.pacienteNome ?? r.cns ?? '(sem paciente)'}
              </div>
              <div className="truncate text-xs text-slate-600">{r.recursoRotulo ?? '—'}</div>
              <div className="text-[11px] text-slate-400">
                {formatarInstante(r.atualizadoEm ?? r.criadoEm)}
              </div>
            </button>
          ))}
        </aside>
      </div>
    </div>
  );
}

/** Desenha o campo conforme o tipo que o catálogo guardou para ele. */
function CampoDinamico({
  c,
  valor,
  desabilitado,
  onChange,
}: {
  c: CampoDinamicoSer;
  valor: string;
  desabilitado?: boolean;
  onChange: (v: string) => void;
}) {
  const rotulo = `${c.rotulo}${c.obrigatorio ? ' *' : ''}`;
  const id = `din-${c.numero}`;

  if (c.tipo === 'textarea') {
    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-96 flex-1">
        <textarea
          id={id}
          rows={3}
          value={valor}
          disabled={desabilitado}
          onChange={(e) => onChange(e.target.value)}
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100"
        />
      </Campo>
    );
  }

  if ((c.tipo === 'select' || c.tipo === 'radio') && c.opcoes?.length) {
    if (c.tipo === 'radio') {
      return (
        <Campo label={rotulo} htmlFor={id} className="min-w-72">
          <div id={id} className="flex flex-wrap gap-3 pt-1">
            {c.opcoes.map((o) => (
              <label key={o.valor} className="flex items-center gap-1.5 text-sm">
                <input
                  type="radio"
                  name={c.campo}
                  value={o.valor}
                  disabled={desabilitado}
                  checked={valor === o.valor}
                  onChange={() => onChange(o.valor)}
                />
                {o.rotulo}
              </label>
            ))}
          </div>
        </Campo>
      );
    }

    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-72">
        <Select id={id} value={valor} disabled={desabilitado} onChange={(e) => onChange(e.target.value)}>
          <option value="">Selecione…</option>
          {c.opcoes.map((o) => (
            <option key={o.valor} value={o.valor}>{o.rotulo}</option>
          ))}
        </Select>
      </Campo>
    );
  }

  return (
    <Campo label={rotulo} htmlFor={id} className="w-56">
      <Input id={id} value={valor} disabled={desabilitado} onChange={(e) => onChange(e.target.value)} />
    </Campo>
  );
}
