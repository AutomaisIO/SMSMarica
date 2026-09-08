import { useState } from 'react';
import { Loader2, Plus } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';

import { criarRegra } from '../api/regulacaoApi';
import type {
  RegraElegibilidade,
  RespostaRegraRegulacao,
  SalvarRegra,
  TipoRegraRegulacao,
} from '../tiposSolicitacao';

/**
 * Cadastro de uma regra à mão (plano 03, tarefa 4.5).
 *
 * <p><b>Por que precisa existir:</b> metade do manual não casa com o catálogo, e o que o
 * importador traz vem por procedimento — não há como cadastrar pelo CSV uma exigência que a
 * regulação de Maricá criou, nem corrigir uma que o extrator partiu ao meio. Sem este
 * formulário, a única porta era o `/docs`.</p>
 *
 * <p><b>O formulário muda conforme o tipo</b>, porque cada um decide de um jeito diferente:
 * dedutível olha o cadastro do paciente, não-dedutível pergunta ao solicitante e precisa saber
 * qual resposta barra, documental pede anexo. Mostrar os três conjuntos ao mesmo tempo faria
 * parecer que dá para combinar — e não dá.</p>
 */
export function FormularioRegra({
  procedimentoId,
  aoCriar,
  aoCancelar,
}: {
  procedimentoId: string;
  aoCriar: (regra: RegraElegibilidade) => void;
  aoCancelar: () => void;
}) {
  const [tipo, setTipo] = useState<TipoRegraRegulacao>('NaoDedutivel');
  const [descricao, setDescricao] = useState('');
  const [fonte, setFonte] = useState('');
  const [pergunta, setPergunta] = useState('');
  const [respostaBloqueia, setRespostaBloqueia] = useState<RespostaRegraRegulacao>('Nao');
  const [documentoRotulo, setDocumentoRotulo] = useState('');
  const [validadeDias, setValidadeDias] = useState('');
  const [idadeMin, setIdadeMin] = useState('');
  const [idadeMax, setIdadeMax] = useState('');
  const [sexo, setSexo] = useState('');
  const [obrigatorio, setObrigatorio] = useState(true);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const numero = (v: string) => (v.trim() === '' ? null : Number(v));

  async function salvar() {
    setErro(null);
    setSalvando(true);
    try {
      const payload: SalvarRegra = {
        procedimentoId,
        procedimentoOrigemId: null,
        sistema: null,
        tipo,
        severidade: 'Bloqueia',
        descricao: descricao.trim(),
        fonte: fonte.trim() || null,
        idadeMinAnos: tipo === 'Dedutivel' ? numero(idadeMin) : null,
        idadeMaxAnos: tipo === 'Dedutivel' ? numero(idadeMax) : null,
        sexo: tipo === 'Dedutivel' && sexo ? sexo : null,
        exigeCpf: false,
        cidsPermitidos: null,
        cidsExcluidos: null,
        pergunta: tipo === 'NaoDedutivel' ? pergunta.trim() || descricao.trim() : null,
        respostaBloqueia: tipo === 'NaoDedutivel' ? respostaBloqueia : null,
        naoSeiVira: null,
        documentoRotulo:
          tipo === 'Documental' ? documentoRotulo.trim() || descricao.trim() : null,
        tipoExameId: null,
        validadeDias: tipo === 'Documental' ? numero(validadeDias) : null,
        obrigatorio,
        ordem: 0,
      };
      aoCriar(await criarRegra(payload));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <div className="space-y-3 rounded-lg border border-slate-300 bg-slate-50 p-4">
      <h2 className="text-sm font-semibold text-slate-800">Nova regra</h2>

      <Campo rotulo="Tipo">
        <select
          value={tipo}
          onChange={(e) => setTipo(e.target.value as TipoRegraRegulacao)}
          className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:border-red-500 focus:outline-none"
        >
          <option value="NaoDedutivel">Pergunta ao solicitante</option>
          <option value="Documental">Exige documento</option>
          <option value="Dedutivel">O sistema decide (idade/sexo)</option>
          <option value="Informativa">Só informa (nunca barra)</option>
        </select>
      </Campo>

      <Campo
        rotulo="Texto da regra"
        ajuda="Copie do manual, literal. É o que aparece na tela de quem solicita."
      >
        <textarea
          value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          rows={3}
          maxLength={2000}
          className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:border-red-500 focus:outline-none"
        />
      </Campo>

      {tipo === 'NaoDedutivel' && (
        <>
          <Campo rotulo="Pergunta" ajuda="Em branco, usa o texto da regra.">
            <Input value={pergunta} onChange={(e) => setPergunta(e.target.value)} maxLength={500} />
          </Campo>
          <Campo
            rotulo="Qual resposta barra"
            ajuda="Critério de inclusão barra quem responde “não”; critério de exclusão barra quem responde “sim”."
          >
            <select
              value={respostaBloqueia}
              onChange={(e) => setRespostaBloqueia(e.target.value as RespostaRegraRegulacao)}
              className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:border-red-500 focus:outline-none"
            >
              <option value="Nao">Não — é critério de inclusão</option>
              <option value="Sim">Sim — é critério de exclusão</option>
            </select>
          </Campo>
        </>
      )}

      {tipo === 'Documental' && (
        <>
          <Campo rotulo="Nome do documento" ajuda="Em branco, usa o texto da regra.">
            <Input
              value={documentoRotulo}
              onChange={(e) => setDocumentoRotulo(e.target.value)}
              maxLength={200}
            />
          </Campo>
          <Campo rotulo="Validade (dias)" ajuda="Em branco, o documento não vence.">
            <Input
              type="number"
              min={1}
              value={validadeDias}
              onChange={(e) => setValidadeDias(e.target.value)}
              className="max-w-[8rem]"
            />
          </Campo>
        </>
      )}

      {tipo === 'Dedutivel' && (
        <>
          <div className="flex gap-3">
            <Campo rotulo="Idade mínima">
              <Input
                type="number"
                min={0}
                value={idadeMin}
                onChange={(e) => setIdadeMin(e.target.value)}
                className="max-w-[7rem]"
              />
            </Campo>
            <Campo rotulo="Idade máxima">
              <Input
                type="number"
                min={0}
                value={idadeMax}
                onChange={(e) => setIdadeMax(e.target.value)}
                className="max-w-[7rem]"
              />
            </Campo>
          </div>
          <Campo rotulo="Sexo">
            <select
              value={sexo}
              onChange={(e) => setSexo(e.target.value)}
              className="max-w-[10rem] rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:border-red-500 focus:outline-none"
            >
              <option value="">indiferente</option>
              <option value="F">feminino</option>
              <option value="M">masculino</option>
            </select>
          </Campo>
          <p className="rounded border border-amber-300 bg-amber-50 p-2 text-xs text-amber-900">
            As regras se somam: o pedido precisa passar em <strong>todas</strong> as regras ativas
            do procedimento. Duas faixas etárias diferentes ativas ao mesmo tempo deixam o
            procedimento intransitável — se o manual traz faixas alternativas, cadastre como
            pergunta, não como dedutível.
          </p>
        </>
      )}

      <Campo rotulo="Fonte" ajuda="De onde veio, para quem for revisar depois. Ex.: CRECE p.12.">
        <Input value={fonte} onChange={(e) => setFonte(e.target.value)} maxLength={300} />
      </Campo>

      <label className="flex items-center gap-2 text-sm text-slate-700">
        <input
          type="checkbox"
          checked={obrigatorio}
          onChange={(e) => setObrigatorio(e.target.checked)}
          className="size-4 accent-red-600"
        />
        Obrigatória
      </label>

      {erro && <p className="text-sm text-red-700">{erro}</p>}

      <p className="text-xs text-slate-500">
        A regra nasce <strong>ativa</strong> — diferente das importadas do manual, que entram
        inativas. Cadastrar à mão já é a decisão de que ela vale.
      </p>

      <div className="flex gap-2">
        <Button onClick={() => void salvar()} disabled={salvando || !descricao.trim()}>
          {salvando ? (
            <Loader2 className="mr-2 size-4 animate-spin" />
          ) : (
            <Plus className="mr-2 size-4" />
          )}
          Criar regra
        </Button>
        <Button variante="secundaria" onClick={aoCancelar} disabled={salvando}>
          Cancelar
        </Button>
      </div>
    </div>
  );
}

function Campo({
  rotulo,
  ajuda,
  children,
}: {
  rotulo: string;
  ajuda?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1">
      <p className="text-sm font-medium text-slate-700">{rotulo}</p>
      {ajuda ? <p className="text-xs text-slate-500">{ajuda}</p> : null}
      {children}
    </div>
  );
}
