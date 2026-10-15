import { useState } from 'react';
import { Camera, MapPin, RefreshCw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useConferenciasFaciais, useSelfieVerificacao } from '@/features/biometria/api/queries';
import { QuadroFoto } from '@/features/biometria/components/QuadroFoto';
import {
  dataHoraConferencia,
  formatarNota,
  linkMapaConferencia,
  rotuloMeio,
  rotuloMomento,
} from '@/features/biometria/lib/rotulos';
import type { VerificacaoFacial } from '@/features/biometria/types';

// Conferência que ficou na fila do tablet (sem rede) chega depois; acima disso, mostra a hora em
// que chegou também, para a central não estranhar o registro "atrasado".
const ATRASO_RELEVANTE_MS = 5 * 60_000;

function chegouAtrasada(v: VerificacaoFacial): boolean {
  return new Date(v.recebidaEm).getTime() - new Date(v.capturadaEm).getTime() > ATRASO_RELEVANTE_MS;
}

function ModalSelfie({ conferencia, aoFechar }: { conferencia: VerificacaoFacial; aoFechar: () => void }) {
  const selfie = useSelfieVerificacao(conferencia.id, true);
  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Selfie da conferência"
      descricao={`${conferencia.motoristaNome ?? 'Motorista não identificado'} · veículo ${
        conferencia.veiculoPlaca
      } · ${dataHoraConferencia(conferencia.capturadaEm)}`}
    >
      <QuadroFoto
        carregando={selfie.isLoading}
        erro={selfie.error}
        base64={selfie.data}
        textoSemFoto="Selfie apagada (retenção de 90 dias)."
        alt="Selfie tirada pelo tablet na conferência"
      />
      {selfie.data ? (
        <p className="mt-3 text-xs text-gray-500">
          Foto tirada pelo tablet no momento da conferência. Fica guardada por 90 dias.
        </p>
      ) : null}
    </Modal>
  );
}

/**
 * Conferências do motorista no tablet do veículo (ADR-0073): cada entrada, cada conferência antes
 * de um translado e o primeiro acesso, aprovadas ou não. Serve à ficha do motorista e ao veículo —
 * `ocultar` tira a coluna que já é a própria tela. Exige Rastreamento/Consulta (quem chama confere).
 */
export function ConferenciasFaciais({
  veiculoId,
  motoristaId,
  ocultar,
  vazio = 'Nenhuma conferência nos últimos 7 dias.',
}: {
  veiculoId?: string;
  motoristaId?: string;
  ocultar?: 'veiculo' | 'motorista';
  vazio?: string;
}) {
  const lista = useConferenciasFaciais({ veiculoId, motoristaId });
  const [vendoSelfie, setVendoSelfie] = useState<VerificacaoFacial | null>(null);

  const colunas: Coluna<VerificacaoFacial>[] = [
    {
      chave: 'quando',
      cabecalho: 'Quando',
      ordenar: (v) => v.capturadaEm,
      render: (v) => (
        <div className="whitespace-nowrap">
          <div>{dataHoraConferencia(v.capturadaEm)}</div>
          {chegouAtrasada(v) ? (
            <div className="text-xs text-gray-500" title="O tablet estava sem rede e mandou depois.">
              chegou {dataHoraConferencia(v.recebidaEm)}
            </div>
          ) : null}
        </div>
      ),
    },
    ...(ocultar === 'motorista'
      ? []
      : [
          {
            chave: 'motorista',
            cabecalho: 'Motorista',
            ordenar: (v: VerificacaoFacial) => v.motoristaNome,
            render: (v: VerificacaoFacial) =>
              v.motoristaId && v.motoristaNome ? (
                <Link to={`/app/motoristas/${v.motoristaId}`} className="font-medium text-red-700 hover:underline">
                  {v.motoristaNome}
                </Link>
              ) : (
                <span className="italic text-gray-500">Não identificado</span>
              ),
          },
        ]),
    ...(ocultar === 'veiculo'
      ? []
      : [
          {
            chave: 'veiculo',
            cabecalho: 'Veículo',
            ordenar: (v: VerificacaoFacial) => v.veiculoPlaca,
            render: (v: VerificacaoFacial) => (
              <Link to={`/app/veiculos/${v.veiculoId}`} className="font-medium text-red-700 hover:underline">
                {v.veiculoPlaca}
              </Link>
            ),
          },
        ]),
    { chave: 'momento', cabecalho: 'Momento', render: (v) => rotuloMomento(v.momento) },
    {
      chave: 'meio',
      cabecalho: 'Como',
      render: (v) => (
        <div>
          <div>{rotuloMeio(v.meio)}</div>
          {v.meio === 'CodigoCentral' && v.liberadaPorNome ? (
            <div className="text-xs text-gray-500">liberado por {v.liberadaPorNome}</div>
          ) : null}
        </div>
      ),
    },
    {
      chave: 'resultado',
      cabecalho: 'Resultado',
      ordenar: (v) => (v.aprovada ? 1 : 0),
      render: (v) => (
        <div>
          <span className={`badge ${v.aprovada ? 'badge-success' : 'badge-error'}`}>
            {v.aprovada ? 'Aprovada' : 'Recusada'}
          </span>
          {v.nota != null ? (
            <div className="mt-0.5 whitespace-nowrap text-xs text-gray-500">
              nota {formatarNota(v.nota)}
              {v.limiar != null ? ` · mínimo ${formatarNota(v.limiar)}` : ''}
            </div>
          ) : null}
          {v.meio === 'Rosto' && !v.provaDeVida ? (
            <div className="text-xs text-amber-700">prova de vida não concluída</div>
          ) : null}
        </div>
      ),
    },
    {
      chave: 'posicao',
      cabecalho: 'Onde',
      render: (v) => {
        const mapa = linkMapaConferencia(v);
        return mapa ? (
          <a
            href={mapa}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1 whitespace-nowrap text-primary-700 hover:underline"
          >
            <MapPin className="h-3.5 w-3.5" /> Ver no mapa
          </a>
        ) : (
          <span className="text-xs text-gray-400">Sem posição</span>
        );
      },
    },
    {
      chave: 'selfie',
      cabecalho: 'Selfie',
      render: (v) =>
        v.temSelfie ? (
          <button
            type="button"
            onClick={() => setVendoSelfie(v)}
            className="inline-flex items-center gap-1 whitespace-nowrap text-primary-700 hover:underline"
          >
            <Camera className="h-3.5 w-3.5" /> Ver selfie
          </button>
        ) : (
          <span className="text-xs text-gray-400">Sem selfie</span>
        ),
    },
  ];

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold text-gray-900">Conferências no tablet (últimos 7 dias)</h2>
          <p className="text-xs text-gray-500">
            Cada entrada no tablet do veículo, cada conferência antes de um translado e o primeiro
            acesso, aprovadas ou não.
          </p>
        </div>
        <button
          type="button"
          onClick={() => lista.refetch()}
          className="inline-flex items-center gap-1.5 rounded-md border border-gray-200 px-2.5 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
        >
          <RefreshCw className={`h-4 w-4 ${lista.isFetching ? 'animate-spin' : ''}`} />
          Atualizar
        </button>
      </div>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(v) => v.id}
        carregando={lista.isLoading}
        vazio={lista.isError ? 'Não foi possível carregar as conferências.' : vazio}
      />

      {vendoSelfie ? <ModalSelfie conferencia={vendoSelfie} aoFechar={() => setVendoSelfie(null)} /> : null}
    </section>
  );
}
