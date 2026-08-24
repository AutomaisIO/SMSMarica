import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { EditorRichText } from '@/shared/ui/EditorRichText';
import {
  useAtualizarTemplate,
  useCadastrarTemplate,
  useTemplatePorId,
} from '@/features/laudo-templates/api/queries';
import type { SalvarLaudoTemplatePayload } from '@/features/laudo-templates/types';
import { ConstrutorEstrutura } from '@/features/laudo-templates/components/ConstrutorEstrutura';
import { PainelChecklist } from '@/features/laudos/checklist/PainelChecklist';
import { estruturaVazia } from '@/features/laudos/checklist/types';
import type { EstruturaChecklist, RespostasChecklist } from '@/features/laudos/checklist/types';

export function LaudoTemplateEditorPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const ehNovo = !id || id === 'novo';

  const detalhe = useTemplatePorId(ehNovo ? null : id ?? null);
  const cadastrar = useCadastrarTemplate();
  const atualizar = useAtualizarTemplate();

  const [nome, setNome] = useState('');
  const [categoria, setCategoria] = useState('');
  const [descricao, setDescricao] = useState('');
  const [html, setHtml] = useState('');
  const [json, setJson] = useState('{}');
  // Por ora só temos laudo de checklist (mamografia) → template novo já nasce
  // como checklist. Pode-se desmarcar para fazer um template de texto livre.
  const [estrutura, setEstrutura] = useState<EstruturaChecklist | null>(() =>
    ehNovo ? estruturaVazia() : null,
  );
  const [preview, setPreview] = useState<RespostasChecklist | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (detalhe.data) {
      setNome(detalhe.data.nome);
      setCategoria(detalhe.data.categoria);
      setDescricao(detalhe.data.descricao ?? '');
      setHtml(detalhe.data.conteudoHtml);
      setJson(detalhe.data.conteudoJson);
      if (detalhe.data.estruturaJson) {
        try {
          setEstrutura(JSON.parse(detalhe.data.estruturaJson) as EstruturaChecklist);
        } catch {
          setEstrutura(null);
        }
      }
    }
  }, [detalhe.data]);

  // Mantém o preview em sincronia com a estrutura sendo editada (preserva o que já foi marcado).
  useEffect(() => {
    setPreview((p) =>
      estrutura ? { estrutura, marcados: p?.marcados ?? {}, biRadsFinal: p?.biRadsFinal ?? null } : null,
    );
  }, [estrutura]);

  function alternarChecklist(usar: boolean) {
    setEstrutura(usar ? (estrutura ?? estruturaVazia()) : null);
  }

  async function salvar() {
    setErro(null);
    const payload: SalvarLaudoTemplatePayload = {
      nome: nome.trim(),
      categoria: categoria.trim(),
      descricao: descricao.trim() || null,
      conteudoJson: json,
      conteudoHtml: html,
      estruturaJson: estrutura && estrutura.secoes.length ? JSON.stringify(estrutura) : null,
    };
    if (!payload.nome) {
      setErro('Nome é obrigatório.');
      return;
    }
    if (!payload.categoria) {
      setErro('Categoria é obrigatória.');
      return;
    }

    try {
      if (ehNovo) {
        const novoId = await cadastrar.mutateAsync(payload);
        navigate(`/app/laudo-templates/${novoId}`, { replace: true });
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
            onClick={() => navigate('/app/laudo-templates')}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" />
            Voltar para templates
          </button>
          <h1 className="mt-1 text-2xl font-semibold text-gray-900">
            {ehNovo ? 'Novo template de laudo' : 'Editar template'}
          </h1>
        </div>
        <Button onClick={salvar} disabled={salvando}>
          {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          {ehNovo ? 'Criar template' : 'Salvar alterações'}
        </Button>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-3">
        <Campo label="Nome do template" htmlFor="nome" className="sm:col-span-2">
          <Input
            id="nome"
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            placeholder="Ex.: Mamografia bilateral — BI-RADS"
          />
        </Campo>
        <Campo label="Categoria" htmlFor="categoria">
          <Input
            id="categoria"
            value={categoria}
            onChange={(e) => setCategoria(e.target.value)}
            placeholder="Ex.: Mamografia"
          />
        </Campo>
        <Campo label="Descrição (opcional)" htmlFor="descricao" className="sm:col-span-3">
          <Input
            id="descricao"
            value={descricao}
            onChange={(e) => setDescricao(e.target.value)}
            placeholder="Observação livre para identificar o template na hora de escolher."
          />
        </Campo>
      </div>

      <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm font-medium text-gray-800">
          <input
            type="checkbox"
            checked={estrutura !== null}
            onChange={(e) => alternarChecklist(e.target.checked)}
            className="h-4 w-4 accent-primary-600"
          />
          Este template usa checklist estruturado
        </label>
        <p className="mt-1 text-xs text-gray-500">
          Com o checklist, a profissional marca as frases (ou preenche a tabela de medidas) e o
          sistema monta o texto do laudo e calcula a conclusão — BI-RADS na mamografia, OMS/DMO na
          densitometria.
        </p>

        {estrutura ? (
          <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Estrutura
              </div>
              <ConstrutorEstrutura estrutura={estrutura} aoMudar={setEstrutura} />
            </div>
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Pré-visualização
              </div>
              {preview && estrutura.secoes.length ? (
                <PainelChecklist respostas={preview} aoMudar={setPreview} />
              ) : (
                <p className="rounded-md border border-dashed border-gray-200 p-4 text-sm text-gray-400">
                  Adicione seções e frases para ver o checklist aqui.
                </p>
              )}
            </div>
          </div>
        ) : null}

        {estrutura ? <ImportarExportarEstrutura estrutura={estrutura} aoAplicar={setEstrutura} /> : null}
      </div>

      {estrutura === null ? (
        <EditorRichText
          valorHtml={html}
          aoMudar={(v) => {
            setHtml(v.html);
            setJson(v.json);
          }}
          placeholder="Escreva o template do laudo aqui…"
          alturaMinima="500px"
        />
      ) : null}

      <p className="text-xs text-gray-500">
        Editar este template não altera laudos já criados — o conteúdo é copiado para o laudo no momento da emissão.
      </p>
    </div>
  );
}

/**
 * Estrutura em JSON, para copiar e colar. Serve para montar um template complexo
 * fora da tela (uma tabela de 7 colunas com todas as notas é penoso no
 * construtor) e, principalmente, para LEVAR um template de uma instância para
 * outra — cada município tem seu próprio banco ([ADR-0043]), então não há como
 * compartilhar template pelo banco.
 */
function ImportarExportarEstrutura({
  estrutura,
  aoAplicar,
}: {
  estrutura: EstruturaChecklist;
  aoAplicar: (e: EstruturaChecklist) => void;
}) {
  const [aberto, setAberto] = useState(false);
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  function abrir() {
    setTexto(JSON.stringify(estrutura, null, 2));
    setErro(null);
    setAberto((a) => !a);
  }

  function aplicar() {
    try {
      const lido = JSON.parse(texto) as EstruturaChecklist;
      if (!Array.isArray(lido?.secoes)) {
        setErro('JSON válido, mas não parece uma estrutura de checklist (falta "secoes").');
        return;
      }
      setErro(null);
      aoAplicar(lido);
    } catch {
      setErro('JSON inválido — confira se o texto foi colado por inteiro.');
    }
  }

  return (
    <div className="mt-4 border-t border-gray-100 pt-3">
      <button
        type="button"
        onClick={abrir}
        className="text-xs font-medium text-gray-500 hover:text-gray-700"
      >
        {aberto ? '− ' : '+ '}Estrutura em JSON (copiar / colar)
      </button>

      {aberto ? (
        <div className="mt-2 space-y-2">
          <textarea
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            spellCheck={false}
            rows={14}
            className="block w-full resize-y rounded-md border border-gray-300 bg-gray-900 px-3 py-2 font-mono text-xs leading-relaxed text-gray-100 focus:outline-none"
          />
          {erro ? <p className="text-xs text-red-600">{erro}</p> : null}
          <div className="flex items-center gap-2">
            <Button variante="outline" onClick={aplicar}>
              Aplicar ao construtor
            </Button>
            <span className="text-xs text-gray-500">
              Substitui a estrutura atual. Só grava ao salvar o template.
            </span>
          </div>
        </div>
      ) : null}
    </div>
  );
}
