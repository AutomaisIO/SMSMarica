import { useEffect, useState } from 'react';
import { AlertTriangle, ArrowRight, Trash2, ArrowLeftRight, Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Campo } from '@/shared/ui/Campo';
import { usePermissao } from '@/shared/auth/authStore';
import { notificar } from '@/shared/ui/Notificacoes';
import {
  DestinoDaOrigem,
  alterarDestino,
  descartarEstudo,
  obterPrevia,
  trocarEstudos,
  listarIncidentesAbertos,
  type IncidenteAberto,
  type PreviaCorrecao,
} from '../api/correcaoIdentidadeApi';

type Acao = 'descartar' | 'alterar' | 'trocar';

/**
 * Correção de identidade de exame — o estudo está no paciente errado.
 *
 * O operador chega aqui com o StudyInstanceUID (copiado da tela de exames) e escolhe UMA das três
 * ações. A tela é deliberadamente literal sobre o que vai acontecer: a operação apaga o objeto
 * original do PACS e o re-armazena reescrito, descarta rascunhos de laudo e revoga o link já
 * enviado ao paciente. Nada disso se desfaz sozinho.
 */
export function CorrecaoIdentidadeExamePage() {
  const podeExecutar = usePermissao('CorrecaoIdentidadeExame', 'Edicao');

  const [studyUid, setStudyUid] = useState('');
  const [accessionDestino, setAccessionDestino] = useState('');
  const [previa, setPrevia] = useState<PreviaCorrecao | null>(null);
  const [acao, setAcao] = useState<Acao>('alterar');
  const [origemJaFez, setOrigemJaFez] = useState(false);
  const [trazerDoDestino, setTrazerDoDestino] = useState(true);
  const [motivo, setMotivo] = useState('');
  const [carregando, setCarregando] = useState(false);
  const [executando, setExecutando] = useState(false);
  const [fila, setFila] = useState<IncidenteAberto[]>([]);

  async function recarregarFila() {
    try {
      setFila(await listarIncidentesAbertos());
    } catch {
      /* a fila é um atalho: se falhar, o operador ainda pode colar o identificador à mão */
    }
  }

  useEffect(() => {
    void recarregarFila();
  }, []);

  async function buscar() {
    if (!studyUid.trim()) return;
    setCarregando(true);
    try {
      const p = await obterPrevia(studyUid.trim(), accessionDestino.trim() || undefined);
      setPrevia(p);
      // Destino que já tem estudo próprio = caso de TROCA, não de "alterar destino".
      if (p.studyDoDestinoSugerido) setAcao('trocar');
    } catch (e) {
      notificar(e instanceof Error ? e.message : 'Não foi possível carregar o exame.', 'erro');
      setPrevia(null);
    } finally {
      setCarregando(false);
    }
  }

  async function executar() {
    if (!previa || !motivo.trim()) return;
    setExecutando(true);
    try {
      if (acao === 'descartar') {
        await descartarEstudo(previa.studyInstanceUID, motivo.trim());
      } else if (acao === 'alterar') {
        await alterarDestino(
          previa.studyInstanceUID,
          previa.destinoAccession,
          origemJaFez ? DestinoDaOrigem.JaFoiFeito : DestinoDaOrigem.DevolverAWorklist,
          motivo.trim(),
        );
      } else {
        await trocarEstudos(
          previa.studyInstanceUID,
          previa.destinoAccession,
          previa.studyDoDestinoSugerido!,
          motivo.trim(),
        );
      }
      notificar('Pronto. O exame agora está no paciente certo.');
      void recarregarFila();
      setPrevia(null);
      setStudyUid('');
      setAccessionDestino('');
      setMotivo('');
    } catch (e) {
      notificar(e instanceof Error ? e.message : 'Não foi possível corrigir. Nada foi alterado.', 'erro');
    } finally {
      setExecutando(false);
    }
  }

  const precisaDestino = acao !== 'descartar';
  const destinoOk = !precisaDestino || (!!previa && previa.destinoExameId !== EMPTY_GUID);
  const podeConfirmar =
    !!previa && !previa.temLaudoAssinado && destinoOk && motivo.trim().length >= 5 && podeExecutar;

  return (
    <div className="mx-auto max-w-4xl space-y-6 p-6">
      <header>
        <h1 className="text-xl font-semibold text-gray-900">Corrigir identidade de exame</h1>
        <p className="mt-1 text-sm text-gray-600">
          Use quando as imagens de um paciente foram gravadas no nome de outro. A correção acerta
          o exame no sistema e também nas imagens guardadas, para que tudo passe a mostrar o
          paciente certo.
        </p>
      </header>

      {fila.length > 0 && (
        <section className="rounded-lg border border-amber-300 bg-amber-50 p-4">
          <h2 className="mb-3 text-sm font-semibold text-amber-900">
            Exames aguardando conferência ({fila.length})
          </h2>
          <ul className="space-y-2">
            {fila.map((i) => (
              <li
                key={i.id}
                className="flex items-start justify-between gap-3 rounded-md bg-white p-3 text-sm"
              >
                <div>
                  <div className="font-medium text-gray-900">
                    {i.pacienteSuspeitoNome ?? 'Paciente não identificado'}
                    {i.automatico && (
                      <span className="ml-2 rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600">
                        detectado pelo sistema
                      </span>
                    )}
                  </div>
                  <div className="text-gray-600">{i.motivo}</div>
                </div>
                <button
                  type="button"
                  className="shrink-0 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                  onClick={() => {
                    setStudyUid(i.studyInstanceUID);
                    setPrevia(null);
                  }}
                >
                  Abrir
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="rounded-lg border border-gray-200 bg-white p-4">
        <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
          <Campo label="Identificador do exame" htmlFor="study-uid" dica="Copie da tela de Exames de Imagem.">
            <Input
              id="study-uid"
              value={studyUid}
              onChange={(e) => setStudyUid(e.target.value)}
              placeholder="Cole aqui o identificador"
            />
          </Campo>
          <Campo label="Nº do pedido do paciente certo" htmlFor="accession-destino">
            <Input
              id="accession-destino"
              value={accessionDestino}
              onChange={(e) => setAccessionDestino(e.target.value)}
              placeholder="260804109"
            />
          </Campo>
        </div>
        <Button className="mt-3" onClick={buscar} disabled={!studyUid.trim() || carregando}>
          <Search className="mr-2 h-4 w-4" />
          {carregando ? 'Carregando…' : 'Carregar exame'}
        </Button>
      </section>

      {previa && (
        <>
          <section className="grid gap-4 sm:grid-cols-2">
            <Cartao titulo="Está hoje no paciente" destaque="erro">
              <Linha rotulo="Paciente" valor={previa.pacienteAtualNome ?? '— sem vínculo —'} />
              <Linha rotulo="Pedido" valor={previa.accessionAtual ?? '—'} />
            </Cartao>
            <Cartao titulo="Vai passar para" destaque="ok">
              <Linha rotulo="Paciente" valor={previa.destinoPacienteNome || '—'} />
              <Linha rotulo="Pedido" valor={previa.destinoAccession || '—'} />
              <Linha rotulo="Exame" valor={previa.destinoProcedimento ?? '—'} />
            </Cartao>
          </section>

          {previa.temLaudoAssinado && (
            <Aviso tom="bloqueio">
              Este exame já tem <b>laudo assinado</b>. Não é possível corrigir agora — fale com a
              médica responsável primeiro, porque um laudo assinado nunca é apagado.
            </Aviso>
          )}

          <section className="rounded-lg border border-gray-200 bg-white p-4">
            <h2 className="mb-3 text-sm font-semibold text-gray-900">O que fazer com estas imagens</h2>
            <div className="space-y-2">
              <Opcao
                marcada={acao === 'descartar'}
                aoMarcar={() => setAcao('descartar')}
                icone={<Trash2 className="h-4 w-4" />}
                titulo="Descartar as imagens"
                descricao="Não servem para ninguém (exame interrompido, teste, repetido). São apagadas e o paciente volta para a lista do aparelho, para refazer o exame."
              />
              <Opcao
                marcada={acao === 'alterar'}
                aoMarcar={() => setAcao('alterar')}
                icone={<ArrowRight className="h-4 w-4" />}
                titulo="Alterar destino"
                descricao="As imagens são de outro paciente. Elas passam para o pedido dele, e o paciente que estava errado é liberado."
              />
              <Opcao
                marcada={acao === 'trocar'}
                aoMarcar={() => setAcao('trocar')}
                desabilitada={!previa.studyDoDestinoSugerido}
                icone={<ArrowLeftRight className="h-4 w-4" />}
                titulo="Trocar um pelo outro"
                descricao={
                  previa.studyDoDestinoSugerido
                    ? 'Os dois pacientes estão com as imagens trocadas entre si. Cada um recebe as suas, e ninguém precisa refazer o exame.'
                    : 'Não se aplica: o outro paciente não tem imagens para devolver em troca.'
                }
              />
            </div>

            {acao === 'alterar' && (
              <label className="mt-4 flex cursor-pointer items-start gap-2 rounded-md bg-gray-50 p-3 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={origemJaFez}
                  onChange={(e) => setOrigemJaFez(e.target.checked)}
                />
                <span>
                  <b>O paciente que estava errado já fez o exame dele.</b>
                  <span className="block text-gray-600">
                    Marque se o exame dele já foi refeito. Assim ele não volta para a lista do
                    aparelho e ninguém corre o risco de examiná-lo de novo sem necessidade.
                  </span>
                </span>
              </label>
            )}

            {acao === 'trocar' && previa.studyDoDestinoSugerido && (
              <label className="mt-4 flex cursor-pointer items-start gap-2 rounded-md bg-gray-50 p-3 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={trazerDoDestino}
                  onChange={(e) => setTrazerDoDestino(e.target.checked)}
                />
                <span>
                  <b>Devolver as imagens do outro paciente no mesmo passo.</b>
                  <span className="block text-gray-600">
                    Encontramos imagens que parecem ser do paciente que estava errado. Marcado,
                    os dois ficam certos de uma vez.
                  </span>
                </span>
              </label>
            )}
          </section>

          <Aviso tom="atencao">
            <b>Confira antes: isto não volta atrás sozinho.</b>
            <ul className="mt-1 list-disc pl-5">
              <li>As imagens antigas, com o nome errado, são apagadas.</li>
              {previa.rascunhosQueSeraoDescartados > 0 && (
                <li>
                  {previa.rascunhosQueSeraoDescartados === 1
                    ? '1 laudo em rascunho será descartado.'
                    : `${previa.rascunhosQueSeraoDescartados} laudos em rascunho serão descartados.`}
                </li>
              )}
              {previa.comunicacaoJaEnviada && (
                <li>
                  O link que o paciente recebeu no WhatsApp <b>deixa de funcionar</b>.
                </li>
              )}
            </ul>
          </Aviso>

          <section className="rounded-lg border border-gray-200 bg-white p-4">
            <Campo
              label="Motivo da correção"
              htmlFor="motivo-correcao"
              required
              dica="Fica registrado junto com o seu nome."
            >
              <Input
                id="motivo-correcao"
                value={motivo}
                onChange={(e) => setMotivo(e.target.value)}
                placeholder="Ex.: paciente errado selecionado no aparelho"
              />
            </Campo>
            <Button
              className="mt-3"
              variante="danger"
              onClick={executar}
              disabled={!podeConfirmar || executando}
            >
              {executando ? 'Corrigindo…' : 'Confirmar correção'}
            </Button>
            {!podeExecutar && (
              <p className="mt-2 text-xs text-gray-500">
                Seu acesso permite consultar, mas não fazer a correção.
              </p>
            )}
          </section>
        </>
      )}
    </div>
  );
}

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

function Cartao({
  titulo,
  destaque,
  children,
}: {
  titulo: string;
  destaque: 'erro' | 'ok';
  children: React.ReactNode;
}) {
  return (
    <div
      className={`rounded-lg border p-4 ${
        destaque === 'erro' ? 'border-red-200 bg-red-50' : 'border-emerald-200 bg-emerald-50'
      }`}
    >
      <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-600">{titulo}</h3>
      <dl className="space-y-1 text-sm">{children}</dl>
    </div>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="flex justify-between gap-3">
      <dt className="text-gray-600">{rotulo}</dt>
      <dd className="text-right font-medium text-gray-900">{valor}</dd>
    </div>
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
        name="acao-correcao"
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
      className={`flex gap-3 rounded-lg border p-4 text-sm ${
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
