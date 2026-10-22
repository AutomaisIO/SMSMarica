import { useState } from 'react';
import { CheckCircle2, Pencil } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';

import { useAtualizarSolicitacao } from '../api/solicitacoesQueries';
import type { SolicitacaoRegulacao } from '../tiposSolicitacao';
import { BadgeNar } from './StatusRegulacaoBadge';
import { SeletorUnidadeEmNomeDe } from './SeletorUnidadeEmNomeDe';

/**
 * NAR na mão de quem regula: em nome de qual unidade o pedido entra no SISREG.
 *
 * <p>A unidade que abriu escolhe, mas é o técnico quem sabe com o CNES de qual unidade o pedido vai
 * de fato — e ele troca sem devolver (pedido do Bernardo, 22/10/2026). Trocar não mexe em
 * formulário, regras nem paciente; vira um "Ajuste" na linha do tempo, com o de → para.</p>
 */
export function UnidadeEmNomeDeNar({ s, aoSalvar }: { s: SolicitacaoRegulacao; aoSalvar: () => void }) {
  const atualizar = useAtualizarSolicitacao();
  const [editando, setEditando] = useState(false);
  const [unidade, setUnidade] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  if (s.fluxo !== 'Nar') return null;

  // Mesma régua do ajuste de risco e CID: quem altera é quem assumiu o caso.
  const podeAlterar = s.status === 'EmAnalise' || s.status === 'FalhaEnvio';

  async function salvar() {
    setErro(null);
    try {
      await atualizar.mutateAsync({ id: s.id, unidadeEmNomeDeId: unidade });
      setEditando(false);
      setSalvo(true);
      aoSalvar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <section className="rounded-lg border border-violet-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
            <BadgeNar /> Em nome de qual unidade
          </h2>
          <p className="mt-0.5 text-xs text-slate-500">
            É com o CNES desta unidade que o pedido entra no SISREG. Aberta por {s.unidadeSolicitanteNome}.
          </p>
        </div>
        {podeAlterar && !editando && (
          <Button
            variante="secundaria"
            tamanho="sm"
            onClick={() => {
              setUnidade(s.unidadeEmNomeDeId ?? '');
              setErro(null);
              setSalvo(false);
              setEditando(true);
            }}
          >
            <Pencil className="size-4" />
            Trocar unidade
          </Button>
        )}
      </div>

      {!editando ? (
        <div className="mt-3 space-y-2 text-sm">
          <p className="text-slate-900">
            <strong>{s.unidadeEmNomeDeNome ?? '—'}</strong>
            {s.unidadeEmNomeDeCnes && <span className="text-slate-600"> · CNES {s.unidadeEmNomeDeCnes}</span>}
          </p>
          {!podeAlterar && ['PendenteRegulacao'].includes(s.status) && (
            <p className="text-xs text-slate-500">Assuma a solicitação para poder trocar.</p>
          )}
          {salvo && (
            <p className="flex items-center gap-1.5 text-sm text-emerald-700">
              <CheckCircle2 className="size-4" /> Unidade trocada. A troca ficou na linha do tempo com o seu nome.
            </p>
          )}
        </div>
      ) : (
        <div className="mt-3 space-y-3">
          <Campo label="Em nome de *" htmlFor="nar-em-nome-de">
            <SeletorUnidadeEmNomeDe id="nar-em-nome-de" valor={unidade} onChange={setUnidade} />
          </Campo>

          {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-800">{erro}</p>}

          <div className="flex flex-wrap gap-2">
            <Button onClick={salvar} disabled={!unidade || unidade === s.unidadeEmNomeDeId || atualizar.isPending}>
              {atualizar.isPending ? 'Salvando…' : 'Salvar troca'}
            </Button>
            <Button variante="secundaria" onClick={() => setEditando(false)} disabled={atualizar.isPending}>
              Cancelar
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}
