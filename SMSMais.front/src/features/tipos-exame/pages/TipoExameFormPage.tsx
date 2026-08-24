import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Loader2, Save, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import {
  useAtualizarTipoExame,
  useCadastrarTipoExame,
  useTipoExamePorId,
} from '@/features/tipos-exame/api/queries';
import { MODALIDADES_DICOM, type ModalidadeDicom } from '@/features/tipos-exame/types';
import { BuscaProcedimentoSigtap } from '@/features/tipos-exame/components/BuscaProcedimentoSigtap';
import type { ProcedimentoSigtap } from '@/features/procedimentos-sigtap/types';

export function TipoExameFormPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const ehNovo = !id || id === 'novo';

  const detalhe = useTipoExamePorId(ehNovo ? null : id ?? null);
  const cadastrar = useCadastrarTipoExame();
  const atualizar = useAtualizarTipoExame();

  const [nome, setNome] = useState('');
  const [codigoSisreg, setCodigoSisreg] = useState('');
  const [procedimento, setProcedimento] = useState<ProcedimentoSigtap | null>(null);
  const [modalidade, setModalidade] = useState<ModalidadeDicom>('Indefinida');
  const [requestedDesc, setRequestedDesc] = useState('');
  const [scheduledDesc, setScheduledDesc] = useState('');
  const [protocolos, setProtocolos] = useState('');
  const [tempo, setTempo] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [enviarParaWorklist, setEnviarParaWorklist] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [buscaAberta, setBuscaAberta] = useState(false);

  useEffect(() => {
    if (detalhe.data) {
      const t = detalhe.data;
      setNome(t.nome);
      setCodigoSisreg(t.codigoSisreg ?? '');
      setProcedimento(
        t.procedimentoSigtapId
          ? {
              id: t.procedimentoSigtapId,
              codigo: t.procedimentoSigtapCodigo,
              nome: t.procedimentoSigtapNome,
              grupo: '',
              subgrupo: '',
              forma: '',
              descricao: '',
              ativo: true,
              competenciaInicio: '',
              competenciaFim: null,
            }
          : null,
      );
      setModalidade(t.modalidadeDicom);
      setRequestedDesc(t.requestedProcedureDescription);
      setScheduledDesc(t.scheduledProcedureStepDescription);
      setProtocolos(t.codigosProtocolo.join(', '));
      setTempo(t.tempoEstimadoMinutos?.toString() ?? '');
      setAtivo(t.ativo);
      setEnviarParaWorklist(t.enviarParaWorklist);
    }
  }, [detalhe.data]);

  async function salvar() {
    setErro(null);
    if (!nome.trim()) return setErro('Nome é obrigatório.');
    // O SIGTAP deixou de ser obrigatório: é correlação de faturamento, não identidade.
    if (!requestedDesc.trim()) return setErro('Descrição do procedimento (DICOM) é obrigatória.');

    const payload = {
      // Sempre em maiúsculas: o nome é o do SISREG e a lista, o exame e o laudo mostram ele.
      nome: nome.trim().toUpperCase(),
      codigoSisreg: codigoSisreg.trim() || null,
      procedimentoSigtapId: procedimento?.id ?? null,
      modalidadeDicom: modalidade,
      requestedProcedureDescription: requestedDesc.trim(),
      scheduledProcedureStepDescription: (scheduledDesc.trim() || requestedDesc.trim()),
      codigosProtocolo: protocolos
        .split(',')
        .map((p) => p.trim())
        .filter((p) => p.length > 0),
      tempoEstimadoMinutos: tempo ? parseInt(tempo, 10) : null,
      unidadePadraoId: null,
      ativo,
      enviarParaWorklist,
    };

    try {
      if (ehNovo) {
        const novoId = await cadastrar.mutateAsync(payload);
        navigate(`/app/tipos-exame/${novoId}`, { replace: true });
      } else if (id) {
        await atualizar.mutateAsync({ id, payload });
      }
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const salvando = cadastrar.isPending || atualizar.isPending;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <button
            type="button"
            onClick={() => navigate('/app/tipos-exame')}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" />
            Voltar
          </button>
          <h1 className="mt-1 text-2xl font-semibold text-gray-900">
            {ehNovo ? 'Novo tipo de exame' : 'Editar tipo de exame'}
          </h1>
        </div>
        <Button onClick={salvar} disabled={salvando}>
          {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          {ehNovo ? 'Criar' : 'Salvar'}
        </Button>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-3">
        <Campo label="Nome do procedimento (SISREG)" htmlFor="nome" className="sm:col-span-2">
          <Input
            id="nome"
            value={nome}
            onChange={(e) => setNome(e.target.value.toUpperCase())}
            placeholder="Ex.: ULTRASONOGRAFIA DE JOELHO DIREITO"
          />
          <p className="mt-1 text-xs text-gray-500">
            Escreva exatamente como o SISREG escreve. É este nome que aparece na lista de
            solicitações, no exame e no laudo — e é por ele que a importação reconhece o
            procedimento.
          </p>
        </Campo>
        <Campo label="Código no SISREG" htmlFor="codigo-sisreg">
          <Input
            id="codigo-sisreg"
            value={codigoSisreg}
            onChange={(e) => setCodigoSisreg(e.target.value)}
            placeholder="Ex.: 1402055"
          />
          <p className="mt-1 text-xs text-gray-500">
            Opcional — o SISREG deixa em branco em boa parte dos agendamentos.
          </p>
        </Campo>
        <Campo label="Modalidade DICOM" htmlFor="modalidade">
          <Select
            id="modalidade"
            value={modalidade}
            onChange={(e) => setModalidade(e.target.value as ModalidadeDicom)}
          >
            {MODALIDADES_DICOM.map((m) => (
              <option key={m.valor} value={m.valor}>
                {m.rotulo}
              </option>
            ))}
          </Select>
        </Campo>

        <Campo label="Procedimento SIGTAP (faturamento)" htmlFor="proc-sigtap" className="sm:col-span-3">
          <div className="flex items-center gap-3">
            <div className="flex-1 min-w-0 rounded-md border border-gray-300 bg-gray-50 px-3 py-2 text-sm">
              {procedimento ? (
                <span>
                  <span className="font-mono text-gray-500 mr-2">{procedimento.codigo}</span>
                  <span className="font-medium text-gray-900">{procedimento.nome}</span>
                </span>
              ) : (
                <span className="text-gray-400">Sem correlação — não impede nada.</span>
              )}
            </div>
            <Button type="button" variante="outline" onClick={() => setBuscaAberta(true)}>
              <Search className="mr-2 h-4 w-4" />
              Buscar
            </Button>
            {procedimento ? (
              <Button type="button" variante="outline" onClick={() => setProcedimento(null)}>
                Limpar
              </Button>
            ) : null}
          </div>
          <p className="mt-1 text-xs text-gray-500">
            Opcional, e só serve ao faturamento. <strong>Não</strong> use o código que vem no
            arquivo do SISREG: ele sai de uma versão defasada da tabela e aponta para outro
            procedimento no SIGTAP oficial. Confira na tabela oficial antes de ligar.
          </p>
        </Campo>

        <Campo label="Requested Procedure Description (DICOM)" htmlFor="reqdesc" className="sm:col-span-2">
          <Input
            id="reqdesc"
            value={requestedDesc}
            onChange={(e) => setRequestedDesc(e.target.value)}
            placeholder="Ex.: MAMOGRAFIA BILATERAL"
          />
        </Campo>
        <Campo label="Tempo estimado (min)" htmlFor="tempo">
          <Input
            id="tempo"
            type="number"
            min={1}
            max={600}
            value={tempo}
            onChange={(e) => setTempo(e.target.value)}
            placeholder="Ex.: 15"
          />
        </Campo>

        <Campo label="Scheduled Procedure Step Description" htmlFor="schdesc" className="sm:col-span-3">
          <Input
            id="schdesc"
            value={scheduledDesc}
            onChange={(e) => setScheduledDesc(e.target.value)}
            placeholder="Se vazio, usa o mesmo do Requested Procedure"
          />
        </Campo>

        <Campo label="Códigos de protocolo (separados por vírgula)" htmlFor="proto" className="sm:col-span-3">
          <Input
            id="proto"
            value={protocolos}
            onChange={(e) => setProtocolos(e.target.value)}
            placeholder="Ex.: CC, MLO"
          />
        </Campo>

        {!ehNovo ? (
          <Campo label="Visível no formulário" htmlFor="ativo">
            <label className="flex h-10 items-center gap-2 text-sm text-gray-700">
              <input
                id="ativo"
                type="checkbox"
                checked={ativo}
                onChange={(e) => setAtivo(e.target.checked)}
              />
              Ativo
            </label>
          </Campo>
        ) : null}

        <Campo label="Integração PACS" htmlFor="worklist" className="sm:col-span-3">
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              id="worklist"
              type="checkbox"
              checked={enviarParaWorklist}
              onChange={(e) => setEnviarParaWorklist(e.target.checked)}
            />
            Enviar para a Worklist do PACS
          </label>
          <p className="mt-1 text-xs text-gray-500">
            Quando desligado, as solicitações deste tipo são criadas normalmente, mas{' '}
            <strong>não</strong> são enviadas à Modality Worklist (o equipamento não as recebe).
            Útil para pausar a integração enquanto o equipamento não está mapeado.
          </p>
        </Campo>
      </div>

      <BuscaProcedimentoSigtap
        aberto={buscaAberta}
        aoFechar={() => setBuscaAberta(false)}
        // Não preenche mais o nome a partir do SIGTAP: o nome é do SISREG, e foi exatamente essa
        // cópia que fez o rótulo do SIGTAP virar o nome exibido de procedimentos diferentes.
        aoSelecionar={(p) => setProcedimento(p)}
      />
    </div>
  );
}
