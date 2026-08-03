import { useMemo, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Loader2,
  MessageCircle,
  Network,
  RefreshCw,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import {
  useAlternarEnvioConfirmacao,
  useAlternarProcedimento,
  useAlternarProfissional,
  useAlternarProfissionaisEmLote,
  useAtualizarMapeamento,
  useMapeamento,
  useSalvarVarreduraAgenda,
  useSincronizarFhir,
  useVarreduraAgenda,
} from '@/features/sisreg-mapeamento/api/queries';
import type { SisregProfissional } from '@/features/sisreg-mapeamento/types';

type Props = {
  /** Unidade-alvo. Vai no header X-Unidade-Id de cada chamada, vencendo o seletor do topo. */
  unidadeId: string;
  podeEditar: boolean;
};

/**
 * A "verdade" do SISREG para uma unidade: quem executa o quê, com o habilita/desabilita que
 * define o custo da varredura da agenda.
 *
 * Cada par profissional × procedimento habilitado custa <b>uma requisição</b> ao SISREG, e o
 * SISREG bloqueia por volume — por isso os indicadores de custo ficam no topo, não escondidos.
 */
export function MapeamentoSisregSecao({ unidadeId, podeEditar }: Props) {
  const mapeamento = useMapeamento(unidadeId);
  const atualizar = useAtualizarMapeamento(unidadeId);
  const sincronizar = useSincronizarFhir(unidadeId);
  const alternarProf = useAlternarProfissional(unidadeId);
  const alternarProc = useAlternarProcedimento(unidadeId);
  const alternarLote = useAlternarProfissionaisEmLote(unidadeId);
  const alternarConfirmacao = useAlternarEnvioConfirmacao(unidadeId);
  const agenda = useVarreduraAgenda(unidadeId);
  const salvarAgenda = useSalvarVarreduraAgenda(unidadeId);

  const [expandidos, setExpandidos] = useState<Set<string>>(new Set());
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null);
  const [filtro, setFiltro] = useState('');
  const [soHabilitados, setSoHabilitados] = useState(false);

  const dados = mapeamento.data;

  const profissionais = useMemo(() => {
    const lista = dados?.profissionais ?? [];
    const termo = filtro.trim().toLowerCase();
    return lista.filter((p) => {
      if (soHabilitados && !p.habilitado) return false;
      if (!termo) return true;
      return (
        p.nome.toLowerCase().includes(termo) ||
        p.cpf.includes(termo) ||
        p.procedimentos.some(
          (proc) => proc.nome.toLowerCase().includes(termo) || proc.codigo.includes(termo),
        )
      );
    });
  }, [dados, filtro, soHabilitados]);

  /** Habilitados que a varredura vai pular por falta do de-para SIGTAP. */
  const semSigtap = useMemo(
    () =>
      (dados?.profissionais ?? [])
        .filter((p) => p.habilitado)
        .flatMap((p) => p.procedimentos)
        .filter((proc) => proc.habilitado && proc.sigtapPendente).length,
    [dados],
  );

  async function executar(acao: () => Promise<unknown>, sucesso: (r: unknown) => string) {
    setAviso(null);
    try {
      const resultado = await acao();
      setAviso({ tipo: 'ok', texto: sucesso(resultado) });
    } catch (e) {
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  /**
   * O gatilho mestre vive na configuração do SISREG da unidade, junto da agenda — por isso
   * reenvia os campos atuais dela: o back trata o payload como PATCH só para este flag.
   */
  function alternarGatilhoUnidade(enviar: boolean) {
    const atual = agenda.data;
    if (!atual) return;

    void executar(
      () =>
        salvarAgenda.mutateAsync({
          ativo: atual.ativo,
          horaLocal: atual.horaLocal.slice(0, 5),
          diasAFrente: atual.diasAFrente,
          enviarConfirmacao: enviar,
        }),
      () =>
        enviar
          ? 'Esta unidade voltará a avisar o paciente por WhatsApp ao importar.'
          : 'Esta unidade deixou de avisar o paciente por WhatsApp ao importar.',
    );
  }

  function alternarExpandido(id: string) {
    setExpandidos((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else novo.add(id);
      return novo;
    });
  }

  return (
    <>
      {aviso && (
        <div
          className={`flex items-start gap-2 rounded-md border p-3 text-sm ${
            aviso.tipo === 'ok'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
              : 'border-red-200 bg-red-50 text-red-700'
          }`}
        >
          {aviso.tipo === 'ok' ? (
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
          ) : (
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          )}
          <span>{aviso.texto}</span>
        </div>
      )}

      <section className="rounded-lg border border-gray-200 bg-white">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-200 p-4">
          <div className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
            <Indicador
              rotulo="Profissionais"
              valor={`${dados?.profissionaisHabilitados ?? 0}/${dados?.totalProfissionais ?? 0}`}
            />
            <Indicador
              rotulo="Procedimentos"
              valor={`${dados?.procedimentosHabilitados ?? 0}/${dados?.totalProcedimentos ?? 0}`}
            />
            <Indicador
              rotulo="Requisições por varredura"
              valor={String(dados?.combinacoesHabilitadas ?? 0)}
              destaque
            />
            {dados?.atualizadoEm && (
              <Indicador
                rotulo="Atualizado em"
                valor={new Date(dados.atualizadoEm).toLocaleString('pt-BR')}
              />
            )}
          </div>

          {podeEditar && (
            <div className="flex flex-wrap gap-2">
              <Button
                disabled={atualizar.isPending}
                onClick={() =>
                  executar(
                    () => atualizar.mutateAsync(),
                    (r) => (r as { mensagem: string }).mensagem,
                  )
                }
              >
                {atualizar.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" /> Consultando o SISREG…
                  </>
                ) : (
                  <>
                    <RefreshCw className="h-4 w-4" /> Atualizar mapeamento
                  </>
                )}
              </Button>
              <Button
                variante="outline"
                disabled={sincronizar.isPending}
                onClick={() =>
                  executar(
                    () => sincronizar.mutateAsync(),
                    (r) => (r as { mensagem: string }).mensagem,
                  )
                }
              >
                {sincronizar.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Network className="h-4 w-4" />
                )}
                Sincronizar profissionais (FHIR)
              </Button>
            </div>
          )}
        </div>

        {/* Gatilho mestre da unidade — acima da lista porque manda em todos os procedimentos. */}
        <div className="flex flex-wrap items-center gap-2 border-b border-gray-100 bg-gray-50 px-4 py-2">
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={agenda.data?.enviarConfirmacao ?? true}
              disabled={!podeEditar || !agenda.data || salvarAgenda.isPending}
              onChange={(e) => alternarGatilhoUnidade(e.target.checked)}
            />
            <MessageCircle className="h-4 w-4 text-gray-500" />
            Avisar o paciente por WhatsApp ao importar solicitações desta unidade
          </label>
          <span className="text-xs text-gray-500">
            Vale para os dois caminhos: varredura da agenda e upload de arquivo.
          </span>
        </div>

        {agenda.data && !agenda.data.enviarConfirmacao && (
          <p className="flex items-start gap-2 border-b border-amber-100 bg-amber-50 px-4 py-2 text-sm text-amber-800">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            Nenhum paciente desta unidade receberá confirmação por WhatsApp ao ser importado —
            nem pela varredura, nem por arquivo.
          </p>
        )}

        {semSigtap > 0 && (
          <p className="flex items-start gap-2 border-b border-amber-100 bg-amber-50 px-4 py-2 text-sm text-amber-800">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <strong>{semSigtap}</strong> procedimentos habilitados ainda não têm código SIGTAP
              confirmado e <strong>não serão varridos</strong>. A agenda do SISREG não informa
              SIGTAP; sem ele a solicitação nasceria sem categoria e sem worklist.
            </span>
          </p>
        )}

        {/* filtros */}
        <div className="flex flex-wrap items-center gap-3 border-b border-gray-100 p-3">
          <input
            className="input flex-1 min-w-[220px]"
            placeholder="Filtrar por profissional, CPF ou procedimento…"
            value={filtro}
            onChange={(e) => setFiltro(e.target.value)}
          />
          <label className="flex items-center gap-2 text-sm text-gray-600">
            <input
              type="checkbox"
              checked={soHabilitados}
              onChange={(e) => setSoHabilitados(e.target.checked)}
            />
            Só habilitados
          </label>
          {podeEditar && (
            <Button
              variante="ghost"
              tamanho="sm"
              disabled={alternarLote.isPending || profissionais.length === 0}
              onClick={() =>
                executar(
                  () =>
                    alternarLote.mutateAsync({
                      ids: profissionais.map((p) => p.id),
                      habilitado: false,
                    }),
                  () => 'Profissionais listados desabilitados.',
                )
              }
            >
              Desabilitar listados
            </Button>
          )}
        </div>

        {/* lista */}
        {mapeamento.isLoading ? (
          <p className="p-6 text-sm text-gray-500">Carregando mapeamento…</p>
        ) : mapeamento.isError ? (
          <p className="p-6 text-sm text-red-600">{extrairMensagemDeErro(mapeamento.error)}</p>
        ) : profissionais.length === 0 ? (
          <div className="p-6 text-sm text-gray-600">
            {dados?.totalProfissionais === 0 ? (
              <>
                Nenhum profissional mapeado ainda. Clique em <strong>Atualizar mapeamento</strong>{' '}
                para buscar a lista da unidade no SISREG.
              </>
            ) : (
              'Nenhum profissional corresponde ao filtro.'
            )}
          </div>
        ) : (
          <ul className="divide-y divide-gray-100">
            {profissionais.map((p) => (
              <LinhaProfissional
                key={p.id}
                profissional={p}
                podeEditar={podeEditar}
                expandido={expandidos.has(p.id)}
                aoExpandir={() => alternarExpandido(p.id)}
                aoAlternarProfissional={(habilitado) =>
                  executar(
                    () => alternarProf.mutateAsync({ id: p.id, habilitado }),
                    () => `${p.nome} ${habilitado ? 'habilitado' : 'desabilitado'}.`,
                  )
                }
                aoAlternarProcedimento={(id, habilitado, nome) =>
                  executar(
                    () => alternarProc.mutateAsync({ id, habilitado }),
                    () => `${nome} ${habilitado ? 'habilitado' : 'desabilitado'}.`,
                  )
                }
                aoAlternarConfirmacao={(procedimentoId, enviar, nome) =>
                  executar(
                    () => alternarConfirmacao.mutateAsync({ id: procedimentoId, enviar }),
                    (r) => {
                      const afetados = (r as { afetados: number }).afetados;
                      // Diz quantos profissionais foram afetados: o operador clicou numa linha e
                      // mudou várias — melhor mostrar do que deixar ele descobrir depois.
                      const alcance = afetados > 1 ? ` (${afetados} profissionais)` : '';
                      return enviar
                        ? `${nome}: aviso por WhatsApp ligado nesta unidade${alcance}.`
                        : `${nome}: aviso por WhatsApp desligado nesta unidade${alcance}.`;
                    },
                  )
                }
              />
            ))}
          </ul>
        )}
      </section>
    </>
  );
}

function Indicador({
  rotulo,
  valor,
  destaque,
}: {
  rotulo: string;
  valor: string;
  destaque?: boolean;
}) {
  return (
    <span className="flex flex-col">
      <span className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className={destaque ? 'font-semibold text-red-700' : 'font-medium text-gray-900'}>
        {valor}
      </span>
    </span>
  );
}

function LinhaProfissional({
  profissional,
  podeEditar,
  expandido,
  aoExpandir,
  aoAlternarProfissional,
  aoAlternarProcedimento,
  aoAlternarConfirmacao,
}: {
  profissional: SisregProfissional;
  podeEditar: boolean;
  expandido: boolean;
  aoExpandir: () => void;
  aoAlternarProfissional: (habilitado: boolean) => void;
  aoAlternarProcedimento: (id: string, habilitado: boolean, nome: string) => void;
  aoAlternarConfirmacao: (procedimentoId: string, enviar: boolean, nome: string) => void;
}) {
  const habilitadosNoProfissional = profissional.procedimentos.filter((p) => p.habilitado).length;

  return (
    <li className={profissional.ausente ? 'bg-gray-50' : undefined}>
      <div className="flex items-center gap-3 p-3">
        <input
          type="checkbox"
          checked={profissional.habilitado}
          disabled={!podeEditar}
          onChange={(e) => aoAlternarProfissional(e.target.checked)}
          aria-label={`Habilitar ${profissional.nome}`}
        />

        <button
          type="button"
          onClick={aoExpandir}
          className="flex flex-1 items-center gap-2 text-left"
        >
          {expandido ? (
            <ChevronDown className="h-4 w-4 text-gray-400" />
          ) : (
            <ChevronRight className="h-4 w-4 text-gray-400" />
          )}
          <span className="flex-1">
            <span className="font-medium text-gray-900">{profissional.nome}</span>
            <span className="ml-2 font-mono text-xs text-gray-500">{profissional.cpf}</span>
            {profissional.ausente && (
              <span className="ml-2 rounded bg-gray-200 px-1.5 py-0.5 text-xs text-gray-600">
                ausente no SISREG
              </span>
            )}
          </span>
        </button>

        <span className="text-xs text-gray-500">
          {habilitadosNoProfissional}/{profissional.procedimentos.length} procedimentos
        </span>

        {profissional.practitionerId ? (
          <span
            className="rounded bg-emerald-50 px-1.5 py-0.5 text-xs text-emerald-700"
            title="Sincronizado com o hub FHIR"
          >
            FHIR
          </span>
        ) : (
          <span className="rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-500">—</span>
        )}
      </div>

      {expandido && (
        <ul className="space-y-1 border-t border-gray-100 bg-gray-50 px-3 py-2 pl-12">
          {profissional.procedimentos.length === 0 ? (
            <li className="py-1 text-sm text-gray-500">
              Nenhum procedimento cadastrado para este profissional na unidade.
            </li>
          ) : (
            profissional.procedimentos.map((proc) => (
              <li key={proc.id} className="flex flex-wrap items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={proc.habilitado}
                  disabled={!podeEditar}
                  onChange={(e) => aoAlternarProcedimento(proc.id, e.target.checked, proc.nome)}
                  aria-label={`Habilitar ${proc.nome}`}
                />
                <span className="font-mono text-xs text-gray-500">{proc.codigo}</span>
                <span className="text-gray-800">{proc.nome}</span>
                {proc.grupo && (
                  <span
                    className="rounded bg-blue-50 px-1.5 py-0.5 text-xs text-blue-700"
                    title="Grupo: a consulta já traz os itens individuais — habilitar os dois duplica requisição"
                  >
                    grupo
                  </span>
                )}
                {proc.sigtapPendente ? (
                  <span
                    className="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-800"
                    title="Sem código SIGTAP confirmado, este procedimento NÃO entra na varredura: a agenda do SISREG não informa SIGTAP, e sem ele a solicitação nasceria sem categoria e sem worklist."
                  >
                    sem SIGTAP
                  </span>
                ) : (
                  <span
                    className="rounded bg-emerald-50 px-1.5 py-0.5 font-mono text-xs text-emerald-700"
                    title="Código SIGTAP confirmado no de-para"
                  >
                    {proc.codigoSigtap}
                  </span>
                )}
                {proc.ausente && (
                  <span className="rounded bg-gray-200 px-1.5 py-0.5 text-xs text-gray-600">
                    ausente
                  </span>
                )}

                {/* Aviso ao paciente para ESTE procedimento NESTA unidade. O título avisa que a
                    mudança alcança o mesmo procedimento sob os outros profissionais da unidade. */}
                <label
                  className="ml-auto flex shrink-0 items-center gap-1.5 text-xs text-gray-600"
                  title={
                    'Avisar o paciente por WhatsApp ao importar este procedimento nesta unidade. ' +
                    'Vale só para esta unidade, e alcança o mesmo procedimento sob os demais ' +
                    'profissionais dela.'
                  }
                >
                  <input
                    type="checkbox"
                    checked={proc.enviarConfirmacao}
                    disabled={!podeEditar}
                    onChange={(e) => aoAlternarConfirmacao(proc.id, e.target.checked, proc.nome)}
                    aria-label={`Avisar por WhatsApp ao importar ${proc.nome}`}
                  />
                  <MessageCircle
                    className={`h-3.5 w-3.5 ${
                      proc.enviarConfirmacao ? 'text-emerald-600' : 'text-gray-400'
                    }`}
                  />
                  zap
                </label>
              </li>
            ))
          )}
        </ul>
      )}
    </li>
  );
}
