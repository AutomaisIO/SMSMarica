import { useState } from 'react';
import { AlertTriangle, ArrowLeftRight, ArrowRight, Trash2, UserRoundX } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import {
  DestinoDaOrigem,
  alterarDestino,
  descartarEstudo,
  obterPrevia,
  trocarEstudos,
  type PreviaCorrecao,
} from '@/features/solicitacoes-exame/api/correcaoIdentidadeApi';
import type { SolicitacaoExame } from '@/features/solicitacoes-exame/types';

type Acao = 'descartar' | 'alterar' | 'trocar';

/**
 * Correção de identidade, no lugar onde o erro aparece: a tela do pedido.
 *
 * Quando o exame chega, ele já se associa e atualiza o pedido — então é aqui que se percebe que
 * as imagens são de outra pessoa, e é aqui que se resolve. Sem fila, sem triagem: quem tem a
 * permissão abre o modal e corrige na hora; quem não tem, não vê o card.
 */
export function CardCorrecaoIdentidade({ s }: { s: SolicitacaoExame }) {
  const podeCorrigir = usePermissao('CorrecaoIdentidadeExame', 'Edicao');
  const [aberto, setAberto] = useState(false);

  // Sem estudo não há identidade para corrigir — o exame ainda não foi feito.
  if (!podeCorrigir || !s.studyInstanceUID) return null;

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4">
      <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
        Correção de identidade
      </h2>
      <p className="text-sm text-gray-600">
        Use se as imagens deste exame <b>não forem deste paciente</b> — em geral quando o paciente
        errado foi selecionado no aparelho.
      </p>
      <Button variante="outline" className="mt-3" onClick={() => setAberto(true)}>
        <UserRoundX className="mr-2 h-4 w-4" />
        Não é este paciente
      </Button>

      {aberto && <ModalCorrigir s={s} aoFechar={() => setAberto(false)} />}
    </section>
  );
}

function ModalCorrigir({ s, aoFechar }: { s: SolicitacaoExame; aoFechar: () => void }) {
  const [previa, setPrevia] = useState<PreviaCorrecao | null>(null);
  const [accessionDestino, setAccessionDestino] = useState('');
  const [acao, setAcao] = useState<Acao>('alterar');
  const [origemJaFez, setOrigemJaFez] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [carregando, setCarregando] = useState(false);
  const [executando, setExecutando] = useState(false);

  async function conferirDestino() {
    if (!accessionDestino.trim()) return;
    setCarregando(true);
    try {
      const p = await obterPrevia(s.studyInstanceUID, accessionDestino.trim());
      setPrevia(p);
      // Destino que já tem exame próprio = os dois estão trocados entre si.
      if (p.studyDoDestinoSugerido) setAcao('trocar');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
      setPrevia(null);
    } finally {
      setCarregando(false);
    }
  }

  async function executar() {
    setExecutando(true);
    try {
      if (acao === 'descartar') {
        await descartarEstudo(s.studyInstanceUID, motivo.trim());
      } else if (acao === 'alterar') {
        await alterarDestino(
          s.studyInstanceUID,
          previa!.destinoAccession,
          origemJaFez ? DestinoDaOrigem.JaFoiFeito : DestinoDaOrigem.DevolverAWorklist,
          motivo.trim(),
        );
      } else {
        await trocarEstudos(
          s.studyInstanceUID,
          previa!.destinoAccession,
          previa!.studyDoDestinoSugerido!,
          motivo.trim(),
        );
      }
      notificar('Pronto. O exame agora está no paciente certo.');
      aoFechar();
      // Recarrega: o pedido mudou de estado e a tela inteira precisa refletir isso.
      window.location.reload();
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    } finally {
      setExecutando(false);
    }
  }

  const precisaDestino = acao !== 'descartar';
  const motivoOk = motivo.trim().length >= 5;
  const podeConfirmar =
    motivoOk && !executando && (!precisaDestino || (!!previa && !!previa.destinoAccession));

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Este exame não é deste paciente" largura="md">
      <div className="space-y-4">
        <Opcao
          marcada={acao === 'alterar'}
          aoMarcar={() => setAcao('alterar')}
          icone={<ArrowRight className="h-4 w-4" />}
          titulo="As imagens são de outro paciente"
          descricao="Passam para o pedido dele. Este paciente é liberado."
        />
        <Opcao
          marcada={acao === 'trocar'}
          aoMarcar={() => setAcao('trocar')}
          desabilitada={!previa?.studyDoDestinoSugerido}
          icone={<ArrowLeftRight className="h-4 w-4" />}
          titulo="Os dois estão trocados entre si"
          descricao={
            previa?.studyDoDestinoSugerido
              ? 'Cada um recebe as suas imagens. Ninguém precisa refazer o exame.'
              : 'Informe o outro paciente abaixo para saber se este é o caso.'
          }
        />
        <Opcao
          marcada={acao === 'descartar'}
          aoMarcar={() => setAcao('descartar')}
          icone={<Trash2 className="h-4 w-4" />}
          titulo="As imagens não servem"
          descricao="São apagadas e o paciente volta para a lista do aparelho, para refazer o exame."
        />

        {precisaDestino && (
          <div className="rounded-md bg-gray-50 p-3">
            <Campo label="Nº do pedido do paciente certo" htmlFor="destino-correcao">
              <div className="flex gap-2">
                <Input
                  id="destino-correcao"
                  value={accessionDestino}
                  onChange={(e) => {
                    setAccessionDestino(e.target.value);
                    setPrevia(null);
                  }}
                  placeholder="Ex.: 260804109"
                />
                <Button
                  variante="outline"
                  onClick={conferirDestino}
                  disabled={!accessionDestino.trim() || carregando}
                >
                  {carregando ? 'Conferindo…' : 'Conferir'}
                </Button>
              </div>
            </Campo>

            {previa?.destinoAccession && (
              <div className="mt-3 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm">
                <div className="font-medium text-gray-900">{previa.destinoPacienteNome}</div>
                <div className="text-gray-600">
                  {previa.destinoProcedimento ?? 'Exame'} · pedido {previa.destinoAccession}
                </div>
              </div>
            )}
          </div>
        )}

        {acao === 'alterar' && previa?.destinoAccession && (
          <label className="flex cursor-pointer items-start gap-2 rounded-md bg-gray-50 p-3 text-sm">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={origemJaFez}
              onChange={(e) => setOrigemJaFez(e.target.checked)}
            />
            <span>
              <b>Este paciente já fez o exame dele.</b>
              <span className="block text-gray-600">
                Marque se o exame já foi refeito. Assim ele não volta para a lista do aparelho.
              </span>
            </span>
          </label>
        )}

        {previa?.temLaudoAssinado ? (
          <Aviso tom="bloqueio">
            Este exame já tem <b>laudo assinado</b>. Fale com a médica responsável antes — um laudo
            assinado nunca é apagado.
          </Aviso>
        ) : (
          <Aviso tom="atencao">
            <b>Confira antes: isto não volta atrás sozinho.</b>
            <ul className="mt-1 list-disc pl-5">
              <li>As imagens com o nome errado são apagadas.</li>
              {(previa?.rascunhosQueSeraoDescartados ?? 0) > 0 && (
                <li>
                  {previa!.rascunhosQueSeraoDescartados === 1
                    ? '1 laudo em rascunho será descartado.'
                    : `${previa!.rascunhosQueSeraoDescartados} laudos em rascunho serão descartados.`}
                </li>
              )}
              {previa?.comunicacaoJaEnviada && (
                <li>
                  O link que o paciente recebeu no WhatsApp <b>deixa de funcionar</b>.
                </li>
              )}
            </ul>
          </Aviso>
        )}

        <Campo label="Motivo" htmlFor="motivo-correcao" required dica="Fica registrado com o seu nome.">
          <Input
            id="motivo-correcao"
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder="Ex.: paciente errado selecionado no aparelho"
          />
        </Campo>

        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar} disabled={executando}>
            Cancelar
          </Button>
          <Button
            variante="danger"
            onClick={executar}
            disabled={!podeConfirmar || previa?.temLaudoAssinado}
          >
            {executando ? 'Corrigindo…' : 'Confirmar correção'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

function Opcao({
  marcada,
  aoMarcar,
  icone,
  titulo,
  descricao,
  desabilitada,
}: {
  marcada: boolean;
  aoMarcar: () => void;
  icone: React.ReactNode;
  titulo: string;
  descricao: string;
  desabilitada?: boolean;
}) {
  return (
    <label
      className={`flex items-start gap-3 rounded-md border p-3 ${
        desabilitada
          ? 'cursor-not-allowed border-gray-200 opacity-60'
          : marcada
            ? 'cursor-pointer border-marica bg-marica/5'
            : 'cursor-pointer border-gray-200 hover:bg-gray-50'
      }`}
    >
      <input
        type="radio"
        name="acao-correcao-identidade"
        className="mt-1"
        checked={marcada}
        disabled={desabilitada}
        onChange={aoMarcar}
      />
      <span className="text-sm">
        <span className="flex items-center gap-2 font-medium text-gray-900">
          {icone}
          {titulo}
        </span>
        <span className="block text-gray-600">{descricao}</span>
      </span>
    </label>
  );
}

function Aviso({ tom, children }: { tom: 'atencao' | 'bloqueio'; children: React.ReactNode }) {
  return (
    <div
      className={`flex gap-3 rounded-lg border p-3 text-sm ${
        tom === 'bloqueio'
          ? 'border-red-300 bg-red-50 text-red-900'
          : 'border-amber-300 bg-amber-50 text-amber-900'
      }`}
    >
      <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />
      <div>{children}</div>
    </div>
  );
}
