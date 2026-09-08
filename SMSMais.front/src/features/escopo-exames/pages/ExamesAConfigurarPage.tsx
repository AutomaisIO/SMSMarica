import { Link } from 'react-router-dom';
import { AlertTriangle, ArrowRight } from 'lucide-react';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { Select } from '@/shared/ui/Select';
import { useState } from 'react';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { rotuloModalidade } from '@/features/equipamentos/types';
import { usePendenciasEscopo } from '@/features/escopo-exames/api/queries';
import type { PendenciaEscopoExame } from '@/features/escopo-exames/types';
import { SituacaoBadge } from '@/features/escopo-exames/components/SituacaoBadge';

/**
 * A fila de trabalho: exames que uma unidade executa mas que ainda não chegam ao aparelho.
 *
 * Existe porque, antes, ninguém descobria que faltava configurar até um paciente estar no balcão e
 * o erro aparecer na autorização. Como a importação passa a trazer o exame para o escopo
 * DESLIGADO, a pendência tem lugar próprio e é visível antes de virar problema.
 *
 * Só aparecem unidades que TÊM aparelho — cobrar destino DICOM de quem não tem máquina seria ruído
 * (Hospital Santo Antônio executa radiografia e não tem equipamento cadastrado).
 */
export function ExamesAConfigurarPage() {
  const [unidadeId, setUnidadeId] = useState('');
  const pendencias = usePendenciasEscopo(unidadeId || undefined);
  const unidades = useListarUnidades();

  const itens = pendencias.data ?? [];
  const parados = itens.reduce((soma, i) => soma + i.examesParados, 0);

  const colunas: Coluna<PendenciaEscopoExame>[] = [
    {
      chave: 'unidade',
      cabecalho: 'Unidade',
      render: (p) => (
        <Link
          to={`/app/unidades/${p.unidadeId}`}
          className="text-left font-medium text-red-700 hover:underline"
        >
          {p.unidadeNome}
        </Link>
      ),
    },
    {
      chave: 'exame',
      cabecalho: 'Exame',
      render: (p) => (
        <div>
          <span className="text-gray-900">{p.tipoExameNome}</span>
          <span className="block text-xs text-gray-500">{rotuloModalidade(p.modalidadeDicom)}</span>
        </div>
      ),
    },
    {
      chave: 'falta',
      cabecalho: 'O que falta',
      render: (p) => (
        <div className="flex flex-wrap items-center gap-2">
          <SituacaoBadge situacao={p.situacao} compativeis={p.situacao === 'ADefinir' ? 2 : 0} />
          <span className="text-sm text-gray-600">{p.oQueFalta}</span>
        </div>
      ),
    },
    {
      chave: 'parados',
      cabecalho: 'Pedidos parados',
      className: 'text-right tabular-nums',
      render: (p) => (
        <span className={p.examesParados > 0 ? 'font-semibold text-amber-700' : 'text-gray-400'}>
          {p.examesParados}
        </span>
      ),
    },
    {
      chave: 'acao',
      cabecalho: '',
      className: 'text-right',
      render: (p) => (
        <Link
          to={`/app/unidades/${p.unidadeId}`}
          className="inline-flex items-center gap-1 text-sm font-medium text-red-700 hover:underline"
          title="Abrir a unidade para configurar"
        >
          Configurar <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Exames a configurar</h1>
        <p className="mt-1 text-sm text-gray-600">
          Exames que a unidade executa mas que ainda não chegam ao aparelho — porque o envio à
          worklist está desligado ou porque falta definir o destino.
        </p>
      </header>

      {parados > 0 ? (
        <div className="flex items-center gap-2 rounded-md border-l-4 border-amber-500 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          <span>
            <strong>{parados}</strong> {parados === 1 ? 'pedido está parado' : 'pedidos estão parados'}{' '}
            aguardando essa configuração.
          </span>
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Select
          aria-label="Filtrar por unidade"
          value={unidadeId}
          onChange={(e) => setUnidadeId(e.target.value)}
          className="max-w-xs"
        >
          <option value="">Todas as unidades</option>
          {(unidades.data ?? [])
            .filter((u) => u.ativo)
            .map((u) => (
              <option key={u.id} value={u.id}>
                {u.nome}
              </option>
            ))}
        </Select>
      </div>

      <Tabela
        colunas={colunas}
        dados={itens}
        chaveLinha={(p) => p.id}
        carregando={pendencias.isLoading}
        vazio="Nada a configurar: todos os exames das unidades com aparelho já estão indo à worklist."
      />
    </div>
  );
}
