import { useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import {
  Activity,
  Building2,
  Bus,
  CalendarClock,
  ClipboardList,
  KeyRound,
  Route as RouteIcon,
  ScanLine,
  Star,
  Truck,
  UserCog,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { useAuth, type ModuloPermissao } from '@/shared/auth/authStore';
import { resolverDestinoMenu } from '@/app/layout/menuConfig';
import {
  alternarFavorito,
  jaRedirecionouNaSessao,
  lerFavorito,
  marcarRedirecionadoNaSessao,
} from '@/shared/menuFavorito/menuFavorito';

type Card = {
  titulo: string;
  descricao: string;
  to: string;
  icone: LucideIcon;
  modulo: ModuloPermissao;
};

const CARDS: Card[] = [
  { titulo: 'Pacientes', descricao: 'Cadastre, edite e consulte pacientes do programa.', to: '/app/pacientes', icone: Users, modulo: 'Pacientes' },
  { titulo: 'Unidades', descricao: 'Locais de saúde que realizam os tratamentos.', to: '/app/unidades', icone: Building2, modulo: 'Unidades' },
  { titulo: 'Motoristas', descricao: 'Agentes de transporte sanitário.', to: '/app/motoristas', icone: Truck, modulo: 'Motoristas' },
  { titulo: 'Usuários', descricao: 'Contas com acesso ao ecossistema.', to: '/app/usuarios', icone: UserCog, modulo: 'Usuarios' },
  { titulo: 'Perfis', descricao: 'Conjuntos de permissões reutilizáveis.', to: '/app/perfis', icone: KeyRound, modulo: 'Perfis' },
  { titulo: 'Tipos de tratamento', descricao: 'Catálogo usado pelos tratamentos.', to: '/app/tipos-tratamento', icone: ClipboardList, modulo: 'TiposTratamento' },
  { titulo: 'Avaliações', descricao: 'Feedback dos pacientes após os translados.', to: '/app/avaliacoes', icone: Star, modulo: 'Avaliacoes' },
  { titulo: 'Veículos', descricao: 'Frota do município.', to: '/app/veiculos', icone: Bus, modulo: 'Veiculos' },
  { titulo: 'Tratamentos', descricao: 'Periodicidade paciente ↔ unidade.', to: '/app/tratamentos', icone: CalendarClock, modulo: 'Tratamentos' },
  { titulo: 'Translados', descricao: 'Rotas diárias e alocação de assentos.', to: '/app/translados', icone: RouteIcon, modulo: 'Translados' },
  { titulo: 'Rastreamento', descricao: 'GPS e geofences.', to: '/app/rastreamento', icone: Activity, modulo: 'Rastreamento' },
  { titulo: 'PACS', descricao: 'Visualizador de imagens DICOM.', to: '/app/pacs', icone: ScanLine, modulo: 'Pacs' },
];

export function InicioPage() {
  const usuario = useAuth((s) => s.usuario);
  const permissoes = useAuth((s) => s.permissoes);
  const cardsVisiveis = CARDS.filter((c) => (permissoes[c.modulo] ?? []).includes('Consulta'));

  const [favorito, setFavorito] = useState<string | null>(() => lerFavorito(usuario?.id));

  // Sincroniza o favorito quando o usuário logado muda.
  useEffect(() => {
    setFavorito(lerFavorito(usuario?.id));
  }, [usuario?.id]);

  // Redirect de entrada: computa uma única vez (idempotente sob StrictMode) se,
  // ao abrir o app, há favorito e ainda não redirecionamos nesta sessão do
  // navegador. Se sim, vai direto ao destino do menu favoritado (encadeamento).
  const [destinoRedirect] = useState<string | null>(() => {
    const fav = lerFavorito(usuario?.id);
    if (fav && !jaRedirecionouNaSessao()) {
      marcarRedirecionadoNaSessao();
      return resolverDestinoMenu(fav);
    }
    return null;
  });
  if (destinoRedirect) {
    return <Navigate to={destinoRedirect} replace />;
  }

  function toggleFavorito(to: string) {
    setFavorito(alternarFavorito(to, usuario?.id));
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">
          Bem-vindo(a){usuario?.nome ? `, ${usuario.nome}` : ''}
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Selecione um módulo abaixo ou use o menu lateral. Você vê apenas o que seus perfis liberam.
        </p>
      </header>

      {cardsVisiveis.length === 0 ? (
        <div className="card p-6 text-sm text-gray-500">
          Você ainda não tem permissões atribuídas. Peça a um administrador para vincular um perfil ao seu usuário.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {cardsVisiveis.map((c) => (
            <Link key={c.to} to={c.to} className="card card-hover group relative p-5">
              <button
                type="button"
                aria-label={favorito === c.to ? 'Remover dos favoritos' : 'Definir como favorito'}
                aria-pressed={favorito === c.to}
                title={
                  favorito === c.to
                    ? 'Menu favorito — ao abrir o app você vai direto para cá. Clique para remover.'
                    : 'Favoritar: ao abrir o app você vai direto para este menu.'
                }
                onClick={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  toggleFavorito(c.to);
                }}
                className="absolute right-3 top-3 rounded-full p-1.5 text-gray-300 hover:bg-gray-100 hover:text-amber-500"
              >
                <Star
                  className="h-5 w-5"
                  fill={favorito === c.to ? 'currentColor' : 'none'}
                  color={favorito === c.to ? '#f59e0b' : 'currentColor'}
                />
              </button>
              <div className="flex items-start gap-4">
                <div className="icon-box icon-box-default">
                  <c.icone className="w-5 h-5" />
                </div>
                <div className="min-w-0 flex-1 pr-6">
                  <h2 className="text-base font-semibold text-gray-900 group-hover:text-primary-700">
                    {c.titulo}
                  </h2>
                  <p className="mt-1 text-sm text-gray-600">{c.descricao}</p>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
