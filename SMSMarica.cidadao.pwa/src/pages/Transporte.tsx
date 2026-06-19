import { CalendarHeart, MapPin } from 'lucide-react';
import { api } from '@/lib/api';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';

export function Transporte() {
  return (
    <Lista
      eyebrow="Tratamento Fora de Domicílio"
      titulo="Transporte"
      carregar={api.translados}
      emptyIcon={CalendarHeart}
      emptyTitulo="Nenhuma viagem agendada"
      emptyDescricao="Quando houver transporte do TFD para o seu tratamento, ele aparece aqui com o horário e a chegada do veículo."
      renderItem={(t) => (
        <Card className="p-4">
          <div className="flex items-center justify-between gap-3">
            <span className="inline-flex items-center gap-1.5 font-display font-semibold text-tinta">
              <MapPin className="h-4 w-4 text-marica" />
              {t.destino}
            </span>
            <Etiqueta status={t.status} />
          </div>
          <p className="mt-1 text-sm text-tinta-mute">{t.data}</p>
        </Card>
      )}
    />
  );
}
