import { http } from '@/shared/api/httpClient';

/** O que o SISREG mudou. Nome do enum, não número — a API serializa enum como string. */
export type TipoAlteracaoAgenda = 'DataHora' | 'Executante' | 'Procedimento' | 'Ausente';

export type AlteracaoAgenda = {
  id: string;
  solicitacaoId: string;
  codigoSolicitacao: string | null;
  tipo: TipoAlteracaoAgenda;
  valorAntes: string | null;
  valorDepois: string | null;
  pacienteNome: string | null;
  procedimentoTexto: string | null;
  unidadeExecutanteNome: string | null;
  unidadeSolicitanteNome: string | null;
  dataAgendada: string | null;
  detectadaEm: string;
  tratadaEm: string | null;
  comunicadaEm: string | null;
};

export type PaginaAlteracoesAgenda = { total: number; itens: AlteracaoAgenda[] };

export async function listarAlteracoesAgenda(
  apenasPendentes: boolean,
  pagina = 0,
  tamanho = 50,
): Promise<PaginaAlteracoesAgenda> {
  const { data } = await http.get<PaginaAlteracoesAgenda>('/alteracoes-agenda', {
    params: { apenasPendentes, pagina, tamanho },
  });
  return data;
}

export async function tratarAlteracaoAgenda(id: string): Promise<void> {
  await http.post(`/alteracoes-agenda/${id}/tratar`);
}

/**
 * Reenvia a confirmação ao paciente com os dados atuais e trata a alteração.
 * Revoga os links anteriores — quem tem a data velha na mão perde o acesso a ela.
 */
export async function comunicarAlteracaoAgenda(id: string): Promise<void> {
  await http.post(`/alteracoes-agenda/${id}/comunicar`);
}
