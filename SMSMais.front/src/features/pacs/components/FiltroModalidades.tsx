import { useEffect, useRef, useState } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { MODALIDADES_DICOM, type ModalidadeDicom } from '@/features/tipos-exame/types';

/**
 * Modalidades que aparecem no filtro da tela de Exames. É o subconjunto que o PACS de fato
 * recebe — "Indefinida" não é modalidade DICOM (é tipo sem configuração, nunca chega numa
 * imagem) e as demais entram quando o município passar a ter o equipamento.
 */
const MODALIDADES_FILTRAVEIS: ModalidadeDicom[] = ['MG', 'US', 'OT', 'CR', 'DX', 'CT', 'MR'];

type Props = {
  id?: string;
  selecionadas: ModalidadeDicom[];
  aoMudar: (modalidades: ModalidadeDicom[]) => void;
};

/**
 * Checkbox de modalidade — o recorte de quem lauda só parte do que chega (ex.: MG e OT).
 * Vira `ModalitiesInStudy` no QIDO, então o filtro acontece no PACS: a página vem cheia e a
 * paginação continua honesta. Nada selecionado = todas.
 */
export function FiltroModalidades({ id, selecionadas, aoMudar }: Props) {
  const [aberto, setAberto] = useState(false);
  const container = useRef<HTMLDivElement>(null);

  // Fecha ao clicar fora — o painel some sem precisar de um botão "OK".
  useEffect(() => {
    if (!aberto) return;
    function aoClicarFora(e: MouseEvent) {
      if (container.current && !container.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto]);

  function alternar(m: ModalidadeDicom) {
    aoMudar(selecionadas.includes(m) ? selecionadas.filter((x) => x !== m) : [...selecionadas, m]);
  }

  const rotulo =
    selecionadas.length === 0
      ? 'Todas'
      : selecionadas.length <= 3
        ? selecionadas.join(', ')
        : `${selecionadas.length} selecionadas`;

  return (
    <div className="relative" ref={container}>
      <button
        id={id}
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="input flex items-center justify-between gap-2 text-left"
      >
        <span className={selecionadas.length === 0 ? 'text-gray-500' : 'truncate font-medium'}>{rotulo}</span>
        <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" />
      </button>

      {aberto ? (
        <div className="absolute z-20 mt-1 w-56 rounded-md border border-gray-200 bg-white p-1 shadow-lg">
          {MODALIDADES_FILTRAVEIS.map((m) => {
            const marcada = selecionadas.includes(m);
            const rotuloModalidade = MODALIDADES_DICOM.find((x) => x.valor === m)?.rotulo ?? m;
            return (
              <button
                key={m}
                type="button"
                onClick={() => alternar(m)}
                className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm text-gray-700 hover:bg-gray-50"
              >
                <span
                  className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border ${
                    marcada ? 'border-primary-600 bg-primary-600 text-white' : 'border-gray-300 bg-white'
                  }`}
                >
                  {marcada ? <Check className="h-3 w-3" /> : null}
                </span>
                <span className="truncate">{rotuloModalidade}</span>
              </button>
            );
          })}
          {selecionadas.length > 0 ? (
            <button
              type="button"
              onClick={() => aoMudar([])}
              className="mt-1 w-full border-t border-gray-100 px-2 py-1.5 text-left text-xs font-medium text-gray-500 hover:text-gray-700"
            >
              Limpar seleção
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
