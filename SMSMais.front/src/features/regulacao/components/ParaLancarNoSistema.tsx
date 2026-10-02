import { useState } from 'react';
import { Check, ClipboardCopy } from 'lucide-react';

import { useFormularioRegulacao } from '../api/solicitacoesQueries';
import { idPendente } from '../api/medicosApi';
import type { SolicitacaoRegulacao } from '../tiposSolicitacao';
import { ROTULO_SISTEMA_REGULACAO } from '../types';
import { CHAVE_CIDS_SECUNDARIOS, lerCidsSecundarios, observacoesComCids } from './CidsSecundarios';

/** As chaves de Observações: SER/SERNIT usam `observacoes`, o SISREG `observacao`. */
const CHAVES_OBSERVACOES = ['observacoes', 'observacao'];

/**
 * "O que digitar no sistema" — o formulário da solicitação campo a campo, com o rótulo do sistema
 * de destino e um botão de copiar em cada valor.
 *
 * <p>Existe porque o lançamento no SER/SERNIT é feito pelo técnico, na tela de lá (envio
 * assistido), e até aqui nenhuma tela nossa mostrava o que a unidade preencheu. É também a "fase
 * final" dos CIDs secundários: os sistemas só têm um CID na tela, e os outros aparecem já no fim
 * das Observações, prontos para colar.</p>
 */
export function ParaLancarNoSistema({ s }: { s: SolicitacaoRegulacao }) {
  const formulario = useFormularioRegulacao(s.procedimentoId, s.fluxo);
  const valores = s.formulario ?? {};
  const destino = s.sistemaDestino;

  if (!destino || !formulario.data) return null;

  const cids = lerCidsSecundarios(texto(valores[CHAVE_CIDS_SECUNDARIOS]));
  const campos = formulario.data.campos
    .filter((c) => c.origens.length === 0 || c.origens.includes(destino) || s.fluxo !== 'Externo')
    .sort((a, b) => a.ordem - b.ordem);

  const linhas = campos
    .map((c) => {
      let valor = texto(valores[c.chave]);
      if (CHAVES_OBSERVACOES.includes(c.chave)) valor = observacoesComCids(valor, cids);
      if (idPendente(valor)) valor = '(médico aguardando cadastro — resolva o cartão acima)';
      // Opção de lista: o técnico procura pelo texto da opção, não pelo código.
      const opcao = c.opcoes?.find((o) => o.valor === valor);
      return { chave: c.chave, rotulo: c.rotulo, valor: opcao?.rotulo ?? valor };
    })
    .filter((l) => l.valor);

  // Sem campo de Observações no formulário: os CIDs secundários aparecem numa linha à parte.
  const temObservacoes = campos.some((c) => CHAVES_OBSERVACOES.includes(c.chave));
  if (!temObservacoes && cids.length > 0) {
    linhas.push({ chave: 'cids', rotulo: 'Observações (CIDs secundários)', valor: observacoesComCids('', cids) });
  }

  if (linhas.length === 0) return null;

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h2 className="text-sm font-semibold text-slate-900">
        Para lançar no {ROTULO_SISTEMA_REGULACAO[destino]}
      </h2>
      <p className="mb-3 text-xs text-slate-500">
        O que a unidade preencheu, campo a campo. Os CIDs secundários já estão no fim das Observações.
      </p>
      <dl className="space-y-2">
        {linhas.map((l) => (
          <div key={l.chave} className="grid grid-cols-[minmax(0,12rem)_1fr_auto] items-start gap-2 text-sm">
            <dt className="text-slate-500">{l.rotulo}</dt>
            <dd className="whitespace-pre-wrap break-words text-slate-900">{l.valor}</dd>
            <Copiar texto={l.valor} />
          </div>
        ))}
      </dl>
    </section>
  );
}

function Copiar({ texto: valor }: { texto: string }) {
  const [copiado, setCopiado] = useState(false);
  return (
    <button
      type="button"
      title="Copiar"
      onClick={() => {
        void navigator.clipboard.writeText(valor).then(() => {
          setCopiado(true);
          setTimeout(() => setCopiado(false), 1500);
        });
      }}
      className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
    >
      {copiado ? <Check className="size-4 text-emerald-600" /> : <ClipboardCopy className="size-4" />}
    </button>
  );
}

function texto(v: unknown): string {
  if (v === null || v === undefined) return '';
  if (Array.isArray(v)) return v.map(String).join('\n');
  return String(v);
}
