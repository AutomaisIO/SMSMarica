import { useEffect, useState } from 'react';
import { MapPin } from 'lucide-react';
import { useLocalizacao } from '@/store/localizacao';

function haQuanto(ms: number): string {
  const min = Math.floor((Date.now() - ms) / 60_000);
  return min < 1 ? 'agora há pouco' : `há ${min} min`;
}

/**
 * A faixa fixa enquanto o paciente compartilha a localização com o motorista (ADR-0079), em todas as telas
 * logadas, logo abaixo da barra de cima: o estado do GPS, o "Parar" e o lembrete de deixar o app aberto.
 */
export function FaixaLocalizacao() {
  const espera = useLocalizacao((s) => s.espera);
  const situacao = useLocalizacao((s) => s.situacao);
  const ultimoEnvio = useLocalizacao((s) => s.ultimoEnvio);
  const parar = useLocalizacao((s) => s.parar);
  // Reescreve o "há X min" sem esperar a próxima posição.
  const [, setTique] = useState(0);
  useEffect(() => {
    const t = window.setInterval(() => setTique((n) => n + 1), 15_000);
    return () => window.clearInterval(t);
  }, []);

  if (!espera?.compartilhando) return null;

  const detalhe =
    situacao === 'semPermissao'
      ? 'O navegador não deixou usar a localização. Libere a localização deste site nas configurações do navegador.'
      : situacao === 'semGps'
        ? 'Este navegador não informa a localização.'
        : situacao === 'pedindoPermissao'
          ? 'Permita o acesso à localização no aviso do navegador.'
          : ultimoEnvio
            ? `Enviada ${haQuanto(ultimoEnvio)}.`
            : 'Procurando a sua posição…';

  return (
    <div
      role="status"
      className="sticky top-[calc(env(safe-area-inset-top)+4.25rem)] z-20 border-b border-lagoa/30 bg-lagoa-claro px-4 py-2 text-tinta"
    >
      <div className="flex items-center gap-2">
        <MapPin className="h-4 w-4 shrink-0 text-lagoa" />
        <p className="flex-1 text-sm font-semibold">Compartilhando sua localização com o motorista</p>
        <button
          type="button"
          onClick={() => void parar()}
          className="rounded-xl px-3 py-1.5 text-sm font-semibold text-marica transition active:bg-marica/10"
        >
          Parar
        </button>
      </div>
      <p className="mt-0.5 text-xs text-tinta-mute">
        {detalhe} Deixe o app aberto: fechado, ele não envia.
      </p>
    </div>
  );
}
