import { useMemo, useState } from 'react';
import {
  AlertTriangle,
  Building2,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  KeyRound,
  Loader2,
  Network,
  RefreshCw,
  Trash2,
  UserCheck,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useAuth } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { ModalCredencial } from '@/features/sisreg-mapeamento/components/ModalCredencial';
import {
  useAlternarProcedimento,
  useAlternarProfissional,
  useAlternarProfissionaisEmLote,
  useAtualizarMapeamento,
  useCredencialUnidade,
  useMapeamento,
  useRemoverCredencial,
  useSincronizarFhir,
  useTestarCredencial,
} from '@/features/sisreg-mapeamento/api/queries';
import type { SisregProfissional } from '@/features/sisreg-mapeamento/types';

/**
 * Mapeamento SISREG da unidade: a "verdade" de quem executa o quê.
 *
 * A tela inteira exige UMA unidade selecionada — a credencial do SISREG é de um operador que
 * enxerga uma unidade só, então "todas as unidades" não é um contexto válido aqui.
 */
export function SisregMapeamentoPage() {
  const unidadeAtivaId = useAuth((s) => s.unidadeAtivaId);
  const unidades = useAuth((s) => s.unidades);

  const mapeamento = useMapeamento(unidadeAtivaId);
  const credencial = useCredencialUnidade(unidadeAtivaId);
  const atualizar = useAtualizarMapeamento(unidadeAtivaId);
  const sincronizar = useSincronizarFhir(unidadeAtivaId);
  const testar = useTestarCredencial(unidadeAtivaId);
  const remover = useRemoverCredencial(unidadeAtivaId);
  const alternarProf = useAlternarProfissional(unidadeAtivaId);
  const alternarProc = useAlternarProcedimento(unidadeAtivaId);
  const alternarLote = useAlternarProfissionaisEmLote(unidadeAtivaId);

  const [expandidos, setExpandidos] = useState<Set<string>>(new Set());
  const [modalAberto, setModalAberto] = useState(false);
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null);
  const [filtro, setFiltro] = useState('');
  const [soHabilitados, setSoHabilitados] = useState(false);

  const nomeUnidade = unidades.find((u) => u.id === unidadeAtivaId)?.nome;

  const profissionais = useMemo(() => {
    const lista = mapeamento.data?.profissionais ?? [];
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
  }, [mapeamento.data, filtro, soHabilitados]);

  // ------------------------------------------------------- sem unidade: barra tudo
  if (!unidadeAtivaId) {
    return (
      <div className="p-6">
        <Cabecalho />
        <div className="mt-6 rounded-lg border border-amber-200 bg-amber-50 p-6">
          <div className="flex gap-3">
            <Building2 className="h-6 w-6 shrink-0 text-amber-600" />
            <div>
              <h2 className="font-medium text-amber-900">Selecione uma unidade</h2>
              <p className="mt-1 text-sm text-amber-800">
                O mapeamento do SISREG é sempre de <strong>uma</strong> unidade: a credencial usada
                é a do operador daquela unidade, e é a agenda dela que será varrida. Escolha a
                unidade no seletor do topo da tela para continuar.
              </p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  async function executar(acao: () => Promise<unknown>, sucesso: (r: unknown) => string) {
    setAviso(null);
    try {
      const resultado = await acao();
      setAviso({ tipo: 'ok', texto: sucesso(resultado) });
    } catch (e) {
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  function alternarExpandido(id: string) {
    setExpandidos((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else novo.add(id);
      return novo;
    });
  }

  const dados = mapeamento.data;
  const semCredencialPropria = credencial.data?.usandoFallbackGlobal ?? false;

  return (
    <div className="p-6">
      <Cabecalho nomeUnidade={nomeUnidade} />

      {/* ---------------------------------------------------------- credencial */}
      <section className="mt-6 rounded-lg border border-gray-200 bg-white p-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex gap-3">
            <KeyRound className="mt-0.5 h-5 w-5 shrink-0 text-gray-500" />
            <div>
              <h2 className="font-medium text-gray-900">Credencial do SISREG desta unidade</h2>
              {credencial.isLoading ? (
                <p className="mt-1 text-sm text-gray-500">Carregando…</p>
              ) : credencial.data?.usuario ? (
                <div className="mt-1 space-y-1 text-sm text-gray-600">
                  <p>
                    Usuário <strong className="font-mono">{credencial.data.usuario}</strong>
                    {credencial.data.unidadeSisregNome && (
                      <>
                        {' '}— confirmado em{' '}
                        <strong>{credencial.data.unidadeSisregNome}</strong>
                        {credencial.data.cnesConfirmado ? ` (CNES ${credencial.data.cnesConfirmado})` : ''}
                      </>
                    )}
                  </p>
                  {credencial.data.validadoEm && (
                    <p className="text-xs text-gray-500">
                      Última validação em{' '}
                      {new Date(credencial.data.validadoEm).toLocaleString('pt-BR')}
                    </p>
                  )}
                </div>
              ) : (
                <p className="mt-1 text-sm text-gray-600">
                  Nenhuma credencial própria — esta unidade está usando a credencial global das
                  Integrações, que enxerga apenas a unidade do operador dela.
                </p>
              )}
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            <Button variante="outline" onClick={() => setModalAberto(true)}>
              <KeyRound className="h-4 w-4" />
              {credencial.data?.usuario ? 'Trocar usuário/senha' : 'Cadastrar credencial'}
            </Button>
            <Button
              variante="secundaria"
              disabled={testar.isPending}
              onClick={() =>
                executar(
                  () => testar.mutateAsync(),
                  (r) => (r as { mensagem: string }).mensagem,
                )
              }
            >
              {testar.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <UserCheck className="h-4 w-4" />
              )}
              Testar
            </Button>
            {credencial.data?.usuario && (
              <Button
                variante="ghost"
                disabled={remover.isPending}
                onClick={() =>
                  executar(
                    () => remover.mutateAsync(),
                    () => 'Credencial da unidade removida — voltou a usar a credencial global.',
                  )
                }
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            )}
          </div>
        </div>

        {semCredencialPropria && !credencial.isLoading && (
          <p className="mt-3 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
            Sem credencial própria, atualizar o mapeamento vai falhar se a credencial global for de
            outra unidade — a conferência de unidade barra antes de gravar qualquer coisa errada.
          </p>
        )}
      </section>

      {aviso && (
        <div
          className={`mt-4 flex items-start gap-2 rounded-md border p-3 text-sm ${
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

      {/* ---------------------------------------------------------- resumo + ações */}
      <section className="mt-6 rounded-lg border border-gray-200 bg-white">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-200 p-4">
          <div className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
            <Indicador rotulo="Profissionais" valor={`${dados?.profissionaisHabilitados ?? 0}/${dados?.totalProfissionais ?? 0}`} />
            <Indicador rotulo="Procedimentos" valor={`${dados?.procedimentosHabilitados ?? 0}/${dados?.totalProcedimentos ?? 0}`} />
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
        </div>

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
              />
            ))}
          </ul>
        )}
      </section>

      <ModalCredencial
        aberto={modalAberto}
        unidadeId={unidadeAtivaId}
        credencial={credencial.data}
        aoFechar={() => setModalAberto(false)}
      />
    </div>
  );
}

function Cabecalho({ nomeUnidade }: { nomeUnidade?: string }) {
  return (
    <div>
      <h1 className="flex items-center gap-2 text-xl font-semibold text-gray-900">
        <Network className="h-5 w-5 text-red-600" /> Mapeamento SISREG
      </h1>
      <p className="mt-1 text-sm text-gray-600">
        Profissionais e procedimentos que o SISREG reconhece
        {nomeUnidade ? (
          <>
            {' '}em <strong>{nomeUnidade}</strong>
          </>
        ) : null}
        . O que estiver habilitado aqui é o que entra na varredura da agenda — cada par
        profissional × procedimento habilitado custa uma requisição ao SISREG.
      </p>
    </div>
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
  expandido,
  aoExpandir,
  aoAlternarProfissional,
  aoAlternarProcedimento,
}: {
  profissional: SisregProfissional;
  expandido: boolean;
  aoExpandir: () => void;
  aoAlternarProfissional: (habilitado: boolean) => void;
  aoAlternarProcedimento: (id: string, habilitado: boolean, nome: string) => void;
}) {
  const habilitadosNoProfissional = profissional.procedimentos.filter((p) => p.habilitado).length;

  return (
    <li className={profissional.ausente ? 'bg-gray-50' : undefined}>
      <div className="flex items-center gap-3 p-3">
        <input
          type="checkbox"
          checked={profissional.habilitado}
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
              <li key={proc.id} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={proc.habilitado}
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
                {proc.ausente && (
                  <span className="rounded bg-gray-200 px-1.5 py-0.5 text-xs text-gray-600">
                    ausente
                  </span>
                )}
              </li>
            ))
          )}
        </ul>
      )}
    </li>
  );
}
