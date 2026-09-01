import { http } from '@/shared/api/httpClient';
import type {
  AlternarTudoDaUnidade,
  ProcedimentoSigtapDePara,
  SisregMapeamento,
} from '@/features/sisreg-mapeamento/types';

/**
 * Todos os endpoints trabalham no contexto de UMA unidade. Quando um `unidadeId` é informado,
 * ele vai explicitamente no header da requisição (o interceptor não o sobrescreve com a unidade
 * ativa do topo) — é o que permite operar a unidade da ROTA em `/app/unidades/{id}`, sem depender
 * do seletor global. Omitido, cai na unidade ativa; sem nenhuma das duas a API recusa com 400.
 */
function cabecalhoUnidade(unidadeId?: string | null) {
  return unidadeId ? { headers: { 'X-Unidade-Id': unidadeId } } : undefined;
}

export async function obterMapeamento(unidadeId?: string | null): Promise<SisregMapeamento> {
  const { data } = await http.get<SisregMapeamento>('/sisreg/mapeamento', cabecalhoUnidade(unidadeId));
  return data;
}

export async function alternarProfissional(
  id: string,
  habilitado: boolean,
  unidadeId?: string | null,
): Promise<void> {
  await http.put(
    `/sisreg/mapeamento/profissionais/${id}/habilitacao`,
    { habilitado },
    cabecalhoUnidade(unidadeId),
  );
}

export async function alternarProcedimento(
  id: string,
  habilitado: boolean,
  unidadeId?: string | null,
): Promise<void> {
  await http.put(
    `/sisreg/mapeamento/procedimentos/${id}/habilitacao`,
    { habilitado },
    cabecalhoUnidade(unidadeId),
  );
}

export async function alternarProfissionaisEmLote(
  ids: string[],
  habilitado: boolean,
  unidadeId?: string | null,
): Promise<void> {
  await http.put(
    '/sisreg/mapeamento/profissionais/habilitacao-lote',
    { ids, habilitado },
    cabecalhoUnidade(unidadeId),
  );
}

/**
 * Aplica de uma vez a um profissional: habilita/desabilita o médico e todos os seus procedimentos
 * (`habilitados`) e liga/desliga o aviso por WhatsApp (`enviarConfirmacao`). É o botão "selecionar
 * tudo do médico" — só automatiza os cliques, sem mudar semântica (o zap segue a regra do
 * procedimento na unidade, alcançando as demais linhas do mesmo código).
 */
export async function alternarProcedimentosDoProfissional(
  id: string,
  habilitados: boolean,
  enviarConfirmacao: boolean,
  unidadeId?: string | null,
): Promise<void> {
  await http.put(
    `/sisreg/mapeamento/profissionais/${id}/procedimentos`,
    { habilitados, enviarConfirmacao },
    cabecalhoUnidade(unidadeId),
  );
}

/**
 * Aplica de uma vez a TODA a unidade: todos os médicos, todos os procedimentos e o aviso por
 * WhatsApp. Mesmo efeito do botão por médico, na unidade inteira — existe para o operador não ter
 * que percorrer 113 médicos um a um. Não vai ao SISREG.
 */
export async function alternarTudoDaUnidade(
  habilitados: boolean,
  unidadeId?: string | null,
): Promise<AlternarTudoDaUnidade> {
  // `enviarConfirmacao` fica de fora de propósito: omitido, o back NÃO encosta no aviso por
  // WhatsApp. Este botão liga o sincronismo; quem decide o aviso é o mestre da unidade e a
  // caixinha de cada procedimento.
  const { data } = await http.put<AlternarTudoDaUnidade>(
    '/sisreg/mapeamento/habilitar-tudo',
    { habilitados },
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

/** De-para do procedimento do SISREG (o `pa`) para o SIGTAP. Catálogo GLOBAL, sem unidade. */
export async function listarDeParaSigtap(somenteNaoConfirmados = false): Promise<ProcedimentoSigtapDePara[]> {
  const { data } = await http.get<ProcedimentoSigtapDePara[]>('/sisreg/mapeamento/procedimentos-sigtap', {
    params: { somenteNaoConfirmados },
  });
  return data;
}

export async function sugerirDeParaSigtap(): Promise<{ sugeridos: number }> {
  const { data } = await http.post<{ sugeridos: number }>('/sisreg/mapeamento/procedimentos-sigtap/sugerir');
  return data;
}

/**
 * Liga/desliga o aviso por WhatsApp deste procedimento NESTA unidade. O back aplica a todas as
 * linhas do mesmo procedimento na unidade (ele costuma aparecer sob vários profissionais) e
 * devolve quantas foram afetadas.
 */
export async function alternarEnvioConfirmacao(
  id: string,
  enviar: boolean,
  unidadeId?: string | null,
): Promise<{ afetados: number }> {
  const { data } = await http.put<{ afetados: number }>(
    `/sisreg/mapeamento/procedimentos/${id}/confirmacao`,
    { enviar },
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function confirmarDeParaSigtap(
  id: string,
  procedimentoSigtapId: string,
): Promise<ProcedimentoSigtapDePara> {
  const { data } = await http.put<ProcedimentoSigtapDePara>(
    `/sisreg/mapeamento/procedimentos-sigtap/${id}`,
    { procedimentoSigtapId },
  );
  return data;
}
