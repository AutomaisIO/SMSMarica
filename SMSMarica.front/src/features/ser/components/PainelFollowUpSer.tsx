import { useState } from 'react';
import { AxiosError } from 'axios';
import { CheckCircle2, Loader2, MessageSquarePlus, UserCheck } from 'lucide-react';

import {
  useRegistrarFollowUpSer,
  useSessaoOperadorSer,
} from '@/features/ser/api/queries';
import { ModalLoginSer } from '@/features/ser/components/ModalLoginSer';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro, type ProblemaApi } from '@/shared/api/httpClient';

/** O código de validação vem como CHAVE em `errors` (ValidacaoException → ValidationProblemDetails). */
function temCodigo(erro: unknown, codigo: string): boolean {
  if (!(erro instanceof AxiosError)) return false;
  const dados = erro.response?.data as ProblemaApi | undefined;
  return Boolean(dados?.errors && codigo in dados.errors);
}

/**
 * Registrar FollowUP na solicitação — <b>escreve no SER</b>.
 *
 * <p>O FollowUP é a observação que não muda a situação: é como a regulação registra no Estado que
 * o contato com o paciente foi feito. Até aqui isso só existia no SER, e quem operava tinha de
 * abrir a tela deles.</p>
 *
 * <p><b>Assinado pelo operador.</b> A credencial do sistema é de sincronismo e só lê; o SER grava
 * o nome de quem fez em cada evento. Por isso a primeira escrita pede o login pessoal do SER — e
 * a tela mostra, antes de enviar, qual nome vai ficar no histórico do Estado.</p>
 */
export function PainelFollowUpSer({ solicitacaoId }: { solicitacaoId: string }) {
  const [aberto, setAberto] = useState(false);
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [pedindoLogin, setPedindoLogin] = useState(false);

  const { data: sessao } = useSessaoOperadorSer();
  const registrar = useRegistrarFollowUpSer(solicitacaoId);

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
      // tela importa, porque o SER já respondeu "salvo com sucesso" sem ter gravado nada.
      setOk(
        `Registrado e conferido no histórico do SER${
          r.evento.data ? ` (${r.evento.data})` : ''
        }.`,
      );
      setTexto('');
      setAberto(false);
    } catch (e) {
      // Sessão do SER ausente ou expirada: em vez de erro seco, pede a senha e reenvia.
      if (temCodigo(e, 'ser.sessao_operador_ausente')) {
        setPedindoLogin(true);
        return;
      }
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <section className="rounded border border-slate-200 bg-slate-50 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <MessageSquarePlus className="size-4 text-red-700" />
        <h4 className="text-sm font-semibold text-slate-800">Registrar FollowUP no SER</h4>

        {sessao?.autenticado ? (
          <span
            className="ml-auto flex items-center gap-1 text-[11px] text-emerald-700"
            title="É este nome que fica no histórico da solicitação, no SER"
          >
            <UserCheck className="size-3.5" />
            assinando como {sessao.usuarioSer}
          </span>
        ) : (
          <span className="ml-auto text-[11px] text-slate-500">exige seu login do SER</span>
        )}
      </div>

      {!aberto && (
        <div className="mt-2">
          <Button variante="secundaria" tamanho="sm" onClick={() => setAberto(true)}>
            Escrever FollowUP
          </Button>
        </div>
      )}

      {aberto && (
        <div className="mt-2 space-y-2">
          <textarea
            className="input min-h-24 w-full"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Ex.: Registramos que o contato com a paciente já foi realizado, e a mesma informa que poderá comparecer."
            autoFocus
          />
          {/* A trilha do SER só guarda o nome do login usado. Se quem pediu o registro é outra
              pessoa (recepção, agente), isso precisa estar escrito no texto — não há outro campo. */}
          <p className="text-[11px] text-slate-500">
            O texto vai para o histórico da solicitação no SER e não pode ser apagado depois.
          </p>

          <div className="flex gap-2">
            <Button tamanho="sm" onClick={enviar} disabled={registrar.isPending}>
              {registrar.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
              Enviar ao SER
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

      <ModalLoginSer
        aberto={pedindoLogin}
        aoFechar={() => setPedindoLogin(false)}
        aoAutenticar={() => {
          // Retoma o envio que disparou o pedido de senha: o operador não redigita o texto.
          setPedindoLogin(false);
          void enviar();
        }}
      />
    </section>
  );
}
