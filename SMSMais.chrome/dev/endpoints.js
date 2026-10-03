// Catálogo dos endpoints do SISREG já mapeados no laboratório
// (Automais.SISREG/docs/APRENDIZADOS.md). A PoC só destaca o que está aqui;
// o resto aparece como "desconhecido" para ajudar a mapear.

export const ENDPOINTS = {
  '/': { nome: 'Login' },
  '/cgi-bin/index': { nome: 'Início (moldura com menu)' },
  '/cgi-bin/avisos': { nome: 'Avisos (tela inicial do f_main)' },
  '/cgi-bin/cadweb50': { nome: 'Consulta CADSUS (CPF/CNS)' },
  '/cgi-bin/marcar': { nome: 'Solicitação de Consultas Ambulatoriais' },
  '/cgi-bin/cons_verificar': { nome: 'Consulta de Autorização/Cancelamento' },
  '/cgi-bin/gerenciador_solicitacao': { nome: 'Consulta de Solicitações Ambulatoriais' },
  '/cgi-bin/cons_agendas': { nome: 'Impressão/Confirmação de Agendas' },
  '/cgi-bin/cons_escalas': { nome: 'Consulta de Escalas Ambulatoriais' },
  '/cgi-bin/cons_marcados_reg': { nome: 'Agendados pela Regulação' },
  '/cgi-bin/expo_solicitacoes': { nome: 'Arquivo Agendamento (TXT)' },
  '/cgi-bin/cons_unidade': { nome: 'Unidades' },
  '/cgi-bin/config_preparo': { nome: 'Cadastro de Preparo' },
  '/cgi-bin/sisreg_ajax': { nome: 'AJAX (listas encadeadas)' },
  '/cgi-bin/recaptcha': { nome: 'CAPTCHA anti-robô' },
};

// Etapas que são gatilhos de negócio (o que interessaria ao SMSMais no futuro).
// `escrita: true` = a operação altera dado no SISREG.
// Para o modo MÍNIMO, os comandos mapeados carregam:
//   `comando`     — rótulo enviado ao SMSMais ("agendou"/"cancelou");
//   `numeroDe`    — onde está o nº da solicitação: 'envio' (campo do form) ou 'resposta' (tela);
//   `campoNumero` — quando 'envio', qual campo tem o número.
export const ETAPAS = {
  ACESSO: { evento: 'login', escrita: false },
  // A GRAVAÇÃO da marcação: o nº da solicitação só existe na tela de confirmação (resposta).
  MARCAR: { evento: 'agendamento', escrita: true, comando: 'agendou', numeroDe: 'resposta' },
  EXCLUIR_SOLICITACAO: { evento: 'cancelamento', escrita: true, comando: 'cancelou', numeroDe: 'envio', campoNumero: 'codigo_solicitacao' },
  CANCELAR_SOLICITACAO: { evento: 'cancelamento', escrita: true, comando: 'cancelou', numeroDe: 'envio', campoNumero: 'co_seq_solicitacao' },
  REENVIAR_REGULACAO: { evento: 'devolucao-regulacao', escrita: true },
  Confirma: { evento: 'confirmacao-comparecimento', escrita: true },
  Falta: { evento: 'falta', escrita: true },
  LST_VAGAS: { evento: 'marcacao-vagas', escrita: false },
  EXIBIR_FICHA: { evento: 'ficha-solicitacao', escrita: false },
  EXPORTAR_ESCALAS: { evento: 'export-escalas', escrita: false },
  INSERIR_PREPARO: { evento: 'preparo', escrita: true },
  ATUALIZAR_PREPARO: { evento: 'preparo', escrita: true },
  EXCLUIR_PREPARO: { evento: 'preparo', escrita: true },
};

// Campos que NUNCA saem do navegador nem aparecem no painel.
export const CAMPOS_SENSIVEIS = new Set(['senha', 'senha_256']);
