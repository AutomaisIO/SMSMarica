import { useState } from 'react';
import { Link2, Loader2, Wand2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useListarPendentes,
  useTiposExameOpcoes,
  useVincularMapeamento,
} from '@/features/mapeamento-sigtap/api/queries';
import type { PendenteMapeamento } from '@/features/mapeamento-sigtap/types';

export function MapeamentoSigtapPage() {
  const pendentes = useListarPendentes();
  const tipos = useTiposExameOpcoes();
  const vincular = useVincularMapeamento();

  const [alvo, setAlvo] = useState<PendenteMapeamento | null>(null);
  const [tipoId, setTipoId] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<string | null>(null);

  function abrir(p: PendenteMapeamento) {
    setAlvo(p);
    setTipoId('');
    setErro(null);
  }

  function confirmarVinculo(ev: React.FormEvent) {
    ev.preventDefault();
    if (!alvo || !tipoId) return;
    setErro(null);
    vincular.mutate(
      { sigtapCodigo: alvo.sigtapCodigo, tipoExameId: tipoId },
      {
        onSuccess: (r) => {
          setResultado(`${r.atualizados} exame(s) vinculado(s) ao tipo — SIGTAP ${alvo.sigtapCodigo}.`);
          setAlvo(null);
        },
        onError: (e) => setErro(extrairMensagemDeErro(e)),
      },
    );
  }

  const colunas: Coluna<PendenteMapeamento>[] = [
    { chave: 'sigtap', cabecalho: 'SIGTAP', render: (p) => <span className="font-mono text-sm">{p.sigtapCodigo || '—'}</span> },
    {
      chave: 'proc',
      cabecalho: 'Procedimento (SISREG)',
      render: (p) => <span className="font-medium text-gray-900">{p.procedimentoTexto ?? '—'}</span>,
    },
    { chave: 'qtd', cabecalho: 'Pendentes', render: (p) => <span className="tabular-nums">{p.quantidade}</span> },
    {
      chave: 'acao',
      cabecalho: 'Ação',
      render: (p) => (
        <button
          type="button"
          onClick={() => abrir(p)}
          className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-white px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-50"
        >
          <Link2 className="h-3.5 w-3.5" />
          Vincular tipo
        </button>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Wand2 className="h-6 w-6 text-primary-600" />
          Mapeamento SIGTAP → Tipo de exame
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Exames de imagem importados sem tipo. Vincule um tipo ao código SIGTAP — todos os pendentes com esse
          código são atualizados de uma vez. Para criar um tipo novo, use “Tipos de exame”.
        </p>
      </header>

      {resultado ? (
        <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
          {resultado}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={pendentes.data ?? []}
        chaveLinha={(p) => p.sigtapCodigo || (p.procedimentoTexto ?? '')}
        carregando={pendentes.isPending}
        vazio="Nenhum exame pendente de mapeamento. 🎉"
      />

      <Modal
        aberto={!!alvo}
        aoFechar={() => setAlvo(null)}
        titulo="Vincular tipo de exame"
      >
        <form onSubmit={confirmarVinculo} className="space-y-4">
          <div className="rounded-md bg-gray-50 px-3 py-2 text-sm text-gray-700">
            <div>
              <span className="text-gray-500">SIGTAP:</span> <span className="font-mono">{alvo?.sigtapCodigo}</span>
            </div>
            <div>
              <span className="text-gray-500">Procedimento:</span> {alvo?.procedimentoTexto ?? '—'}
            </div>
            <div>
              <span className="text-gray-500">Exames pendentes:</span> {alvo?.quantidade}
            </div>
          </div>

          <Campo label="Tipo de exame" htmlFor="map-tipo" required>
            <select
              id="map-tipo"
              className="input"
              value={tipoId}
              onChange={(e) => setTipoId(e.target.value)}
              required
            >
              <option value="">Selecione…</option>
              {(tipos.data ?? []).map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nome}
                </option>
              ))}
            </select>
          </Campo>

          {erro ? <p className="text-sm text-red-600">{erro}</p> : null}

          <div className="flex justify-end gap-2">
            <Button type="button" variante="secundaria" onClick={() => setAlvo(null)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={!tipoId || vincular.isPending}>
              {vincular.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Vincular e preencher {alvo?.quantidade ?? ''}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
