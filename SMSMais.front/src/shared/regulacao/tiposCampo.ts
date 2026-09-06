/**
 * Tipos de campo de formulário comuns aos sistemas de regulação (SER, SERNIT e, a partir do
 * ADR-0052, o fluxo Externo unificado).
 *
 * <p>Os três falam o mesmo dialeto porque os dois primeiros são a mesma aplicação JSF instalada
 * em lugares diferentes. Antes desta extração, `features/ser` e `features/sernit` tinham cópias
 * byte a byte destes tipos e do componente que os desenha — diferindo só no nome do sistema
 * dentro dos comentários.</p>
 */

export type OpcaoRegulacao = { valor: string; rotulo: string };

/**
 * Campo que o sistema acrescenta conforme o recurso escolhido — é o que faz oncologia pedir
 * peso, altura, IMC e datas de biópsia enquanto uma consulta comum pede só três textos.
 */
export type CampoDinamicoRegulacao = {
  numero: string;
  /** Nome nativo do campo (JSF) — é por ele que o valor viaja no envio. */
  campo: string;
  rotulo: string;
  /** `text`, `textarea`, `select`, `radio`, `checkbox` ou `date`. */
  tipo: string;
  obrigatorio: boolean;
  opcoes: OpcaoRegulacao[] | null;
};

export type CampoPacienteRegulacao = {
  /** Nome nativo. Alguns telefones têm id POSICIONAL (`j_idNNN`) — nunca chumbar. */
  campo: string;
  rotulo: string;
  valor: string | null;
  tipo: 'text' | 'select';
  obrigatorio: boolean;
  /**
   * A identidade (nome, CPF, CNS, nascimento, sexo, mãe, raça) vem travada com `disabled`, e
   * campo travado não é enviado pelo navegador: esses valores nem chegam ao Gravar. Editá-los
   * na nossa tela seria oferecer uma digitação que o sistema de destino descarta.
   */
  editavel: boolean;
  opcoes: OpcaoRegulacao[] | null;
};

/** Nome do sistema, usado só nas mensagens ao operador. */
export type NomeSistemaRegulacao = 'SER' | 'SERNIT';

// -------------------------------------------------------------- datas

/**
 * Os sistemas usam `dd/MM/yyyy` no `rich:calendar`; o input nativo de data fala ISO. A conversão
 * fica na borda para que o valor guardado no rascunho seja sempre o formato que o destino espera.
 */
export function paraIso(br: string): string {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(br.trim());
  return m ? `${m[3]}-${m[2]}-${m[1]}` : '';
}

export function paraBr(iso: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : '';
}
