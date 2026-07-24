import { useState } from 'react';
import { AlertTriangle, CheckCircle2, Download, FileSpreadsheet, Loader2, RefreshCw } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { apurarAba, listarIndicadores } from '@/features/indicadores/api/indicadoresApi';
import {
  baixarBlob,
  gerarXlsxIndicadores,
  type AbaExportacao,
} from '@/features/indicadores/lib/exportarExcel';
import {
  ABAS,
  type AbaIndicador,
  type FiltroIndicador,
  type IndicadorResumo,
} from '@/features/indicadores/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  aba: AbaIndicador;
  rotuloAba: string;
  filtro: FiltroIndicador;
  unidadeNome: string;
  itensAtual: IndicadorResumo[];
};

type Fase =
  | { tipo: 'idle' }
  | { tipo: 'trabalhando'; texto: string }
  | { tipo: 'ok'; arquivo: string }
  | { tipo: 'erro'; texto: string };

function formatarData(iso: string): string {
  const [a, m, d] = iso.split('-');
  return d && m && a ? `${d}/${m}/${a}` : iso;
}

function nomeArquivo(escopo: string, filtro: FiltroIndicador): string {
  const limpo = escopo.replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-|-$/g, '');
  return `Indicadores_${limpo}_${filtro.inicio}_a_${filtro.fim}.xlsx`;
}

export function ModalExportar({ aberto, aoFechar, aba, rotuloAba, filtro, unidadeNome, itensAtual }: Props) {
  const [fase, setFase] = useState<Fase>({ tipo: 'idle' });
  const ocupado = fase.tipo === 'trabalhando';

  async function gerar(abasDados: AbaExportacao[], escopo: string) {
    setFase({ tipo: 'trabalhando', texto: 'Gerando a planilha…' });
    const blob = await gerarXlsxIndicadores({
      abas: abasDados,
      unidadeNome,
      inicio: filtro.inicio,
      fim: filtro.fim,
    });
    const arquivo = nomeArquivo(escopo, filtro);
    baixarBlob(blob, arquivo);
    setFase({ tipo: 'ok', arquivo });
  }

  async function exportarAtual() {
    try {
      await gerar([{ aba, rotulo: rotuloAba, itens: itensAtual }], rotuloAba);
    } catch (e) {
      setFase({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  async function exportarTudoCache() {
    try {
      const abasDados: AbaExportacao[] = [];
      for (const meta of ABAS) {
        setFase({ tipo: 'trabalhando', texto: `Buscando ${meta.rotulo}…` });
        const itens = await listarIndicadores(meta.id, filtro);
        abasDados.push({ aba: meta.id, rotulo: meta.rotulo, itens });
      }
      await gerar(abasDados, 'Completo');
    } catch (e) {
      setFase({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  async function exportarTudoReapurar() {
    try {
      const abasDados: AbaExportacao[] = [];
      for (let i = 0; i < ABAS.length; i++) {
        const meta = ABAS[i];
        setFase({ tipo: 'trabalhando', texto: `Reapurando ${meta.rotulo} (${i + 1}/${ABAS.length})…` });
        const itens = await apurarAba(meta.id, filtro);
        abasDados.push({ aba: meta.id, rotulo: meta.rotulo, itens });
      }
      await gerar(abasDados, 'Completo');
    } catch (e) {
      setFase({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  function fecharTudo() {
    setFase({ tipo: 'idle' });
    aoFechar();
  }

  return (
    <Modal aberto={aberto} aoFechar={fecharTudo} titulo="Exportar indicadores para Excel" largura="md">
      <div className="space-y-4">
        <div className="rounded-lg bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <p className="font-medium text-slate-700">Confirme o recorte selecionado:</p>
          <ul className="mt-1 space-y-0.5">
            <li>
              Unidade: <strong>{unidadeNome}</strong>
            </li>
            <li>
              Período: <strong>{formatarData(filtro.inicio)}</strong> a{' '}
              <strong>{formatarData(filtro.fim)}</strong>
            </li>
          </ul>
        </div>

        {fase.tipo === 'ok' ? (
          <p className="flex items-start gap-2 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
            Arquivo <strong>{fase.arquivo}</strong> gerado. Verifique os downloads do navegador.
          </p>
        ) : fase.tipo === 'erro' ? (
          <p className="flex items-start gap-2 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            {fase.texto}
          </p>
        ) : fase.tipo === 'trabalhando' ? (
          <p className="flex items-center gap-2 rounded-lg bg-slate-50 px-4 py-3 text-sm text-slate-600">
            <Loader2 className="h-4 w-4 animate-spin" />
            {fase.texto}
          </p>
        ) : null}

        <div className="space-y-2">
          <button
            type="button"
            disabled={ocupado}
            onClick={exportarAtual}
            className="flex w-full items-start gap-3 rounded-lg border border-slate-200 px-4 py-3 text-left transition hover:border-primary-300 hover:bg-primary-50/40 disabled:opacity-50"
          >
            <FileSpreadsheet className="mt-0.5 h-5 w-5 shrink-0 text-primary-600" />
            <span>
              <span className="block text-sm font-medium text-slate-800">Exportar aba atual</span>
              <span className="block text-xs text-slate-500">
                Só a aba <strong>{rotuloAba}</strong>, com os dados já em tela.
              </span>
            </span>
          </button>

          <button
            type="button"
            disabled={ocupado}
            onClick={exportarTudoCache}
            className="flex w-full items-start gap-3 rounded-lg border border-slate-200 px-4 py-3 text-left transition hover:border-primary-300 hover:bg-primary-50/40 disabled:opacity-50"
          >
            <Download className="mt-0.5 h-5 w-5 shrink-0 text-primary-600" />
            <span>
              <span className="block text-sm font-medium text-slate-800">
                Exportar tudo (última apuração)
              </span>
              <span className="block text-xs text-slate-500">
                Todas as abas com o último resultado salvo. Rápido, sem reprocessar a base.
              </span>
            </span>
          </button>

          <button
            type="button"
            disabled={ocupado}
            onClick={exportarTudoReapurar}
            className="flex w-full items-start gap-3 rounded-lg border border-slate-200 px-4 py-3 text-left transition hover:border-amber-300 hover:bg-amber-50/40 disabled:opacity-50"
          >
            <RefreshCw className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
            <span>
              <span className="block text-sm font-medium text-slate-800">Reapurar tudo e exportar</span>
              <span className="block text-xs text-slate-500">
                Reprocessa as 5 abas na base do hospital antes de gerar. Pode levar vários minutos.
              </span>
            </span>
          </button>
        </div>

        <div className="flex justify-end gap-2 pt-1">
          <Button variante="ghost" onClick={fecharTudo} disabled={ocupado}>
            Fechar
          </Button>
        </div>
      </div>
    </Modal>
  );
}
