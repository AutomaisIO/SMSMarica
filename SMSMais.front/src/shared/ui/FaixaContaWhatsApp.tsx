import { useQuery } from '@tanstack/react-query';
import { AlertOctagon } from 'lucide-react';
import { http } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';

interface FaixaContaWhatsApp {
  mostrar: boolean;
  desde: string | null;
  codigo: string | null;
  motivo: string | null;
  origem: string | null;
  ultimaConfirmacaoEm: string | null;
  proximaSondaEm: string | null;
  enviosSegurados: number;
  ultimaConsultaSaudeEm: string | null;
}

const ORIGEM: Record<string, string> = {
  envio: 'recusa da Meta no envio',
  webhook: 'recusa da Meta no recibo',
  'saude-meta': 'consulta de saúde da conta na Meta',
};

const hora = (iso: string | null) => formatarInstante(iso, { hour: '2-digit', minute: '2-digit' });

/**
 * Faixa vermelha quando a CONTA do WhatsApp parou (pagamento, bloqueio, token): nada sai para
 * paciente nenhum até alguém resolver na Meta. Só aparece para quem resolve — o servidor decide
 * (ContaWhatsApp:FaixaParaLogins) e devolve `mostrar=false` aos demais.
 *
 * Existe porque em 19/10/2026 a conta ficou duas horas sem pagamento e o único aviso de erro da
 * plataforma também saía pelo WhatsApp — morreu no mesmo erro. Some sozinha quando a conta volta.
 */
export function FaixaContaWhatsApp() {
  const { data } = useQuery({
    queryKey: ['sistema', 'whatsapp-conta', 'faixa'],
    queryFn: async () => (await http.get<FaixaContaWhatsApp>('/sistema/whatsapp-conta/faixa')).data,
    refetchInterval: 60_000,
    refetchIntervalInBackground: true,
    retry: false,
  });

  if (!data?.mostrar) return null;

  return (
    <div role="alert" className="border-b border-red-700 bg-red-600 px-4 py-2 text-sm text-white shadow-md">
      <div className="mx-auto flex max-w-7xl items-start gap-2">
        <AlertOctagon className="mt-0.5 h-5 w-5 shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="font-semibold">
            WhatsApp PARADO desde {hora(data.desde)} — nenhuma mensagem está saindo para os pacientes.
          </p>
          <p className="mt-0.5">{data.motivo}</p>
          <p className="mt-0.5 text-xs text-red-100">
            Código {data.codigo} · detectado por {ORIGEM[data.origem ?? ''] ?? data.origem}
            {data.enviosSegurados > 0 ? ` · ${data.enviosSegurados} envio(s) segurado(s)` : ''}
            {' · '}os envios estão guardados e saem sozinhos quando a conta voltar (nova tentativa às{' '}
            {hora(data.proximaSondaEm)}
            {data.ultimaConsultaSaudeEm ? `; última consulta à Meta às ${hora(data.ultimaConsultaSaudeEm)}` : ''}).
          </p>
        </div>
      </div>
    </div>
  );
}
