import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import {
  CalendarClock,
  CalendarHeart,
  CalendarPlus,
  ChevronRight,
  FileText,
  FlaskConical,
  HeartPulse,
  Stethoscope,
} from 'lucide-react';
import { cn } from '@/lib/cn';
import { useAuth } from '@/store/auth';
import { usePerfil } from '@/store/perfil';
import { formatarCpf } from '@/components/AppShell';
import { Avatar } from '@/components/ui';

const ATALHOS = [
  { to: '/agendados/consultas', label: 'Consultas', desc: 'Agendadas', icon: CalendarClock, tom: 'lagoa' },
  { to: '/agendados/exames', label: 'Exames', desc: 'Agendados', icon: CalendarPlus, tom: 'lagoa' },
  { to: '/atendimentos', label: 'Atendimentos', desc: 'Suas consultas', icon: Stethoscope, tom: 'lagoa' },
  { to: '/exames', label: 'Exames', desc: 'Resultados', icon: FlaskConical, tom: 'lagoa' },
  { to: '/laudos', label: 'Laudos', desc: 'Documentos', icon: FileText, tom: 'marica' },
  { to: '/transporte', label: 'Transporte', desc: 'TFD e viagens', icon: CalendarHeart, tom: 'marica' },
] as const;

export function Home() {
  const sessao = useAuth((s) => s.paciente);
  const perfil = usePerfil((s) => s.perfil);
  const carregar = usePerfil((s) => s.carregar);

  useEffect(() => {
    if (!perfil) void carregar();
  }, [perfil, carregar]);

  const nome = perfil?.nomeSocial || perfil?.nome || sessao?.nome || 'Cidadão';
  const primeiro = nome.split(' ')[0];
  const cpf = perfil?.cpf ?? sessao?.cpf ?? null;

  return (
    <div className="animate-rise space-y-7">
      <p className="pb-2 text-[15px] text-tinta-mute">
        Olá, <span className="font-semibold text-tinta">{primeiro}</span>. Bem-vindo de volta.
      </p>

      {/* ASSINATURA — Cartão do Cidadão */}
      <Link to="/perfil" aria-label="Ver meu perfil">
        <article className="guilloche relative overflow-hidden rounded-3xl bg-gradient-to-br from-vinho to-marica p-5 text-white shadow-cartao">
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.2em] text-white/85">
              <HeartPulse className="h-3.5 w-3.5" />
              Cartão do Cidadão
            </div>
            <span className="font-display text-sm font-semibold tracking-tight text-white/90">Saúde Maricá</span>
          </div>

          <div className="mt-6 flex items-center gap-4">
            <Avatar
              src={perfil?.fotoBase64 ?? null}
              nome={nome}
              size={60}
              className="bg-white/15 text-white ring-2 ring-white/30"
            />
            <div className="min-w-0">
              <p className="truncate font-display text-xl font-semibold leading-tight">{nome}</p>
              {cpf && <p className="text-sm text-white/80">CPF {formatarCpf(cpf)}</p>}
            </div>
          </div>

          <div className="mt-5 flex items-end justify-between">
            <div>
              <p className="text-[10px] uppercase tracking-widest text-white/60">Cartão SUS</p>
              <p className="font-mono text-sm tracking-wider text-white/90">
                {perfil?.cns ? formatarCns(perfil.cns) : '— — —'}
              </p>
            </div>
            <span className="inline-flex items-center gap-1 rounded-full bg-white/15 px-3 py-1 text-xs font-medium">
              Ver perfil <ChevronRight className="h-3.5 w-3.5" />
            </span>
          </div>
        </article>
      </Link>

      {/* Atalhos */}
      <section>
        <div className="grid grid-cols-2 gap-3">
          {ATALHOS.map((a) => (
            <Link
              key={a.to}
              to={a.to}
              className="group flex flex-col gap-3 rounded-2xl border border-areia bg-white p-4 shadow-carta transition active:scale-[.98]"
            >
              <span
                className={cn(
                  'grid h-12 w-12 place-items-center rounded-2xl',
                  a.tom === 'lagoa' ? 'bg-lagoa-claro text-lagoa' : 'bg-marica/10 text-marica',
                )}
              >
                <a.icon className="h-6 w-6" />
              </span>
              <div>
                <p className="font-display text-base font-semibold text-tinta">{a.label}</p>
                <p className="text-xs text-tinta-mute">{a.desc}</p>
              </div>
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}

function formatarCns(cns: string): string {
  const d = cns.replace(/\D/g, '');
  if (d.length !== 15) return cns;
  return `${d.slice(0, 3)} ${d.slice(3, 7)} ${d.slice(7, 11)} ${d.slice(11)}`;
}
