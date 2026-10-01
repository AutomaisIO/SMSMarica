import { useEffect, useRef, useState } from 'react';
import { CheckCircle2, ImagePlus, X } from 'lucide-react';
import { TelaSimulada } from '@/features/manual/components/TelaSimulada';

type Print = { url: string; nome: string; local: boolean };

const TIPOS = [
  { valor: 'Bug', rotulo: 'Bug', dica: 'Algo está com erro ou se comportando de forma errada.' },
  { valor: 'Mudanca', rotulo: 'Mudança', dica: 'Ajuste em algo que já existe.' },
  { valor: 'Sugestao', rotulo: 'Sugestão', dica: 'Ideia de melhoria ou funcionalidade nova.' },
  { valor: 'Duvida', rotulo: 'Dúvida', dica: 'Dúvida sobre como usar o sistema.' },
];

/** Print desenhado — para quem quer ver o efeito sem tirar um print de verdade. */
const PRINT_EXEMPLO =
  'data:image/svg+xml;utf8,' +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="160" height="120"><rect width="160" height="120" fill="#f1f5f9"/>' +
      '<rect x="10" y="10" width="140" height="14" rx="3" fill="#cbd5e1"/><rect x="10" y="34" width="90" height="8" rx="2" fill="#e2e8f0"/>' +
      '<rect x="10" y="50" width="120" height="8" rx="2" fill="#e2e8f0"/><rect x="10" y="80" width="60" height="22" rx="4" fill="#94a3b8"/>' +
      '<text x="80" y="115" font-size="9" text-anchor="middle" fill="#64748b" font-family="sans-serif">print (exemplo)</text></svg>',
  );

function ehCampoDeTexto(el: EventTarget | null): boolean {
  return el instanceof HTMLTextAreaElement || el instanceof HTMLInputElement;
}

/**
 * Réplica do modal "Abrir ticket" com o Ctrl+V de verdade: um print colado aqui aparece só no
 * navegador de quem está lendo (URL local), nunca é enviado. Mesma regra da tela: cópia que traz
 * texto junto, colada num campo de texto, cola o texto.
 */
export function SimulacaoAbrirTicket() {
  const [tipo, setTipo] = useState('Bug');
  const [titulo, setTitulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [prints, setPrints] = useState<Print[]>([]);
  const [aberto, setAberto] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const printsRef = useRef(prints);
  printsRef.current = prints;

  useEffect(() => () => printsRef.current.forEach((p) => p.local && URL.revokeObjectURL(p.url)), []);

  function reiniciar() {
    prints.forEach((p) => p.local && URL.revokeObjectURL(p.url));
    setTipo('Bug');
    setTitulo('');
    setDescricao('');
    setPrints([]);
    setAberto(false);
    setErro(null);
  }

  function aoColar(e: React.ClipboardEvent) {
    const imagens = Array.from(e.clipboardData.files).filter((f) => f.type.startsWith('image/'));
    if (imagens.length === 0) return;
    if (e.clipboardData.types.includes('text/plain') && ehCampoDeTexto(e.target)) return;
    e.preventDefault();
    const n = prints.length;
    setPrints((atuais) => [
      ...atuais,
      ...imagens.map((f, i) => ({ url: URL.createObjectURL(f), nome: `print-${n + i + 1}.png`, local: true })),
    ]);
  }

  function enviar() {
    if (!titulo.trim() || !descricao.trim()) {
      setErro('Preencha o título e a descrição.');
      return;
    }
    setErro(null);
    setAberto(true);
  }

  const dica = TIPOS.find((t) => t.valor === tipo)?.dica;

  return (
    <TelaSimulada
      titulo="Simulação: abrir um ticket com print"
      descricao="Pode colar um print de verdade com Ctrl+V: ele aparece só aqui, nada é enviado."
      aoReiniciar={reiniciar}
    >
      {aberto ? (
        <div className="space-y-3 rounded-theme-lg border border-emerald-200 bg-emerald-50 p-4">
          <p className="flex items-center gap-2 text-sm font-semibold text-emerald-800">
            <CheckCircle2 className="h-4 w-4" /> Ticket aberto com sucesso! (exemplo)
          </p>
          <p className="text-sm text-emerald-900">
            Na tela de verdade ele entraria em <strong>Meus Tickets</strong> como <strong>#128</strong>, com status{' '}
            <strong>Aberto</strong>
            {prints.length > 0 ? ` e ${prints.length} ${prints.length === 1 ? 'anexo' : 'anexos'}` : ''}. Clique em
            Recomeçar para tentar de novo.
          </p>
        </div>
      ) : (
        <div
          tabIndex={-1}
          onPaste={aoColar}
          className="space-y-3 rounded-theme-lg border border-gray-200 bg-white p-4 outline-none focus:ring-2 focus:ring-primary-200"
        >
          <p className="text-sm font-semibold text-gray-800">Abrir ticket</p>
          <label className="block text-xs font-medium text-gray-600">
            O que você quer fazer?
            <select
              value={tipo}
              onChange={(e) => setTipo(e.target.value)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
            >
              {TIPOS.map((t) => (
                <option key={t.valor} value={t.valor}>{t.rotulo}</option>
              ))}
            </select>
            <span className="mt-0.5 block font-normal text-gray-500">{dica}</span>
          </label>
          <label className="block text-xs font-medium text-gray-600">
            Título
            <input
              value={titulo}
              onChange={(e) => setTitulo(e.target.value)}
              placeholder="Ex.: Botão Salvar da ficha não responde (exemplo)"
              className="mt-1 block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
            />
          </label>
          <label className="block text-xs font-medium text-gray-600">
            Descrição
            <textarea
              value={descricao}
              rows={3}
              onChange={(e) => setDescricao(e.target.value)}
              placeholder="O que você fez, o que esperava e o que aconteceu."
              className="mt-1 block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
            />
          </label>
          <div className="space-y-2">
            <p className="text-xs font-medium text-gray-600">Anexos (opcional)</p>
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => setPrints((p) => [...p, { url: PRINT_EXEMPLO, nome: 'print (exemplo)', local: false }])}
                className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                title="Na tela de verdade, este botão abre a escolha de arquivo; aqui ele só acrescenta um print de exemplo."
              >
                <ImagePlus className="h-3.5 w-3.5" /> Anexar imagem
              </button>
              <span className="text-xs text-gray-400">ou cole com Ctrl+V</span>
            </div>
            {prints.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {prints.map((p, i) => (
                  <div key={`${p.url}-${i}`} className="relative">
                    <img src={p.url} alt={p.nome} className="h-16 w-16 rounded-md object-cover ring-1 ring-gray-200" />
                    <button
                      type="button"
                      onClick={() => {
                        if (p.local) URL.revokeObjectURL(p.url);
                        setPrints((atuais) => atuais.filter((_, j) => j !== i));
                      }}
                      className="absolute -right-1.5 -top-1.5 rounded-full bg-gray-800 p-0.5 text-white"
                      title="Remover"
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
          {erro && <p className="text-xs text-primary-700">{erro}</p>}
          <div className="flex justify-end">
            <button
              type="button"
              onClick={enviar}
              className="rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700"
            >
              Abrir ticket
            </button>
          </div>
        </div>
      )}
    </TelaSimulada>
  );
}
