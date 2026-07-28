import { useEffect, useState } from 'react';
import { History, Loader2, Play, Save, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import {
  useApurarIndicador,
  useAtualizarIndicador,
  useCriarIndicador,
  useExcluirIndicador,
  useFontesIndicador,
  useIndicador,
  useVersoesIndicador,
} from '@/features/indicadores/api/queries';
import {
  OPERADORES,
  TIPOS,
  type AbaIndicador,
  type FiltroIndicador,
  type IndicadorResumo,
  type MetaOperador,
  type ResultadoIndicador,
  type SalvarIndicadorPayload,
  type SituacaoIndicador,
  type TipoResultadoIndicador,
} from '@/features/indicadores/types';

type Props = {
  /** `null` = cadastrar indicador novo. */
  indicadorId: string | null;
  aba: AbaIndicador;
  filtro: FiltroIndicador;
  podeEditar: boolean;
  agrupadores: IndicadorResumo[];
  onFechar: () => void;
};

const SITUACOES: { valor: SituacaoIndicador; rotulo: string }[] = [
  { valor: 'Validado', rotulo: 'Validado (conferido com dado real)' },
  { valor: 'NaoValidado', rotulo: 'Não validado' },
  { valor: 'SemMotor', rotulo: 'Sem motor' },
  { valor: 'ForaDoBanco', rotulo: 'Fora do banco (depende de processo)' },
];

type Form = {
  numero: string;
  ordem: string;
  indicadorPaiId: string;
  nome: string;
  memoriaCalculo: string;
  fonteDeclarada: string;
  meta: string;
  metaOperador: string;
  metaValor: string;
  metaValorMaximo: string;
  pontuacao: string;
  tipoResultado: TipoResultadoIndicador;
  unidadeMedida: string;
  fatorDensidade: string;
  situacao: SituacaoIndicador;
  fonteId: string;
  sql: string;
  sqlAnalitico: string;
  ressalva: string;
  ativo: boolean;
  nota: string;
};

const VAZIO: Form = {
  numero: '',
  ordem: '0',
  indicadorPaiId: '',
  nome: '',
  memoriaCalculo: '',
  fonteDeclarada: '',
  meta: '',
  metaOperador: '',
  metaValor: '',
  metaValorMaximo: '',
  pontuacao: '',
  tipoResultado: 'Razao',
  unidadeMedida: '',
  fatorDensidade: '',
  situacao: 'SemMotor',
  fonteId: '',
  sql: '',
  sqlAnalitico: '',
  ressalva: '',
  ativo: true,
  nota: '',
};

function n(v: string): number | null {
  const t = v.trim().replace(',', '.');
  return t === '' ? null : Number(t);
}

function s(v: string): string | null {
  return v.trim() === '' ? null : v.trim();
}

export function ModalIndicador({
  indicadorId,
  aba,
  filtro,
  podeEditar,
  agrupadores,
  onFechar,
}: Props) {
  const novo = indicadorId === null;
  const detalhe = useIndicador(indicadorId ?? undefined);
  const versoes = useVersoesIndicador(indicadorId ?? undefined);
  const fontes = useFontesIndicador();
  const criar = useCriarIndicador();
  const atualizar = useAtualizarIndicador();
  const excluir = useExcluirIndicador();
  const apurar = useApurarIndicador();

  const [form, setForm] = useState<Form>(VAZIO);
  const [erro, setErro] = useState<string | null>(null);
  const [previa, setPrevia] = useState<ResultadoIndicador | null>(null);
  const [verHistorico, setVerHistorico] = useState(false);

  useEffect(() => {
    const d = detalhe.data;
    if (!d) return;
    setForm({
      numero: d.numero,
      ordem: String(d.ordem),
      indicadorPaiId: d.indicadorPaiId ?? '',
      nome: d.nome,
      memoriaCalculo: d.memoriaCalculo ?? '',
      fonteDeclarada: d.fonteDeclarada ?? '',
      meta: d.meta ?? '',
      metaOperador: d.metaOperador ?? '',
      metaValor: d.metaValor !== null ? String(d.metaValor) : '',
      metaValorMaximo: d.metaValorMaximo !== null ? String(d.metaValorMaximo) : '',
      pontuacao: d.pontuacao !== null ? String(d.pontuacao) : '',
      tipoResultado: d.tipoResultado,
      unidadeMedida: d.unidadeMedida ?? '',
      fatorDensidade: d.fatorDensidade !== null ? String(d.fatorDensidade) : '',
      situacao: d.situacao,
      fonteId: d.fonteId ?? '',
      sql: d.sql ?? '',
      sqlAnalitico: d.sqlAnalitico ?? '',
      ressalva: d.ressalva ?? '',
      ativo: d.ativo,
      nota: '',
    });
  }, [detalhe.data]);

  function alterar<K extends keyof Form>(chave: K, valor: Form[K]) {
    setForm((f) => ({ ...f, [chave]: valor }));
  }

  function montarPayload(): SalvarIndicadorPayload {
    return {
      aba,
      numero: form.numero.trim(),
      ordem: Number(form.ordem) || 0,
      indicadorPaiId: form.indicadorPaiId || null,
      nome: form.nome.trim(),
      memoriaCalculo: s(form.memoriaCalculo),
      fonteDeclarada: s(form.fonteDeclarada),
      meta: s(form.meta),
      metaOperador: (form.metaOperador || null) as MetaOperador | null,
      metaValor: n(form.metaValor),
      metaValorMaximo: n(form.metaValorMaximo),
      pontuacao: n(form.pontuacao),
      tipoResultado: form.tipoResultado,
      unidadeMedida: s(form.unidadeMedida),
      fatorDensidade: n(form.fatorDensidade),
      situacao: form.situacao,
      fonteId: form.fonteId || null,
      sql: s(form.sql),
      sqlAnalitico: s(form.sqlAnalitico),
      ressalva: s(form.ressalva),
      ativo: form.ativo,
      nota: s(form.nota),
    };
  }

  async function gravar() {
    setErro(null);
    try {
      if (novo) {
        await criar.mutateAsync(montarPayload());
      } else {
        await atualizar.mutateAsync({ id: indicadorId!, payload: montarPayload() });
        await versoes.refetch();
      }
      onFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function executarPrevia() {
    if (!indicadorId) return;
    setErro(null);
    setPrevia(null);
    try {
      setPrevia(await apurar.mutateAsync({ id: indicadorId, filtro, previa: true }));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function remover() {
    if (!indicadorId) return;
    setErro(null);
    try {
      await excluir.mutateAsync(indicadorId);
      onFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const contrato = TIPOS.find((t) => t.valor === form.tipoResultado)?.contrato ?? '';
  const somenteLeitura = !podeEditar;
  const carregando = !novo && detalhe.isLoading;
  const salvando = criar.isPending || atualizar.isPending;

  return (
    <Modal
      aberto
      aoFechar={onFechar}
      titulo={novo ? 'Novo indicador' : `${form.numero} · ${form.nome}`}
      largura="lg"
    >
      {carregando ? (
        <div className="flex items-center justify-center py-10 text-slate-400">
          <Loader2 className="h-5 w-5 animate-spin" />
        </div>
      ) : (
        <div className="space-y-5">
          {/* ---- identificação ---- */}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-6">
            <div className="sm:col-span-1">
              <Campo label="Nº" htmlFor="ind-numero">
                <Input
                  id="ind-numero"
                  value={form.numero}
                  disabled={somenteLeitura}
                  onChange={(e) => alterar('numero', e.target.value)}
                />
              </Campo>
            </div>
            <div className="sm:col-span-1">
              <Campo label="Ordem" htmlFor="ind-ordem">
                <Input
                  id="ind-ordem"
                  value={form.ordem}
                  disabled={somenteLeitura}
                  onChange={(e) => alterar('ordem', e.target.value)}
                />
              </Campo>
            </div>
            <div className="sm:col-span-4">
              <Campo label="Indicador" htmlFor="ind-nome">
                <Input
                  id="ind-nome"
                  value={form.nome}
                  disabled={somenteLeitura}
                  onChange={(e) => alterar('nome', e.target.value)}
                />
              </Campo>
            </div>
          </div>

          {/* ---- habilita / desabilita ---- */}
          <div
            className={`flex items-center justify-between gap-3 rounded-lg border p-3 ${
              form.ativo ? 'border-slate-200' : 'border-amber-200 bg-amber-50/40'
            }`}
          >
            <div className="text-xs">
              <span className="font-medium text-slate-700">
                {form.ativo ? 'Indicador habilitado' : 'Indicador desabilitado'}
              </span>
              <span className="mt-0.5 block text-[11px] leading-relaxed text-slate-400">
                Desligado, aparece esmaecido na tabela — só o nome, sem número, badge, meta ou
                ressalva — e não conta na pontuação da aba.
              </span>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={form.ativo}
              disabled={somenteLeitura}
              onClick={() => alterar('ativo', !form.ativo)}
              className={`relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition ${
                form.ativo ? 'bg-emerald-500' : 'bg-slate-300'
              } ${somenteLeitura ? 'cursor-not-allowed opacity-60' : ''}`}
            >
              <span
                className={`inline-block h-5 w-5 transform rounded-full bg-white shadow transition ${
                  form.ativo ? 'translate-x-5' : 'translate-x-0.5'
                }`}
              />
            </button>
          </div>

          <Campo label="Memória de cálculo (como pactuada na planilha)" htmlFor="ind-memoria">
            <textarea
              id="ind-memoria"
              value={form.memoriaCalculo}
              readOnly={somenteLeitura}
              rows={2}
              onChange={(e) => alterar('memoriaCalculo', e.target.value)}
              className="w-full rounded-lg border border-slate-200 p-2 text-xs text-slate-700 focus:border-slate-400 focus:outline-none"
            />
          </Campo>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Campo label="Fonte declarada na planilha" htmlFor="ind-fonte-decl">
              <Input
                id="ind-fonte-decl"
                value={form.fonteDeclarada}
                disabled={somenteLeitura}
                placeholder="PEP ou SIH"
                onChange={(e) => alterar('fonteDeclarada', e.target.value)}
              />
            </Campo>
            <Campo label="Agrupado sob" htmlFor="ind-pai">
              <Select
                id="ind-pai"
                value={form.indicadorPaiId}
                disabled={somenteLeitura}
                onChange={(e) => alterar('indicadorPaiId', e.target.value)}
              >
                <option value="">— nenhum (linha de topo)</option>
                {agrupadores
                  .filter((a) => a.id !== indicadorId)
                  .map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.numero} · {a.nome}
                    </option>
                  ))}
              </Select>
            </Campo>
          </div>

          {/* ---- meta e pontuação ---- */}
          <fieldset className="rounded-lg border border-slate-200 p-3">
            <legend className="px-1 text-xs font-medium text-slate-600">Meta e pontuação</legend>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-5">
              <div className="sm:col-span-2">
                <Campo label="Meta (texto da planilha)" htmlFor="ind-meta">
                  <Input
                    id="ind-meta"
                    value={form.meta}
                    disabled={somenteLeitura}
                    placeholder="≤ 8 h"
                    onChange={(e) => alterar('meta', e.target.value)}
                  />
                </Campo>
              </div>
              <Campo label="Operador" htmlFor="ind-operador">
                <Select
                  id="ind-operador"
                  value={form.metaOperador}
                  disabled={somenteLeitura}
                  onChange={(e) => alterar('metaOperador', e.target.value)}
                >
                  <option value="">— não pontua</option>
                  {OPERADORES.map((o) => (
                    <option key={o.valor} value={o.valor}>
                      {o.rotulo}
                    </option>
                  ))}
                </Select>
              </Campo>
              <Campo label="Valor" htmlFor="ind-meta-valor">
                <Input
                  id="ind-meta-valor"
                  value={form.metaValor}
                  disabled={somenteLeitura}
                  placeholder="8"
                  onChange={(e) => alterar('metaValor', e.target.value)}
                />
              </Campo>
              <Campo label="Peso (pontos)" htmlFor="ind-pontuacao">
                <Input
                  id="ind-pontuacao"
                  value={form.pontuacao}
                  disabled={somenteLeitura}
                  placeholder="0,5"
                  onChange={(e) => alterar('pontuacao', e.target.value)}
                />
              </Campo>
            </div>
            {form.metaOperador === 'Entre' && (
              <div className="mt-3 w-40">
                <Campo label="Valor máximo" htmlFor="ind-meta-max">
                  <Input
                    id="ind-meta-max"
                    value={form.metaValorMaximo}
                    disabled={somenteLeitura}
                    onChange={(e) => alterar('metaValorMaximo', e.target.value)}
                  />
                </Campo>
              </div>
            )}
            <p className="mt-2 text-[11px] leading-relaxed text-slate-400">
              A pontuação é tudo-ou-nada, como na planilha: bateu a meta leva o peso cheio, não
              bateu leva zero. Percentual é fração — meta de 85% se escreve <code>0,85</code>.
            </p>
          </fieldset>

          {/* ---- motor ---- */}
          <fieldset className="rounded-lg border border-slate-200 p-3">
            <legend className="px-1 text-xs font-medium text-slate-600">Motor do cálculo</legend>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
              <div className="sm:col-span-2">
                <Campo label="Tipo de resultado" htmlFor="ind-tipo">
                  <Select
                    id="ind-tipo"
                    value={form.tipoResultado}
                    disabled={somenteLeitura}
                    onChange={(e) =>
                      alterar('tipoResultado', e.target.value as TipoResultadoIndicador)
                    }
                  >
                    {TIPOS.map((t) => (
                      <option key={t.valor} value={t.valor}>
                        {t.rotulo}
                      </option>
                    ))}
                  </Select>
                </Campo>
              </div>
              <Campo label="Unidade" htmlFor="ind-unidade">
                <Input
                  id="ind-unidade"
                  value={form.unidadeMedida}
                  disabled={somenteLeitura}
                  placeholder="min, h, dias, %"
                  onChange={(e) => alterar('unidadeMedida', e.target.value)}
                />
              </Campo>
              <Campo label="Fator (densidade)" htmlFor="ind-fator">
                <Input
                  id="ind-fator"
                  value={form.fatorDensidade}
                  disabled={somenteLeitura || form.tipoResultado !== 'Densidade'}
                  placeholder="1000"
                  onChange={(e) => alterar('fatorDensidade', e.target.value)}
                />
              </Campo>
            </div>

            <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Campo label="Base de dados" htmlFor="ind-fonte">
                <Select
                  id="ind-fonte"
                  value={form.fonteId}
                  disabled={somenteLeitura}
                  onChange={(e) => alterar('fonteId', e.target.value)}
                >
                  <option value="">— sem base</option>
                  {(fontes.data ?? []).map((f) => (
                    <option key={f.id} value={f.id}>
                      {f.nome}
                    </option>
                  ))}
                </Select>
              </Campo>
              <Campo label="Situação" htmlFor="ind-situacao">
                <Select
                  id="ind-situacao"
                  value={form.situacao}
                  disabled={somenteLeitura}
                  onChange={(e) => alterar('situacao', e.target.value as SituacaoIndicador)}
                >
                  {SITUACOES.map((s2) => (
                    <option key={s2.valor} value={s2.valor}>
                      {s2.rotulo}
                    </option>
                  ))}
                </Select>
              </Campo>
            </div>

            <div className="mt-3">
              <div className="mb-1.5 flex items-baseline justify-between">
                <label htmlFor="ind-sql" className="text-xs font-medium text-slate-700">
                  SQL
                </label>
                <span className="text-[11px] text-slate-400">
                  deve devolver {contrato} · parâmetros <code>:ini</code> <code>:fim</code>{' '}
                  <code>:hospital</code>
                </span>
              </div>
              <textarea
                id="ind-sql"
                value={form.sql}
                readOnly={somenteLeitura}
                onChange={(e) => alterar('sql', e.target.value)}
                spellCheck={false}
                rows={12}
                className="w-full rounded-lg border border-slate-200 bg-slate-50 p-3 font-mono text-xs leading-relaxed text-slate-800 focus:border-slate-400 focus:outline-none"
                placeholder={
                  'SELECT COUNT(*) AS numerador,\n       COUNT(*) AS denominador\n  FROM infosaude.baa\n WHERE cd_hospital = :hospital\n   AND dt_chegada >= :ini AND dt_chegada < :fim'
                }
              />
              <p className="mt-1 text-[11px] text-slate-400">
                Somente leitura: um único SELECT/WITH. Comando de escrita é bloqueado antes de
                chegar ao banco do hospital.
              </p>
            </div>
          </fieldset>

          {/* ---- evidência (relatório analítico) ---- */}
          <fieldset className="rounded-lg border border-slate-200 p-3">
            <legend className="px-1 text-xs font-medium text-slate-600">
              Evidência (relatório analítico)
            </legend>
            <p className="mb-2 text-[11px] leading-relaxed text-slate-500">
              Opcional. É o que sustenta o número na exportação: uma linha por registro, dizendo
              quais entraram na conta e quais ficaram de fora — e por quê. Repare que o analítico{' '}
              <strong>classifica</strong> em vez de filtrar: se os excluídos sumirem no{' '}
              <code>WHERE</code>, não há o que auditar.
            </p>
            <div className="mb-1.5 flex items-baseline justify-between">
              <label htmlFor="ind-sql-analitico" className="text-xs font-medium text-slate-700">
                SQL analítico
              </label>
              <span className="text-[11px] text-slate-400">
                colunas livres + <code>incluido</code> (S/N) e <code>motivo_exclusao</code>
              </span>
            </div>
            <textarea
              id="ind-sql-analitico"
              value={form.sqlAnalitico}
              readOnly={somenteLeitura}
              onChange={(e) => alterar('sqlAnalitico', e.target.value)}
              spellCheck={false}
              rows={10}
              className="w-full rounded-lg border border-slate-200 bg-slate-50 p-3 font-mono text-xs leading-relaxed text-slate-800 focus:border-slate-400 focus:outline-none"
              placeholder={
                'SELECT a.cd_atendimento    AS atendimento,\n' +
                '       a.nm_paciente       AS paciente,\n' +
                '       a.dt_chegada        AS chegada,\n' +
                "       CASE WHEN a.dt_atend_medico IS NULL THEN 'N' ELSE 'S' END AS incluido,\n" +
                "       CASE WHEN a.dt_atend_medico IS NULL\n" +
                "            THEN 'Sem hora de atendimento médico registrada' END AS motivo_exclusao\n" +
                '  FROM infosaude.baa a\n' +
                ' WHERE a.cd_hospital = :hospital\n' +
                '   AND a.dt_chegada >= :ini AND a.dt_chegada < :fim'
              }
            />
            <p className="mt-1 text-[11px] text-slate-400">
              Roda sob demanda, só quando alguém exporta — nada é gravado. Teto de 5.000 linhas por
              indicador; acima disso a planilha avisa que a evidência veio truncada.
            </p>
          </fieldset>

          <Campo label="Ressalva (aparece junto do número na tabela)" htmlFor="ind-ressalva">
            <textarea
              id="ind-ressalva"
              value={form.ressalva}
              readOnly={somenteLeitura}
              rows={2}
              onChange={(e) => alterar('ressalva', e.target.value)}
              className="w-full rounded-lg border border-slate-200 p-2 text-xs text-slate-700 focus:border-slate-400 focus:outline-none"
              placeholder="Ex.: a hora do atendimento médico só está preenchida em 48,7% dos casos."
            />
          </Campo>

          {previa && (
            <div className="rounded-lg border border-slate-200 p-3 text-xs">
              <p className="mb-1 font-medium text-slate-700">Prévia (não gravada)</p>
              {previa.erro ? (
                <p className="text-rose-600">{previa.erro}</p>
              ) : previa.distribuicao ? (
                <p className="text-slate-600">
                  {previa.distribuicao.length} linhas · primeira: {previa.distribuicao[0]?.rotulo} ={' '}
                  {previa.distribuicao[0]?.quantidade}
                </p>
              ) : (
                <p className="tabular-nums text-slate-600">
                  numerador {previa.numerador ?? '—'} ÷ denominador {previa.denominador ?? '—'} ={' '}
                  <strong>{previa.valor ?? '—'}</strong>
                  {previa.atingiuMeta !== null &&
                    ` · ${previa.atingiuMeta ? 'atingiu' : 'não atingiu'} a meta (${previa.pontuacaoApurada ?? 0} pt)`}
                </p>
              )}
              <p className="mt-1 text-slate-400">{previa.duracaoMs} ms</p>
            </div>
          )}

          {erro && <p className="rounded-lg bg-rose-50 px-3 py-2 text-xs text-rose-700">{erro}</p>}

          {podeEditar && !novo && (
            <Campo label="Nota da versão (só quando o SQL muda)" htmlFor="ind-nota">
              <Input
                id="ind-nota"
                value={form.nota}
                onChange={(e) => alterar('nota', e.target.value)}
                placeholder="Ex.: passou a excluir os classificados como vermelho"
              />
            </Campo>
          )}

          <div className="flex items-center justify-between gap-3 border-t border-slate-100 pt-4">
            <div className="flex items-center gap-3">
              {!novo && (
                <button
                  type="button"
                  onClick={() => setVerHistorico((v) => !v)}
                  className="inline-flex items-center gap-1.5 text-xs text-slate-500 hover:text-slate-800"
                >
                  <History className="h-3.5 w-3.5" />
                  {detalhe.data?.totalVersoes ?? 0} versões
                </button>
              )}
              {podeEditar && !novo && (
                <button
                  type="button"
                  onClick={remover}
                  disabled={excluir.isPending}
                  className="inline-flex items-center gap-1.5 text-xs text-rose-500 hover:text-rose-700"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                  Excluir
                </button>
              )}
            </div>

            <div className="flex items-center gap-2">
              {!novo && (
                <Button variante="secundaria" onClick={executarPrevia} disabled={apurar.isPending}>
                  {apurar.isPending ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Play className="h-4 w-4" />
                  )}
                  Executar prévia
                </Button>
              )}
              {podeEditar && (
                <Button onClick={gravar} disabled={salvando}>
                  {salvando ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="h-4 w-4" />
                  )}
                  {novo ? 'Cadastrar' : 'Gravar'}
                </Button>
              )}
            </div>
          </div>

          {verHistorico && (
            <div className="max-h-64 space-y-2 overflow-y-auto border-t border-slate-100 pt-3">
              {(versoes.data ?? []).map((v) => (
                <div key={v.id} className="rounded-lg bg-slate-50 p-2.5 text-xs">
                  <div className="flex items-center justify-between text-slate-500">
                    <span className="font-medium text-slate-700">v{v.numero}</span>
                    <span>
                      {new Date(v.criadoEm).toLocaleString('pt-BR')}
                      {v.criadoPorNome && ` · ${v.criadoPorNome}`}
                    </span>
                  </div>
                  {v.nota && <p className="mt-1 text-slate-600">{v.nota}</p>}
                  <button
                    type="button"
                    onClick={() => alterar('sql', v.sql)}
                    className="mt-1 text-[11px] text-sky-600 hover:underline"
                  >
                    Carregar este SQL no editor
                  </button>
                </div>
              ))}
              {versoes.data?.length === 0 && (
                <p className="text-xs text-slate-400">Nenhuma versão gravada ainda.</p>
              )}
            </div>
          )}
        </div>
      )}
    </Modal>
  );
}
