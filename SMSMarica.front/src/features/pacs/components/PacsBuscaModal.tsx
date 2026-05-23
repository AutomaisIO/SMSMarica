import { useEffect, useState } from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useBuscarEstudos } from '@/features/pacs/api/queries';
import type { Estudo, TipoBuscaNome } from '@/features/pacs/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  aoSelecionar: (estudo: Estudo) => void;
};

const LIMITE_PADRAO = 10;

export function PacsBuscaModal({ aberto, aoFechar, aoSelecionar }: Props) {
  const [nome, setNome] = useState('');
  const [tipoBuscaNome, setTipoBuscaNome] = useState<TipoBuscaNome>('inicio');
  const [dataInicial, setDataInicial] = useState('');
  const [dataFinal, setDataFinal] = useState('');
  const [limite, setLimite] = useState(LIMITE_PADRAO);

  const busca = useBuscarEstudos();

  useEffect(() => {
    if (!aberto) return;
    setNome('');
    setTipoBuscaNome('inicio');
    setDataInicial('');
    setDataFinal('');
    setLimite(LIMITE_PADRAO);
    busca.mutate({
      nome: '',
      tipoBuscaNome: 'inicio',
      dataInicial: '',
      dataFinal: '',
      limite: LIMITE_PADRAO,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aberto]);

  function aoSubmeter(e: React.FormEvent) {
    e.preventDefault();
    busca.mutate({ nome, tipoBuscaNome, dataInicial, dataFinal, limite });
  }

  // Ordena do mais novo para o mais velho. O backend já pede orderby=-StudyDate
  // quando não há filtro, mas alguns dcm4chee ignoram o parâmetro — então
  // garantimos a ordem aqui também (data + hora desc).
  const estudos = [...(busca.data ?? [])].sort((a, b) => {
    const chaveA = `${a.studyDate}${a.studyTime}`;
    const chaveB = `${b.studyDate}${b.studyTime}`;
    return chaveB.localeCompare(chaveA);
  });

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Buscar exame" largura="lg">
      <form onSubmit={aoSubmeter} className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <label className="block sm:col-span-2">
            <span className="mb-1 block text-sm font-medium text-gray-700">Nome do paciente</span>
            <Input
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              placeholder={
                tipoBuscaNome === 'inicio' ? 'Início do nome...' : 'Qualquer parte do nome...'
              }
            />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-gray-700">Modo de busca</span>
            <Select
              value={tipoBuscaNome}
              onChange={(e) => setTipoBuscaNome(e.target.value as TipoBuscaNome)}
            >
              <option value="inicio">Somente início</option>
              <option value="qualquer">Qualquer ocorrência</option>
            </Select>
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
