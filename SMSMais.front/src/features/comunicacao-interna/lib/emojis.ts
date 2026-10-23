/**
 * Emojis do seletor da Comunicação interna — lista própria, sem biblioteca: os mais usados por
 * categoria e uma categoria de trabalho/saúde. Unicode puro (o Windows desenha com a fonte dele).
 */
export type CategoriaEmoji = { id: string; rotulo: string; icone: string; emojis: string[] };

export const CATEGORIAS_EMOJI: CategoriaEmoji[] = [
  {
    id: 'carinhas',
    rotulo: 'Carinhas',
    icone: '😀',
    emojis: [
      '😀', '😃', '😄', '😁', '😆', '😅', '😂', '🤣', '😊', '🙂', '😉', '😍', '🥰', '😘', '😋', '😎',
      '🤩', '🥳', '🤗', '🤔', '🤨', '😐', '😑', '😶', '🙄', '😏', '😬', '😮‍💨', '😌', '😔', '😪', '😴',
      '😷', '🤒', '🤕', '🥵', '🥶', '😵', '🤯', '😕', '😟', '🙁', '😮', '😲', '😳', '🥺', '😢', '😭',
      '😱', '😤', '😡', '🤬', '🫡', '🤫', '🤭', '🫢', '😇', '🤓', '🧐', '🙃', '🫠',
    ],
  },
  {
    id: 'gestos',
    rotulo: 'Gestos',
    icone: '👍',
    emojis: [
      '👍', '👎', '👌', '✌️', '🤞', '🤟', '🤙', '👋', '🤚', '✋', '🖐️', '👏', '🙌', '🫶', '🤝', '🙏',
      '💪', '👊', '✊', '🤛', '🤜', '☝️', '👆', '👇', '👉', '👈', '🫵', '✍️', '👀', '🧠', '🫂', '🙋',
      '🙋‍♀️', '🙋‍♂️', '🤷', '🤷‍♀️', '🤷‍♂️', '🙆', '🙅', '💁',
    ],
  },
  {
    id: 'coracoes',
    rotulo: 'Corações',
    icone: '❤️',
    emojis: [
      '❤️', '🧡', '💛', '💚', '💙', '💜', '🤎', '🖤', '🤍', '💖', '💗', '💓', '💞', '💕', '💝', '❤️‍🩹',
      '💯', '✨', '⭐', '🌟', '🔥', '🎉', '🎊', '🎂', '🏆', '🥇', '🌹', '🌻', '☀️', '🌈',
    ],
  },
  {
    id: 'trabalho',
    rotulo: 'Trabalho e saúde',
    icone: '🏥',
    emojis: [
      '✅', '☑️', '✔️', '❌', '❎', '⚠️', '🚨', '❗', '❓', '‼️', '🔴', '🟠', '🟡', '🟢', '🔵', '⚪',
      '📌', '📍', '📎', '🖇️', '📅', '🗓️', '📋', '📝', '🗒️', '📄', '📑', '🧾', '📁', '🗂️', '📊', '📈',
      '📞', '☎️', '📱', '📠', '💻', '🖨️', '✉️', '📧', '📨', '🔔', '🔕', '⏳', '⌛', '⏰', '🕐', '⏱️',
      '🏥', '🚑', '🩺', '💊', '💉', '🩹', '🩻', '🩸', '🧪', '🦷', '🦴', '🫀', '🫁', '🧬', '🦽', '🛏️',
      '👩‍⚕️', '👨‍⚕️', '🧑‍⚕️', '👩‍💼', '👨‍💼', '🚐', '🚗', '🗺️', '🔑', '🔒', '🚀', '💡', '🎯', '🆗', '🆕', '🔄',
    ],
  },
  {
    id: 'objetos',
    rotulo: 'Objetos e símbolos',
    icone: '💡',
    emojis: [
      '☕', '🍵', '🥤', '🍕', '🍔', '🍰', '🍫', '🍎', '🎁', '🎈', '🎵', '📷', '🔍', '🔧', '🔨', '⚙️',
      '🧹', '🧯', '🔋', '🔌', '💰', '💳', '🏠', '🏢', '🚪', '🪑', '🌧️', '⛈️', '🌡️', '❄️', '🌙', '⚡',
      '➡️', '⬅️', '⬆️', '⬇️', '↩️', '🔁', '➕', '➖', '✖️', '🟰', '#️⃣', '🔟', '🆘', '⛔', '🚫', '♻️',
    ],
  },
];

const CHAVE_RECENTES = 'smsmarica.interno.emojisRecentes';
const MAX_RECENTES = 24;

/** Recentes desta pessoa neste navegador (conveniência — some com o cache, sem problema). */
export function lerRecentes(): string[] {
  try {
    const bruto = localStorage.getItem(CHAVE_RECENTES);
    const lista = bruto ? (JSON.parse(bruto) as unknown) : [];
    return Array.isArray(lista) ? lista.filter((x): x is string => typeof x === 'string').slice(0, MAX_RECENTES) : [];
  } catch {
    return [];
  }
}

export function registrarRecente(emoji: string): void {
  try {
    const nova = [emoji, ...lerRecentes().filter((e) => e !== emoji)].slice(0, MAX_RECENTES);
    localStorage.setItem(CHAVE_RECENTES, JSON.stringify(nova));
  } catch {
    /* navegador sem armazenamento — recentes não persistem */
  }
}
