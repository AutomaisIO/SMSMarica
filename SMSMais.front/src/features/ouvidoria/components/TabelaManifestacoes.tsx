import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { formatarInstante } from '@/shared/lib/datas';
import {
  IdentificacaoBadge,
  PrioridadeManifestacaoBadge,
  StatusManifestacaoBadge,
  TipoManifestacaoBadge,
} from '@/features/ouvidoria/components/badges';
import { PrazoChip } from '@/features/ouvidoria/components/PrazoChip';
import type { ManifestacaoListaDto } from '@/features/ouvidoria/types';

type Props = {
  dados: ManifestacaoListaDto[];
  carregando?: boolean;
  aoAbrir: (m: ManifestacaoListaDto) => void;
  /** Ponto de resposta: sem coluna de manifestante (o DTO já vem sem, mas a coluna também não aparece). */
  ocultarManifestante?: boolean;
  vazio?: string;
};

/** Tabela padrão da fila: protocolo, tipo, status, prioridade, assunto, unidade, ponto, prazo, última atividade. */
export function TabelaManifestacoes({ dados, carregando, aoAbrir, ocultarManifestante, vazio }: Props) {
  const colunas: Coluna<ManifestacaoListaDto>[] = [
    {
      chave: 'protocolo',
      cabecalho: 'Protocolo',
      className: 'whitespace-nowrap font-mono text-sm text-slate-700',
      ordenar: (m) => m.protocolo,
      render: (m) => (
        <div className="flex items-center gap-1.5">
          <span>{m.protocolo}</span>
          <IdentificacaoBadge identificacao={m.identificacao} />
        </div>
      ),
    },
    { chave: 'tipo', cabecalho: 'Tipo', ordenar: (m) => m.tipo, render: (m) => <TipoManifestacaoBadge tipo={m.tipo} /> },
    { chave: 'status', cabecalho: 'Status', ordenar: (m) => m.status, render: (m) => <StatusManifestacaoBadge status={m.status} /> },
    {
      chave: 'prioridade',
      cabecalho: 'Prioridade',
      ordenar: (m) => ({ Normal: 1, Alta: 2, Urgente: 3 })[m.prioridade],
      render: (m) => <PrioridadeManifestacaoBadge prioridade={m.prioridade} />,
    },
    {
      chave: 'assunto',
      cabecalho: 'Assunto / resumo',
      ordenar: (m) => m.assuntoNome,
      render: (m) => (
        <div className="max-w-xs">
          <div className="text-sm text-slate-800">{m.assuntoNome ?? <span className="text-slate-400">Sem assunto</span>}</div>
          {m.resumo ? <div className="truncate text-xs text-slate-500" title={m.resumo}>{m.resumo}</div> : null}
        </div>
      ),
    },
    ...(ocultarManifestante
      ? []
      : [
          {
            chave: 'manifestante',
            cabecalho: 'Manifestante',
            className: 'text-sm text-slate-600',
            ordenar: (m: ManifestacaoListaDto) => m.manifestanteNome,
            render: (m: ManifestacaoListaDto) =>
              m.manifestanteNome ?? <span className="text-xs text-slate-400">{m.identificacao === 'Anonima' ? 'anônimo' : 'restrito'}</span>,
          } satisfies Coluna<ManifestacaoListaDto>,
        ]),
    { chave: 'unidade', cabecalho: 'Unidade', className: 'text-sm text-slate-600', ordenar: (m) => m.unidadeNome, render: (m) => m.unidadeNome ?? '—' },
    { chave: 'ponto', cabecalho: 'Ponto de resposta', className: 'text-sm text-slate-600', ordenar: (m) => m.pontoRespostaNome, render: (m) => m.pontoRespostaNome ?? '—' },
    {
      chave: 'prazo',
      cabecalho: 'Prazo',
      ordenar: (m) => m.prazoRespostaEm,
      render: (m) => (
        <div className="flex flex-col gap-1">
          <PrazoChip prazo={m.prazoRespostaEm} status={m.status} rotulo="Cidadão" />
          {m.prazoAreaEm && m.status === 'Encaminhada' ? <PrazoChip prazo={m.prazoAreaEm} status={m.status} rotulo="Área" /> : null}
        </div>
      ),
    },
    {
      chave: 'atividade',
      cabecalho: 'Última atividade',
      className: 'whitespace-nowrap text-sm text-slate-500',
      ordenar: (m) => m.ultimaAtividadeEm,
      render: (m) => formatarInstante(m.ultimaAtividadeEm),
    },
  ];

  return (
    <Tabela
      colunas={colunas}
      dados={dados}
      chaveLinha={(m) => m.id}
      carregando={carregando}
      aoClicarLinha={aoAbrir}
      dicaLinha="Abrir manifestação"
      scrollXFlutuante
      classeLinha={(m) => (m.atrasada && m.status !== 'Respondida' ? 'bg-red-50/40 hover:bg-red-50' : undefined)}
      vazio={<div className="py-8 text-center text-sm text-slate-400">{vazio ?? 'Nenhuma manifestação encontrada.'}</div>}
    />
  );
}
