import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, ArrowLeft, ArrowRight, Check, FilePlus2, Loader2, Send } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Select } from '@/shared/ui/Select';
import { UploadAnexo } from '@/shared/ui/UploadAnexo';
import { CampoDinamico } from '@/shared/regulacao/CampoDinamico';
import { notificar } from '@/shared/ui/Notificacoes';
import { cn } from '@/shared/lib/cn';

import { useConfiguracaoFluxo } from '../api/queries';
import {
  useAnexarArquivo,
  useAtualizarSolicitacao,
  useCriarSolicitacao,
  useEnviarParaFila,
  useExigencias,
  useFormularioRegulacao,
  usePendencias,
  useRemoverArquivo,
} from '../api/solicitacoesQueries';
import { BuscaProcedimento } from '../components/BuscaProcedimento';
import { PassoPaciente } from '../components/wizard/PassoPaciente';
import { PassoRegras } from '../components/wizard/PassoRegras';
import { ExamesInternosSugeridos } from '../components/ExamesInternosSugeridos';
import type { FluxoRegulacao } from '../tiposSolicitacao';
import type { PacienteResumoRegulacao, RegulacaoProcedimentoItem } from '../types';

type Passo = 'procedimento' | 'destino' | 'paciente' | 'regras' | 'formulario' | 'revisao';

const PASSOS: { id: Passo; rotulo: string }[] = [
  { id: 'procedimento', rotulo: 'Procedimento' },
  { id: 'destino', rotulo: 'Destino' },
  { id: 'paciente', rotulo: 'Paciente' },
  // Entrou no incremento 4, entre paciente e formulário: as regras dependem de quem é o
  // paciente (idade, sexo, CID) e decidem o que o formulário vai exigir.
  { id: 'regras', rotulo: 'Regras' },
  { id: 'formulario', rotulo: 'Formulário e anexos' },
  { id: 'revisao', rotulo: 'Revisão' },
];

/**
 * Abertura de solicitação (planos 02 e 10, ordem D-5).
 *
 * <p><b>Por que o procedimento vem antes do paciente.</b> É a escolha do procedimento que revela
 * se há oferta interna em Maricá e em quais sistemas externos ele existe — e é isso que decide o
 * destino. Perguntar o paciente primeiro obrigaria a refazer o caminho quando o destino não
 * tivesse oferta.</p>
 *
 * <p><b>A solicitação nasce como rascunho ao entrar no formulário</b>, e não no fim: os anexos
 * precisam de um dono para serem enviados, e um rascunho salvo é melhor do que perder o
 * preenchimento se a tela fechar.</p>
 *
 * <p>O passo de <b>regras de elegibilidade</b> (D-5) ainda não existe: o motor é do incremento 4.
 * Quando entrar, encaixa entre "Paciente" e "Formulário".</p>
 */
export function NovaSolicitacaoPage() {
  const navegar = useNavigate();
  const config = useConfiguracaoFluxo();

  const [passo, setPasso] = useState<Passo>('procedimento');
  const [procedimento, setProcedimento] = useState<RegulacaoProcedimentoItem | null>(null);
  const [fluxo, setFluxo] = useState<FluxoRegulacao | null>(null);
  const [destino, setDestino] = useState<string>('');
  const [paciente, setPaciente] = useState<PacienteResumoRegulacao | null>(null);
  const [solicitacaoId, setSolicitacaoId] = useState<string | null>(null);
  const [valores, setValores] = useState<Record<string, string>>({});

  const criar = useCriarSolicitacao();
  const atualizar = useAtualizarSolicitacao();
  const enviar = useEnviarParaFila();
  const anexar = useAnexarArquivo();
  const remover = useRemoverArquivo();

  const formulario = useFormularioRegulacao(procedimento?.id ?? null, fluxo);
  const exigencias = useExigencias(solicitacaoId);
  const pendencias = usePendencias(passo === 'revisao' ? solicitacaoId : null);

  const temInterno = (procedimento?.executantesInternos.length ?? 0) > 0;
  const temExterno = !!procedimento?.existeExterno.ser || !!procedimento?.existeExterno.sernit;

  /**
   * Com oferta interna, o normal é resolver dentro do município — o Externo só aparece quando a
   * configuração permite, ou quando não há oferta interna nenhuma.
   */
  const fluxosDisponiveis = useMemo<FluxoRegulacao[]>(() => {
    const lista: FluxoRegulacao[] = [];
    if (temInterno) lista.push('Interno');
    if (temExterno && (!temInterno || config.data?.permitirExternoComInterno)) lista.push('Externo');
    lista.push('Nar');
    return lista;
  }, [temInterno, temExterno, config.data?.permitirExternoComInterno]);

  const destinosExternos = useMemo(() => {
    const lista: { valor: string; rotulo: string }[] = [];
    if (procedimento?.existeExterno.ser) lista.push({ valor: 'Ser', rotulo: 'SER (SES-RJ)' });
    if (procedimento?.existeExterno.sernit) lista.push({ valor: 'Sernit', rotulo: 'SERNIT (Niterói)' });
    return lista;
  }, [procedimento]);

  const indice = PASSOS.findIndex((p) => p.id === passo);

  function podeAvancar(): boolean {
    if (passo === 'procedimento') return !!procedimento;
    if (passo === 'destino') return !!fluxo && (fluxo !== 'Externo' || !!destino);
    if (passo === 'paciente') return !!paciente;
    return true;
  }

  /**
   * Ao sair do passo do paciente a solicitação vira rascunho. Antes isso acontecia só ao entrar
   * no formulário; a avaliação de regras (incremento 4) precisa da solicitação existindo, porque
   * é nela que os destinos e as caixinhas são gravados.
   */
  async function avancar() {
    if (passo === 'paciente' && !solicitacaoId && procedimento && paciente && fluxo) {
      try {
        const s = await criar.mutateAsync({
          fluxo,
          procedimentoId: procedimento.id,
          pacienteId: paciente.id,
          sistemaDestino: fluxo === 'Externo' ? destino : fluxo === 'Nar' ? 'Sisreg' : null,
        });
        setSolicitacaoId(s.id);
        notificar(`Rascunho ${s.numeroLocal} criado.`);
      } catch {
        return; // o interceptor já mostrou o erro
      }
    }
    setPasso(PASSOS[Math.min(indice + 1, PASSOS.length - 1)].id);
  }

  async function salvarFormulario() {
    if (!solicitacaoId) return;
    await atualizar.mutateAsync({ id: solicitacaoId, formulario: valores });
  }

  async function enviarParaAFila() {
    if (!solicitacaoId) return;
    try {
      await salvarFormulario();
      const s = await enviar.mutateAsync(solicitacaoId);
      notificar(`Solicitação ${s.numeroLocal} enviada para a pré-regulação.`);
      navegar('/app/regulacao/configuracao');
    } catch {
      // Validação com a lista de pendências já chega pelo interceptor.
      pendencias.refetch();
    }
  }

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <header className="flex items-center gap-3">
        <FilePlus2 className="size-6 text-red-600" />
        <div>
          <h1 className="text-xl font-semibold">Nova solicitação</h1>
          <p className="text-sm text-slate-500">
            Escolha o procedimento, o destino e o paciente; o formulário se monta conforme o
            sistema de destino.
          </p>
        </div>
      </header>

      <ol className="flex flex-wrap gap-1 rounded-md border border-slate-200 bg-white p-1 text-sm">
        {PASSOS.map((p, i) => (
          <li key={p.id} className="flex-1">
            <div
              className={cn(
                'rounded px-3 py-1.5 text-center',
                i === indice
                  ? 'bg-red-600 font-medium text-white'
                  : i < indice
                    ? 'text-emerald-700'
                    : 'text-slate-400',
              )}
            >
              {i < indice ? <Check className="mr-1 inline size-3.5" /> : null}
              {p.rotulo}
            </div>
          </li>
        ))}
      </ol>

      <section className="rounded-md border border-slate-200 bg-white p-4">
        {passo === 'procedimento' ? (
          <BuscaProcedimento value={procedimento} onChange={setProcedimento} autoFocus />
        ) : null}

        {passo === 'destino' ? (
          <div className="space-y-4">
            <Campo label="Para onde vai esta solicitação?" htmlFor="fluxo">
              <div className="space-y-2">
                {fluxosDisponiveis.map((f) => (
                  <label key={f} className="flex cursor-pointer items-start gap-2">
                    <input
                      type="radio"
                      name="fluxo"
                      id={f === fluxosDisponiveis[0] ? "fluxo" : undefined}
                      checked={fluxo === f}
                      onChange={() => {
                        setFluxo(f);
                        if (f !== 'Externo') setDestino('');
                      }}
                      className="mt-0.5 accent-red-600"
                    />
                    <span>
                      <span className="block text-sm font-medium text-slate-800">
                        {f === 'Interno' ? 'Interno (SISREG — Maricá)' : f === 'Externo' ? 'Externo' : 'NAR (agendamento indireto)'}
                      </span>
                      <span className="block text-xs text-slate-500">
                        {f === 'Interno'
                          ? `${procedimento?.executantesInternos.length} unidade(s) com vaga no SISREG.`
                          : f === 'Externo'
                            ? 'Executado pelo Estado ou por outro município.'
                            : 'Sempre SISREG, em nome de outra unidade solicitante.'}
                      </span>
                    </span>
                  </label>
                ))}
              </div>
            </Campo>

            {fluxo === 'Externo' ? (
              <Campo label="Sistema de destino" htmlFor="destino">
                <Select
                  id="destino"
                  value={destino}
                  onChange={(e) => setDestino(e.target.value)}
                  className="max-w-xs"
                >
                  <option value="">Selecione…</option>
                  {destinosExternos.map((d) => (
                    <option key={d.valor} value={d.valor}>
                      {d.rotulo}
                    </option>
                  ))}
                </Select>
              </Campo>
            ) : null}

            {fluxo === 'Nar' ? (
              <p className="rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                O NAR precisa da unidade em nome de quem a solicitação é aberta — esse passo entra
                junto com a fila do agente regulador (incremento 3). Por ora, use Interno ou Externo.
              </p>
            ) : null}
          </div>
        ) : null}

        {passo === 'paciente' ? (
          <PassoPaciente
            value={paciente}
            onChange={setPaciente}
            exigirCpf={config.data?.exigirCpf ?? true}
          />
        ) : null}

        {passo === 'regras' ? (
          <PassoRegras solicitacaoId={solicitacaoId} />
        ) : passo === 'formulario' ? (
          <div className="space-y-5">
            {formulario.isLoading ? <p className="text-sm text-slate-500">Montando o formulário…</p> : null}

            <div className="flex flex-wrap gap-4">
              {(formulario.data?.campos ?? []).map((c) => (
                <div key={c.chave} className="min-w-72">
                  <CampoDinamico
                    sistema={destino === 'Sernit' ? 'SERNIT' : 'SER'}
                    c={{
                      numero: c.chave,
                      campo: c.chave,
                      rotulo: c.rotulo,
                      tipo: c.tipo,
                      obrigatorio: c.obrigatorio,
                      opcoes: c.opcoes?.map((o) => ({ valor: o.valor, rotulo: o.rotulo })) ?? null,
                    }}
                    valor={valores[c.chave] ?? ''}
                    onChange={(v) => setValores((atual) => ({ ...atual, [c.chave]: v }))}
                  />
                  {c.origens.length === 1 && fluxo === 'Externo' ? (
                    <p className="mt-0.5 text-[11px] text-slate-400">
                      exigido só pelo {c.origens[0] === 'Ser' ? 'SER' : 'SERNIT'}
                    </p>
                  ) : null}
                </div>
              ))}
            </div>

            <div className="space-y-2 border-t border-slate-200 pt-4">
              <h2 className="text-sm font-semibold text-slate-700">Anexos</h2>
              {(exigencias.data ?? []).map((e) => (
                <div key={e.id}>
                <UploadAnexo
                  titulo={e.titulo}
                  obrigatoria={e.obrigatoria}
                  situacao={e.criticaTexto ?? undefined}
                  accept={config.data?.anexoTiposPermitidos ?? ['application/pdf']}
                  limiteMb={config.data?.anexoLimiteMb ?? 15}
                  arquivos={e.arquivos}
                  onEnviar={async (files) => {
                    for (const arquivo of files) {
                      await anexar.mutateAsync({ solicitacaoId: solicitacaoId!, exigenciaId: e.id, arquivo });
                    }
                  }}
                  onRemover={async (arquivoId) => {
                    await remover.mutateAsync({ solicitacaoId: solicitacaoId!, arquivoId });
                  }}
                />
                {/* Só nas caixinhas de regra: em "Anexos gerais" não há o que sugerir, porque
                    não existe um documento específico sendo pedido. */}
                {e.regraId && solicitacaoId && (
                  <ExamesInternosSugeridos solicitacaoId={solicitacaoId} exigenciaId={e.id} />
                )}
                </div>
              ))}
            </div>
          </div>
        ) : null}

        {passo === 'revisao' ? (
          <div className="space-y-4">
            <dl className="grid gap-2 sm:grid-cols-2">
              <Linha rotulo="Procedimento" valor={procedimento?.nome} />
              <Linha rotulo="Destino" valor={fluxo === 'Externo' ? destino : fluxo ?? undefined} />
              <Linha rotulo="Paciente" valor={paciente?.nome} />
              <Linha rotulo="CPF" valor={paciente?.cpf ?? 'não informado'} />
            </dl>

            {pendencias.isLoading ? (
              <p className="text-sm text-slate-500">Conferindo…</p>
            ) : (pendencias.data?.length ?? 0) > 0 ? (
              <div className="rounded-md border border-amber-300 bg-amber-50 p-3">
                <p className="flex items-center gap-1.5 text-sm font-medium text-amber-900">
                  <AlertTriangle className="size-4" /> Ainda falta:
                </p>
                <ul className="mt-1 list-inside list-disc text-sm text-amber-800">
                  {pendencias.data!.map((p) => (
                    <li key={p.codigo}>{p.descricao}</li>
                  ))}
                </ul>
              </div>
            ) : (
              <p className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
                Tudo certo — a solicitação pode ir para a pré-regulação.
              </p>
            )}
          </div>
        ) : null}
      </section>

      <div className="flex items-center justify-between">
        <Button
          variante="outline"
          disabled={indice === 0}
          onClick={() => setPasso(PASSOS[Math.max(indice - 1, 0)].id)}
        >
          <ArrowLeft className="mr-1 size-4" /> Voltar
        </Button>

        {passo === 'revisao' ? (
          <Button
            disabled={enviar.isPending || (pendencias.data?.length ?? 1) > 0}
            onClick={enviarParaAFila}
          >
            {enviar.isPending ? (
              <Loader2 className="mr-1 size-4 animate-spin" />
            ) : (
              <Send className="mr-1 size-4" />
            )}
            Enviar para a pré-regulação
          </Button>
        ) : (
          <Button
            disabled={!podeAvancar() || criar.isPending}
            onClick={async () => {
              if (passo === 'formulario') await salvarFormulario();
              await avancar();
            }}
          >
            {criar.isPending ? <Loader2 className="mr-1 size-4 animate-spin" /> : null}
            Avançar <ArrowRight className="ml-1 size-4" />
          </Button>
        )}
      </div>
    </div>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor?: string }) {
  return (
    <div className="rounded border border-slate-200 px-3 py-2">
      <dt className="text-xs text-slate-500">{rotulo}</dt>
      <dd className="text-sm text-slate-900">{valor ?? '—'}</dd>
    </div>
  );
}
