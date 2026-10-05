import { useQuery } from '@tanstack/react-query';

import { http } from '@/shared/api/httpClient';
import { hojeSP } from '@/shared/lib/datas';

/**
 * A idade que acompanha o nome do paciente em toda tela ("54a", "8m").
 *
 * <p><b>Uma requisição por tela, não uma por linha.</b> Cada nome pede a idade do seu paciente, e
 * os pedidos que chegam juntos (uma tabela de 50 linhas renderizando) viram um POST só a
 * `/pacientes/idades`. Sem isso, a fila de 50 pacientes faria 50 idas ao hub FHIR.</p>
 *
 * <p>Quando a tela já tem a data de nascimento, ela passa a data e nada é pedido.</p>
 */

type IdadePaciente = { id: string; idadeMeses: number | null };

/** Espera curta para juntar os pedidos de um mesmo render. */
const JANELA_MS = 15;
/** O backend aceita até 200 ids por chamada. */
const LOTE = 200;

let pendentes = new Map<string, ((meses: number | null) => void)[]>();
let agendado: ReturnType<typeof setTimeout> | null = null;

async function despachar() {
  agendado = null;
  const lote = pendentes;
  pendentes = new Map();
  const ids = [...lote.keys()];

  for (let i = 0; i < ids.length; i += LOTE) {
    const parte = ids.slice(i, i + LOTE);
    let respostas: IdadePaciente[] = [];
    try {
      const { data } = await http.post<IdadePaciente[]>('/pacientes/idades', { ids: parte });
      respostas = data;
    } catch {
      // Sem idade a tela segue com o nome — não é motivo de erro na frente do usuário.
    }
    const porId = new Map(respostas.map((r) => [r.id, r.idadeMeses]));
    for (const id of parte) for (const resolver of lote.get(id) ?? []) resolver(porId.get(id) ?? null);
  }
}

function carregarIdadeMeses(id: string): Promise<number | null> {
  return new Promise((resolver) => {
    const fila = pendentes.get(id);
    if (fila) fila.push(resolver);
    else pendentes.set(id, [resolver]);
    agendado ??= setTimeout(() => void despachar(), JANELA_MS);
  });
}

/** Meses completos entre a data de nascimento (aaaa-mm-dd) e hoje em Brasília. */
export function idadeEmMeses(nascimento: string | null | undefined): number | null {
  const n = /^(\d{4})-(\d{2})-(\d{2})/.exec(nascimento ?? '');
  const h = /^(\d{4})-(\d{2})-(\d{2})/.exec(hojeSP());
  if (!n || !h) return null;
  let meses = (Number(h[1]) - Number(n[1])) * 12 + (Number(h[2]) - Number(n[2]));
  if (Number(h[3]) < Number(n[3])) meses -= 1;
  return meses < 0 ? null : meses;
}

/** Só os anos ("54a"). Bebê, abaixo de um ano, em meses ("8m"). */
export function formatarIdadeCurta(meses: number | null | undefined): string | null {
  if (meses == null) return null;
  return meses < 12 ? `${meses}m` : `${Math.floor(meses / 12)}a`;
}

/** A idade inteira, para o passar do mouse: "54 anos e 3 meses", "8 meses". */
export function formatarIdadeExtensa(meses: number | null | undefined): string | null {
  if (meses == null) return null;
  const anos = Math.floor(meses / 12);
  const resto = meses % 12;
  const txtMeses = `${resto} ${resto === 1 ? 'mês' : 'meses'}`;
  if (anos === 0) return txtMeses;
  const txtAnos = `${anos} ${anos === 1 ? 'ano' : 'anos'}`;
  return resto === 0 ? txtAnos : `${txtAnos} e ${txtMeses}`;
}

/**
 * Idade do paciente em meses. Com `nascimento` conhecido, calcula na hora; sem ele, pede ao
 * servidor (em lote com os outros nomes da tela).
 */
export function useIdadePacienteMeses(
  pacienteId: string | null | undefined,
  nascimento?: string | null,
): number | null {
  const temData = nascimento !== undefined;
  const consulta = useQuery({
    queryKey: ['pacientes', 'idade', pacienteId],
    queryFn: () => carregarIdadeMeses(pacienteId!),
    enabled: !!pacienteId && !temData,
    // Idade muda uma vez por mês; o cadastro, raramente.
    staleTime: 60 * 60_000,
    gcTime: 2 * 60 * 60_000,
  });
  return temData ? idadeEmMeses(nascimento) : (consulta.data ?? null);
}
