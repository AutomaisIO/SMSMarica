import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Loader2, Plus, Upload } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';

import { BuscaProcedimento } from '../components/BuscaProcedimento';
import { FormularioRegra } from '../components/FormularioRegra';
import { ativarRegra, excluirRegra, importarRegrasCsv, listarRegras } from '../api/regulacaoApi';
import type { ImportacaoRegrasResultado, RegraElegibilidade } from '../tiposSolicitacao';
import type { RegulacaoProcedimentoItem } from '../types';

const ROTULO_TIPO: Record<string, string> = {
  Dedutivel: 'O sistema decide',
  NaoDedutivel: 'Pergunta ao solicitante',
  Documental: 'Exige documento',
  Informativa: 'Só informa',
};

/**
 * Curadoria das regras de elegibilidade (plano 03, módulo 51).
 *
 * <p><b>O importador traz as 1.169 regras dos manuais INATIVAS.</b> Esta tela é onde elas viram
 * regra de verdade — e a decisão é clínica, não automatizável: o extrator classificou 974 linhas
 * como pergunta, e ativar todas transformaria o wizard num interrogatório. O trabalho aqui é
 * escolher as poucas que realmente barram, marcar as demais como informativas e descartar o
 * resto.</p>
 */
export function RegrasElegibilidadePage() {
  const qc = useQueryClient();
  const [procedimento, setProcedimento] = useState<RegulacaoProcedimentoItem | null>(null);
  const [mostrarInativas, setMostrarInativas] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ImportacaoRegrasResultado | null>(null);
  const [criando, setCriando] = useState(false);
  const arquivoRef = useRef<HTMLInputElement>(null);

  const regras = useQuery({
    queryKey: ['regulacao', 'regras', procedimento?.id, mostrarInativas],
    queryFn: () => listarRegras(procedimento!.id, mostrarInativas),
    enabled: !!procedimento,
  });

  function invalidar() {
    void qc.invalidateQueries({ queryKey: ['regulacao', 'regras'] });
  }

  const alternar = useMutation({
    mutationFn: ({ id, ativo }: { id: string; ativo: boolean }) => ativarRegra(id, ativo),
    onSuccess: invalidar,
  });
  const excluir = useMutation({ mutationFn: excluirRegra, onSuccess: invalidar });

  const importar = useMutation({
    mutationFn: importarRegrasCsv,
    onSuccess: (r) => {
      setResultado(r);
      invalidar();
    },
    onError: (e) => setErro(extrairMensagemDeErro(e)),
  });

  const lista = regras.data ?? [];
  const ativas = lista.filter((r) => r.ativo).length;

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center gap-3">
        <BookOpen className="size-6 text-red-700" />
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Regras de elegibilidade</h1>
          <p className="text-sm text-slate-600">
            O que o manual da regulação exige por procedimento.
          </p>
        </div>
        <div className="ml-auto">
          <input
            ref={arquivoRef}
            type="file"
            accept=".csv,text/csv"
            className="hidden"
            onChange={(e) => {
              const arquivo = e.target.files?.[0];
              if (arquivo) {
                setErro(null);
                importar.mutate(arquivo);
              }
              e.target.value = '';
            }}
          />
          <Button
            variante="secundaria"
            onClick={() => arquivoRef.current?.click()}
            disabled={importar.isPending}
          >
            {importar.isPending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Upload className="size-4" />
            )}
            Importar CSV do manual
          </Button>
        </div>
      </header>

      {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-800">{erro}</p>}

      {resultado && (
        <div className="rounded-lg border border-sky-300 bg-sky-50 p-3 text-sm text-sky-900">
          <p>
            <strong>{resultado.criadas}</strong> regra(s) importada(s) de {resultado.lidas} linha(s).
            {resultado.semRecurso > 0 && ` ${resultado.semRecurso} sem recurso no catálogo.`}
          </p>
          {resultado.avisos.length > 0 && (
            <ul className="mt-1 space-y-0.5 text-xs">
              {resultado.avisos.slice(0, 8).map((a) => (
                <li key={a}>• {a}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div className="rounded-lg border border-slate-200 bg-white p-4">
        <BuscaProcedimento value={procedimento} onChange={setProcedimento} />
        {procedimento && (
          <p className="mt-2 text-sm text-slate-700">
            <strong>{procedimento.nome}</strong>
          </p>
        )}
      </div>

      {procedimento && (
        <>
          <div className="flex flex-wrap items-center gap-3 text-sm">
            <span className="text-slate-600">
              {ativas} ativa(s) de {lista.length}
            </span>
            <label className="flex items-center gap-1.5 text-slate-700">
              <input
                type="checkbox"
                checked={mostrarInativas}
                onChange={(e) => setMostrarInativas(e.target.checked)}
              />
              mostrar inativas
            </label>
            <Button
              variante="secundaria"
              className="ml-auto"
              onClick={() => setCriando((c) => !c)}
            >
              <Plus className="size-4" />
              Nova regra
            </Button>
          </div>

          {criando && (
            <FormularioRegra
              procedimentoId={procedimento.id}
              aoCancelar={() => setCriando(false)}
              aoCriar={() => {
                setCriando(false);
                invalidar();
              }}
            />
          )}

          {regras.isLoading && <p className="text-sm text-slate-500">Carregando…</p>}

          {!regras.isLoading && lista.length === 0 && (
            <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">
              Nenhuma regra cadastrada para este procedimento. Importe o CSV do manual ou use
              “Nova regra”.
            </p>
          )}

          <ul className="space-y-2">
            {lista.map((r) => (
              <LinhaRegra
                key={r.id}
                regra={r}
                aoAlternar={() => alternar.mutate({ id: r.id, ativo: !r.ativo })}
                aoExcluir={() => excluir.mutate(r.id)}
                ocupado={alternar.isPending || excluir.isPending}
              />
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

function LinhaRegra({
  regra,
  aoAlternar,
  aoExcluir,
  ocupado,
}: {
  regra: RegraElegibilidade;
  aoAlternar: () => void;
  aoExcluir: () => void;
  ocupado: boolean;
}) {
  return (
    <li
      className={`rounded-lg border p-3 ${
        regra.ativo ? 'border-emerald-300 bg-white' : 'border-slate-200 bg-slate-50'
      }`}
    >
      <div className="flex flex-wrap items-start gap-2">
        <div className="min-w-0 flex-1">
          <p className="text-sm text-slate-900">{regra.descricao}</p>

          <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
            <span className="rounded bg-slate-100 px-1.5 py-0.5">{ROTULO_TIPO[regra.tipo]}</span>
            {regra.sistema && <span>{regra.sistema}</span>}
            {regra.versao > 1 && <span>v{regra.versao}</span>}
            {regra.fonte && <span>{regra.fonte}</span>}
          </div>

          {regra.pergunta && (
            <p className="mt-1 text-xs text-slate-600">
              Pergunta: “{regra.pergunta}” — barra quando a resposta é{' '}
              <strong>{regra.respostaBloqueia === 'Nao' ? 'não' : 'sim'}</strong>
            </p>
          )}
          {regra.documentoRotulo && (
            <p className="mt-1 text-xs text-slate-600">Documento: {regra.documentoRotulo}</p>
          )}
          {(regra.idadeMinAnos !== null || regra.idadeMaxAnos !== null || regra.sexo) && (
            <p className="mt-1 text-xs text-slate-600">
              {regra.idadeMinAnos !== null && `mín. ${regra.idadeMinAnos} anos `}
              {regra.idadeMaxAnos !== null && `máx. ${regra.idadeMaxAnos} anos `}
              {regra.sexo && `sexo ${regra.sexo}`}
            </p>
          )}
        </div>

        <div className="flex shrink-0 gap-1">
          <button
            type="button"
            onClick={aoAlternar}
            disabled={ocupado}
            className={`rounded px-2 py-1 text-xs font-medium ${
              regra.ativo
                ? 'bg-emerald-100 text-emerald-800 hover:bg-emerald-200'
                : 'bg-slate-200 text-slate-700 hover:bg-slate-300'
            }`}
          >
            {regra.ativo ? 'Ativa' : 'Inativa'}
          </button>
          <button
            type="button"
            onClick={aoExcluir}
            disabled={ocupado}
            className="rounded px-2 py-1 text-xs text-slate-500 hover:text-red-700"
          >
            excluir
          </button>
        </div>
      </div>
    </li>
  );
}
