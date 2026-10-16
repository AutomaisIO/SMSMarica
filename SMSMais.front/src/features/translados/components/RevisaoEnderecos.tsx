import { useEffect, useState } from 'react';
import { CircleMarker, MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { Loader2, MapPin } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { useEnderecosParaRevisar, useFixarPontoDoEndereco } from '@/features/translados/api/queries';
import type { EnderecoParaRevisar } from '@/features/translados/types';

const CENTRO_MARICA: [number, number] = [-22.9197, -42.8186];

/** "-22.91, -42.81" (colado do Google Maps, por exemplo) → [lat, lng]. */
function lerPonto(texto: string): [number, number] | null {
  const m = texto.trim().match(/^(-?\d{1,2}(?:[.,]\d+)?)\s*[,; ]\s*(-?\d{1,3}(?:[.,]\d+)?)$/);
  if (!m) return null;
  const lat = Number(m[1].replace(',', '.'));
  const lng = Number(m[2].replace(',', '.'));
  if (Math.abs(lat) > 90 || Math.abs(lng) > 180 || (lat === 0 && lng === 0)) return null;
  return [lat, lng];
}

function CliqueMarca({ aoMarcar }: { aoMarcar: (p: [number, number]) => void }) {
  useMapEvents({ click: (e) => aoMarcar([e.latlng.lat, e.latlng.lng]) });
  return null;
}

function Centralizar({ ponto }: { ponto: [number, number] | null }) {
  const map = useMap();
  useEffect(() => {
    if (ponto) map.panTo(ponto);
  }, [map, ponto]);
  return null;
}

/**
 * Endereços de paciente que o Google não localizou (ou localizou só pela rua/bairro). Enquanto
 * ficam aqui, o paciente não entra na geração do translado ("Sem geolocalização"). Quem conhece a
 * cidade marca o ponto no mapa (ou cola "latitude, longitude") e salva; depois, pré-visualize de novo.
 */
export function RevisaoEnderecos() {
  const podeFixar = usePermissao('Unidades', 'Edicao');
  const lista = useEnderecosParaRevisar(podeFixar);
  const fixar = useFixarPontoDoEndereco();
  const [aberto, setAberto] = useState<EnderecoParaRevisar | null>(null);
  const [ponto, setPonto] = useState<[number, number] | null>(null);
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  if (!podeFixar) {
    return (
      <p className="mt-3 text-xs text-gray-600">
        Para marcar no mapa os endereços que o Google não achou é preciso a permissão de edição de Unidades.
        Peça a quem cuida das Unidades de Atendimento.
      </p>
    );
  }

  function abrir(e: EnderecoParaRevisar) {
    setAberto(e);
    setErro(null);
    const ja = e.latitude !== 0 || e.longitude !== 0 ? ([e.latitude, e.longitude] as [number, number]) : null;
    setPonto(ja);
    setTexto(ja ? `${ja[0].toFixed(6)}, ${ja[1].toFixed(6)}` : '');
  }

  function marcar(p: [number, number]) {
    setPonto(p);
    setTexto(`${p[0].toFixed(6)}, ${p[1].toFixed(6)}`);
  }

  async function salvar() {
    if (!aberto || !ponto) return;
    setErro(null);
    try {
      await fixar.mutateAsync({ id: aberto.id, latitude: ponto[0], longitude: ponto[1] });
      setAberto(null);
      setPonto(null);
      setTexto('');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const itens = lista.data ?? [];
  return (
    <div className="mt-4 rounded-lg border border-amber-200 bg-white p-4">
      <h4 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
        <MapPin className="h-4 w-4 text-amber-600" /> Endereços para marcar no mapa
      </h4>
      {lista.isLoading ? (
        <p className="mt-2 flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
        </p>
      ) : itens.length === 0 ? (
        <p className="mt-2 text-sm text-gray-600">
          Nenhum endereço esperando ponto. Se acabou de marcar, clique em <strong>Pré-visualizar</strong> de novo.
        </p>
      ) : (
        <ul className="mt-2 divide-y divide-gray-100">
          {itens.map((e) => (
            <li key={e.id} className="py-2">
              <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
                <span className="text-gray-800">{e.enderecoNormalizado}</span>
                {aberto?.id === e.id ? null : (
                  <Button variante="outline" tamanho="sm" onClick={() => abrir(e)}>
                    Marcar no mapa
                  </Button>
                )}
              </div>
              {aberto?.id === e.id ? (
                <div className="mt-3 space-y-3">
                  <div className="overflow-hidden rounded-md border border-gray-200" style={{ height: 320 }}>
                    <MapContainer center={ponto ?? CENTRO_MARICA} zoom={ponto ? 17 : 12} style={{ height: '100%', width: '100%' }}>
                      <TileLayer
                        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                      />
                      <CliqueMarca aoMarcar={marcar} />
                      <Centralizar ponto={ponto} />
                      {ponto ? <CircleMarker center={ponto} radius={9} pathOptions={{ weight: 3 }} /> : null}
                    </MapContainer>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <Input
                      aria-label="Latitude e longitude"
                      placeholder="Clique no mapa ou cole: -22.9197, -42.8186"
                      value={texto}
                      onChange={(ev) => {
                        setTexto(ev.target.value);
                        setPonto(lerPonto(ev.target.value));
                      }}
                      className="max-w-xs"
                    />
                    <Button onClick={salvar} disabled={!ponto || fixar.isPending}>
                      {fixar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                      Salvar ponto
                    </Button>
                    <Button variante="ghost" onClick={() => setAberto(null)}>
                      Fechar
                    </Button>
                  </div>
                  {texto && !ponto ? (
                    <p className="text-xs text-red-700">Use o formato “latitude, longitude”, como -22.9197, -42.8186.</p>
                  ) : null}
                  {erro ? <p className="text-xs text-red-700">{erro}</p> : null}
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
