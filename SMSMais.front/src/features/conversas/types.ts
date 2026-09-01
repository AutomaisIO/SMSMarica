export type StatusConversa = 'Aberta' | 'Pendente' | 'Resolvida' | 'Fechada';
export type DirecaoMensagem = 'Saida' | 'Entrada';
export type TipoMensagem =
  | 'Texto' | 'Imagem' | 'Documento' | 'Audio' | 'Video' | 'Template' | 'NotaInterna' | 'Sistema' | 'Robo';
export type AssuntoConversa = 'Tfd' | 'MarcacaoConsulta' | 'Duvida' | 'Atendente' | 'Outro';
export type StatusMensagem = 'Enviada' | 'Entregue' | 'Lida' | 'Falha' | 'Recebida';
/**
 * 'Unidade' é a FILA (sem responsável, das minhas unidades + triagem geral); 'Minhas' são as
 * que eu atendo. As duas são disjuntas. 'NaoAtribuidas' sobrevive só por compatibilidade — o
 * backend a trata como alias de 'Unidade'.
 */
export type AbaConversas = 'Minhas' | 'Unidade' | 'NaoAtribuidas' | 'Todas';

export type ConversaListItem = {
  id: string;
  telefoneCanonical: string;
  nomeContato: string | null;
  pacienteId: string | null;
  /**
   * Nome COMPLETO do paciente resolvido do banco (hub FHIR) pelo vínculo/telefone.
   * NÃO substitui o nomeContato (perfil do WhatsApp) — os dois convivem para expor
   * divergência (telefone cadastrado na pessoa errada).
   */
  pacienteNome: string | null;
  /** ❗ há pendência aberta de "número errado": quem atende este telefone disse que NÃO é o paciente. */
  contatoNegado: boolean;
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
  /** Exemplo de cada variável ({{1}}, {{2}}…), como aprovado na Meta. Vira placeholder do campo. */
  exemplos: string[];
};

/** Um dos cadastros que carregam o telefone da conversa (celular de família). */
export type PacienteDoTelefone = {
  pacienteId: string;
  nome: string;
  cpf: string | null;
  dataNascimento: string | null;
  /** O paciente vinculado à conversa — o que o resto do sistema trata como "o dono". */
  titular: boolean;
};

/** Candidato a destinatário na abertura de conversa (busca por solicitação, CPF, CNS ou nome). */
export type ContatoConversa = {
  pacienteId: string;
  nome: string;
  telefone: string | null;
  cpf: string | null;
  dataNascimento: string | null;
  /** Por onde foi achado: "Solicitação SISREG 123456" ou "Cadastro". */
  origem: string;
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

/** Atendente que pode receber a conversa por encaminhamento (ativo, com o módulo, vinculado). */
export type AtendenteElegivel = {
  usuarioId: string;
  nome: string;
  responsavelAtual: boolean;
};

/** Unidade da rede que pode receber a conversa por transferência. */
export type UnidadeDestino = {
  id: string;
  nome: string;
};

/** Contadores de não-lidas (minhas × fila) para o sino/badge sem carregar a lista. */
export type ResumoConversas = {
  minhasNaoLidas: number;
  filaNaoLidas: number;
  todasNaoLidas: number;
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
