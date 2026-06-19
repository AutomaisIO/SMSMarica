import { useEffect, useMemo, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import L, { type LatLngBoundsExpression } from 'leaflet';
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { Bus, MapPin, RefreshCw, Users } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { listarFrota } from '@/features/rastreamento/api/rastreamentoApi';
import type { FrotaVeiculo } from '@/features/rastreamento/types';

const CENTRO_MARICA: [number, number] = [-22.9197, -42.8186];
const INTERVALO_MS = 15_000; // GPS chega ~1/min; 15s mantém o mapa fresco sem custo

const STATUS: Record<number, { rotulo: string; cor: string }> = {
  1: { rotulo: 'Planejada', cor: '#6b7280' },
  2: { rotulo: 'Em andamento', cor: '#16a34a' },
  3: { rotulo: 'Concluída', cor: '#2563eb' },
  4: { rotulo: 'Cancelada', cor: '#dc2626' },
};

function statusDe(s: number) {
  return STATUS[s] ?? { rotulo: `Status ${s}`, cor: '#6b7280' };
}

function dataLocalHoje(): string {
  const d = new Date();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

function haQuantoTempo(iso: string | null): string {
  if (!iso) return 'sem posição';
  const s = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
  if (s < 60) return `há ${s}s`;
  const m = Math.round(s / 60);
  if (m < 60) return `há ${m} min`;
  return `há ${Math.round(m / 60)} h`;
}

function estaParado(iso: string | null): boolean {
  if (!iso) return true;
  return Date.now() - new Date(iso).getTime() > 5 * 60_000; // sem reportar há +5min
}

const ICONE_BUS_SVG =
  '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="white" ' +
  'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
  '<path d="M8 6v6"/><path d="M15 6v6"/><path d="M2 12h19.6"/>' +
  '<path d="M18 18h3s.5-1.7.8-2.8c.1-.4.2-.8.2-1.2 0-.4-.1-.8-.2-1.2l-1.4-5C20.6 6.8 19.9 6 19 6H5a2 2 0 0 0-1.9 1.5L1.7 12.7c-.1.4-.2.8-.2 1.2 0 .4.1.8.2 1.2C2 16.3 2.5 18 2.5 18H5"/>' +
  '<circle cx="7" cy="18" r="2"/><path d="M9 18h5"/><circle cx="16" cy="18" r="2"/></svg>';

function iconeVan(cor: string, parado: boolean): L.DivIcon {
  return L.divIcon({
    className: 'frota-marker',
    html:
      `<div style="background:${cor};width:34px;height:34px;border-radius:50%;` +
      `border:2px solid white;box-shadow:0 1px 4px rgba(0,0,0,.45);` +
      `display:flex;align-items:center;justify-content:center;opacity:${parado ? 0.5 : 1};">` +
      `${ICONE_BUS_SVG}</div>`,
    iconSize: [34, 34],
    iconAnchor: [17, 17],
    popupAnchor: [0, -18],
  });
}

/** Centraliza o mapa na frota apenas na 1ª carga com posições (refresh não reposiciona). */
function AjustarBounds({ pontos }: { pontos: Array<[number, number]> }) {
  const map = useMap();
  const jaAjustou = useRef(false);
  useEffect(() => {
    map.invalidateSize();
    if (jaAjustou.current || pontos.length === 0) return;
    if (pontos.length === 1) {
      map.setView(pontos[0], 14);
    } else {
      map.fitBounds(pontos as LatLngBoundsExpression, { padding: [48, 48], maxZoom: 15 });
    }
    jaAjustou.current = true;
  }, [pontos, map]);
  return null;
}

export function MapaFrotaPage() {
  const hoje = useMemo(() => dataLocalHoje(), []);
  const frota = useQuery({
    queryKey: ['rastreamento', 'frota', hoje],
    queryFn: () => listarFrota(hoje),
    refetchInterval: INTERVALO_MS,
    refetchOnWindowFocus: true,
  });

  const veiculos = frota.data ?? [];
  const comPosicao = veiculos.filter(
    (v): v is FrotaVeiculo & { latitude: number; longitude: number } =>
      v.latitude != null && v.longitude != null,
  );
  const pontos = comPosicao.map((v) => [v.latitude, v.longitude] as [number, number]);

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Mapa da frota</h1>
          <p className="mt-1 text-sm text-gray-600">
            Posição dos veículos em tempo real — atualiza a cada 15&nbsp;segundos.
          </p>
        </div>
        <div className="flex items-center gap-3 text-sm text-gray-500">
          <span>
            {veiculos.length} veículo(s) · {comPosicao.length} com sinal
          </span>
          <button
            type="button"
            onClick={() => frota.refetch()}
            className="inline-flex items-center gap-1.5 rounded-md border border-gray-200 px-2.5 py-1.5 text-gray-700 hover:bg-gray-50"
          >
            <RefreshCw className={`h-4 w-4 ${frota.isFetching ? 'animate-spin' : ''}`} />
            Atualizar
          </button>
        </div>
      </header>

      {frota.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(frota.error)}
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_320px]">
        <div className="overflow-hidden rounded-lg border border-gray-200" style={{ height: 600 }}>
          <MapContainer
            center={CENTRO_MARICA}
            zoom={12}
            scrollWheelZoom
            style={{ height: '100%', width: '100%' }}
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <AjustarBounds pontos={pontos} />
            {comPosicao.map((v) => {
              const st = statusDe(v.status);
              return (
                <Marker
                  key={v.rotaId}
                  position={[v.latitude, v.longitude]}
                  icon={iconeVan(st.cor, estaParado(v.atualizadoEm))}
                >
                  <Popup>
                    <div className="space-y-1 text-sm">
                      <div className="font-semibold text-gray-900">{v.motoristaNome}</div>
                      <div className="text-gray-700">
                        {v.veiculoPlaca}
                        {v.veiculoModelo ? ` · ${v.veiculoModelo}` : ''}
                      </div>
                      <div className="flex items-center gap-1.5">
                        <span
                          className="inline-block h-2.5 w-2.5 rounded-full"
                          style={{ background: st.cor }}
                        />
                        <span className="text-gray-700">{st.rotulo}</span>
                      </div>
                      <div className="text-gray-600">{v.qtdPacientes} paciente(s)</div>
                      <div className="text-xs text-gray-500">
                        Atualizado {haQuantoTempo(v.atualizadoEm)}
                      </div>
                    </div>
                  </Popup>
                </Marker>
              );
            })}
          </MapContainer>
        </div>

        <aside className="space-y-2">
          {veiculos.length === 0 && !frota.isLoading ? (
            <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-6 text-center text-sm text-gray-500">
              Nenhuma rota para hoje.
            </div>
          ) : null}
          {veiculos.map((v) => {
            const st = statusDe(v.status);
            const semSinal = v.latitude == null || v.longitude == null;
            return (
              <div
                key={v.rotaId}
                className="rounded-lg border border-gray-200 bg-white p-3 shadow-sm"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span
                      className="flex h-8 w-8 items-center justify-center rounded-full text-white"
                      style={{ background: st.cor, opacity: estaParado(v.atualizadoEm) ? 0.5 : 1 }}
                    >
                      <Bus className="h-4 w-4" />
                    </span>
                    <div>
                      <div className="text-sm font-semibold text-gray-900">{v.motoristaNome}</div>
                      <div className="text-xs text-gray-500">
                        {v.veiculoPlaca}
                        {v.veiculoModelo ? ` · ${v.veiculoModelo}` : ''}
                      </div>
                    </div>
                  </div>
                  <span
                    className="rounded-full px-2 py-0.5 text-[11px] font-medium text-white"
                    style={{ background: st.cor }}
                  >
                    {st.rotulo}
                  </span>
                </div>
                <div className="mt-2 flex items-center justify-between text-xs text-gray-500">
                  <span className="inline-flex items-center gap-1">
                    <Users className="h-3.5 w-3.5" />
                    {v.qtdPacientes} paciente(s)
                  </span>
                  <span className={`inline-flex items-center gap-1 ${semSinal ? 'text-amber-600' : ''}`}>
                    <MapPin className="h-3.5 w-3.5" />
                    {haQuantoTempo(v.atualizadoEm)}
                  </span>
                </div>
              </div>
            );
          })}
        </aside>
      </div>
    </div>
  );
}
