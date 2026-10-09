import { useState } from 'react';
import { CheckCircle2, Pencil } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BadgeRisco, SeletorRisco } from '@/shared/regulacao/ClassificacaoRisco';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';

import { useAtualizarSolicitacao, useFormularioRegulacao } from '../api/solicitacoesQueries';
import type { SolicitacaoRegulacao } from '../tiposSolicitacao';
import { SeletorCidRegulacao } from './SeletorCidRegulacao';

/** Chaves canônicas do bloco fixo (`RegulacaoFormularioService.ChaveClassificacaoRisco` / `ChaveHipoteseCid`). */
const CHAVE_RISCO = 'classificacao_risco';
const CHAVE_CID = 'hipotese_cid';

/**
 * Classificação de risco e Hipótese (CID) na mão de quem regula, antes de enviar ao SER/SERNIT.
 *
 * <p>A unidade é obrigada a preencher os dois, mas é a regulação que decide: reclassificar o risco
 * e trocar o CID (inclusive depois de o sistema recusar o código) é rotina do técnico. Antes
 * disto, o único caminho era devolver à unidade e esperar. Cada alteração vira um "Ajuste" na
 * linha do tempo, com o nome de quem fez e o de → para de cada campo.</p>
 */
export function AjusteRiscoCid({ s, aoSalvar }: { s: SolicitacaoRegulacao; aoSalvar: () => void }) {
  const formulario = useFormularioRegulacao(s.procedimentoId, s.fluxo);
  const atualizar = useAtualizarSolicitacao();

  const [editando, setEditando] = useState(false);
  const [risco, setRisco] = useState('');
  const [cid, setCid] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  const destino = s.sistemaDestino;
  if (s.fluxo !== 'Externo' || (destino !== 'Ser' && destino !== 'Sernit')) return null;

  // Na fila sem dono, mostra para leitura: quem altera é quem assumiu o caso.
  const podeAlterar = s.status === 'EmAnalise' || s.status === 'FalhaEnvio';
  if (!podeAlterar && s.status !== 'PendenteRegulacao') return null;

  // A opção de um sistema só vale nele ("Prioridade 1" é do SER; "Emergência", do SERNIT).
  const opcoesRisco = (formulario.data?.campos.find((c) => c.chave === CHAVE_RISCO)?.opcoes ?? [])
    .filter((o) => o.origens.includes(destino))
    .map((o) => ({ valor: o.valor, rotulo: o.rotulo }));

  const riscoAtual = texto(s.formulario?.[CHAVE_RISCO]);
  const cidAtual = texto(s.formulario?.[CHAVE_CID]);
  const nomeSistema = destino === 'Sernit' ? 'SERNIT' : 'SER';

  function abrir() {
    setRisco(riscoAtual);
    setCid(cidAtual);
    setErro(null);
    setSalvo(false);
    setEditando(true);
  }

  async function salvar() {
    setErro(null);
    try {
      await atualizar.mutateAsync({
        id: s.id,
        formulario: { ...s.formulario, [CHAVE_RISCO]: risco, [CHAVE_CID]: cid },
      });
      setEditando(false);
      setSalvo(true);
      aoSalvar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const mudou = risco !== riscoAtual || cid !== cidAtual;

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Classificação de risco e CID</h2>
          <p className="mt-0.5 text-xs text-slate-500">
            O que a unidade informou. Você pode alterar antes de enviar ao {nomeSistema} — a
            alteração fica na linha do tempo com o seu nome.
          </p>
        </div>
        {podeAlterar && !editando && (
          <Button variante="secundaria" tamanho="sm" onClick={abrir}>
            <Pencil className="size-4" />
            Alterar
          </Button>
        )}
      </div>

      {!editando ? (
        <dl className="mt-3 space-y-2 text-sm">
          <div className="grid grid-cols-[minmax(0,12rem)_1fr] items-start gap-2">
            <dt className="text-slate-500">Classificação de risco</dt>
            <dd>
              {riscoAtual ? (
                <BadgeRisco valor={riscoAtual} rotulo={opcoesRisco.find((o) => o.valor === riscoAtual)?.rotulo} />
              ) : (
                <span className="text-amber-700">não preenchida</span>
              )}
            </dd>
          </div>
          <div className="grid grid-cols-[minmax(0,12rem)_1fr] items-start gap-2">
            <dt className="text-slate-500">Hipótese (CID)</dt>
            <dd className="break-words text-slate-900">
              {cidAtual || <span className="text-amber-700">não preenchida</span>}
            </dd>
          </div>
          {!podeAlterar && <p className="text-xs text-slate-500">Assuma a solicitação para poder alterar.</p>}
          {salvo && (
            <p className="flex items-center gap-1.5 text-sm text-emerald-700">
              <CheckCircle2 className="size-4" /> Alterado. A mudança ficou na linha do tempo com o seu nome.
            </p>
          )}
        </dl>
      ) : (
        <div className="mt-3 space-y-4">
          <Campo label="Classificação de risco *" htmlFor="ajuste-risco">
            {opcoesRisco.length > 0 ? (
              <SeletorRisco id="ajuste-risco" opcoes={opcoesRisco} valor={risco} onChange={setRisco} />
            ) : (
              <p className="text-sm text-amber-700">
                {formulario.isLoading
                  ? 'Carregando as opções…'
                  : `Sem as opções de risco do ${nomeSistema}. Recopie o catálogo em Configurações.`}
              </p>
            )}
          </Campo>

          <SeletorCidRegulacao
            rotulo="Hipótese (CID) *"
            procedimentoId={s.procedimentoId}
            sistema={destino}
            valor={cid}
            onChange={setCid}
          />

          {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-800">{erro}</p>}

          <div className="flex flex-wrap gap-2">
            <Button onClick={salvar} disabled={!mudou || !risco || !cid || atualizar.isPending}>
              {atualizar.isPending ? 'Salvando…' : 'Salvar alteração'}
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

function texto(v: unknown): string {
  if (v === null || v === undefined) return '';
  return String(v);
}
