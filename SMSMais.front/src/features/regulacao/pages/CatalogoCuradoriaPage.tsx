import { useState } from 'react';
import { Check, Loader2, Pencil, RefreshCw, X } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { notificar } from '@/shared/ui/Notificacoes';

import {
  useConfirmarPareamento,
  useRejeitarPareamento,
  useRenomearCanonico,
  useSincronizarCatalogo,
  useSugestoesPareamento,
} from '../api/queries';
import { BuscaProcedimento } from '../components/BuscaProcedimento';
import type { CatalogoSyncResultado, RegulacaoProcedimentoItem, SugestaoPareamento } from '../types';

const ROTULO_SISTEMA: Record<string, string> = {
  Sisreg: 'SISREG',
  Ser: 'SER',
  Sernit: 'SERNIT',
  Esus: 'eSUS',
};

/**
 * Curadoria do catálogo canônico (ADR-0052), aba da Configuração da Regulação.
 *
 * <p>O sincronismo só **sugere** pares entre sistemas; quem confirma é uma pessoa. A razão está
 * medida no spike c: dos candidatos que o pareamento automático levanta, alguns são falsos
 * (“Cirurgia Plástica Pediátrica” do SERNIT contra “CIRURGIA PLASTICA - ORELHA” do SER), e
 * confirmar sozinho ligaria a regra clínica de um procedimento a outro.</p>
 */
export function CatalogoCuradoriaPage() {
  const sugestoes = useSugestoesPareamento();
  const sincronizar = useSincronizarCatalogo();
  const [ultimoSync, setUltimoSync] = useState<CatalogoSyncResultado | null>(null);

  async function aoSincronizar() {
    try {
      const r = await sincronizar.mutateAsync();
      setUltimoSync(r);
      notificar(
        `Catálogo sincronizado: ${r.origensNovas} origens novas, ${r.canonicosNovos} procedimentos novos.`,
      );
    } catch {
      /* o interceptor do httpClient já notifica o erro */
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-2xl text-sm text-slate-600">
          O catálogo reúne, sob um procedimento só, o que o SISREG, o SER e o SERNIT chamam de
          formas diferentes. O sincronismo <strong>sugere</strong> os pares; confirmar é sempre
          decisão de gente.
        </p>
        <Button onClick={aoSincronizar} disabled={sincronizar.isPending}>
          {sincronizar.isPending ? (
            <Loader2 className="mr-2 size-4 animate-spin" />
          ) : (
            <RefreshCw className="mr-2 size-4" />
          )}
          Sincronizar agora
        </Button>
      </div>

      {ultimoSync ? (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
          <Cartao rotulo="Origens novas" valor={ultimoSync.origensNovas} />
          <Cartao rotulo="Desativadas" valor={ultimoSync.origensDesativadas} />
          <Cartao rotulo="Procedimentos novos" valor={ultimoSync.canonicosNovos} />
          <Cartao rotulo="Embeddings gerados" valor={ultimoSync.embeddingsGerados} />
          <Cartao
            rotulo="Sem embedding"
            valor={ultimoSync.semEmbedding}
            alerta={ultimoSync.semEmbedding > 0}
          />
          <Cartao rotulo="Sugestões" valor={ultimoSync.sugestoes} />
        </div>
      ) : null}

      {ultimoSync && ultimoSync.semEmbedding > 0 ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          {ultimoSync.semEmbedding} origens ficaram sem embedding — o provedor falhou nesses lotes.
          O catálogo está válido e a busca por texto funciona; rode de novo para completar a busca
          por semelhança.
        </p>
      ) : null}

      <section>
        <h2 className="mb-2 text-sm font-semibold text-slate-700">
          Sugestões de pareamento entre sistemas
        </h2>

        {sugestoes.isLoading ? (
          <p className="text-sm text-slate-500">Carregando…</p>
        ) : (sugestoes.data?.length ?? 0) === 0 ? (
          <p className="rounded-md border border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
            Nenhuma sugestão pendente.
          </p>
        ) : (
          <ul className="space-y-2">
            {sugestoes.data!.map((s) => (
              <LinhaSugestao key={s.origemId} sugestao={s} />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Cartao({ rotulo, valor, alerta }: { rotulo: string; valor: number; alerta?: boolean }) {
  return (
    <div
      className={
        alerta
          ? 'rounded-md border border-amber-300 bg-amber-50 p-3'
          : 'rounded-md border border-slate-200 bg-white p-3'
      }
    >
      <p className="text-xs text-slate-500">{rotulo}</p>
      <p className="text-lg font-semibold text-slate-900">{valor}</p>
    </div>
  );
}

function LinhaSugestao({ sugestao }: { sugestao: SugestaoPareamento }) {
  const confirmar = useConfirmarPareamento();
  const rejeitar = useRejeitarPareamento();
  const renomear = useRenomearCanonico();

  const [escolhendoOutro, setEscolhendoOutro] = useState(false);
  const [editandoNome, setEditandoNome] = useState(false);
  const [nome, setNome] = useState(sugestao.canonicoAtual);

  const ocupado = confirmar.isPending || rejeitar.isPending || renomear.isPending;

  async function aoConfirmar(procedimentoId: string) {
    await confirmar.mutateAsync({ origemId: sugestao.origemId, procedimentoId });
    notificar('Pareamento confirmado.');
  }

  return (
    <li className="rounded-md border border-slate-200 bg-white p-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-600">
              {ROTULO_SISTEMA[sugestao.sistema] ?? sugestao.sistema}
            </span>
            <span className="truncate font-medium text-slate-900">{sugestao.rotulo}</span>
          </div>

          <p className="mt-1 text-xs text-slate-500">
            hoje em <strong className="text-slate-700">{sugestao.canonicoAtual}</strong> — sugerido
            mover para <strong className="text-slate-700">{sugestao.sugeridoNome}</strong>{' '}
            <span className="text-slate-400">
              ({Math.round(sugestao.score * 100)}% de semelhança)
            </span>
          </p>
        </div>

        <div className="flex shrink-0 flex-wrap gap-2">
          <Button tamanho="sm" disabled={ocupado} onClick={() => aoConfirmar(sugestao.sugeridoId)}>
            <Check className="mr-1 size-3.5" /> Confirmar
          </Button>
          <Button
            tamanho="sm"
            variante="outline"
            disabled={ocupado}
            onClick={async () => {
              await rejeitar.mutateAsync(sugestao.origemId);
              notificar('Sugestão descartada.');
            }}
          >
            <X className="mr-1 size-3.5" /> Rejeitar
          </Button>
          <Button
            tamanho="sm"
            variante="outline"
            disabled={ocupado}
            onClick={() => setEscolhendoOutro((v) => !v)}
          >
            Escolher outro
          </Button>
          <Button
            tamanho="sm"
            variante="ghost"
            disabled={ocupado}
            onClick={() => setEditandoNome((v) => !v)}
            aria-label="Renomear o procedimento canônico atual"
          >
            <Pencil className="size-3.5" />
          </Button>
        </div>
      </div>

      {editandoNome ? (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Input
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            className="max-w-md"
            placeholder="Nome do procedimento canônico"
          />
          <Button
            tamanho="sm"
            disabled={ocupado || !nome.trim()}
            onClick={async () => {
              await renomear.mutateAsync({ id: sugestao.canonicoAtualId, nome: nome.trim() });
              setEditandoNome(false);
              notificar('Nome atualizado.');
            }}
          >
            Salvar nome
          </Button>
        </div>
      ) : null}

      {escolhendoOutro ? (
        <div className="mt-3">
          <p className="mb-1 text-xs text-slate-500">
            Escolha o procedimento canônico que deve receber esta origem:
          </p>
          <BuscaProcedimento
            value={null}
            onChange={async (item: RegulacaoProcedimentoItem | null) => {
              if (!item) return;
              await aoConfirmar(item.id);
              setEscolhendoOutro(false);
            }}
          />
        </div>
      ) : null}
    </li>
  );
}
