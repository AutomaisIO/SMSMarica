import { useState } from 'react';
import { AlertTriangle, CheckCircle2, Download, FileSpreadsheet, Loader2, RefreshCw } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { apurarAba, listarIndicadores, obterAnalitico } from '@/features/indicadores/api/indicadoresApi';
import {
  baixarBlob,
  gerarXlsxIndicadores,
  type AbaExportacao,
} from '@/features/indicadores/lib/exportarExcel';
import {
  ABAS,
  type AbaIndicador,
  type AnaliticoIndicador,
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
  | { tipo: 'ok'; arquivo: string; evidencias: number; falhas: number }
  | { tipo: 'erro'; texto: string };

function formatarData(iso: string): string {
  const [a, m, d] = iso.split('-');
  return d && m && a ? `${d}/${m}/${a}` : iso;
}

function nomeArquivo(escopo: string, filtro: FiltroIndicador): string {
  const limpo = escopo.replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-|-$/g, '');
  return `Indicadores_${limpo}_${filtro.inicio}_a_${filtro.fim}.xlsx`;
}

/** Indicadores que podem render evidência: habilitados e com SQL analítico cadastrado. */
function comAnalitico(abas: AbaExportacao[]): IndicadorResumo[] {
  return abas.flatMap((a) => a.itens.filter((i) => i.ativo && i.temAnalitico));
}

export function ModalExportar({ aberto, aoFechar, aba, rotuloAba, filtro, unidadeNome, itensAtual }: Props) {
  const [fase, setFase] = useState<Fase>({ tipo: 'idle' });
  const [comEvidencia, setComEvidencia] = useState(true);
  const ocupado = fase.tipo === 'trabalhando';

  /**
   * Busca a evidência de cada indicador, em sequência. É o Oracle vivo do hospital do outro
   * lado — nada de disparar dezenas de consultas em paralelo para montar uma planilha. Falha de
   * um indicador não derruba a exportação: vira uma planilha de evidência com o erro escrito,
   * que é mais honesto do que a planilha simplesmente não existir.
   */
  async function buscarEvidencias(
    abasDados: AbaExportacao[],
  ): Promise<{ mapa: Map<string, AnaliticoIndicador>; falhas: number }> {
    const alvos = comAnalitico(abasDados);
    const mapa = new Map<string, AnaliticoIndicador>();
    let falhas = 0;

    for (let i = 0; i < alvos.length; i++) {
      const it = alvos[i];
      setFase({
        tipo: 'trabalhando',
        texto: `Levantando a evidência de ${it.numero} (${i + 1}/${alvos.length})…`,
      });
      try {
        mapa.set(it.id, await obterAnalitico(it.id, filtro));
      } catch (e) {
        falhas += 1;
        mapa.set(it.id, {
          indicadorId: it.id,
          numero: it.numero,
          nome: it.nome,
          colunas: [],
          linhas: [],
          indiceIncluido: -1,
          indiceMotivo: -1,
          incluidos: 0,
          excluidos: 0,
          truncado: false,
          limiteLinhas: 0,
          duracaoMs: 0,
          executadoEm: new Date().toISOString(),
          erro: extrairMensagemDeErro(e),
        });
      }
    }

    return { mapa, falhas };
  }

  async function gerar(abasDados: AbaExportacao[], escopo: string) {
    let analiticos: Map<string, AnaliticoIndicador> | undefined;
    let falhas = 0;

    if (comEvidencia) {
      const r = await buscarEvidencias(abasDados);
      analiticos = r.mapa;
      falhas = r.falhas;
    }

    setFase({ tipo: 'trabalhando', texto: 'Gerando a planilha…' });
    const blob = await gerarXlsxIndicadores({
      abas: abasDados,
      unidadeNome,
      inicio: filtro.inicio,
      fim: filtro.fim,
      analiticos,
    });
    const arquivo = nomeArquivo(escopo, filtro);
    baixarBlob(blob, arquivo);
    setFase({ tipo: 'ok', arquivo, evidencias: analiticos?.size ?? 0, falhas });
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

  const alvosAtual = comAnalitico([{ aba, rotulo: rotuloAba, itens: itensAtual }]).length;

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

        <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-slate-200 px-4 py-3">
          <input
            type="checkbox"
            checked={comEvidencia}
            disabled={ocupado}
            onChange={(e) => setComEvidencia(e.target.checked)}
            className="mt-0.5 h-4 w-4 shrink-0 accent-primary-600"
          />
          <span>
            <span className="block text-sm font-medium text-slate-800">
              Incluir a evidência linha a linha
            </span>
            <span className="block text-xs text-slate-500">
              Uma planilha por indicador com os registros que entraram na conta e os que foram
              excluídos, com o motivo. Consulta a base do hospital na hora — deixa a exportação bem
              mais lenta.{' '}
              {alvosAtual > 0
                ? `Na aba ${rotuloAba}, ${alvosAtual} indicador(es) têm analítico cadastrado.`
                : `Nenhum indicador da aba ${rotuloAba} tem SQL analítico cadastrado ainda.`}
            </span>
          </span>
        </label>

        {fase.tipo === 'ok' ? (
          <div className="flex items-start gap-2 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              Arquivo <strong>{fase.arquivo}</strong> gerado. Verifique os downloads do navegador.
              {fase.evidencias > 0 && (
                <span className="mt-0.5 block text-xs">
                  {fase.evidencias} planilha(s) de evidência
                  {fase.falhas > 0 && ` · ${fase.falhas} falhou/falharam (o erro está escrito na planilha)`}
                </span>
              )}
            </span>
          </div>
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
