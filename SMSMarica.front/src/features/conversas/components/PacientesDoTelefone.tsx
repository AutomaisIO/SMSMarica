import { Users } from 'lucide-react';
import { usePacientesDoTelefone } from '@/features/conversas/api/queries';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { formatarNomeProprio } from '@/shared/lib/nomes';

function idade(nascimento: string | null): string {
  if (!nascimento) return '';
  const [a, m, d] = nascimento.split('-').map(Number);
  if (!a) return '';
  const hoje = new Date();
  let anos = hoje.getFullYear() - a;
  const fezAniversario =
    hoje.getMonth() + 1 > m || (hoje.getMonth() + 1 === m && hoje.getDate() >= d);
  if (!fezAniversario) anos -= 1;
  return anos >= 0 ? `${anos} anos` : '';
}

/**
 * Um celular só costuma estar no cadastro da família inteira (mãe, filho, avó). Antes, o
 * sistema escolhia UM desses cadastros em silêncio e mostrava só ele — quem atende falava
 * "olá, Maria" sem saber que do outro lado podia estar o filho. Aqui listamos todos os
 * cadastros com este número, marcando qual é o titular da conversa. Só aparece quando há
 * mais de um: com um cadastro só, o cabeçalho já diz tudo.
 */
export function PacientesDoTelefone({ conversaId }: { conversaId: string }) {
  const { data } = usePacientesDoTelefone(conversaId);
  if (!data || data.length < 2) return null;

  return (
    <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md border border-amber-200 bg-amber-50 px-2 py-1.5">
      <span className="flex items-center gap-1 text-xs font-medium text-amber-800">
        <Users className="h-3.5 w-3.5" />
        {data.length} cadastros com este telefone:
      </span>
      {data.map((p) => (
        <span
          key={p.pacienteId}
          className={`inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs ${
            p.titular ? 'bg-white font-medium text-gray-900 ring-1 ring-amber-300' : 'text-gray-700'
          }`}
          title={[
            p.titular ? 'Titular desta conversa' : null,
            p.cpf ? `CPF ${p.cpf}` : null,
            idade(p.dataNascimento),
          ]
            .filter(Boolean)
            .join(' · ')}
        >
          {formatarNomeProprio(p.nome)}
          {idade(p.dataNascimento) ? (
            <span className="text-gray-400">({idade(p.dataNascimento)})</span>
          ) : null}
          <NomePacienteComResumo pacienteId={p.pacienteId} mostrarWhatsApp={false} />
        </span>
      ))}
    </div>
  );
}
