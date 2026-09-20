import { useMemo } from 'react';
import { Select } from '@/shared/ui/Select';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { useAssuntos, usePontosResposta } from '@/features/ouvidoria/api/queries';
import { ROTULO_TIPO_PONTO } from '@/features/ouvidoria/lib/rotulos';
import type { AssuntoDto, OuvidoriaTipoPontoResposta } from '@/features/ouvidoria/types';

// ---- Assunto → subassunto (dois selects encadeados sobre a lista plana com `paiId`) ----

type SeletorAssuntoProps = {
  assuntoId: string | null;
  subassuntoId: string | null;
  aoMudar: (assuntoId: string | null, subassuntoId: string | null) => void;
  disabled?: boolean;
  idBase?: string;
  /** Inclui inativos (para triagem de manifestação antiga). */
  incluirInativos?: boolean;
};

export function SeletorAssunto({ assuntoId, subassuntoId, aoMudar, disabled, idBase = 'assunto', incluirInativos }: SeletorAssuntoProps) {
  const { data: assuntos = [], isLoading } = useAssuntos();

  const { raizes, filhosDe } = useMemo(() => {
    const visiveis = assuntos.filter((a) => incluirInativos || a.ativo || a.id === assuntoId || a.id === subassuntoId);
    const ordenar = (x: AssuntoDto, y: AssuntoDto) => x.ordem - y.ordem || x.nome.localeCompare(y.nome, 'pt-BR');
    const raizes = visiveis.filter((a) => !a.paiId).sort(ordenar);
    const filhosDe = (paiId: string) => visiveis.filter((a) => a.paiId === paiId).sort(ordenar);
    return { raizes, filhosDe };
  }, [assuntos, incluirInativos, assuntoId, subassuntoId]);

  const filhos = assuntoId ? filhosDe(assuntoId) : [];

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      <div className="flex flex-col">
        <label htmlFor={`${idBase}-pai`} className="label">Assunto</label>
        <Select
          id={`${idBase}-pai`}
          value={assuntoId ?? ''}
          disabled={disabled || isLoading}
          onChange={(e) => aoMudar(e.target.value || null, null)}
        >
          <option value="">{isLoading ? 'Carregando…' : 'Selecione'}</option>
          {raizes.map((a) => (
            <option key={a.id} value={a.id}>
              {a.nome}
              {a.ativo ? '' : ' (inativo)'}
            </option>
          ))}
        </Select>
      </div>
      <div className="flex flex-col">
        <label htmlFor={`${idBase}-sub`} className="label">Subassunto</label>
        <Select
          id={`${idBase}-sub`}
          value={subassuntoId ?? ''}
          disabled={disabled || !assuntoId || filhos.length === 0}
          onChange={(e) => aoMudar(assuntoId, e.target.value || null)}
        >
          <option value="">{!assuntoId ? 'Escolha o assunto' : filhos.length === 0 ? 'Sem subassuntos' : 'Opcional'}</option>
          {filhos.map((a) => (
            <option key={a.id} value={a.id}>
              {a.nome}
              {a.ativo ? '' : ' (inativo)'}
            </option>
          ))}
        </Select>
      </div>
    </div>
  );
}

// ---- Ponto de resposta ----

type SeletorPontoProps = {
  value: string;
  onChange: (id: string) => void;
  /** Restringe ao tipo (denúncia só vai para `Apuracao`). */
  tipo?: OuvidoriaTipoPontoResposta;
  disabled?: boolean;
  id?: string;
  incluirInativos?: boolean;
  /** Texto da opção vazia (filtro usa "Todos os pontos"). */
  rotuloVazio?: string;
};

export function SeletorPontoResposta({ value, onChange, tipo, disabled, id = 'ponto-resposta', incluirInativos, rotuloVazio = 'Selecione' }: SeletorPontoProps) {
  const { data: pontos = [], isLoading } = usePontosResposta();
  const lista = pontos
    .filter((p) => (incluirInativos || p.ativo || p.id === value) && (!tipo || p.tipo === tipo))
    .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));

  return (
    <Select id={id} value={value} disabled={disabled || isLoading} onChange={(e) => onChange(e.target.value)}>
      <option value="">{isLoading ? 'Carregando…' : lista.length === 0 ? 'Nenhum ponto de resposta cadastrado' : rotuloVazio}</option>
      {lista.map((p) => (
        <option key={p.id} value={p.id}>
          {p.nome} — {ROTULO_TIPO_PONTO[p.tipo]}
          {p.unidadeNome ? ` (${p.unidadeNome})` : ''}
          {p.ativo ? '' : ' (inativo)'}
        </option>
      ))}
    </Select>
  );
}

// ---- Unidade (API de unidades já existente no front) ----

type SeletorUnidadeProps = {
  value: string;
  onChange: (id: string) => void;
  disabled?: boolean;
  id?: string;
  rotuloVazio?: string;
  className?: string;
};

export function SeletorUnidade({ value, onChange, disabled, id = 'unidade', rotuloVazio = 'Selecione', className }: SeletorUnidadeProps) {
  const { data: unidades = [], isLoading } = useListarUnidades();
  const lista = [...unidades]
    .filter((u) => u.ativo || u.id === value)
    .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));

  return (
    <Select id={id} value={value} disabled={disabled || isLoading} onChange={(e) => onChange(e.target.value)} className={className}>
      <option value="">{isLoading ? 'Carregando…' : rotuloVazio}</option>
      {lista.map((u) => (
        <option key={u.id} value={u.id}>
          {u.nome}
        </option>
      ))}
    </Select>
  );
}
