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
  // mas sem estourar memória numa tela gigante. Quando corta, a captura vai marcada
  // com `truncado: true` e o tamanho original — para nunca analisarmos um pedaço
  // achando que é o todo.
  RESPOSTA_MAX_CHARS: 4_000_000,

  // Fila em espera (API fora, ou sem login). O service worker do MV3 é DESLIGADO pelo
  // Chrome quando fica ocioso: a fila é persistida em storage.local a cada mudança,
  // senão o que não foi enviado morre junto com ele.
  BUFFER_MAX_ITENS: 5000,
  BUFFER_MAX_BYTES: 80_000_000, // teto real da fila (precisa de "unlimitedStorage")
  BUFFER_SALVAR_APOS_MS: 800, // debounce da gravação da fila

  // Requisição que nunca "termina" (streaming, aba fechada no meio) não pode ficar
  // presa: passado este tempo ela é enfileirada como está.
  REQUISICAO_TIMEOUT_MS: 90_000,

  // Conta do PILOTO: sem ela, a extensão só envia se alguém tiver logado no painel neste Chrome
  // (é o `auth-content.js` quem lê a sessão). Numa máquina de consultório isso não acontece, e
  // sem o blur a fila só cresce. Preenchendo aqui, o service worker se autentica sozinho e
  // RENOVA quando o token vence — o token do SMSMarica dura 8 h, então um valor cravado morreria
  // no meio do dia seguinte.
  //
  // ATENÇÃO: NÃO PUBLICAR com isto preenchido. O `publicar.sh` espelha este arquivo no repositório
  // PÚBLICO SMSMais/extensao-sisreg — ele agora recusa publicar se houver credencial aqui.
  // Use um usuário DEDICADO ao piloto (não o de uma pessoa), com o mínimo de permissão.
  PILOTO: { email: '', senha: '' },

  // Marca de fallback quando a API não responde /publico/instituicao.
  MARCA_PADRAO: { nomeCurto: 'SMSMarica', corPrimaria: '#C8102E' },
};

// Endpoint de ingestão.
export const ROTA_CAPTURAS = '/extensao/sisreg/capturas';

// Sítios observados. Cada captura é carimbada com o `id` (origem), para o hub distinguir de
// qual sistema veio. Para monitorar um sistema novo: acrescente uma linha aqui E o host em
// manifest.json (host_permissions + os dois content_scripts).
//
// `blur`  — true bloqueia o site até o login no SMSMais. É o que GARANTE que há sessão para
//           enviar: sem login, a fila só cresce e nada chega ao hub.
// `modo`:
//  - 'minimo'  → envia SÓ o comando + o nº da solicitação (sem PII); o resto é descartado no
//                background antes de qualquer envio. É o que a apresentação usa no SISREG.
//  - 'analise' → captura BURRA: manda envio+retorno crus (ainda estamos aprendendo o sistema).
// `tudo`  — só vale em 'analise'. Liga a captura PROFUNDA: cabeçalhos de ida e volta, cadeia de
//           302, cliques e __doPostBack, troca de URL sem recarregar, downloads de relatório e
//           re-captura da tela quando o AJAX reescreve o DOM. É o necessário para reconstruir
//           uma operação de ASP.NET WebForms/Telerik depois, fora da hora.
// `verbos` — reconhecedor de operações do sistema (arquivo próprio). Com ele, além da matéria
//           bruta sai um EVENTO nomeado (paciente-criado / paciente-agendado /
//           paciente-acolhido) com as chaves já extraídas. Sem ele, só matéria bruta.
export const SITIOS = [
  { id: 'sisreg', label: 'SISREG', host: 'sisregiii.saude.gov.br', blur: true, modo: 'minimo' },
  // `blur: true` é DELIBERADO, inclusive no PC do médico (decidido em 22/09/2026). Ele bloqueia
  // o site até haver sessão do SMSMarica — e é justamente isso que GARANTE que a captura seja
  // enviada, em vez de engordar a fila em silêncio. O risco de travar alguém no meio do
  // atendimento é coberto por gente: o operador autentica na instalação e há técnico no local.
  { id: 'ecosistemas', label: 'Prime (Eco)', host: 'marica.ecosistemas.com.br', blur: true, modo: 'analise', tudo: true, verbos: 'prime' },
];
