import { useEffect, useState } from 'react';
import { ChevronLeft, ChevronRight, Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useBuscarEstudos } from '@/features/pacs/api/queries';
import type { Estudo, FiltroBusca, TipoBuscaNome } from '@/features/pacs/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  aoSelecionar: (estudo: Estudo) => void;
};

const LIMITE_PADRAO = 10;

function hojeIso(): string {
  const agora = new Date();
  const ano = agora.getFullYear();
  const mes = String(agora.getMonth() + 1).padStart(2, '0');
  const dia = String(agora.getDate()).padStart(2, '0');
  return `${ano}-${mes}-${dia}`;
}

/** Filtro inicial usado no auto-load: ignora o que o usuário tem no form
 *  e devolve os últimos N exames independente de data. */
function filtroInicial(): FiltroBusca {
  return {
    nome: '',
    tipoBuscaNome: 'inicio',
    dataInicial: '',
    dataFinal: '',
    limite: LIMITE_PADRAO,
    offset: 0,
  };
}

export function PacsBuscaModal({ aberto, aoFechar, aoSelecionar }: Props) {
  const [nome, setNome] = useState('');
  const [tipoBuscaNome, setTipoBuscaNome] = useState<TipoBuscaNome>('inicio');
  const [dataInicial, setDataInicial] = useState(() => hojeIso());
  const [dataFinal, setDataFinal] = useState(() => hojeIso());
  const [limite, setLimite] = useState(LIMITE_PADRAO);
  // Filtro que de fato está aplicado na lista exibida (separado do estado do
  // form, que pode estar sendo digitado). Carrega o auto-load na primeira abertura.
  const [filtroAplicado, setFiltroAplicado] = useState<FiltroBusca>(() => filtroInicial());
  const [pagina, setPagina] = useState(1);

  const busca = useBuscarEstudos();

  useEffect(() => {
    if (!aberto) return;
    const hoje = hojeIso();
    setNome('');
    setTipoBuscaNome('inicio');
    setDataInicial(hoje);
    setDataFinal(hoje);
    setLimite(LIMITE_PADRAO);
    const inicial = filtroInicial();
    setFiltroAplicado(inicial);
    setPagina(1);
    busca.mutate(inicial);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aberto]);

  function aoSubmeter(e: React.FormEvent) {
    e.preventDefault();
    const novo: FiltroBusca = {
      nome,
      tipoBuscaNome,
      dataInicial,
      dataFinal,
      limite,
      offset: 0,
    };
    setFiltroAplicado(novo);
    setPagina(1);
    busca.mutate(novo);
  }

  function trocarPagina(direcao: -1 | 1) {
    const novaPagina = pagina + direcao;
    if (novaPagina < 1) return;
    const novo: FiltroBusca = {
      ...filtroAplicado,
      offset: (novaPagina - 1) * filtroAplicado.limite,
    };
    setFiltroAplicado(novo);
    setPagina(novaPagina);
    busca.mutate(novo);
  }

  // Ordena do mais novo para o mais velho como rede de segurança — o backend
  // já pede orderby=-StudyDate,-StudyTime, mas garantimos a ordem aqui também.
  const estudos = [...(busca.data ?? [])].sort((a, b) => {
    const chaveA = `${a.studyDate}${a.studyTime}`;
    const chaveB = `${b.studyDate}${b.studyTime}`;
    return chaveB.localeCompare(chaveA);
  });

  // QIDO-RS não expõe count barato, então inferimos "tem próxima" pela heurística:
  // se a página veio cheia (length === limite), provavelmente há mais.
  const temProximaPagina = estudos.length === filtroAplicado.limite;
  const mostrarPaginacao = pagina > 1 || temProximaPagina;

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
          <>
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

            {mostrarPaginacao ? (
              <div className="mt-3 flex items-center justify-between">
                <span className="text-sm text-gray-500">Página {pagina}</span>
                <div className="flex gap-2">
                  <Button
                    type="button"
                    variante="ghost"
                    onClick={() => trocarPagina(-1)}
                    disabled={pagina <= 1 || busca.isPending}
                  >
                    <ChevronLeft className="mr-1 h-4 w-4" />
                    Anterior
                  </Button>
                  <Button
                    type="button"
                    variante="ghost"
                    onClick={() => trocarPagina(1)}
                    disabled={!temProximaPagina || busca.isPending}
                  >
                    Próxima
                    <ChevronRight className="ml-1 h-4 w-4" />
                  </Button>
                </div>
              </div>
            ) : null}
          </>
        ) : null}
      </div>
    </Modal>
  );
}
