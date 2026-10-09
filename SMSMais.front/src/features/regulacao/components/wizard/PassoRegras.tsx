import { useState } from 'react';

import { Button } from '@/shared/ui/Button';

import { useElegibilidade, useResponderRegras } from '../../api/solicitacoesQueries';
import type { PerguntaPendente, RespostaRegraRegulacao } from '../../tiposSolicitacao';
import { VisaoRegras } from './VisaoRegras';

/**
 * O passo "Regras" do assistente: busca a avaliação deste pedido e guarda as respostas (plano 03).
 *
 * <p>O desenho mora em {@link VisaoRegras}, que a tela de Regras de elegibilidade também usa na
 * prévia "Ver como o solicitante vê" — assim a prévia é, por construção, o que o solicitante vê.</p>
 */
export function PassoRegras({ solicitacaoId }: { solicitacaoId: string | null }) {
  const avaliacao = useElegibilidade(solicitacaoId);
  const responder = useResponderRegras();
  const [rascunho, setRascunho] = useState<Record<string, RespostaRegraRegulacao>>({});
  const [marcadas, setMarcadas] = useState<Record<string, string[]>>({});

  if (!solicitacaoId) {
    return <p className="text-sm text-slate-500">Escolha o paciente para o sistema conferir as regras.</p>;
  }
  if (avaliacao.isLoading) return <p className="text-sm text-slate-500">Conferindo as regras…</p>;

  const a = avaliacao.data;
  if (!a) return null;

  async function salvarRespostas() {
    if (!solicitacaoId || Object.keys(rascunho).length === 0) return;
    // Só a lista respondida "Sim" leva opções — "Nenhuma destas" e "Não sei" não marcam nada.
    const opcoes = Object.fromEntries(
      Object.entries(marcadas).filter(([regraId, lista]) => rascunho[regraId] === 'Sim' && lista.length > 0),
    );
    await responder.mutateAsync({ id: solicitacaoId, respostas: rascunho, opcoes });
    setRascunho({});
    setMarcadas({});
  }

  function alternarOpcao(p: PerguntaPendente, opcao: string) {
    const atuais = marcadas[p.regraId] ?? [];
    const proximas = atuais.includes(opcao) ? atuais.filter((o) => o !== opcao) : [...atuais, opcao];
    setMarcadas((m) => ({ ...m, [p.regraId]: proximas }));
    setRascunho((r) => {
      const { [p.regraId]: _anterior, ...resto } = r;
      // Marcar alguma é o "Sim"; desmarcar a última deixa a pergunta sem resposta de novo.
      return proximas.length > 0 ? { ...resto, [p.regraId]: 'Sim' } : resto;
    });
  }

  function responderPergunta(p: PerguntaPendente, resposta: RespostaRegraRegulacao) {
    // Na lista, "Nenhuma destas" e "Não sei" desmarcam as caixas; na pergunta simples não há caixa.
    if (p.opcoes && p.opcoes.length > 0) setMarcadas((m) => ({ ...m, [p.regraId]: [] }));
    setRascunho((r) => ({ ...r, [p.regraId]: resposta }));
  }

  return (
    <VisaoRegras
      avaliacao={a}
      rascunho={rascunho}
      marcadas={marcadas}
      onAlternarOpcao={alternarOpcao}
      onResponder={responderPergunta}
      rodapePerguntas={
        <Button
          className="mt-3"
          onClick={salvarRespostas}
          disabled={responder.isPending || Object.keys(rascunho).length === 0}
        >
          Salvar respostas
        </Button>
      }
    />
  );
}
