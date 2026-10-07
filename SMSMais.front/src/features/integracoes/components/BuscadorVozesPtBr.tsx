import { useState } from 'react';
import { Loader2, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useUsarVozBiblioteca, useVozesBiblioteca } from '@/features/integracoes/api';

const IDADES: Record<string, string> = { young: 'jovem', middle_aged: 'adulto', old: 'idoso' };

/** Busca vozes pt-BR na biblioteca do ElevenLabs e deixa escolher uma (adiciona à conta e usa). */
export function BuscadorVozesPtBr({ podeEditar }: { podeEditar: boolean }) {
  const [aberto, setAberto] = useState(false);
  const [genero, setGenero] = useState('');
  const [idade, setIdade] = useState('');
  const [buscaInput, setBuscaInput] = useState('');
  const [busca, setBusca] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [escolhida, setEscolhida] = useState<string | null>(null);

  const vozes = useVozesBiblioteca(
    { genero: genero || undefined, idade: idade || undefined, busca: busca || undefined },
    aberto,
  );
  const usar = useUsarVozBiblioteca();
  const lista = vozes.data ?? [];

  function aoUsar(v: { publicOwnerId: string; vozId: string; nome: string }) {
    setErro(null);
    usar.mutate(v, {
      onSuccess: () => setEscolhida(v.vozId),
      onError: (e) => setErro(extrairMensagemDeErro(e)),
    });
  }

  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50/60 p-3">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex items-center gap-2 text-sm font-medium text-primary-700"
      >
        <Search className="h-4 w-4" />
        Buscar vozes em português do Brasil
      </button>

      {aberto ? (
        <div className="mt-3 space-y-3">
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <Select value={genero} onChange={(e) => setGenero(e.target.value)} aria-label="Gênero">
              <option value="">Qualquer gênero</option>
              <option value="female">Feminina</option>
              <option value="male">Masculina</option>
            </Select>
            <Select value={idade} onChange={(e) => setIdade(e.target.value)} aria-label="Idade">
              <option value="">Qualquer idade</option>
              <option value="young">Jovem</option>
              <option value="middle_aged">Adulto</option>
              <option value="old">Idoso</option>
            </Select>
            <div className="flex gap-2">
              <Input
                value={buscaInput}
                onChange={(e) => setBuscaInput(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), setBusca(buscaInput))}
                placeholder="Buscar por nome…"
              />
              <Button type="button" variante="secundaria" onClick={() => setBusca(buscaInput)}>
                Buscar
              </Button>
            </div>
          </div>

          {vozes.isLoading ? (
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Buscando…
            </div>
          ) : null}

          {vozes.isError ? (
            <p className="text-sm text-red-700">{extrairMensagemDeErro(vozes.error)}</p>
          ) : null}

          {!vozes.isLoading && lista.length === 0 ? (
            <p className="text-sm text-gray-500">
              Nenhuma voz encontrada (confira se a chave está salva e tem acesso a vozes).
            </p>
          ) : null}

          {erro ? <p className="text-sm text-red-700">{erro}</p> : null}

          <ul className="max-h-80 space-y-2 overflow-y-auto">
            {lista.map((v) => (
              <li
                key={v.vozId}
                className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-gray-200 bg-white px-3 py-2"
              >
                <div className="min-w-0">
                  <span className="block truncate text-sm font-medium text-gray-900">{v.nome}</span>
                  <span className="text-[11px] text-gray-500">
                    {v.genero === 'male' ? 'masculina' : v.genero === 'female' ? 'feminina' : '—'}
                    {v.idade ? ` · ${IDADES[v.idade] ?? v.idade}` : ''}
                  </span>
                </div>
                <div className="flex items-center gap-2">
                  {v.previewUrl ? (
                    <audio controls preload="none" src={v.previewUrl} className="h-8 w-44" />
                  ) : null}
                  {podeEditar ? (
                    <Button
                      type="button"
                      tamanho="sm"
                      variante={escolhida === v.vozId ? 'secundaria' : 'primaria'}
                      disabled={usar.isPending}
                      onClick={() => aoUsar({ publicOwnerId: v.publicOwnerId, vozId: v.vozId, nome: v.nome })}
                    >
                      {escolhida === v.vozId ? 'Em uso' : 'Usar esta voz'}
                    </Button>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
