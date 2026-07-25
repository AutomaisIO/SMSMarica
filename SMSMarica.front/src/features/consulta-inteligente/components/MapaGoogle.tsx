import { useEffect, useRef, useState } from 'react';
import { carregarGoogleMaps } from '../lib/googleMaps';
import type { VizSpec } from '../types';

// Centro padrão: Maricá/RJ (quando não há pontos para enquadrar).
const CENTRO_MARICA = { lat: -22.9192, lng: -42.8186 };

/** Renderiza um mapa Google a partir de um VizSpec de mapa (pontos, calor ou polígono). */
export function MapaGoogle({ spec }: { spec: VizSpec }) {
  const ref = useRef<HTMLDivElement>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        const maps = await carregarGoogleMaps();
        if (cancelado || !ref.current) return;

        const map = new maps.Map(ref.current, {
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: false,
        });
        const bounds = new maps.LatLngBounds();
        let algum = false;

        const pontos = spec.pontos ?? [];
        const poligonos = spec.poligonos ?? [];

        if (spec.tipo === 'mapa_calor') {
          const data = pontos.map((p) => ({
            location: new maps.LatLng(p.lat, p.lng),
            weight: p.peso ?? p.valor ?? 1,
          }));
          // eslint-disable-next-line no-new
          new maps.visualization.HeatmapLayer({ data, map, radius: 24 });
          pontos.forEach((p) => {
            bounds.extend(new maps.LatLng(p.lat, p.lng));
            algum = true;
          });
        } else if (spec.tipo === 'mapa_poligono') {
          poligonos.forEach((pg) => {
            const path = pg.coordenadas.map(([lat, lng]) => ({ lat, lng }));
            // eslint-disable-next-line no-new
            new maps.Polygon({
              paths: path,
              map,
              strokeColor: '#C8102E',
              strokeWeight: 2,
              fillColor: '#C8102E',
              fillOpacity: 0.2,
            });
            path.forEach((c) => {
              bounds.extend(c);
              algum = true;
            });
          });
        } else {
          // mapa_pontos (default)
          pontos.forEach((p) => {
            const marker = new maps.Marker({
              position: { lat: p.lat, lng: p.lng },
              map,
              title: p.rotulo ?? undefined,
            });
            if (p.rotulo || p.valor != null) {
              const info = new maps.InfoWindow({
                content: `${p.rotulo ?? ''}${p.valor != null ? ` — ${p.valor}` : ''}`,
              });
              marker.addListener('click', () => info.open(map, marker));
            }
            bounds.extend(new maps.LatLng(p.lat, p.lng));
            algum = true;
          });
        }

        if (algum) {
          map.fitBounds(bounds);
          if (pontos.length + poligonos.length === 1) {
            maps.event.addListenerOnce(map, 'idle', () => map.setZoom(Math.min(15, map.getZoom())));
          }
        } else {
          map.setCenter(CENTRO_MARICA);
          map.setZoom(11);
        }
      } catch (e) {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar o mapa.');
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [spec]);

  if (erro) {
    return (
      <div className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800">
        Mapa indisponível: {erro}
      </div>
    );
  }
  return <div ref={ref} className="h-80 w-full overflow-hidden rounded-md border border-gray-200" />;
}
