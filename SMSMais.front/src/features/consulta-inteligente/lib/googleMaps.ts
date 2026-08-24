import { http } from '@/shared/api/httpClient';

// Carregador único do Google Maps JS. A chave vem do backend (reusa a config Google Maps do
// TFD, restrita por referrer ao domínio do painel). Carrega o script uma vez, com a lib de
// visualização (necessária para o heatmap). Retorna `google.maps` (tipado como unknown/any:
// não adicionamos @types/google.maps).

let promessa: Promise<GoogleMaps> | null = null;

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type GoogleMaps = any;

async function obterChave(): Promise<string | null> {
  const { data } = await http.get<{ apiKey: string | null }>('/ia/chat/maps-key');
  return data.apiKey ?? null;
}

export function carregarGoogleMaps(): Promise<GoogleMaps> {
  if (promessa) return promessa;
  promessa = (async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = window as any;
    if (w.google?.maps) return w.google.maps;

    const chave = await obterChave();
    if (!chave) {
      throw new Error('Chave do Google Maps não configurada (Transporte de Pacientes → Google Maps).');
    }

    await new Promise<void>((resolve, reject) => {
      const existente = document.getElementById('google-maps-js');
      if (existente) {
        existente.addEventListener('load', () => resolve());
        existente.addEventListener('error', () => reject(new Error('Falha ao carregar o Google Maps.')));
        return;
      }
      const s = document.createElement('script');
      s.id = 'google-maps-js';
      s.src =
        `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(chave)}` +
        '&libraries=visualization&language=pt-BR&region=BR';
      s.async = true;
      s.onload = () => resolve();
      s.onerror = () => reject(new Error('Falha ao carregar o Google Maps (verifique a chave e o domínio).'));
      document.head.appendChild(s);
    });

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return (window as any).google.maps;
  })();
  return promessa;
}
