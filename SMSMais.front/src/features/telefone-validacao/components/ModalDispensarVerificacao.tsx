import { useEffect, useState } from 'react';
import { AlertTriangle, Loader2, MessageSquareOff, ShieldOff } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { Campo } from '@/shared/ui/Campo';
import { useMotivosDispensa, useRegistrarDispensa } from '@/features/telefone-validacao/api/queries';
import type { MotivoDispensaContato } from '@/features/telefone-validacao/api/dispensaContatoApi';

type Props = {
  aberto: boolean;
  /** Id do paciente (hub FHIR) — a dispensa é por pessoa, não por exame. */
  pacienteId: string;
  /** Nome, só para o texto do consentimento ficar concreto no balcão. */
  pacienteNome?: string | null;
  aoFechar: () => void;
  aoRegistrado?: () => void;
};

/**
 * Registra a DISPENSA de verificação: o paciente não vai validar o WhatsApp e o motivo fica
 * gravado. É a saída para quem não tem celular, não tem o app, ou não consegue confirmar o
 * código — casos em que o gate de contato verificado deixava a pessoa parada na recepção sem
 * ninguém poder autorizar o exame.
 *
 * O motivo não é burocracia: ele decide se resultado e laudo ainda saem por WhatsApp ou se a
 * entrega passa a ser presencial. Por isso a consequência da escolha aparece na tela ANTES de
 * confirmar, e a ciência do paciente é obrigatória.
 */
export function ModalDispensarVerificacao({
  aberto, pacienteId, pacienteNome, aoFechar, aoRegistrado,
}: Props) {
  const motivos = useMotivosDispensa(aberto);
  const registrar = useRegistrarDispensa();
  const [motivo, setMotivo] = useState<MotivoDispensaContato | ''>('');
  const [descricao, setDescricao] = useState('');
  const [ciente, setCiente] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  // Cada abertura começa do zero: reaproveitar a escolha anterior faria a recepção confirmar
  // no automático um motivo que era de outro paciente.
  useEffect(() => {
    if (!aberto) return;
    setMotivo('');
    setDescricao('');
    setCiente(false);
    setErro(null);
  }, [aberto]);

  const opcoes = motivos.data ?? [];
  const escolhido = opcoes.find((o) => o.motivo === motivo);
  const precisaDescricao = escolhido?.exigeDescricao ?? false;
  const podeConfirmar =
    Boolean(escolhido) && ciente && (!precisaDescricao || descricao.trim().length >= 5) && !registrar.isPending;

  async function confirmar() {
    if (!escolhido || !podeConfirmar) return;
    setErro(null);
    try {
      await registrar.mutateAsync({
        pacienteId,
        motivo: escolhido.motivo,
        motivoDescricao: descricao.trim() || null,
        pacienteCiente: ciente,
      });
      aoRegistrado?.();
      aoFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Dispensar verificação do WhatsApp" largura="md">
      <div className="flex flex-col gap-4">
        <p className="text-sm text-gray-600">
          Use quando {pacienteNome ? <strong>{pacienteNome}</strong> : 'o paciente'} não puder confirmar o
          código no WhatsApp. O motivo fica registrado com o seu nome e a data, e o exame pode ser
          autorizado normalmente.
        </p>

        {motivos.isLoading ? (
          <span className="flex items-center gap-2 text-sm text-gray-500">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando motivos…
          </span>
        ) : (
          <Campo label="Motivo" htmlFor="dispensa-motivo" required>
            <div id="dispensa-motivo" className="flex flex-col gap-1.5">
              {opcoes.map((o) => (
                <label
                  key={o.motivo}
                  className={`flex cursor-pointer items-start gap-2 rounded-md border px-3 py-2 text-sm ${
                    motivo === o.motivo
                      ? 'border-primary-400 bg-primary-50'
                      : 'border-gray-200 hover:bg-gray-50'
                  }`}
                >
                  <input
                    type="radio"
                    name="motivo-dispensa"
                    className="mt-1"
                    checked={motivo === o.motivo}
                    onChange={() => setMotivo(o.motivo)}
                  />
                  <span className="flex-1 text-gray-900">{o.rotulo}</span>
                  {/* Sinaliza na própria lista o que muda nos avisos — quem escolhe entende a
                      consequência sem precisar selecionar para descobrir. */}
                  {!o.permiteEnvio ? (
                    <MessageSquareOff
                      className="mt-0.5 h-4 w-4 shrink-0 text-amber-600"
                      aria-label="Resultado e laudo não vão por WhatsApp"
                    />
                  ) : null}
                </label>
              ))}
            </div>
          </Campo>
        )}

        {precisaDescricao ? (
          <Campo
            label="Descreva o motivo"
            htmlFor="dispensa-descricao"
            required
            dica="Mínimo de 5 caracteres — este texto é o que a auditoria vai ler."
          >
            <textarea
              id="dispensa-descricao"
              value={descricao}
              onChange={(e) => setDescricao(e.target.value)}
              rows={3}
              maxLength={1000}
              className="input"
              placeholder="Ex.: celular quebrado, vai trocar de número nesta semana"
            />
          </Campo>
        ) : null}

        {escolhido ? (
          <p
            className={`flex items-start gap-2 rounded border p-2 text-sm ${
              escolhido.permiteEnvio
                ? 'border-blue-200 bg-blue-50 text-blue-900'
                : 'border-amber-200 bg-amber-50 text-amber-900'
            }`}
          >
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>{escolhido.consequencia}</span>
          </p>
        ) : null}

        <label className="flex items-start gap-2 text-sm text-gray-800">
          <input
            type="checkbox"
            className="mt-1"
            checked={ciente}
            onChange={(e) => setCiente(e.target.checked)}
          />
          <span>
            Informei o paciente de que ele <strong>não receberá avisos, resultado nem laudo pelo
            WhatsApp</strong> enquanto o número não for verificado, e ele concordou.
          </span>
        </label>

        {erro ? <p className="text-sm text-red-700">{erro}</p> : null}

        <div className="flex justify-end gap-2 border-t border-gray-100 pt-4">
          <Button variante="ghost" onClick={aoFechar} disabled={registrar.isPending}>
            Cancelar
          </Button>
          <Button onClick={confirmar} disabled={!podeConfirmar}>
            {registrar.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <ShieldOff className="h-4 w-4" />
            )}
            Registrar dispensa
          </Button>
        </div>
      </div>
    </Modal>
  );
}
