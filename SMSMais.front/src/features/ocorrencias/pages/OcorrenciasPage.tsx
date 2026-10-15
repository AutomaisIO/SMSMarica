import { useState } from 'react';
import { CheckCircle2, Clock, MapPin, RefreshCw, UserCheck } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { Tabs } from '@/shared/ui/Tabs';
import {
  useAtenderOcorrencia,
  useEncerrarOcorrencia,
  useOcorrencias,
} from '@/features/ocorrencias/api/queries';
import { SITUACOES_OCORRENCIA, TIPOS_OCORRENCIA } from '@/features/ocorrencias/lib/rotulos';
import type { Ocorrencia } from '@/features/ocorrencias/types';

function dataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function haQuantoTempo(iso: string): string {
  const min = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60_000));
  if (min < 1) return 'agora';
  if (min < 60) return `há ${min} min`;
  const h = Math.round(min / 60);
  return h < 24 ? `há ${h} h` : `há ${Math.round(h / 24)} dia(s)`;
}

/** Link do mapa (OpenStreetMap) na posição do REPORT. */
function linkMapa(o: Ocorrencia): string | null {
  if (o.latitude == null || o.longitude == null) return null;
  return `https://www.openstreetmap.org/?mlat=${o.latitude}&mlon=${o.longitude}#map=17/${o.latitude}/${o.longitude}`;
}

function CartaoOcorrencia({
  o,
  podeAgir,
  aoEncerrar,
}: {
  o: Ocorrencia;
  podeAgir: boolean;
  aoEncerrar: (o: Ocorrencia) => void;
}) {
  const atender = useAtenderOcorrencia();
  const tipo = TIPOS_OCORRENCIA[o.tipo] ?? TIPOS_OCORRENCIA.Outro;
  const situacao = SITUACOES_OCORRENCIA[o.situacao];
  const Icone = tipo.icone;
  const mapa = linkMapa(o);
  const aberta = o.situacao === 'Aberta';
  const encerrada = o.situacao === 'Encerrada';

  return (
    <div
      className={`rounded-lg border bg-white p-4 shadow-sm ${aberta ? 'border-red-300' : 'border-gray-200'}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <span
            className={`flex h-10 w-10 items-center justify-center rounded-full text-white ${
              encerrada ? 'bg-gray-400' : 'bg-red-600'
            }`}
          >
            <Icone className="h-5 w-5" />
          </span>
          <div>
            <div className="font-semibold text-gray-900">{tipo.rotulo}</div>
            <div className="text-sm text-gray-600">
              Veículo {o.veiculoPlaca}
              {o.veiculoModelo ? ` · ${o.veiculoModelo}` : ''}
            </div>
          </div>
        </div>
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${situacao.classe}`}>
          {situacao.rotulo}
        </span>
      </div>

      {o.descricao ? (
        <p className="mt-3 whitespace-pre-line rounded-md bg-gray-50 px-3 py-2 text-sm text-gray-800">
          {o.descricao}
        </p>
      ) : null}

      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500">
        <span className="inline-flex items-center gap-1">
          <Clock className="h-3.5 w-3.5" />
          Recebida {dataHora(o.recebidaEm)} ({haQuantoTempo(o.recebidaEm)})
        </span>
        {mapa ? (
          <a
            href={mapa}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1 text-primary-700 hover:underline"
          >
            <MapPin className="h-3.5 w-3.5" />
            Ver no mapa{o.precisaoM != null ? ` (±${Math.round(o.precisaoM)} m)` : ''}
          </a>
        ) : (
          <span className="inline-flex items-center gap-1 text-amber-700">
            <MapPin className="h-3.5 w-3.5" />
            Sem posição (tablet sem GPS no momento)
          </span>
        )}
        {o.atendidaEm ? (
          <span className="inline-flex items-center gap-1">
            <UserCheck className="h-3.5 w-3.5" />
            Atendida {dataHora(o.atendidaEm)}
            {o.atendidaPorNome ? ` por ${o.atendidaPorNome}` : ''}
          </span>
        ) : null}
        {o.encerradaEm ? (
          <span className="inline-flex items-center gap-1">
            <CheckCircle2 className="h-3.5 w-3.5" />
            Encerrada {dataHora(o.encerradaEm)}
            {o.encerradaPorNome ? ` por ${o.encerradaPorNome}` : ''}
          </span>
        ) : null}
      </div>

      {o.observacaoCentral ? (
        <p className="mt-2 text-sm text-gray-700">
          <span className="font-medium">Observação da central:</span> {o.observacaoCentral}
        </p>
      ) : null}

      {podeAgir && !encerrada ? (
        <div className="mt-3 flex flex-wrap gap-2">
          {aberta ? (
            <Button onClick={() => atender.mutate(o.id)} disabled={atender.isPending}>
              {atender.isPending ? 'Registrando…' : 'Atender'}
            </Button>
          ) : null}
          <Button variante="outline" onClick={() => aoEncerrar(o)}>
            Encerrar
          </Button>
          {atender.isError ? (
            <span className="self-center text-sm text-red-700">{extrairMensagemDeErro(atender.error)}</span>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function ListaOcorrencias({
  encerradas,
  podeAgir,
  aoEncerrar,
}: {
  encerradas: boolean;
  podeAgir: boolean;
  aoEncerrar: (o: Ocorrencia) => void;
}) {
  const lista = useOcorrencias(encerradas);
  if (lista.isError) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(lista.error)}
      </div>
    );
  }
  const itens = lista.data ?? [];
  if (!lista.isLoading && itens.length === 0) {
    return (
      <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-8 text-center text-sm text-gray-500">
        {encerradas
          ? 'Nenhuma ocorrência encerrada nos últimos 30 dias.'
          : 'Nenhuma ocorrência em aberto. Quando um motorista tocar em REPORT no tablet, ela aparece aqui na hora.'}
      </div>
    );
  }
  return (
    <div className="space-y-3">
      {itens.map((o) => (
        <CartaoOcorrencia key={o.id} o={o} podeAgir={podeAgir} aoEncerrar={aoEncerrar} />
      ))}
    </div>
  );
}

/**
 * Central de ocorrências dos veículos: o que os motoristas reportaram pelo botão REPORT do
 * tablet (acidente, pane, paciente passou mal, outro). Atender/Encerrar exigem
 * Rastreamento/Edição. Chega em tempo real (aviso vermelho global) e a lista se atualiza sozinha.
 */
export function OcorrenciasPage() {
  const podeAgir = usePermissao('Rastreamento', 'Edicao');
  const abertas = useOcorrencias(false);
  const encerrar = useEncerrarOcorrencia();
  const [encerrando, setEncerrando] = useState<Ocorrencia | null>(null);
  const [observacao, setObservacao] = useState('');

  const abrirEncerrar = (o: Ocorrencia) => {
    encerrar.reset();
    setObservacao('');
    setEncerrando(o);
  };

  const confirmarEncerrar = () => {
    if (!encerrando) return;
    encerrar.mutate(
      { id: encerrando.id, observacao: observacao.trim() || null },
      { onSuccess: () => setEncerrando(null) },
    );
  };

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold text-gray-900">Ocorrências dos veículos</h1>
            <AjudaManual artigo="ocorrencias" />
          </div>
          <p className="mt-1 text-sm text-gray-600">
            Avisos que os motoristas mandam pelo botão REPORT do tablet do carro.
          </p>
        </div>
        <button
          type="button"
          onClick={() => abertas.refetch()}
          className="inline-flex items-center gap-1.5 rounded-md border border-gray-200 px-2.5 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
        >
          <RefreshCw className={`h-4 w-4 ${abertas.isFetching ? 'animate-spin' : ''}`} />
          Atualizar
        </button>
      </header>

      <Tabs
        abas={[
          {
            id: 'abertas',
            rotulo: 'Em aberto',
            badge: abertas.data?.length || undefined,
            conteudo: <ListaOcorrencias encerradas={false} podeAgir={podeAgir} aoEncerrar={abrirEncerrar} />,
          },
          {
            id: 'encerradas',
            rotulo: 'Encerradas (30 dias)',
            conteudo: <ListaOcorrencias encerradas podeAgir={podeAgir} aoEncerrar={abrirEncerrar} />,
          },
        ]}
      />

      <Modal
        aberto={encerrando != null}
        aoFechar={() => setEncerrando(null)}
        titulo="Encerrar ocorrência"
        descricao={
          encerrando
            ? `${TIPOS_OCORRENCIA[encerrando.tipo]?.rotulo ?? encerrando.tipo} — veículo ${encerrando.veiculoPlaca}`
            : undefined
        }
      >
        <div className="space-y-3">
          <label className="block text-sm font-medium text-gray-700" htmlFor="observacao-central">
            O que foi feito (opcional)
          </label>
          <textarea
            id="observacao-central"
            value={observacao}
            onChange={(e) => setObservacao(e.target.value)}
            maxLength={1000}
            rows={3}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm"
            placeholder="Ex.: guincho acionado, paciente levado à UPA, motorista seguiu viagem."
          />
          {encerrar.isError ? (
            <div className="text-sm text-red-700">{extrairMensagemDeErro(encerrar.error)}</div>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button variante="ghost" onClick={() => setEncerrando(null)}>
              Cancelar
            </Button>
            <Button onClick={confirmarEncerrar} disabled={encerrar.isPending}>
              {encerrar.isPending ? 'Encerrando…' : 'Encerrar'}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
