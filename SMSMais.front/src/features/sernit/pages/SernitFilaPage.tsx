import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ClipboardList, Search, X } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useBuscaSernit, useResumoSernit } from '@/features/sernit/api/queries';
import { SituacaoSernitBadge } from '@/features/sernit/components/SituacaoSernitBadge';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import {
  ROTULO_SITUACAO,
  SITUACOES_SERNIT,
  type BuscaSernitFiltro,
  type SituacaoSernit,
  type SolicitacaoSernitLista,
} from '@/features/sernit/types';

const TAMANHO_PAGINA = 25;

function data(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('pt-BR');
}

/** CPF/CNS vêm só com dígitos do banco; formatamos na exibição. */
function cpf(valor: string | null): string {
  if (!valor) return '—';
  return valor.length === 11
    ? `${valor.slice(0, 3)}.${valor.slice(3, 6)}.${valor.slice(6, 9)}-${valor.slice(9)}`
    : valor;
}

export function SernitFilaPage() {
  const navegar = useNavigate();

  const [situacao, setSituacao] = useState<SituacaoSernit | ''>('EmFila');
  const [termo, setTermo] = useState('');
  const [termoAplicado, setTermoAplicado] = useState('');
  const [pagina, setPagina] = useState(1);

  const filtro = useMemo<BuscaSernitFiltro>(
    () => ({
      situacao: situacao || undefined,
      termo: termoAplicado || undefined,
      pagina,
      tamanho: TAMANHO_PAGINA,
    }),
    [situacao, termoAplicado, pagina],
  );

  const { data: resultado, isLoading } = useBuscaSernit(filtro);
  const { data: resumo } = useResumoSernit();

  const totalPaginas = resultado ? Math.max(1, Math.ceil(resultado.total / resultado.tamanho)) : 1;

  function aplicarBusca() {
    setTermoAplicado(termo.trim());
    setPagina(1);
  }

  function trocarSituacao(nova: SituacaoSernit | '') {
    setSituacao(nova);
    setPagina(1);
  }

  const colunas: Coluna<SolicitacaoSernitLista>[] = [
    {
      chave: 'idSernit',
      cabecalho: 'ID',
      className: 'w-24',
      ordenar: (s) => Number(s.idSernit),
      render: (s) => <span className="font-mono text-xs text-slate-600">{s.idSernit}</span>,
    },
    {
      chave: 'tipo',
      cabecalho: 'Tipo',
      className: 'w-24',
      ordenar: (s) => s.tipo,
      render: (s) => <span className="text-xs uppercase text-slate-500">{s.tipo}</span>,
    },
    {
      chave: 'recurso',
      cabecalho: 'Recurso',
      ordenar: (s) => s.recurso,
      render: (s) => <span className="text-sm">{s.recurso}</span>,
    },
    {
      chave: 'dataSolicitacao',
      cabecalho: 'Solicitado em',
      className: 'w-32',
      ordenar: (s) => s.dataSolicitacao ?? '',
      render: (s) => (
        <div className="text-sm">
          <div>{data(s.dataSolicitacao)}</div>
          {s.diasNaFila != null && (
            <div className="text-xs text-slate-500">{s.diasNaFila} dias</div>
          )}
        </div>
      ),
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      ordenar: (s) => s.pacienteNome,
      render: (s) => (
        <div>
          {/* Com o paciente já conciliado no hub, a linha ganha o bonequinho (resumo) e o
              atalho de WhatsApp — os mesmos do resto da aplicação. Sem conciliação ainda,
              mostra só o nome: os dois componentes falam por id, não por nome.
              O stopPropagation é essencial: a linha inteira navega para o detalhe, e sem ele
              clicar no bonequinho/zap abriria o modal e JÁ navegaria para fora por baixo. */}
          {s.pacienteId ? (
            <span onClick={(e) => e.stopPropagation()}>
              <NomePacienteComResumo
                pacienteId={s.pacienteId}
                nome={s.pacienteNome}
                classNameNome="text-sm font-medium"
                mostrarWhatsApp
              />
            </span>
          ) : (
            <div className="text-sm font-medium">{s.pacienteNome}</div>
          )}
          <div className="text-xs text-slate-500">{s.idadeTexto ?? ''}</div>
        </div>
      ),
    },
    {
      chave: 'cpf',
      cabecalho: 'CPF',
      className: 'w-36',
      render: (s) => <span className="font-mono text-xs">{cpf(s.cpf)}</span>,
    },
    {
      chave: 'cid',
      cabecalho: 'CID',
      render: (s) => <span className="text-xs text-slate-600">{s.cid ?? '—'}</span>,
    },
    {
      chave: 'situacao',
      cabecalho: 'Situação',
      className: 'w-40',
      ordenar: (s) => s.situacao,
      render: (s) => (
        <div className="space-y-1">
          <SituacaoSernitBadge situacao={s.situacao} />
          {s.situacaoAnterior && s.situacaoAnterior !== s.situacao && (
            <div className="text-[11px] text-slate-500">
              veio de {ROTULO_SITUACAO[s.situacaoAnterior]}
            </div>
          )}
        </div>
      ),
    },
    {
      chave: 'historico',
      cabecalho: 'Histórico',
      className: 'w-28',
      render: (s) =>
        s.historicoIndisponivel ? (
          // O SERNIT não oferece histórico para solicitações em Alta — explicar é melhor
          // que mostrar "0 eventos" e deixar o operador achar que perdemos o dado.
          <span className="text-xs text-slate-400" title="O SERNIT não oferece histórico nesta situação">
            indisponível
          </span>
        ) : (
          <span className="text-xs text-slate-600">{s.eventosCount} eventos</span>
        ),
    },
  ];

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <ClipboardList className="size-6 text-red-600" />
        <div>
          <h1 className="text-xl font-semibold">SERNIT — fila de Niterói</h1>
          <p className="text-sm text-slate-500">
            Solicitações de consulta e exame reguladas pelo SERNIT (Niterói), espelhadas na nossa base.
            A busca lê o nosso banco — o motor de varredura é quem fala com o SERNIT.
          </p>
        </div>
      </header>

      {/* Cards por situação: dão o tamanho da fila antes de qualquer filtro. */}
      {resumo && resumo.length > 0 && (
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => trocarSituacao('')}
            className={`rounded-lg border px-3 py-2 text-left transition ${
              situacao === '' ? 'border-red-300 bg-red-50' : 'border-slate-200 bg-white hover:bg-slate-50'
            }`}
          >
            <div className="text-lg font-semibold">
              {resumo.reduce((soma, r) => soma + r.quantidade, 0)}
            </div>
            <div className="text-xs text-slate-500">Todas</div>
          </button>

          {SITUACOES_SERNIT.map((s) => {
            const item = resumo.find((r) => r.situacao === s);
            if (!item) return null;
            return (
              <button
                key={s}
                type="button"
                onClick={() => trocarSituacao(s)}
                className={`rounded-lg border px-3 py-2 text-left transition ${
                  situacao === s ? 'border-red-300 bg-red-50' : 'border-slate-200 bg-white hover:bg-slate-50'
                }`}
              >
                <div className="text-lg font-semibold">{item.quantidade}</div>
                <div className="text-xs text-slate-500">{ROTULO_SITUACAO[s]}</div>
              </button>
            );
          })}
        </div>
      )}

      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-white p-4">
        <Campo label="Situação" htmlFor="sernit-situacao" className="w-56">
          <Select
            id="sernit-situacao"
            value={situacao}
            onChange={(e) => trocarSituacao(e.target.value as SituacaoSernit | '')}
          >
            <option value="">Todas</option>
            {SITUACOES_SERNIT.map((s) => (
              <option key={s} value={s}>
                {ROTULO_SITUACAO[s]}
              </option>
            ))}
          </Select>
        </Campo>

        <Campo label="Buscar" htmlFor="sernit-buscar" className="min-w-72 flex-1">
          <Input
            id="sernit-buscar"
            value={termo}
            onChange={(e) => setTermo(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && aplicarBusca()}
            placeholder="Nome, CPF, CNS, ID do SERNIT ou recurso"
          />
        </Campo>

        <Button onClick={aplicarBusca}>
          <Search className="size-4" /> Pesquisar
        </Button>

        {termoAplicado && (
          <Button
            variante="secundaria"
            onClick={() => {
              setTermo('');
              setTermoAplicado('');
              setPagina(1);
            }}
          >
            <X className="size-4" /> Limpar
          </Button>
        )}
      </div>

      <Tabela
        colunas={colunas}
        dados={resultado?.itens ?? []}
        chaveLinha={(s) => s.id}
        carregando={isLoading}
        scrollXFlutuante
        aoClicarLinha={(s) => navegar(`/app/regulacao/sernit/${s.id}`)}
        dicaLinha="Clique para ver o histórico da solicitação"
        vazio={
          <div className="py-8 text-center text-sm text-slate-500">
            Nenhuma solicitação encontrada. Se a base ainda está vazia, rode a carga inicial em
            Regulação → Configuração.
          </div>
        }
      />

      {resultado && resultado.total > 0 && (
        <div className="flex items-center justify-between text-sm text-slate-600">
          <span>
            {resultado.total} solicitação(ões) — página {resultado.pagina} de {totalPaginas}
          </span>
          <div className="flex gap-2">
            <Button
              variante="secundaria"
              disabled={pagina <= 1}
              onClick={() => setPagina((p) => Math.max(1, p - 1))}
            >
              Anterior
            </Button>
            <Button
              variante="secundaria"
              disabled={pagina >= totalPaginas}
              onClick={() => setPagina((p) => p + 1)}
            >
              Próxima
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
