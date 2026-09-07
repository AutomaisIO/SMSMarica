import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { formatarInstante } from '@/shared/lib/datas';

import { ROTULO_FLUXO, StatusRegulacaoBadge } from './StatusRegulacaoBadge';
import type { SolicitacaoRegulacaoLista } from '../tiposSolicitacao';

/**
 * A tabela da fila, usada pela unidade e pelo agente.
 *
 * <p><b>A coluna do número mostra o externo quando ele existe.</b> Depois que a solicitação entra
 * no SISREG/SER/SERNIT, é aquele número que a paciente traz no papel e que o atendente do outro
 * sistema pergunta — o nosso vira rastro (ADR-0052). Mostrar os dois lado a lado só faria a
 * pessoa ler o errado em voz alta.</p>
 *
 * <p>A coluna "Unidade" some para quem só enxerga a própria: repetir o mesmo nome em toda linha
 * gasta largura que o nome do paciente usa melhor.</p>
 */
export function TabelaSolicitacoes({
  dados,
  carregando,
  mostrarUnidade,
  mostrarAgente,
  aoAbrir,
  vazio,
}: {
  dados: SolicitacaoRegulacaoLista[];
  carregando?: boolean;
  mostrarUnidade?: boolean;
  mostrarAgente?: boolean;
  aoAbrir?: (item: SolicitacaoRegulacaoLista) => void;
  vazio?: string;
}) {
  const colunas: Coluna<SolicitacaoRegulacaoLista>[] = [
    {
      chave: 'numero',
      cabecalho: 'Número',
      className: 'whitespace-nowrap font-mono text-xs',
      ordenar: (s) => s.numeroExterno ?? s.numeroLocal,
      render: (s) =>
        s.numeroExterno ? (
          <span title="Número no sistema de regulação">{s.numeroExterno}</span>
        ) : (
          <span className="text-slate-500" title="Número interno — some quando o externo chegar">
            PR-{s.numeroLocal}
          </span>
        ),
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      ordenar: (s) => s.pacienteNome,
      render: (s) => (
        <div className="min-w-0">
          <p className="truncate font-medium text-slate-900">{s.pacienteNome}</p>
          {/* Sem CPF a solicitação não sai da fila — avisar aqui evita a descoberta no "Enviar". */}
          {!s.pacienteCpf && <p className="text-xs text-amber-700">sem CPF</p>}
        </div>
      ),
    },
    {
      chave: 'procedimento',
      cabecalho: 'Procedimento',
      ordenar: (s) => s.procedimento,
      render: (s) => <span className="line-clamp-2 text-slate-700">{s.procedimento}</span>,
    },
    {
      chave: 'fluxo',
      cabecalho: 'Fluxo',
      className: 'whitespace-nowrap',
      ordenar: (s) => s.fluxo,
      render: (s) => (
        <div className="text-xs">
          <p className="text-slate-700">{ROTULO_FLUXO[s.fluxo]}</p>
          {s.unidadeEmNomeDe && (
            <p className="text-slate-500" title="NAR: aberta em nome de outra unidade">
              p/ {s.unidadeEmNomeDe}
            </p>
          )}
        </div>
      ),
    },
    {
      chave: 'status',
      cabecalho: 'Situação',
      className: 'whitespace-nowrap',
      ordenar: (s) => s.status,
      render: (s) => <StatusRegulacaoBadge status={s.status} />,
    },
  ];

  if (mostrarUnidade) {
    colunas.splice(2, 0, {
      chave: 'unidade',
      cabecalho: 'Unidade',
      ordenar: (s) => s.unidadeSolicitante,
      render: (s) => <span className="text-slate-700">{s.unidadeSolicitante}</span>,
    });
  }

  if (mostrarAgente) {
    colunas.push({
      chave: 'agente',
      cabecalho: 'Agente',
      className: 'whitespace-nowrap',
      ordenar: (s) => s.agenteNome ?? '',
      render: (s) =>
        s.agenteNome ? (
          <span className="text-slate-700">{s.agenteNome}</span>
        ) : (
          <span className="text-slate-400">—</span>
        ),
    });
  }

  colunas.push({
    chave: 'quando',
    cabecalho: 'Atualizada',
    className: 'whitespace-nowrap text-xs text-slate-500',
    ordenar: (s) => s.atualizadoEm ?? s.criadoEm,
    render: (s) => formatarInstante(s.atualizadoEm ?? s.criadoEm),
  });

  return (
    <Tabela
      colunas={colunas}
      dados={dados}
      chaveLinha={(s) => s.id}
      carregando={carregando}
      aoClicarLinha={aoAbrir}
      dicaLinha="Clique para abrir a solicitação"
      vazio={vazio ?? 'Nenhuma solicitação nesta situação.'}
      idTabela="regulacao-solicitacoes"
    />
  );
}
