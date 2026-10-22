import { useState } from 'react';
import { CheckCircle2, Loader2 } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';

import { useConferirNoSistema } from '../api/solicitacoesQueries';
import type { ConferenciaExterna, FluxoRegulacao, SolicitacaoRegulacao } from '../tiposSolicitacao';
import { ROTULO_SISTEMA_REGULACAO, type SistemaRegulacao } from '../types';
import { ResultadoConferencia } from './ConferenciaNoSistema';

/**
 * "Registrar envio": o técnico lançou o pedido na tela do próprio sistema de regulação (SISREG,
 * SER, SERNIT ou ESUS) e traz de volta o número gerado.
 *
 * <p><b>Nada aqui escreve em sistema externo</b> (D-11). O número é a chave: a partir dele a
 * conciliação casa esta solicitação com o espelho que as varreduras trazem.</p>
 *
 * <p><b>E na mesma hora a plataforma confere</b> (pedido do Bernardo, 22/10/2026): lê o pedido lá,
 * pelo número, com a credencial da integração, e mostra AQUI, no modal, onde o lançamento ficou
 * diferente do pedido — número digitado errado aparece como "não achou". O registro vale de
 * qualquer jeito: conferência é aviso, não trava.</p>
 */
export function ModalRegistrarEnvio({
  solicitacao,
  aberto,
  aoFechar,
  aoConfirmar,
  salvando,
}: {
  solicitacao: SolicitacaoRegulacao;
  aberto: boolean;
  aoFechar: () => void;
  /** Registra o número; devolve se deu certo (o erro, a página mostra). */
  aoConfirmar: (sistema: string, numeroExterno: string) => Promise<boolean>;
  salvando?: boolean;
}) {
  // Interno e NAR terminam no SISREG por definição (D-9) — não se escolhe.
  const fluxo: FluxoRegulacao = solicitacao.fluxo;
  const destinoFixo: SistemaRegulacao | null =
    fluxo === 'Nar' || fluxo === 'Interno' ? 'Sisreg' : null;

  const [sistema, setSistema] = useState<SistemaRegulacao>(
    destinoFixo ?? solicitacao.sistemaDestino ?? 'Ser',
  );
  const [numero, setNumero] = useState('');
  const [registrado, setRegistrado] = useState(false);
  const [conferencia, setConferencia] = useState<ConferenciaExterna | null>(null);
  const [erroConferencia, setErroConferencia] = useState<string | null>(null);
  const conferir = useConferirNoSistema();

  const nomeSistema = ROTULO_SISTEMA_REGULACAO[sistema];

  async function registrar() {
    if (!(await aoConfirmar(sistema, numero.trim()))) return;
    setRegistrado(true);
    try {
      setConferencia(await conferir.mutateAsync(solicitacao.id));
    } catch (e) {
      // A conferência não desfaz o registro: só não deu para ler agora.
      setErroConferencia(extrairMensagemDeErro(e));
    }
  }

  function fechar() {
    setRegistrado(false);
    setConferencia(null);
    setErroConferencia(null);
    setNumero('');
    aoFechar();
  }

  return (
    <Modal aberto={aberto} aoFechar={fechar} titulo="Registrar envio">
      {!registrado ? (
        <div className="space-y-3">
          <p className="text-sm text-slate-600">
            Use isto depois de incluir o pedido na tela do próprio sistema. O número que ele gerou passa a
            identificar este caso, e a plataforma confere o pedido lá logo em seguida.
          </p>

          {fluxo === 'Nar' && solicitacao.unidadeEmNomeDeCnes && (
            <p className="rounded border border-violet-200 bg-violet-50 p-2 text-sm text-violet-900">
              NAR: o pedido deve ter entrado no SISREG com o CNES <strong>{solicitacao.unidadeEmNomeDeCnes}</strong>{' '}
              ({solicitacao.unidadeEmNomeDeNome}). A conferência confere isso.
            </p>
          )}

          <Campo label="Sistema" htmlFor="registrar-sistema">
            <Select
              id="registrar-sistema"
              value={sistema}
              onChange={(e) => setSistema(e.target.value as SistemaRegulacao)}
              disabled={!!destinoFixo}
            >
              <option value="Sisreg">SISREG</option>
              <option value="Ser">SER (SES-RJ)</option>
              <option value="Sernit">SERNIT (Niterói)</option>
              <option value="EsusSg">ESUS São Gonçalo</option>
            </Select>
          </Campo>

          <Campo label="Número gerado pelo sistema" htmlFor="registrar-numero" required>
            <Input
              id="registrar-numero"
              value={numero}
              onChange={(e) => setNumero(e.target.value)}
              placeholder="ex.: 123456789"
              autoFocus
            />
          </Campo>

          <div className="flex justify-end gap-2 pt-1">
            <Button variante="secundaria" onClick={fechar} disabled={salvando}>
              Cancelar
            </Button>
            <Button onClick={registrar} disabled={salvando || numero.trim().length === 0}>
              Registrar
            </Button>
          </div>
        </div>
      ) : (
        <div className="space-y-3">
          {/* O desfecho fica no modal: é aqui que o técnico está olhando. */}
          <p className="flex items-center gap-1.5 text-sm text-emerald-700">
            <CheckCircle2 className="size-4" /> Número {numero.trim()} registrado — a solicitação está como
            enviada ao {nomeSistema}.
          </p>

          {conferir.isPending ? (
            <p className="flex items-center gap-2 text-sm text-slate-600">
              <Loader2 className="size-4 animate-spin" /> Conferindo o pedido no {nomeSistema}…
            </p>
          ) : conferencia ? (
            <ResultadoConferencia c={conferencia} />
          ) : erroConferencia ? (
            <p className="rounded bg-amber-50 p-3 text-sm text-amber-900">
              Não deu para conferir agora ({erroConferencia}). O número ficou registrado; use “Conferir de
              novo” no cartão da conferência.
            </p>
          ) : null}

          <div className="flex justify-end pt-1">
            <Button onClick={fechar} disabled={conferir.isPending}>
              Fechar
            </Button>
          </div>
        </div>
      )}
    </Modal>
  );
}
