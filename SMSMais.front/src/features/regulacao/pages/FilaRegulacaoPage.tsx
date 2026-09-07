import { useMemo, useState } from 'react';
import { ClipboardCheck, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

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
 * A fila do agente regulador — o município inteiro (plano 04).
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

  const resumo = useResumoFilaRegulacao();

  const status = useMemo(() => ABAS_FILA.find((a) => a.id === aba)?.status ?? [], [aba]);

  const filtro = useMemo(
    () => ({
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
          <h1 className="text-xl font-semibold text-slate-900">Fila da regulação</h1>
          <p className="text-sm text-slate-600">
            Todas as unidades do município. Clique numa solicitação para ver o caso inteiro.
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
          </Select>
        </Campo>
      </div>

      <TabelaSolicitacoes
        dados={pagina.data?.itens ?? []}
        carregando={pagina.isLoading}
        mostrarUnidade
        mostrarAgente
        aoAbrir={(s) => navegar(`/app/regulacao/solicitacoes/${s.id}`)}
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
