import {
  AlertTriangle,
  CheckCircle2,
  CircleDashed,
  HelpCircle,
  MinusCircle,
  ShieldX,
  type LucideIcon,
} from 'lucide-react';

import type { VereditoAnaliseRegras } from '@/shared/regulacao/analiseRegras/tipos';

export const ICONE_VEREDITO: Record<VereditoAnaliseRegras, LucideIcon> = {
  Bloqueado: ShieldX,
  AConferir: HelpCircle,
  ComRessalva: AlertTriangle,
  Apto: CheckCircle2,
  SemRegras: MinusCircle,
  SemProcedimento: CircleDashed,
};
