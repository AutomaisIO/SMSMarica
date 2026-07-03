export type StatusConversa = 'Aberta' | 'Pendente' | 'Resolvida' | 'Fechada';
export type DirecaoMensagem = 'Saida' | 'Entrada';
export type TipoMensagem =
  | 'Texto' | 'Imagem' | 'Documento' | 'Audio' | 'Video' | 'Template' | 'NotaInterna' | 'Sistema';
export type AssuntoConversa = 'Tfd' | 'MarcacaoConsulta' | 'Duvida' | 'Atendente' | 'Outro';
export type StatusMensagem = 'Enviada' | 'Entregue' | 'Lida' | 'Falha' | 'Recebida';
export type AbaConversas = 'Minhas' | 'Unidade' | 'NaoAtribuidas' | 'Todas';

export type ConversaListItem = {
  id: string;
  telefoneCanonical: string;
  nomeContato: string | null;
  pacienteId: string | null;
  assunto: AssuntoConversa | null;
  status: StatusConversa;
  operadorResponsavelId: string | null;
  operadorResponsavelNome: string | null;
  unidadeId: string | null;
  unidadeNome: string | null;
  ultimaMensagemEm: string | null;
  ultimaMensagemDirecao: DirecaoMensagem | null;
  ultimaMensagemPreview: string | null;
  naoLidas: number;
  janelaExpiraEm: string | null;
  podeTextoLivre: boolean;
};

export type Mensagem = {
  id: string;
  conversaId: string | null;
  direcao: DirecaoMensagem;
  tipoMensagem: TipoMensagem | null;
  conteudo: string | null;
  template: string | null;
  autorUsuarioId: string | null;
  autorNomeExibicao: string | null;
  status: StatusMensagem;
  ocorridoEm: string;
};

export type TemplateWhatsApp = {
  nome: string;
  idioma: string;
  categoria: string;
  corpo: string | null;
  parametros: number;
};

export type IniciarConversaPayload = {
  telefone: string;
  pacienteId?: string | null;
  nomeContato?: string | null;
  assunto?: AssuntoConversa | null;
  template: string;
  idioma: string;
  parametros: string[];
};

/** Payload dos eventos SignalR (espelha ConversaEventoRealtime do backend). */
export type ConversaEventoRealtime = {
  conversaId: string;
  operadorResponsavelId: string | null;
  unidadeId: string | null;
  telefoneCanonical: string;
  nomeContato: string | null;
  preview: string | null;
  naoLidas: number;
  ocorridoEm: string | null;
};

export const ROTULO_ASSUNTO: Record<AssuntoConversa, string> = {
  Tfd: 'TFD',
  MarcacaoConsulta: 'Marcação',
  Duvida: 'Dúvida',
  Atendente: 'Atendente',
  Outro: 'Outro',
};
