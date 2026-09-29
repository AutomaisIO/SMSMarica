import { Check } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Input } from '@/shared/ui/Input';
import {
  CORES_FANTASIA,
  CORES_RENAVAM,
  contornoDaCor,
  luminancia,
  resolverCorVeiculo,
} from '@/features/veiculos/lib/corVeiculo';

type Props = {
  id: string;
  valor: string;
  aoMudar: (valor: string) => void;
};

/**
 * Paleta com as cores do documento do veículo (tabela do RENAVAM) + campo livre. Clicar numa
 * amostra grava o nome da cor; o texto livre continua valendo ("Prata metálico", "Azul marinho").
 */
export function SeletorCorVeiculo({ id, valor, aoMudar }: Props) {
  const resolvida = resolverCorVeiculo(valor);

  return (
    <div className="space-y-2">
      <div role="radiogroup" aria-label="Cores do documento do veículo" className="flex flex-wrap gap-1.5">
        {CORES_RENAVAM.map((c) => {
          const ativa = resolvida.base?.nome === c.nome;
          const fundo =
            c.nome === 'Fantasia'
              ? `conic-gradient(${CORES_FANTASIA.join(',')},${CORES_FANTASIA[0]})`
              : c.hex;
          return (
            <button
              key={c.nome}
              type="button"
              role="radio"
              aria-checked={ativa}
              aria-label={c.nome}
              title={c.nome}
              onClick={() => aoMudar(c.nome)}
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-full border-2 transition hover:scale-110',
                'focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500 focus-visible:ring-offset-1',
                ativa && 'ring-2 ring-primary-600 ring-offset-2',
              )}
              style={{ background: fundo, borderColor: contornoDaCor(c.hex) }}
            >
              {ativa ? (
                <Check
                  className={cn(
                    'h-3.5 w-3.5',
                    luminancia(c.hex) > 0.45 && c.nome !== 'Fantasia' ? 'text-gray-900' : 'text-white',
                  )}
                  strokeWidth={3}
                />
              ) : null}
            </button>
          );
        })}
      </div>
      <Input
        id={id}
        value={valor}
        onChange={(e) => aoMudar(e.target.value)}
        maxLength={40}
        required
        placeholder="Ou escreva: Prata metálico, Azul marinho…"
      />
      {valor.trim() && !resolvida.reconhecida ? (
        <p className="text-xs text-amber-700">
          Cor não reconhecida — o desenho fica cinza. Escolha na paleta ou escreva um nome como
          “Prata” ou “Azul marinho”.
        </p>
      ) : null}
    </div>
  );
}
