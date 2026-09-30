import { CalendarHeart, Clock, MapPin, Users } from 'lucide-react';
import { api, type ViagemTransporte } from '@/lib/api';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';
import { MeusAcompanhantes } from '@/components/MeusAcompanhantes';

const ROTULO_STATUS: Record<string, string> = {
  Pendente: 'Agendada',
  Confirmada: 'Confirmada',
  AguardandoRetorno: 'Aguardando retorno',
  Realizada: 'Realizada',
  NaoRealizada: 'Não realizada',
  Cancelada: 'Cancelada',
};

/** "2026-10-02" → "sexta-feira, 02/10" (data sem fuso: vale como está). */
function formatarDia(iso: string): string {
  const [a, m, d] = iso.split('-').map(Number);
  return new Date(a, m - 1, d).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: '2-digit' });
}

export function Transporte() {
  return (
    <>
      <Lista<ViagemTransporte>
        eyebrow="Transporte de Pacientes"
        titulo="Minhas viagens"
        carregar={api.translados}
        emptyIcon={CalendarHeart}
        emptyTitulo="Nenhuma viagem agendada"
        emptyDescricao="Quando o transporte da Prefeitura estiver agendado para o seu atendimento, as viagens aparecem aqui."
        renderItem={(v) => (
          <Card className="p-4">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="font-display font-semibold capitalize text-tinta">{formatarDia(v.data)}</p>
                <p className="mt-1 inline-flex items-center gap-1.5 text-sm text-tinta">
                  <MapPin className="h-4 w-4 shrink-0 text-marica" />
                  <span className="truncate">
                    {v.destino}
                    {v.cidade ? ` · ${v.cidade}` : ''}
                  </span>
                </p>
              </div>
              <Etiqueta status={ROTULO_STATUS[v.status] ?? v.status} />
            </div>
            {v.tipoTratamento ? <p className="mt-1 text-sm text-tinta-mute">{v.tipoTratamento}</p> : null}
            <p className="mt-2 inline-flex items-center gap-1.5 text-sm text-tinta-mute">
              <Clock className="h-4 w-4" />
              {v.horaBusca
                ? `Busca prevista às ${v.horaBusca.slice(0, 5)}`
                : 'O horário de busca é informado na véspera.'}
            </p>
            {v.acompanhantes.length > 0 ? (
              <p className="mt-1 inline-flex items-center gap-1.5 text-sm text-tinta-mute">
                <Users className="h-4 w-4" /> Acompanhante: {v.acompanhantes.join(', ')}
              </p>
            ) : null}
          </Card>
        )}
      />
      <MeusAcompanhantes />
    </>
  );
}
