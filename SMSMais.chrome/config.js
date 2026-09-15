// Configuração da extensão. Em modo desenvolvedor, ajuste API_BASE para onde o
// backend está rodando (sua máquina, ou api.smsmarica.online).

export const CONFIG = {
  // Para onde as capturas são enviadas. Sem barra no fim.
  API_BASE: 'https://api.smsmarica.online',

  // Origem do painel do SMSMais, de onde a extensão lê a sessão (login).
  PAINEL_ORIGIN: 'https://smsmarica.online',

  // Envio em lote das capturas.
  LOTE_MAX_ITENS: 40, // envia ao juntar este tanto
  LOTE_INTERVALO_MS: 4000, // ...ou a cada este tempo
  LOTE_MAX_BYTES: 3_000_000, // teto de segurança do JSON de um lote (antes de comprimir)

  // Teto do HTML de resposta guardado por tela (antes de comprimir). "Manter tudo",
  // mas sem estourar memória numa tela gigante.
  RESPOSTA_MAX_CHARS: 2_000_000,

  // Marca de fallback quando a API não responde /publico/instituicao.
  MARCA_PADRAO: { nomeCurto: 'SMSMarica', corPrimaria: '#C8102E' },
};

// Endpoint de ingestão (a criar no backend).
export const ROTA_CAPTURAS = '/extensao/sisreg/capturas';
