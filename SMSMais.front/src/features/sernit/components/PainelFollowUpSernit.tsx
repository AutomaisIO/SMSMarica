import { useState } from 'react';
import { CheckCircle2, Loader2, MessageSquarePlus, UserCheck } from 'lucide-react';

import { useRegistrarFollowUpSernit } from '@/features/sernit/api/queries';
import { ModalLoginSernit } from '@/features/sernit/components/ModalLoginSernit';
import { useSessaoSernitObrigatoria } from '@/features/sernit/lib/sessaoSernit';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

/**
 * Registrar FollowUP na solicitação — <b>escreve no SERNIT</b>.
 *
 * <p>O FollowUP é a observação que não muda a situação: é como a regulação registra em Niterói que
 * o contato com o paciente foi feito. Até aqui isso só existia no SERNIT, e quem operava tinha de
 * abrir a tela deles.</p>
 *
 * <p><b>Assinado pelo operador.</b> A credencial do sistema é de sincronismo e só lê; o SERNIT grava
 * o nome de quem fez em cada evento. Quando falta sessão, a tela <b>pede a senha</b> — nunca
 * mostra a recusa do backend como erro.</p>
 */
export function PainelFollowUpSernit({ solicitacaoId }: { solicitacaoId: string }) {
  const [aberto, setAberto] = useState(false);
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const { autenticado, operador, usuarioSernit, comSessao, tratouFaltaDeSessao, modal } =
    useSessaoSernitObrigatoria();
  const registrar = useRegistrarFollowUpSernit(solicitacaoId);

  async function enviar() {
    setErro(null);
    setOk(null);

    if (!texto.trim()) {
      setErro('Escreva a observação do FollowUP.');
      return;
    }

    try {
      const r = await registrar.mutateAsync(texto.trim());
      // O backend só devolve OK depois de RELER o histórico e achar o evento lá — dizer isso na
      // tela importa, porque o SERNIT já respondeu "salvo com sucesso" sem ter gravado nada.
      setOk(
        `Registrado e conferido no histórico do SERNIT${r.evento.data ? ` (${r.evento.data})` : ''}.`,
      );
      setTexto('');
      setAberto(false);
    } catch (e) {
      // Sessão caiu no meio (expirou, API reiniciou): pede a senha e reenvia sozinho.
      if (tratouFaltaDeSessao(e, enviar)) return;
      setErro(extrairMensagemDeErro(e));
    }
  }

  // Fechado, o painel é só o botão da barra de ações do topo — quem abre é quem vai escrever.
  if (!aberto) {
    return (
      <>
        {/* Pede a senha JÁ, e não depois de escrever: descobrir que precisa entrar só ao clicar
            em Enviar faz o operador redigitar contexto que já tinha na cabeça. */}
        <Button
          variante="secundaria"
          tamanho="sm"
          onClick={() => comSessao(() => setAberto(true))}
          title="Registrar uma observação no histórico da solicitação, no SERNIT"
        >
          <MessageSquarePlus className="mr-1 size-4" />+ FollowUp
        </Button>

        {ok && (
          <p className="flex w-full items-center gap-1 text-xs text-emerald-700">
            <CheckCircle2 className="size-4" /> {ok}
          </p>
        )}

        <ModalLoginSernit {...modal} />
      </>
    );
  }

  return (
    <section className="w-full rounded border border-slate-200 bg-slate-50 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <MessageSquarePlus className="size-4 text-red-700" />
        <h4 className="text-sm font-semibold text-slate-800">Registrar FollowUP no SERNIT</h4>

        {autenticado && (
          <span
            className="ml-auto flex items-center gap-1 text-[11px] text-emerald-700"
            title={`Login do SERNIT: ${usuarioSernit ?? '—'}`}
          >
            <UserCheck className="size-3.5" />
            assinando como {operador}
          </span>
        )}
      </div>

      {(
        <div className="mt-2 space-y-2">
          <textarea
            className="input min-h-24 w-full"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Ex.: Registramos que o contato com a paciente já foi realizado, e a mesma informa que poderá comparecer."
            autoFocus
          />
          {/* A trilha do SERNIT só guarda o nome do login usado. Se quem pediu o registro é outra
              pessoa (recepção, agente), isso precisa estar escrito no texto — não há outro campo. */}
          <p className="text-[11px] text-slate-500">
            O texto vai para o histórico da solicitação no SERNIT e não pode ser apagado depois.
          </p>

          <div className="flex gap-2">
            <Button tamanho="sm" onClick={enviar} disabled={registrar.isPending}>
              {registrar.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
              Enviar ao SERNIT
            </Button>
            <Button
              variante="ghost"
              tamanho="sm"
              onClick={() => {
                setAberto(false);
                setErro(null);
              }}
              disabled={registrar.isPending}
            >
              Cancelar
            </Button>
          </div>
        </div>
      )}

      {ok && (
        <p className="mt-2 flex items-center gap-1 text-xs text-emerald-700">
          <CheckCircle2 className="size-4" /> {ok}
        </p>
      )}
      {erro && (
        <p className="mt-2 rounded border border-red-200 bg-red-50 p-2 text-xs text-red-800">
          {erro}
        </p>
      )}

      <ModalLoginSernit {...modal} />
    </section>
  );
}
