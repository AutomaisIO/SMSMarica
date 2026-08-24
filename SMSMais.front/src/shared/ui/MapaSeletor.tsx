import { useEffect, useMemo, useRef, useState } from 'react';
import L from 'leaflet';
import { MapContainer, Marker, TileLayer, useMap } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import iconUrl from 'leaflet/dist/images/marker-icon.png';
import iconRetinaUrl from 'leaflet/dist/images/marker-icon-2x.png';
import shadowUrl from 'leaflet/dist/images/marker-shadow.png';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { MapPin, Locate } from 'lucide-react';

// Bundlers (Vite/webpack) quebram os caminhos default dos ícones do
// Leaflet. Sobrescrever explicitamente com import resolve isso.
const iconePadrao = L.icon({
  iconUrl,
  iconRetinaUrl,
  shadowUrl,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});
L.Marker.prototype.options.icon = iconePadrao;

type Coordenada = { lat: number; lng: number };

type Props = {
  /** Coordenada atual. Se null, o mapa começa centralizado em Maricá-RJ. */
  valor: Coordenada | null;
  aoMudar: (c: Coordenada) => void;
  /** Endereço opcional para geocodificar via OSM Nominatim ao clicar "Localizar". */
  enderecoParaBuscar?: string | null;
  altura?: number;
  desabilitado?: boolean;
};

const CENTRO_DEFAULT: Coordenada = { lat: -22.9197, lng: -42.8186 }; // Maricá-RJ
const ZOOM_PIN = 17;

/**
 * Recentraliza imperativamente quando o `gatilho` muda. Também chama
 * invalidateSize para corrigir o caso em que o container só ganha altura
 * após o layout (default do MapContainer não recalcula sozinho — vira
 * mapa "de outro continente" no zoom out).
 */
function ControleDeCentro({
  centro,
  gatilho,
}: {
  centro: Coordenada | null;
  gatilho: number;
}) {
  const map = useMap();
  useEffect(() => {
    map.invalidateSize();
    if (!centro) return;
    map.setView([centro.lat, centro.lng], Math.max(map.getZoom(), ZOOM_PIN), {
      animate: gatilho > 0,
    });
    // gatilho na deps lista para forçar re-execução em montagem inicial
    // e em pedidos explícitos (clique no mapa, "Localizar pelo endereço").
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gatilho]);
  return null;
}

export function MapaSeletor({
  valor,
  aoMudar,
  enderecoParaBuscar,
  altura = 320,
  desabilitado,
}: Props) {
  const [buscando, setBuscando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  // Cada incremento dispara o ControleDeCentro a recentralizar. Drag NÃO
  // incrementa (preserva o que o usuário está vendo após arrastar o pin).
  const [gatilhoCentralizar, setGatilhoCentralizar] = useState(0);
  const markerRef = useRef<L.Marker | null>(null);

  const centroInicial = valor ?? CENTRO_DEFAULT;
  const zoomInicial = valor ? ZOOM_PIN : 13;

  const eventos = useMemo(
    () => ({
      dragend() {
        const m = markerRef.current;
        if (!m) return;
        const pos = m.getLatLng();
        // sem incrementar gatilho — o mapa fica onde o usuário largou.
        aoMudar({ lat: pos.lat, lng: pos.lng });
      },
    }),
    [aoMudar],
  );

  async function localizarEndereco() {
    if (!enderecoParaBuscar?.trim()) {
      setErro('Preencha o endereço primeiro para localizar no mapa.');
      return;
    }
    setBuscando(true);
    setErro(null);
    try {
      const url = new URL('https://nominatim.openstreetmap.org/search');
      url.searchParams.set('q', enderecoParaBuscar);
      url.searchParams.set('format', 'json');
      url.searchParams.set('limit', '1');
      url.searchParams.set('countrycodes', 'br');
      const resp = await fetch(url.toString(), {
        headers: { Accept: 'application/json' },
      });
      if (!resp.ok) throw new Error('Falha ao consultar o mapa.');
      const dados = (await resp.json()) as Array<{ lat: string; lon: string }>;
      if (dados.length === 0) {
        setErro('Endereço não encontrado no mapa. Posicione o pin manualmente.');
        return;
      }
      const c: Coordenada = { lat: parseFloat(dados[0].lat), lng: parseFloat(dados[0].lon) };
      aoMudar(c);
      setGatilhoCentralizar((g) => g + 1);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setBuscando(false);
    }
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-3">
        <p className="text-xs text-gray-500">
          {valor
            ? `GPS: ${valor.lat.toFixed(6)}, ${valor.lng.toFixed(6)}${
                desabilitado ? '' : ' — arraste o pin para ajustar.'
              }`
            : 'Sem coordenada definida. Use "Localizar" ou clique no mapa para posicionar o pin.'}
        </p>
        {desabilitado ? null : (
          <Button
            type="button"
            variante="outline"
            tamanho="sm"
            onClick={localizarEndereco}
            disabled={buscando}
          >
            <Locate className="h-4 w-4" />
            {buscando ? 'Buscando…' : 'Localizar pelo endereço'}
          </Button>
        )}
      </div>

      <div
        className="overflow-hidden rounded-md border border-gray-200"
        style={{ height: altura }}
      >
        <MapContainer
          center={[centroInicial.lat, centroInicial.lng]}
          zoom={zoomInicial}
          scrollWheelZoom
          style={{ height: '100%', width: '100%' }}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <ControleDeCentro centro={valor} gatilho={gatilhoCentralizar} />
          <ClickHandler
            ativo={!desabilitado}
            aoClicar={(c) => {
              aoMudar(c);
              setGatilhoCentralizar((g) => g + 1);
            }}
          />
          {valor ? (
            <Marker
              position={[valor.lat, valor.lng]}
              draggable={!desabilitado}
              eventHandlers={eventos}
              ref={(m) => {
                markerRef.current = m;
              }}
            />
          ) : null}
        </MapContainer>
      </div>

      {erro ? (
        <p className="flex items-start gap-1 text-xs text-red-700">
          <MapPin className="mt-0.5 h-3 w-3 shrink-0" />
          {erro}
        </p>
      ) : null}
    </div>
  );
}

function ClickHandler({
  ativo,
  aoClicar,
}: {
  ativo: boolean;
  aoClicar: (c: Coordenada) => void;
}) {
  const map = useMap();
  useEffect(() => {
    if (!ativo) return;
    const handler = (e: L.LeafletMouseEvent) => {
      aoClicar({ lat: e.latlng.lat, lng: e.latlng.lng });
    };
    map.on('click', handler);
    return () => {
      map.off('click', handler);
    };
  }, [ativo, aoClicar, map]);
  return null;
}
