import { useState } from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useBuscarEstudos } from '@/features/pacs/api/queries';
import type { Estudo } from '@/features/pacs/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  aoSelecionar: (estudo: Estudo) => void;
};

export function PacsBuscaModal({ aberto, aoFechar, aoSelecionar }: Props) {
  const [nome, setNome] = useState('');
  const [dataInicial, setDataInicial] = useState('');
  const [dataFinal, setDataFinal] = useState('');
  const [limite, setLimite] = useState(10);

  const busca = useBuscarEstudos();

  function aoSubmeter(e: React.FormEvent) {
    e.preventDefault();
    busca.mutate({ nome, dataInicial, dataFinal, limite });
  }

  const estudos = busca.data ?? [];

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Buscar exame" largura="lg">
      <form onSubmit={aoSubmeter} className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <label className="block sm:col-span-2">
            <span className="mb-1 block text-sm font-medium text-gray-700">Nome do paciente</span>
            <Input
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              placeholder="Qualquer parte do nome..."
            />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-gray-700">Data inicial</span>
            <Input type="date" value={dataInicial} onChange={(e) => setDataInicial(e.target.value)} />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-gray-700">Data final</span>
            <Input type="date" value={dataFinal} onChange={(e) => setDataFinal(e.target.value)} />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-gray-700">Limite</span>
            <Input
              type="number"
              min={1}
              max={100}
              value={limite}
              onChange={(e) => setLimite(Number(e.target.value) || 10)}
            />
          </label>
        </div>

        <div className="flex justify-end gap-2">
          <Button type="button" variante="ghost" onClick={aoFechar}>
            Cancelar
          </Button>
          <Button type="submit" disabled={busca.isPending}>
            <Search className="mr-2 h-4 w-4" />
            {busca.isPending ? 'Buscando...' : 'Buscar'}
          </Button>
        </div>
      </form>

      <div className="mt-5">
        {busca.isError ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(busca.error)}
          </div>
        ) : null}

        {busca.isSuccess && estudos.length === 0 ? (
          <p className="text-sm text-gray-500">Nenhum exame encontrado.</p>
        ) : null}

        {estudos.length > 0 ? (
          <ul className="divide-y divide-gray-100 rounded-md border border-gray-200">
            {estudos.map((estudo) => (
              <li key={estudo.studyInstanceUID}>
                <button
                  type="button"
                  onClick={() => aoSelecionar(estudo)}
                  className="flex w-full flex-col items-start gap-0.5 px-4 py-3 text-left hover:bg-gray-50"
                >
                  <span className="font-medium text-gray-900">{estudo.patientName || 'Sem nome'}</span>
                  <span className="text-sm text-gray-500">
                    {[estudo.modalidade, estudo.studyDescription, estudo.studyDateFormatado]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </Modal>
  );
}
