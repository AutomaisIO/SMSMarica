import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CalendarClock, ClipboardList, Search, X } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { formatarWallClock, hojeSP } from '@/shared/lib/datas';
import { BadgeAnaliseRegras } from '@/shared/regulacao/analiseRegras/BadgeAnaliseRegras';
import { ChipsVeredito, OpcoesVeredito } from '@/shared/regulacao/analiseRegras/FiltroVeredito';
import type { VereditoAnaliseRegras } from '@/shared/regulacao/analiseRegras/tipos';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { useBuscaEsusSg, useResumoEsusSg } from '@/features/esussg/api/queries';
import { SituacaoEsusSgBadge } from '@/features/esussg/components/SituacaoEsusSgBadge';
import { PrioridadeEsusSg } from '@/features/esussg/components/PrioridadeEsusSg';
import { formatarCpf, limparTexto } from '@/features/esussg/lib/formatacao';
import { dataHoraAgendada } from '@/features/esussg/lib/agendamento';
import {
  ROTULO_SITUACAO_ESUSSG,
  SITUACOES_ESUSSG,
  TIPOS_RECURSO_ESUSSG,
  type BuscaEsusSgFiltro,
  type SituacaoEsusSg,
  type SolicitacaoEsusSgLista,
  type TipoRecursoEsusSg,
} from '@/features/esussg/types';

const TAMANHO_PAGINA = 25;

/** "aaaa-mm-dd" + n dias, sem passar pelo fuso do navegador. */
function somarDias(dataIso: string, dias: number): string {
  const [a, m, d] = dataIso.split('-').map(Number);
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

/**
 * Regulação → ESUS SG → Fila: os pedidos do município no ESUS de São Gonçalo, espelhados na nossa
 * base — os que esperam na fila, os agendados e os que saíram da fila.
 *
 * Só leitura (ADR-0063): a busca lê o nosso banco, e quem fala com o ESUS é o motor de varredura.
 */
export function EsusSgFilaPage() {
  const navegar = useNavigate();

  const [situacao, setSituacao] = useState<SituacaoEsusSg | ''>('EmFila');
  const [tipo, setTipo] = useState<TipoRecursoEsusSg | ''>('');
  const [recurso, setRecurso] = useState('');
  const [veredito, setVeredito] = useState<VereditoAnaliseRegras | ''>('');
  // Janela de agendamento: só o cartão "próximos 30 dias" liga. Vazio = sem recorte por data.
  const [janelaAgendada, setJanelaAgendada] = useState<{ inicio: string; fim: string } | null>(null);
  const [termo, setTermo] = useState('');
  const [termoAplicado, setTermoAplicado] = useState('');
  const [pagina, setPagina] = useState(1);

  const filtro = useMemo<BuscaEsusSgFiltro>(
    () => ({
      situacao: situacao || undefined,
      tipo: tipo || undefined,
      recurso: recurso || undefined,
      veredito: veredito || undefined,
      termo: termoAplicado || undefined,
      agendadaInicio: janelaAgendada?.inicio,
      agendadaFim: janelaAgendada?.fim,
      pagina,
      tamanho: TAMANHO_PAGINA,
    }),
    [situacao, tipo, recurso, veredito, termoAplicado, janelaAgendada, pagina],
  );

  const { data: resultado, isLoading } = useBuscaEsusSg(filtro);
  const { data: resumo } = useResumoEsusSg();

  const totalPaginas = resultado ? Math.max(1, Math.ceil(resultado.total / resultado.tamanho)) : 1;
  const totalGeral = resumo?.porSituacao.reduce((soma, r) => soma + r.quantidade, 0) ?? 0;

  function aplicarBusca() {
    setTermoAplicado(termo.trim());
    setPagina(1);
  }

  function trocarSituacao(nova: SituacaoEsusSg | '') {
    setSituacao(nova);
    setJanelaAgendada(null);
    setPagina(1);
  }

  function verProximos30Dias() {
    const hoje = hojeSP();
    setSituacao('Agendada');
    setJanelaAgendada({ inicio: hoje, fim: somarDias(hoje, 30) });
    setPagina(1);
  }

  function trocarVeredito(v: VereditoAnaliseRegras | '') {
    setVeredito(v);
    setPagina(1);
  }

  const temFiltroExtra = Boolean(termoAplicado || tipo || recurso || veredito || janelaAgendada);

  function limpar() {
    setTermo('');
    setTermoAplicado('');
    setTipo('');
    setRecurso('');
    setVeredito('');
    setJanelaAgendada(null);
    setPagina(1);
  }

  const colunas: Coluna<SolicitacaoEsusSgLista>[] = [
    {
      chave: 'idEsusSg',
      cabecalho: 'Nº ESUS',
      className: 'w-24',
      ordenar: (s) => Number(s.idEsusSg) || s.idEsusSg,
      render: (s) => <span className="font-mono text-xs text-slate-600">{s.idEsusSg}</span>,
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      ordenar: (s) => s.pacienteNome,
      render: (s) => (
        <div>
          {/* Conciliado no hub: ganha o resumo (com o atalho para a ficha) e o WhatsApp. O
              stopPropagation é essencial — a linha inteira navega para o detalhe. */}
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
          <div className="font-mono text-[11px] text-slate-500">
            {formatarCpf(s.cpf) ?? (s.cns ? `CNS ${s.cns}` : '')}
          </div>
        </div>
      ),
    },
    {
      chave: 'recurso',
      cabecalho: 'Procedimento',
      ordenar: (s) => s.recurso,
      render: (s) => (
        <div>
          <div className="text-sm">{s.recurso}</div>
          <div className="text-[11px] uppercase text-slate-500">{s.tipo}</div>
        </div>
      ),
    },
    {
      chave: 'prioridade',
      cabecalho: 'Prioridade',
      className: 'w-36',
      ordenar: (s) => s.prioridade ?? '',
      render: (s) => <PrioridadeEsusSg prioridade={s.prioridade} cor={s.prioridadeCor} />,
    },
    {
      chave: 'entrada',
      cabecalho: 'Entrada na fila',
      className: 'w-32',
      ordenar: (s) => s.dataEntradaFila ?? '',
      render: (s) => (
        <div className="text-sm">
          <div>{formatarWallClock(s.dataEntradaFila)}</div>
          {s.diasNaFila != null && (
            <div className="text-xs text-slate-500">{s.diasNaFila} dias</div>
          )}
        </div>
      ),
    },
    {
      chave: 'posicao',
      cabecalho: 'Posição',
      className: 'w-20',
      ordenar: (s) => s.posicaoFila ?? Number.MAX_SAFE_INTEGER,
      render: (s) =>
        s.posicaoFila != null ? (
          <span className="text-sm font-medium tabular-nums" title="Posição regulada na fila do procedimento, no ESUS">
            {s.posicaoFila}º
          </span>
        ) : (
          <span className="text-slate-400">—</span>
        ),
    },
    {
      chave: 'agendado',
      cabecalho: 'Agendado para',
      className: 'w-48',
      ordenar: (s) => s.dataAgendada ?? '',
      render: (s) => {
        const quando = dataHoraAgendada(s.dataHoraAgendadaTexto, s.dataAgendada);
        if (!quando) return <span className="text-slate-400">—</span>;
        return (
          <div className="text-sm">
            <div>{quando}</div>
            {s.unidadeExecutora && (
              <div className="truncate text-xs text-slate-500" title={limparTexto(s.unidadeExecutora) ?? ''}>
                {limparTexto(s.unidadeExecutora)}
              </div>
            )}
          </div>
        );
      },
    },
    {
      chave: 'situacao',
      cabecalho: 'Situação',
      className: 'w-36',
      ordenar: (s) => s.situacao,
      render: (s) => (
        <div className="space-y-1">
          <SituacaoEsusSgBadge situacao={s.situacao} />
          {s.situacaoAnterior && s.situacaoAnterior !== s.situacao && (
            <div className="text-[11px] text-slate-500">
              veio de {ROTULO_SITUACAO_ESUSSG[s.situacaoAnterior]}
            </div>
          )}
        </div>
      ),
    },
    {
      chave: 'analise',
      cabecalho: 'Análise das regras',
      className: 'w-36',
      ordenar: (s) => s.analise?.veredito ?? '',
      render: (s) => <BadgeAnaliseRegras analise={s.analise} />,
    },
  ];

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <ClipboardList className="size-6 text-red-600" />
        <div>
          <div className="flex items-center gap-1">
            <h1 className="text-xl font-semibold">ESUS São Gonçalo — fila de regulação</h1>
            <AjudaManual artigo="esus-sao-goncalo" secao="fila" />
          </div>
          <p className="text-sm text-slate-500">
            Pedidos do município no ESUS de São Gonçalo (PPI de exames), espelhados na nossa base:
            quem espera na fila, quem foi agendado e quem saiu da fila. Só leitura — a busca lê o
            nosso banco e o motor de varredura é quem fala com o ESUS.
          </p>
        </div>
      </header>

      {/* Cards por situação: dão o tamanho da fila antes de qualquer filtro. */}
      {resumo && (
        <div className="flex flex-wrap gap-2">
          <CartaoSituacao
            ativo={situacao === '' && !janelaAgendada}
            quantidade={totalGeral}
            rotulo="Todas"
            onClick={() => trocarSituacao('')}
          />
          {SITUACOES_ESUSSG.map((s) => {
            const item = resumo.porSituacao.find((r) => r.situacao === s);
            if (!item) return null;
            return (
              <CartaoSituacao
                key={s}
                ativo={situacao === s && !janelaAgendada}
                quantidade={item.quantidade}
                rotulo={ROTULO_SITUACAO_ESUSSG[s]}
                onClick={() => trocarSituacao(s)}
              />
            );
          })}
          <CartaoSituacao
            ativo={janelaAgendada !== null}
            quantidade={resumo.agendadosProximos30Dias}
            rotulo="Agendados nos próximos 30 dias"
            icone={<CalendarClock className="size-3.5" />}
            onClick={verProximos30Dias}
          />
        </div>
      )}

      {resumo && resumo.porVeredito.length > 0 && (
        <ChipsVeredito valor={veredito} aoMudar={trocarVeredito} contagens={resumo.porVeredito} />
      )}

      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-white p-4">
        <Campo label="Situação" htmlFor="esussg-situacao" className="w-44">
          <Select
            id="esussg-situacao"
            value={situacao}
            onChange={(e) => trocarSituacao(e.target.value as SituacaoEsusSg | '')}
          >
            <option value="">Todas</option>
            {SITUACOES_ESUSSG.map((s) => (
              <option key={s} value={s}>
                {ROTULO_SITUACAO_ESUSSG[s]}
              </option>
            ))}
          </Select>
        </Campo>

        <Campo label="Tipo" htmlFor="esussg-tipo" className="w-36">
          <Select
            id="esussg-tipo"
            value={tipo}
            onChange={(e) => {
              setTipo(e.target.value as TipoRecursoEsusSg | '');
              setPagina(1);
            }}
          >
            <option value="">Todos</option>
            {TIPOS_RECURSO_ESUSSG.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </Select>
        </Campo>

        <Campo label="Procedimento" htmlFor="esussg-recurso" className="min-w-56 flex-1">
          <Select
            id="esussg-recurso"
            value={recurso}
            onChange={(e) => {
              setRecurso(e.target.value);
              setPagina(1);
            }}
          >
            <option value="">Todos</option>
            {(resumo?.porRecurso ?? []).map((r) => (
              <option key={r.texto} value={r.texto}>
                {r.texto} ({r.quantidade})
              </option>
            ))}
          </Select>
        </Campo>

        <Campo label="Análise das regras" htmlFor="esussg-veredito" className="w-44">
          <Select
            id="esussg-veredito"
            value={veredito}
            onChange={(e) => trocarVeredito(e.target.value as VereditoAnaliseRegras | '')}
          >
            <OpcoesVeredito />
          </Select>
        </Campo>

        <Campo label="Buscar" htmlFor="esussg-buscar" className="min-w-64 flex-1">
          <Input
            id="esussg-buscar"
            value={termo}
            onChange={(e) => setTermo(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && aplicarBusca()}
            placeholder="Nome, CPF, CNS ou nº do pedido no ESUS"
          />
        </Campo>

        <Button onClick={aplicarBusca}>
          <Search className="size-4" /> Pesquisar
        </Button>

        {temFiltroExtra && (
          <Button variante="secundaria" onClick={limpar}>
            <X className="size-4" /> Limpar
          </Button>
        )}
      </div>

      {janelaAgendada && (
        <p className="text-xs text-slate-500">
          Mostrando os agendados de {formatarWallClock(janelaAgendada.inicio)} a{' '}
          {formatarWallClock(janelaAgendada.fim)}.
        </p>
      )}

      <Tabela
        colunas={colunas}
        dados={resultado?.itens ?? []}
        chaveLinha={(s) => s.id}
        carregando={isLoading}
        scrollXFlutuante
        aoClicarLinha={(s) => navegar(`/app/regulacao/esussg/${s.id}`)}
        dicaLinha="Clique para ver o pedido, o agendamento e a trilha"
        vazio={
          <div className="py-8 text-center text-sm text-slate-500">
            Nenhum pedido encontrado. Se a base ainda está vazia, rode a carga inicial em ESUS SG →
            Configuração.
          </div>
        }
      />

      {resultado && resultado.total > 0 && (
        <div className="flex items-center justify-between text-sm text-slate-600">
          <span>
            {resultado.total} pedido(s) — página {resultado.pagina} de {totalPaginas}
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

function CartaoSituacao({
  ativo,
  quantidade,
  rotulo,
  icone,
  onClick,
}: {
  ativo: boolean;
  quantidade: number;
  rotulo: string;
  icone?: React.ReactNode;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-lg border px-3 py-2 text-left transition ${
        ativo ? 'border-red-300 bg-red-50' : 'border-slate-200 bg-white hover:bg-slate-50'
      }`}
    >
      <div className="text-lg font-semibold">{quantidade}</div>
      <div className="flex items-center gap-1 text-xs text-slate-500">
        {icone}
        {rotulo}
      </div>
    </button>
  );
}
