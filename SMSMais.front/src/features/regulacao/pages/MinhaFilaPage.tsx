import { useMemo, useState } from 'react';
import { ClipboardList, FilePlus2, Search } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';

import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';

import { ABAS_FILA, AbasFilaRegulacao } from '../components/AbasFilaRegulacao';
import { TabelaSolicitacoes } from '../components/TabelaSolicitacoes';
import { useResumoFilaRegulacao, useSolicitacoes } from '../api/solicitacoesQueries';

/**
 * A fila da unidade solicitante (plano 04).
 *
 * <p>Mostra o que a unidade abriu e em que pé está. O agente regulador tem a tela dele — esta
 * responde a pergunta da ponta: "e o pedido daquela paciente?".</p>
 *
 * <p><b>O escopo é do backend, não desta tela.</b> Quem tem o módulo 48 recebe daqui o município
 * inteiro; por isso o aviso no topo, para o agente saber que está vendo tudo e não confundir com
 * a fila da própria unidade.</p>
 */
export function MinhaFilaPage() {
  const navegar = useNavigate();
  const [aba, setAba] = useState(ABAS_FILA[1].id); // pré-regulação: onde a espera acontece
  const [busca, setBusca] = useState('');
  const [buscaAplicada, setBuscaAplicada] = useState('');

  const resumo = useResumoFilaRegulacao();

  const status = useMemo(
    () => ABAS_FILA.find((a) => a.id === aba)?.status ?? [],
    [aba],
  );

  const filtro = useMemo(
    () => ({ status, busca: buscaAplicada || undefined, tamanho: 50 }),
    [status, buscaAplicada],
  );
  const pagina = useSolicitacoes(filtro);

  function submeterBusca(e: React.FormEvent) {
    e.preventDefault();
    setBuscaAplicada(busca.trim());
  }

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center gap-3">
        <ClipboardList className="size-6 text-red-700" />
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Solicitações</h1>
          <p className="text-sm text-slate-600">
            {resumo.data?.veTodasUnidades
              ? 'Você está vendo as solicitações de todas as unidades.'
              : 'Solicitações abertas pelas suas unidades.'}
          </p>
        </div>
        <div className="ml-auto">
          <Link to="/app/regulacao/solicitacoes/nova">
            <Button>
              <FilePlus2 className="size-4" />
              Nova solicitação
            </Button>
          </Link>
        </div>
      </header>

      <AbasFilaRegulacao ativa={aba} aoTrocar={setAba} resumo={resumo.data} />

      <form onSubmit={submeterBusca} className="flex max-w-md gap-2">
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

      <TabelaSolicitacoes
        dados={pagina.data?.itens ?? []}
        carregando={pagina.isLoading}
        mostrarUnidade={resumo.data?.veTodasUnidades}
        aoAbrir={(s) => navegar(`/app/regulacao/solicitacoes/${s.id}`)}
        vazio={
          buscaAplicada
            ? 'Nenhuma solicitação encontrada para essa busca.'
            : 'Nenhuma solicitação nesta situação.'
        }
      />

      {(pagina.data?.total ?? 0) > (pagina.data?.itens.length ?? 0) && (
        <p className="text-xs text-slate-500">
          Mostrando {pagina.data?.itens.length} de {pagina.data?.total}. Use a busca para chegar a
          uma solicitação específica.
        </p>
      )}
    </div>
  );
}
