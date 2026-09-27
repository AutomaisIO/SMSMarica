import { useEffect, useState } from 'react';
import { Bot, Loader2, X } from 'lucide-react';
import { http, extrairMensagemDeErro } from '@/shared/api/httpClient';

type Largada = {
  conversaId: string;
  fone4: string;
  temDono: boolean;
  ultimaEntradaEm: string;
  ultimoTexto: string | null;
};

type Resultado = {
  largadasComJanela: number;
  puladasCortesia: number;
  semAncora: number;
  criadas: number;
  lote: Largada[];
};

async function chamar(lote: number, aplicar: boolean): Promise<Resultado> {
  const { data } = await http.post<Resultado>('/conversas/robo-retomar-largadas', { lote, aplicar });
  return data;
}

/**
 * Retomada EM LOTE das "largadas" (supervisão): prévia primeiro — quantas são, quem entra no
 * lote — e o disparo só depois do clique consciente. O robô responde uma a uma pela fila normal,
 * e o RESULTADO fica neste modal (regra da casa: desfecho de ato com efeito não vai em toast).
 */
export function RetomarLargadasDialog({ aoFechar }: { aoFechar: () => void }) {
  const [lote, setLote] = useState(50);
  const [previa, setPrevia] = useState<Resultado | null>(null);
  const [resultado, setResultado] = useState<Resultado | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [disparando, setDisparando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;
    setCarregando(true);
    setErro(null);
    chamar(lote, false)
      .then((r) => ativo && setPrevia(r))
      .catch((e) => ativo && setErro(extrairMensagemDeErro(e)))
      .finally(() => ativo && setCarregando(false));
    return () => {
      ativo = false;
    };
  }, [lote]);

  async function disparar() {
    setDisparando(true);
    setErro(null);
    try {
      setResultado(await chamar(lote, true));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setDisparando(false);
    }
  }

  const dataHoraCurta = (iso: string) =>
    new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4">
      <div className="flex max-h-[85vh] w-full max-w-lg flex-col rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <Bot className="h-4 w-4 text-indigo-600" /> Retomar largadas com o robô
          </h2>
          <button type="button" onClick={aoFechar} className="rounded p-1 text-gray-400 hover:bg-gray-100">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto px-4 py-3 text-sm">
          {resultado ? (
            <div className="rounded-md bg-emerald-50 p-3 text-emerald-800 ring-1 ring-emerald-200">
              <p>
                <strong>{resultado.criadas} retomada(s) disparada(s).</strong> O robô responde uma a uma —
                acompanhe pelas conversas. {resultado.semAncora > 0 && `${resultado.semAncora} ficaram sem âncora e foram puladas. `}
                Restam {Math.max(0, resultado.largadasComJanela - resultado.criadas)} largadas com janela
                aberta; abra este diálogo de novo para o próximo lote.
              </p>
            </div>
          ) : (
            <>
              <p className="text-gray-700">
                “Largada” = a <strong>última palavra é do cidadão</strong>, a janela de 24h ainda está
                aberta e o robô não tem resposta a caminho. O robô envia um <strong>resumo do ponto em que
                a conversa parou</strong> e pergunta como a pessoa quer seguir. Quem terminou só com
                “obrigado/ok” fica de fora.
              </p>
              {carregando ? (
                <p className="text-gray-500">Contando…</p>
              ) : previa ? (
                <>
                  <div className="rounded-md bg-gray-50 p-3 ring-1 ring-gray-200">
                    <p>
                      <strong>{previa.largadasComJanela}</strong> largadas com janela aberta ·{' '}
                      {previa.puladasCortesia} terminaram em cortesia (fora) · lote atual:{' '}
                      <strong>{previa.lote.length}</strong>
                    </p>
                    <label className="mt-2 flex items-center gap-2 text-xs text-gray-600">
                      Tamanho do lote
                      <input
                        type="number"
                        min={1}
                        max={200}
                        value={lote}
                        onChange={(e) => setLote(Math.max(1, Math.min(200, Number(e.target.value) || 1)))}
                        className="w-20 rounded border border-gray-300 px-2 py-1"
                      />
                    </label>
                  </div>
                  <div className="max-h-56 overflow-y-auto rounded-md border border-gray-100">
                    {previa.lote.map((l) => (
                      <div key={l.conversaId} className="border-b border-gray-50 px-3 py-1.5 text-xs last:border-b-0">
                        <span className="font-medium">…{l.fone4}</span>{' '}
                        <span className={l.temDono ? 'text-amber-600' : 'text-gray-500'}>
                          {l.temDono ? 'com dono' : 'sem dono'}
                        </span>{' '}
                        · {dataHoraCurta(l.ultimaEntradaEm)} · <span className="text-gray-600">“{l.ultimoTexto ?? ''}”</span>
                      </div>
                    ))}
                  </div>
                </>
              ) : null}
            </>
          )}
          {erro && <p className="text-red-600">{erro}</p>}
        </div>

        {!resultado && (
          <div className="flex justify-end gap-2 border-t border-gray-100 px-4 py-3">
            <button type="button" onClick={aoFechar} className="rounded-md px-3 py-2 text-sm text-gray-600 hover:bg-gray-100">
              Cancelar
            </button>
            <button
              type="button"
              disabled={carregando || disparando || !previa || previa.lote.length === 0}
              onClick={disparar}
              className="flex items-center gap-1.5 rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              {disparando ? <Loader2 className="h-4 w-4 animate-spin" /> : <Bot className="h-4 w-4" />}
              Disparar {previa?.lote.length ?? 0} retomada(s)
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
