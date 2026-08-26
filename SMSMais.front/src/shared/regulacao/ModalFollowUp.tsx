import { useState, type ComponentType } from 'react';
import { CheckCircle2, Loader2, MessageSquarePlus, UserCheck } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

/**
 * Sessão do sistema de origem (SER/SERNIT) já resolvida pelo painel da feature.
 *
 * <p>Os hooks `useSessaoSerObrigatoria`/`useSessaoSernitObrigatoria` devolvem exatamente este
 * shape — só muda o nome do campo do login (`usuarioSer`/`usuarioSernit`), que o wrapper mapeia
 * para `usuarioSistema`. Manter a sessão do lado da feature evita que este componente
 * compartilhado importe de `features/*`.</p>
 */
export type SessaoFollowUp = {
  autenticado: boolean;
  /** Nome de quem está assinando, para a tela. */
  operador: string | null;
  /** Login usado no sistema de origem, exibido no `title` do badge (suporte). */
  usuarioSistema: string | null;
  comSessao: (acao: () => void) => void;
  tratouFaltaDeSessao: (erro: unknown, repetir: () => void | Promise<void>) => boolean;
  /** Props prontas para o modal de login do sistema. */
  modal: { aberto: boolean; aoFechar: () => void; aoAutenticar?: () => void };
};

/** O que o registro de FollowUP devolve — só o que esta tela usa. */
type FollowUpResultado = { evento: { data?: string | null } };

type Props = {
  /** Rótulo do sistema de origem: `"SER"` ou `"SERNIT"`. */
  sistema: string;
  sessao: SessaoFollowUp;
  registrar: {
    mutateAsync: (texto: string) => Promise<FollowUpResultado>;
    isPending: boolean;
  };
  /** Modal de autenticação do sistema (`ModalLoginSer`/`ModalLoginSernit`). */
  ModalLogin: ComponentType<{
    aberto: boolean;
    aoFechar: () => void;
    aoAutenticar?: () => void;
  }>;
};

/**
 * Registrar FollowUP na solicitação — <b>escreve no sistema de origem (SER/SERNIT)</b>.
 *
 * <p>Um único modal para os dois sistemas. O FollowUP é a observação que não muda a situação: é
 * como a regulação registra no Estado/Município que o contato com o paciente foi feito.</p>
 *
 * <p><b>Assinado pelo operador.</b> A credencial do sistema é de sincronismo e só lê; o sistema de
 * origem grava o nome de quem fez em cada evento. Quando falta sessão, a tela <b>pede a senha</b> —
 * nunca mostra a recusa do backend como erro.</p>
 *
 * <p>O gatilho é só o botão da barra de ações; o preenchimento abre em <b>modal</b> (não empurra
 * a página de detalhe da solicitação).</p>
 */
export function ModalFollowUp({ sistema, sessao, registrar, ModalLogin }: Props) {
  const [aberto, setAberto] = useState(false);
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const { autenticado, operador, usuarioSistema, comSessao, tratouFaltaDeSessao, modal } = sessao;

  function fechar() {
    setAberto(false);
    setErro(null);
  }

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
      // tela importa, porque o sistema de origem já respondeu "salvo com sucesso" sem ter gravado.
      setOk(
        `Registrado e conferido no histórico do ${sistema}${r.evento.data ? ` (${r.evento.data})` : ''}.`,
      );
      setTexto('');
      setAberto(false);
    } catch (e) {
      // Sessão caiu no meio (expirou, API reiniciou): pede a senha e reenvia sozinho.
      if (tratouFaltaDeSessao(e, enviar)) return;
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <>
      {/* Pede a senha JÁ, e não depois de escrever: descobrir que precisa entrar só ao clicar
          em Enviar faz o operador redigitar contexto que já tinha na cabeça. */}
      <Button
        variante="secundaria"
        tamanho="sm"
        onClick={() =>
          comSessao(() => {
            setOk(null);
            setErro(null);
            setAberto(true);
          })
        }
        title={`Registrar uma observação no histórico da solicitação, no ${sistema}`}
      >
        <MessageSquarePlus className="mr-1 size-4" />+ FollowUp
      </Button>

      {ok && !aberto && (
        <p className="flex w-full items-center gap-1 text-xs text-emerald-700">
          <CheckCircle2 className="size-4" /> {ok}
        </p>
      )}

      <Modal
        aberto={aberto}
        // Enquanto grava, não deixa fechar por Esc/clique fora: a ação está a caminho do SER.
        aoFechar={registrar.isPending ? () => {} : fechar}
        titulo={`Registrar FollowUP no ${sistema}`}
        largura="md"
      >
        <div className="space-y-2">
          {autenticado && (
            <span
              className="flex items-center gap-1 text-[11px] text-emerald-700"
              title={`Login do ${sistema}: ${usuarioSistema ?? '—'}`}
            >
              <UserCheck className="size-3.5" />
              assinando como {operador}
            </span>
          )}

          <textarea
            className="input min-h-32 w-full"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Ex.: Registramos que o contato com a paciente já foi realizado, e a mesma informa que poderá comparecer."
            autoFocus
          />
          {/* A trilha do sistema só guarda o nome do login usado. Se quem pediu o registro é outra
              pessoa (recepção, agente), isso precisa estar escrito no texto — não há outro campo. */}
          <p className="text-[11px] text-slate-500">
            O texto vai para o histórico da solicitação no {sistema} e não pode ser apagado depois.
          </p>

          {erro && (
            <p className="rounded border border-red-200 bg-red-50 p-2 text-xs text-red-800">
              {erro}
            </p>
          )}

          <div className="mt-2 flex justify-end gap-2">
            <Button
              variante="ghost"
              tamanho="sm"
              onClick={fechar}
              disabled={registrar.isPending}
            >
              Cancelar
            </Button>
            <Button tamanho="sm" onClick={enviar} disabled={registrar.isPending}>
              {registrar.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
              Enviar ao {sistema}
            </Button>
          </div>
        </div>
      </Modal>

      <ModalLogin {...modal} />
    </>
  );
}
