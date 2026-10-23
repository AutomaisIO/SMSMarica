import { Building2, Users } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { corAvatar, iniciais } from '@/features/comunicacao-interna/lib/formatar';

/** Ponto de presença: verde = está com o painel aberto agora; cinza = não está. */
export function PontoOnline({ online, className }: { online: boolean; className?: string }) {
  return (
    <span
      className={cn(
        'inline-block h-2.5 w-2.5 shrink-0 rounded-full ring-2 ring-white',
        online ? 'bg-emerald-500' : 'bg-gray-300',
        className,
      )}
      title={online ? 'Online — está com o painel aberto' : 'Offline'}
      aria-label={online ? 'Online' : 'Offline'}
    />
  );
}

/** Avatar de pessoa (iniciais, cor estável pelo id) com o ponto de presença no canto. */
export function AvatarPessoa({
  id,
  nome,
  online,
  tamanho = 'md',
}: {
  id: string;
  nome: string;
  online?: boolean;
  tamanho?: 'sm' | 'md';
}) {
  return (
    <span className="relative inline-flex shrink-0">
      <span
        className={cn(
          'inline-flex items-center justify-center rounded-full font-semibold',
          tamanho === 'sm' ? 'h-7 w-7 text-[10px]' : 'h-9 w-9 text-xs',
          corAvatar(id),
        )}
      >
        {iniciais(nome)}
      </span>
      {online !== undefined && <PontoOnline online={online} className="absolute -bottom-0.5 -right-0.5" />}
    </span>
  );
}

/** Avatar de conversa de unidade (todos das unidades). */
export function AvatarUnidade({ tamanho = 'md', equipe = false }: { tamanho?: 'sm' | 'md'; equipe?: boolean }) {
  const Icone = equipe ? Users : Building2;
  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-full bg-orange-100 text-orange-700',
        tamanho === 'sm' ? 'h-7 w-7' : 'h-9 w-9',
      )}
    >
      <Icone className={tamanho === 'sm' ? 'h-3.5 w-3.5' : 'h-4 w-4'} />
    </span>
  );
}
