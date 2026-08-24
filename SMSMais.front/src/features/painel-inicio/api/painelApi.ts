import { http } from '@/shared/api/httpClient';
import type {
  DirecaoPainel,
  LenteEscopo,
  PainelInicio,
  PendenciaImportacaoBusca,
} from '@/features/painel-inicio/types';

/** Uma requisição para a home inteira (ADR-0033 §1) — nunca uma por raia. */
export async function obterPainelInicio(
  lente: LenteEscopo,
  direcao: DirecaoPainel,
): Promise<PainelInicio> {
  const { data } = await http.get<PainelInicio>('/painel/inicio', { params: { lente, direcao } });
  return data;
}

/**
 * Pendências de importação que casam com o termo — o bloco da busca de Solicitações.
 * Endpoint irmão da listagem (que devolve array nu e não pode ser embrulhada), gateado pela
 * permissão da TELA (`SolicitacoesExame`), não pela do módulo SISREG: quem precisa disto é a
 * recepção. Ver ADR-0035.
 */
export async function buscarPendenciasImportacao(busca: string): Promise<PendenciaImportacaoBusca[]> {
  const { data } = await http.get<PendenciaImportacaoBusca[]>(
    '/solicitacoes-exame/pendencias-importacao',
    { params: { busca } },
  );
  return data;
}

/** "Informar CPF e importar": vincula o paciente e replica a linha do SISREG. */
export async function resolverPendenciaComPaciente(
  falhaId: string,
  corpo: { cpf?: string; pacienteId?: string },
): Promise<{ falhaId: string; resolvida: boolean; mensagem: string }> {
  const { data } = await http.post(`/sisreg/importacao/falhas/${falhaId}/resolver-com-paciente`, corpo);
  return data;
}
