import { CalendarClock, CalendarPlus, Clock, MapPin, Stethoscope } from 'lucide-react';
import { api, type Agendamento } from '@/lib/api';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';

export function ConsultasAgendadas() {
  return (
    <Lista
      eyebrow="Agenda"
      titulo="Consultas agendadas"
      carregar={() => api.agendamentos('consulta')}
      emptyIcon={CalendarClock}
      emptyTitulo="Nenhuma consulta agendada"
      emptyDescricao="Suas próximas consultas marcadas vão aparecer aqui com data, profissional e local."
      renderItem={(a) => <AgendamentoCard agendamento={a} />}
    />
  );
}

export function ExamesAgendados() {
  return (
    <Lista
      eyebrow="Agenda"
      titulo="Exames agendados"
      carregar={() => api.agendamentos('exame')}
      emptyIcon={CalendarPlus}
      emptyTitulo="Nenhum exame agendado"
      emptyDescricao="Seus próximos exames marcados vão aparecer aqui com data, tipo e local."
      renderItem={(a) => <AgendamentoCard agendamento={a} />}
    />
  );
}

function AgendamentoCard({ agendamento: a }: { agendamento: Agendamento }) {
  return (
    <Card className="p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate font-display font-semibold text-tinta">{a.titulo}</p>
          <p className="mt-0.5 flex items-center gap-1.5 text-sm text-tinta-mute">
            <Clock className="h-4 w-4 shrink-0" />
            {formatarDataHora(a.inicioEm)}
          </p>
        </div>
        <Etiqueta status={a.status} />
      </div>

      <div className="mt-3 space-y-1.5 text-sm text-tinta-mute">
        {a.profissional && (
          <p className="flex items-center gap-1.5">
            <Stethoscope className="h-4 w-4 shrink-0" />
            <span className="truncate">{a.profissional}</span>
          </p>
        )}
        {a.unidade && (
          <p className="flex items-center gap-1.5">
            <MapPin className="h-4 w-4 shrink-0" />
            <span className="truncate">{a.unidade}</span>
          </p>
        )}
      </div>
    </Card>
  );
}

function formatarDataHora(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
