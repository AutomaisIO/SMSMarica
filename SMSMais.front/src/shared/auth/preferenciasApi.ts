import { http } from '@/shared/api/httpClient';

/** Preferências de UI do usuário, persistidas no servidor (por usuário). */
export type PreferenciasUi = {
  /** Tela default de cada seção do menu: id da seção → rota. */
  menuDefaults: Record<string, string>;
  /** Altura (px) da caixa de digitação do chat de conversas. */
  alturaComposerChat?: number;
  /** Enter envia a mensagem no chat (Shift+Enter quebra linha). */
  enviarComEnter?: boolean;
  /** Na lista de Solicitações de Exame, ver por padrão a visão de solicitante (ticket #84). */
  verComoSolicitante?: boolean;
  /**
   * Larguras (px) das colunas das tabelas redimensionáveis, por tela (ticket #99):
   * id da tela → (chave da coluna → largura). Enviar sempre o mapa completo.
   */
  largurasTabela?: Record<string, Record<string, number>>;
  /**
   * Modalidades DICOM que a tela de Exames de imagem mostra por padrão (ex.: ["MG","OT"]).
   * Vira o filtro ModalitiesInStudy do QIDO. Vazio = todas.
   */
  examesModalidades?: string[];
  /** Ids dos tipos de exame marcados na tela de Exames de imagem. Vazio = todos. */
  examesTipos?: string[];
  /**
   * Sistemas reguladores que a tela de Regras de elegibilidade NÃO lista (ex.: `["Sisreg"]`).
   * Conveniência: some da listagem, mas as regras do sistema omitido continuam valendo no
   * wizard. Vazio = mostra todos.
   */
  regulacaoSistemasOcultos?: string[];
  /**
   * Bip sonoro de mensagem nova da Central de Atendimento silenciado? Persistido no usuário
   * (ticket #127): silenciou, continua silenciado entre sessões/máquinas até reativar.
   * Ausente/false = bip ligado. Substitui o comportamento só-de-sessão do ticket #44.
   */
  bipChatSilenciado?: boolean;
  /** Técnicos reguladores marcados no filtro das Notificações do SER. Vazio = todos. */
  notificacoesSerTecnicos?: string[];
  /** Técnicos reguladores marcados no filtro das Notificações do SERNIT. Vazio = todos. */
  notificacoesSernitTecnicos?: string[];
};

export async function obterPreferencias(): Promise<PreferenciasUi> {
  const { data } = await http.get<PreferenciasUi>('/identidade/me/preferencias');
  return {
    menuDefaults: data?.menuDefaults ?? {},
    alturaComposerChat: data?.alturaComposerChat ?? undefined,
    enviarComEnter: data?.enviarComEnter ?? undefined,
    verComoSolicitante: data?.verComoSolicitante ?? undefined,
    largurasTabela: data?.largurasTabela ?? undefined,
    examesModalidades: data?.examesModalidades ?? undefined,
    examesTipos: data?.examesTipos ?? undefined,
    regulacaoSistemasOcultos: data?.regulacaoSistemasOcultos ?? undefined,
    bipChatSilenciado: data?.bipChatSilenciado ?? undefined,
    notificacoesSerTecnicos: data?.notificacoesSerTecnicos ?? undefined,
    notificacoesSernitTecnicos: data?.notificacoesSernitTecnicos ?? undefined,
  };
}

/**
 * Salva preferências. O servidor faz merge por campo: enviar só a parte que mudou
 * (ex.: `{ menuDefaults }` ou `{ enviarComEnter }`) preserva os demais campos.
 */
export async function salvarPreferencias(preferencias: Partial<PreferenciasUi>): Promise<void> {
  await http.put('/identidade/me/preferencias', preferencias);
}
