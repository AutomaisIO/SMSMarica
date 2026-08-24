import { useState, type KeyboardEvent } from 'react';
import { X } from 'lucide-react';
import { Input } from '@/shared/ui/Input';

type Props = {
  itens: string[];
  onChange: (itens: string[]) => void;
  placeholder?: string;
  id?: string;
};

/**
 * Campo de tags simples: digite e pressione Enter (ou ',') para adicionar.
 * Usado para listas curtas de strings (alergias, medicamentos, etc.).
 */
export function ListaChips({ itens, onChange, placeholder, id }: Props) {
  const [valor, setValor] = useState('');

  function adicionar(texto: string) {
    const t = texto.trim();
    if (!t) return;
    if (itens.includes(t)) return;
    onChange([...itens, t]);
    setValor('');
  }

  function remover(idx: number) {
    onChange(itens.filter((_, i) => i !== idx));
  }

  function aoTeclar(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      adicionar(valor);
    } else if (e.key === 'Backspace' && valor === '' && itens.length > 0) {
      remover(itens.length - 1);
    }
  }

  return (
    <div className="space-y-2">
      <Input
        id={id}
        value={valor}
        onChange={(e) => setValor(e.target.value)}
        onKeyDown={aoTeclar}
        onBlur={() => adicionar(valor)}
        placeholder={placeholder ?? 'Digite e pressione Enter'}
      />
      {itens.length > 0 ? (
        <ul className="flex flex-wrap gap-2">
          {itens.map((it, idx) => (
            <li
              key={`${it}-${idx}`}
              className="inline-flex items-center gap-1 rounded-full bg-red-50 px-3 py-1 text-xs text-red-800"
            >
              {it}
              <button
                type="button"
                aria-label={`Remover ${it}`}
                onClick={() => remover(idx)}
                className="text-red-700 hover:text-red-900"
              >
                <X className="h-3 w-3" />
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
