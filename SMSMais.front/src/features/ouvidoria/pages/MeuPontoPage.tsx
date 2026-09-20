import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Inbox } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Input } from '@/shared/ui/Input';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { useManifestacoes } from '@/features/ouvidoria/api/queries';
import { TabelaManifestacoes } from '@/features/ouvidoria/components/TabelaManifestacoes';
import type { FiltroManifestacoes, OuvidoriaStatus } from '@/features/ouvidoria/types';

const TAMANHOS = [25, 50, 100] as const;

type Recorte = 'pendentes' | 'respondidas' | 'todas';
const RECORTES: { id: Recorte; rotulo: string; status: OuvidoriaStatus[] | undefined }[] = [
  { id: 'pendentes', rotulo: 'Aguardando minha área', status: ['Encaminhada'] },
  { id: 'respondidas', rotulo: 'Respondidas pela área', status: ['RespondidaPelaArea', 'EmValidacao'] },
  { id: 'todas', rotulo: 'Todas do meu ponto', status: undefined },
];

/**
 * Fila do membro de ponto de resposta (`OuvidoriaPontoResposta`): o backend já recorta pelos pontos
 * de que ele é membro e NUNCA manda dados do manifestante — aqui a coluna também não existe.
 */
export function MeuPontoPage() {
  const navigate = useNavigate();
  const [recorte, setRecorte] = useState<Recorte>('pendentes');
  const [busca, setBusca] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(TAMANHOS[0]);

  const filtro = useMemo<FiltroManifestacoes>(
    () => ({ status: RECORTES.find((r) => r.id === recorte)?.status, busca, pagina, tamanho }),
    [recorte, busca, pagina, tamanho],
  );
  const lista = useManifestacoes(filtro);

  return (
    <div className="space-y-4">
      <div>
        <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
          <Inbox className="h-5 w-5 text-red-600" aria-hidden="true" />
          Meu ponto de resposta
        </h1>
        <p className="text-sm text-slate-500">
          Manifestações encaminhadas à sua unidade ou área. Você vê o relato e responde; quem manifestou fica com a ouvidoria.
        </p>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-64">
          <label htmlFor="mp-busca" className="sr-only">Buscar por protocolo</label>
          <Input
            id="mp-busca"
            placeholder="Protocolo…"
            value={busca}
            onChange={(e) => {
              setBusca(e.target.value);
              setPagina(1);
            }}
          />
        </div>
        <div className="w-60">
          <label htmlFor="mp-recorte" className="sr-only">Recorte</label>
          <Select
            id="mp-recorte"
            value={recorte}
            onChange={(e) => {
              setRecorte(e.target.value as Recorte);
              setPagina(1);
            }}
          >
            {RECORTES.map((r) => (
              <option key={r.id} value={r.id}>
                {r.rotulo}
              </option>
            ))}
          </Select>
        </div>
      </div>

      {lista.isError ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </p>
      ) : null}

      <TabelaManifestacoes
        dados={lista.data?.itens ?? []}
        carregando={lista.isLoading}
        ocultarManifestante
        aoAbrir={(m) => navigate(`/app/ouvidoria/meu-ponto/${m.id}`)}
        vazio="Nada encaminhado ao seu ponto neste recorte."
      />
      <Paginacao
        pagina={pagina}
        tamanho={tamanho}
        total={lista.data?.total ?? 0}
        aoMudarPagina={setPagina}
        aoMudarTamanho={(t) => {
          setTamanho(t);
          setPagina(1);
        }}
        tamanhos={TAMANHOS}
      />
    </div>
  );
}
