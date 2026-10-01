import { useMemo, useState } from 'react';
import { ClipboardCheck, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { useAuth } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';

import { ABAS_FILA, AbasFilaRegulacao } from '../components/AbasFilaRegulacao';
import { TabelaSolicitacoes } from '../components/TabelaSolicitacoes';
import { useResumoFilaRegulacao, useSolicitacoes } from '../api/solicitacoesQueries';
import type { FluxoRegulacao } from '../tiposSolicitacao';
import type { SistemaRegulacao } from '../types';

/**
 * A fila do agente regulador (plano 04) — o município inteiro, ou a unidade escolhida no topo.
 *
 * <p>A rota é gateada por `RegulacaoTriagem` (48); a ampliação do escopo é do backend. Esta tela
 * acrescenta o que só faz sentido para quem vê tudo: filtrar por fluxo e por sistema de destino,
 * e olhar só o que ainda não tem agente.</p>
 *
 * <p><b>As ações (assumir, ajustar, devolver, recusar) ainda não estão aqui</b> — são as tarefas
 * 3.5 e 3.6. Esta entrega é a visão; sem ela, o agente não tem por onde começar.</p>
 */
export function FilaRegulacaoPage() {
  const navegar = useNavigate();
  const [aba, setAba] = useState(ABAS_FILA[1].id);
  const [busca, setBusca] = useState('');
  const [buscaAplicada, setBuscaAplicada] = useState('');
  const [fluxo, setFluxo] = useState<FluxoRegulacao | ''>('');
  const [sistema, setSistema] = useState<SistemaRegulacao | ''>('');

  // Fila do agente: o município inteiro com "todas" no topo, ou a unidade escolhida lá — o backend
  // só amplia para quem tem o 48. Rascunhos, mesmo aqui, são só os do próprio agente.
  const resumo = useResumoFilaRegulacao(true);
  const unidades = useAuth((s) => s.unidades);
  const unidadeAtivaId = useAuth((s) => s.unidadeAtivaId);
  const unidadeAtiva = unidades.find((u) => u.id === unidadeAtivaId) ?? null;

  const status = useMemo(() => ABAS_FILA.find((a) => a.id === aba)?.status ?? [], [aba]);

  const filtro = useMemo(
    () => ({
      filaDoMunicipio: true,
      status,
      busca: buscaAplicada || undefined,
      fluxo: fluxo || undefined,
      sistema: sistema || undefined,
      tamanho: 50,
    }),
    [status, buscaAplicada, fluxo, sistema],
  );
  const pagina = useSolicitacoes(filtro);

  function submeterBusca(e: React.FormEvent) {
    e.preventDefault();
    setBuscaAplicada(busca.trim());
  }

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center gap-3">
        <ClipboardCheck className="size-6 text-red-700" />
        <div>
          <div className="flex items-center gap-1">
            <h1 className="text-xl font-semibold text-slate-900">Fila da regulação</h1>
            <AjudaManual artigo="regulacao-solicitacoes" secao="detalhe" />
          </div>
          <p className="text-sm text-slate-600">
            {resumo.data?.veTodasUnidades
              ? 'Todas as unidades do município.'
              : unidadeAtiva
                ? unidadeAtiva.nome
                : 'Unidade escolhida no topo.'}
          </p>
        </div>
      </header>

      <AbasFilaRegulacao ativa={aba} aoTrocar={setAba} resumo={resumo.data} />

      <div className="flex flex-wrap items-end gap-3">
        <form onSubmit={submeterBusca} className="flex max-w-md flex-1 gap-2">
          <Input
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Nome do paciente, CPF ou número"
            aria-label="Buscar solicitação"
          />
          <Button variante="secundaria" type="submit">
            <Search className="size-4" />
            Buscar
          </Button>
        </form>

        <Campo label="Fluxo" htmlFor="filtro-fluxo">
          <Select
            id="filtro-fluxo"
            value={fluxo}
            onChange={(e) => setFluxo(e.target.value as FluxoRegulacao | '')}
          >
            <option value="">Todos</option>
            <option value="Interno">Interno (SISREG)</option>
            <option value="Externo">Externo</option>
            <option value="Nar">NAR</option>
          </Select>
        </Campo>

        <Campo label="Destino" htmlFor="filtro-destino">
          <Select
            id="filtro-destino"
            value={sistema}
            onChange={(e) => setSistema(e.target.value as SistemaRegulacao | '')}
          >
            <option value="">Todos</option>
            <option value="Sisreg">SISREG</option>
            <option value="Ser">SER (SES-RJ)</option>
            <option value="Sernit">SERNIT (Niterói)</option>
            <option value="EsusSg">ESUS São Gonçalo</option>
          </Select>
        </Campo>
      </div>

      <TabelaSolicitacoes
        dados={pagina.data?.itens ?? []}
        carregando={pagina.isLoading}
        mostrarUnidade={resumo.data?.veTodasUnidades ?? true}
        mostrarAgente
        aoAbrir={(s) =>
          navegar(
            s.status === 'Rascunho'
              ? `/app/regulacao/solicitacoes/${s.id}/editar`
              : `/app/regulacao/solicitacoes/${s.id}`,
          )
        }
        vazio="Nenhuma solicitação nesta situação com os filtros atuais."
      />

      {(pagina.data?.total ?? 0) > (pagina.data?.itens.length ?? 0) && (
        <p className="text-xs text-slate-500">
          Mostrando {pagina.data?.itens.length} de {pagina.data?.total}. Refine com os filtros ou a
          busca.
        </p>
      )}
    </div>
  );
}
