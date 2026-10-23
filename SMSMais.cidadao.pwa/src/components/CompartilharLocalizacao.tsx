import { useState } from 'react';
import { MapPin } from 'lucide-react';
import { useLocalizacao } from '@/store/localizacao';
import { Card, PrimaryButton } from '@/components/ui';

/**
 * O convite para compartilhar a localização com o motorista no dia da viagem (ADR-0079). Aparece só
 * enquanto o paciente espera um embarque (a ida, a partir de 2 h antes da busca, ou o resgate) e ainda
 * não compartilha. Antes do pedido de permissão do navegador, explica quem vê, quando e como parar.
 */
export function CompartilharLocalizacao({ className = 'mb-6' }: { className?: string }) {
  const espera = useLocalizacao((s) => s.espera);
  const iniciando = useLocalizacao((s) => s.iniciando);
  const erro = useLocalizacao((s) => s.erro);
  const compartilhar = useLocalizacao((s) => s.compartilhar);
  const [explicando, setExplicando] = useState(false);
  const [adiado, setAdiado] = useState(false);

  if (!espera || espera.compartilhando || adiado) return null;

  const quando =
    espera.trecho === 'Resgate'
      ? `O carro vai buscar você em ${espera.unidade}.`
      : espera.horaBusca
        ? `O carro vai buscar você hoje. Busca prevista às ${espera.horaBusca.slice(0, 5)}.`
        : 'O carro vai buscar você hoje.';

  return (
    <section className={className}>
      <Card className="border-2 border-lagoa/40 p-4">
        <p className="flex items-center gap-2 font-display text-lg font-semibold text-tinta">
          <MapPin className="h-5 w-5 text-marica" /> Ajude o motorista a achar você
        </p>
        <p className="mt-1 text-sm text-tinta-mute">{quando}</p>

        {!explicando ? (
          <>
            <p className="mt-3 text-sm text-tinta">
              Compartilhe a sua localização: o motorista vê onde você está e chega mais fácil.
            </p>
            <div className="mt-3">
              <PrimaryButton onClick={() => setExplicando(true)}>Compartilhar minha localização</PrimaryButton>
            </div>
          </>
        ) : (
          <>
            <ul className="mt-3 list-disc space-y-1 pl-5 text-sm text-tinta">
              <li>Só o motorista do carro que vai buscar você vê onde você está.</li>
              <li>Só enquanto você espera o embarque, e só com o app aberto.</li>
              <li>Para sozinho quando você embarcar. Você pode parar quando quiser.</li>
              <li>Guardamos só a última posição, e ela é apagada no embarque.</li>
            </ul>
            <p className="mt-2 text-xs text-tinta-mute">
              Depois de tocar em Compartilhar, o navegador pede para usar a sua localização.{' '}
              <a className="underline" href="/privacidade/" target="_blank" rel="noopener">
                Política de Privacidade
              </a>
            </p>
            {erro ? (
              <p role="alert" className="mt-2 text-sm text-red-700">
                {erro}
              </p>
            ) : null}
            <div className="mt-3 space-y-2">
              <PrimaryButton carregando={iniciando} onClick={() => void compartilhar()}>
                Compartilhar
              </PrimaryButton>
              <button
                type="button"
                onClick={() => setAdiado(true)}
                className="w-full rounded-2xl border border-areia bg-white px-4 py-3 text-base font-semibold text-tinta active:bg-papel"
              >
                Agora não
              </button>
            </div>
          </>
        )}
      </Card>
    </section>
  );
}
