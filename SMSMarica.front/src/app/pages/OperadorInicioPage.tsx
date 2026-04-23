import { Link } from 'react-router-dom';
import {
  Activity,
  Building2,
  Bus,
  CalendarClock,
  Route as RouteIcon,
  Star,
  Truck,
  UserCog,
  Users,
  type LucideIcon,
} from 'lucide-react';

type Card = {
  titulo: string;
  descricao: string;
  to: string;
  icone: LucideIcon;
  status: 'completo' | 'parcial';
};

const cards: Card[] = [
  {
    titulo: 'Pacientes',
    descricao: 'Cadastre, edite e consulte pacientes do programa.',
    to: '/operador/pacientes',
    icone: Users,
    status: 'completo',
  },
  {
    titulo: 'Unidades',
    descricao: 'Locais de saúde que realizam os tratamentos.',
    to: '/operador/unidades',
    icone: Building2,
    status: 'completo',
  },
  {
    titulo: 'Motoristas',
    descricao: 'Agentes de transporte sanitário.',
    to: '/operador/motoristas',
    icone: Truck,
    status: 'completo',
  },
  {
    titulo: 'Usuários',
    descricao: 'Contas com acesso ao ecossistema.',
    to: '/operador/usuarios',
    icone: UserCog,
    status: 'completo',
  },
  {
    titulo: 'Avaliações',
    descricao: 'Feedback dos pacientes após os translados.',
    to: '/operador/avaliacoes',
    icone: Star,
    status: 'completo',
  },
  {
    titulo: 'Veículos',
    descricao: 'Frota. Escrita ainda pendente no server.',
    to: '/operador/veiculos',
    icone: Bus,
    status: 'parcial',
  },
  {
    titulo: 'Tratamentos',
    descricao: 'Periodicidade paciente ↔ unidade. Escrita pendente.',
    to: '/operador/tratamentos',
    icone: CalendarClock,
    status: 'parcial',
  },
  {
    titulo: 'Translados',
    descricao: 'Rotas e alocação. Escrita pendente.',
    to: '/operador/translados',
    icone: RouteIcon,
    status: 'parcial',
  },
  {
    titulo: 'Rastreamento',
    descricao: 'GPS e geofences. Escrita pendente.',
    to: '/operador/rastreamento',
    icone: Activity,
    status: 'parcial',
  },
];

export function OperadorInicioPage() {
  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Bem-vindo(a) ao painel do operador</h1>
        <p className="mt-1 text-sm text-gray-600">
          A partir daqui você gerencia cadastros e acompanha a operação diária.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
        {cards.map((c) => (
          <Link key={c.to} to={c.to} className="card card-hover group p-5">
            <div className="flex items-start gap-4">
              <div className="icon-box icon-box-default">
                <c.icone className="w-5 h-5" />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-2">
                  <h2 className="text-base font-semibold text-gray-900 group-hover:text-primary-700">
                    {c.titulo}
                  </h2>
                  {c.status === 'parcial' ? (
                    <span className="badge badge-warning shrink-0">Leitura</span>
                  ) : null}
                </div>
                <p className="mt-1 text-sm text-gray-600">{c.descricao}</p>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
