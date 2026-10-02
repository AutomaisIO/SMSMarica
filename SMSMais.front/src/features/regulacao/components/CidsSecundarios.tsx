import { useState } from 'react';
import { Plus, X } from 'lucide-react';

import { Input } from '@/shared/ui/Input';

import type { SistemaRegulacao } from '../types';
import { SeletorCidRegulacao } from './SeletorCidRegulacao';

/** Chave do formulário (objeto `canonico`) onde os CIDs secundários ficam, um por linha. */
export const CHAVE_CIDS_SECUNDARIOS = 'cids_secundarios';

export function lerCidsSecundarios(valor: string | undefined | null): string[] {
  return (valor ?? '')
    .split('\n')
    .map((c) => c.trim())
    .filter(Boolean);
}

/**
 * O texto das Observações como ele vai para o sistema de destino: o que a unidade escreveu e, no
 * fim, os CIDs secundários. Os sistemas (SER, SERNIT, SISREG) só têm UM campo de CID na tela —
 * o secundário viaja nas Observações. Espelha `RegulacaoFormularioService.ComCidsSecundarios`.
 */
export function observacoesComCids(observacoes: string | undefined | null, cids: string[]): string {
  const base = (observacoes ?? '').trim();
  if (cids.length === 0) return base;
  const linha = `CID(s) secundário(s): ${cids.join('; ')}`;
  return base ? `${base}\n\n${linha}` : linha;
}

/**
 * CIDs secundários — além do CID principal (a Hipótese). Escolhe-se um por vez na mesma caixa de
 * CID do recurso e o "+" acrescenta à lista. Sem caixa de CID do recurso (fluxo Interno/SISREG,
 * onde o CID é digitado), é um campo de texto com o mesmo "+".
 */
export function CidsSecundarios({
  valor,
  onChange,
  procedimentoId,
  sistema,
  comCaixaDoRecurso,
  desabilitado,
}: {
  /** Os CIDs, um por linha (é como ficam guardados). */
  valor: string;
  onChange: (valor: string) => void;
  procedimentoId: string | null;
  sistema: SistemaRegulacao | null;
  /** Externo: usa a caixa de CID do recurso. Interno: texto livre. */
  comCaixaDoRecurso: boolean;
  desabilitado?: boolean;
}) {
  const cids = lerCidsSecundarios(valor);
  const [escolhendo, setEscolhendo] = useState(false);
  const [texto, setTexto] = useState('');

  function acrescentar(cid: string) {
    const c = cid.trim();
    if (!c || cids.includes(c)) return;
    onChange([...cids, c].join('\n'));
    setEscolhendo(false);
    setTexto('');
  }

  function remover(cid: string) {
    onChange(cids.filter((c) => c !== cid).join('\n'));
  }

  return (
    <div className="min-w-96 flex-1">
      <p className="text-sm font-medium text-slate-700">CIDs secundários</p>
      <p className="text-xs text-slate-500">
        Opcional. No sistema de destino eles vão no fim das Observações — a tela de lá só tem um CID.
      </p>

      {cids.length > 0 && (
        <ul className="mt-1.5 flex flex-wrap gap-1.5">
          {cids.map((c) => (
            <li
              key={c}
              className="flex items-center gap-1 rounded-full border border-slate-300 bg-slate-50 px-2 py-0.5 text-xs text-slate-800"
            >
              {c}
              {!desabilitado && (
                <button type="button" onClick={() => remover(c)} aria-label={`Remover ${c}`}>
                  <X className="size-3 text-slate-500 hover:text-slate-800" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {!desabilitado &&
        (escolhendo ? (
          <div className="mt-2">
            {comCaixaDoRecurso ? (
              <SeletorCidRegulacao
                rotulo="CID secundário"
                procedimentoId={procedimentoId}
                sistema={sistema}
                valor=""
                onChange={acrescentar}
              />
            ) : (
              <div className="flex gap-2">
                <Input
                  value={texto}
                  onChange={(e) => setTexto(e.target.value)}
                  placeholder="Ex.: E11 Diabetes mellitus tipo 2"
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      acrescentar(texto);
                    }
                  }}
                />
                <button
                  type="button"
                  onClick={() => acrescentar(texto)}
                  className="rounded-md border border-slate-300 px-2 text-sm hover:bg-slate-50"
                >
                  Adicionar
                </button>
              </div>
            )}
            <button
              type="button"
              onClick={() => setEscolhendo(false)}
              className="mt-1 text-xs text-slate-500 hover:underline"
            >
              Cancelar
            </button>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => setEscolhendo(true)}
            className="mt-1.5 inline-flex items-center gap-1 rounded-md border border-dashed border-slate-300 px-2 py-1 text-xs font-medium text-primary-700 hover:bg-slate-50"
          >
            <Plus className="size-3.5" /> Adicionar CID secundário
          </button>
        ))}
    </div>
  );
}
