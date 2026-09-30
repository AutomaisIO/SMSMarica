import { useState } from 'react';
import { Gauge, Loader2, PlayCircle, RotateCcw } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import {
  useRearmarColetaIndicadores,
  useRetomarColetaIndicadores,
  useSalvarColetaIndicadores,
  useStatusColetaIndicadores,
} from '@/features/sisreg/api/queries';
import type { ColetorIndicadorSisreg, EsperaColetaIndicadores } from '@/features/sisreg/types';

const NOME_COLETOR: Record<ColetorIndicadorSisreg, string> = {
  Faltas: 'Faltas (absenteísmo oficial)',
  Canceladas: 'Marcações canceladas do mês',
  Desfechos: 'Devolvidas, negadas e canceladas antes de agendar',
  Ppi: 'Cotas PPI',
};

const TEXTO_ESPERA: Record<EsperaColetaIndicadores, string> = {
  Desligada: 'Desligado.',
  ChaveMestraDesligada: 'Parado: o sincronismo automático do SISREG (no topo da tela) está desligado.',
  PausadaPorCaptcha: 'Pausado: o SISREG pediu CAPTCHA.',
  ForaDoHorario: 'Aguardando o horário (01:20 às 18:00): à noite a sessão é da varredura das agendas.',
  OutroMotorUsandoASessao: 'Aguardando a vez: outro motor está usando a sessão do SISREG.',
  TetoDoColetor: 'Aguardando: atingiu o teto do coletor nesta hora.',
  OrcamentoGlobalCurto: 'Aguardando: o orçamento de requisições do operador está apertado nesta hora.',
};

function dataHora(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function janela(inicio: string, fim: string) {
  const f = (d: string) => `${d.slice(8, 10)}/${d.slice(5, 7)}/${d.slice(0, 4)}`;
  return inicio === fim ? f(inicio) : `${f(inicio)} a ${f(fim)}`;
}

/**
 * Coletor dos INDICADORES DE REGULAÇÃO: mantém em dia, a partir de agora, o que a tela Regulação →
 * SISREG → Indicadores mostra e o espelho da agenda não tem — lista oficial de faltas, cotas PPI,
 * marcações canceladas do mês e devolvidas/negadas por unidade.
 *
 * <p>Nasce desligado. Cada leitura sai do mesmo orçamento anti-robô do operador que o robô de produção
 * usa; por isso o coletor anda devagar (uma requisição a cada 30 segundos, no máximo 150 por hora),
 * só de dia e cedendo a vez a qualquer outro motor.</p>
 */
export function IndicadoresColetaSecao() {
  const status = useStatusColetaIndicadores();
  const salvar = useSalvarColetaIndicadores();
  const retomar = useRetomarColetaIndicadores();
  const rearmar = useRearmarColetaIndicadores();
  const [erro, setErro] = useState<string | null>(null);

  const s = status.data;
  const ocupado = salvar.isPending || retomar.isPending || rearmar.isPending;

  function acao(p: Promise<unknown>) {
    setErro(null);
    p.catch((e) => setErro(extrairMensagemDeErro(e)));
  }

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <Gauge className="h-4 w-4 text-primary-600" />
          Indicadores de Regulação — coleta no SISREG
        </h2>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          Mantém em dia o que a tela <strong>Regulação → SISREG → Indicadores</strong> precisa e a
          agenda espelhada não traz: a <strong>lista oficial de faltas</strong> (lida quando a semana
          já tem 30 dias, depois que as unidades confirmaram as chegadas), as <strong>cotas PPI</strong>{' '}
          do mês fechado, as <strong>marcações canceladas</strong> do mês e as solicitações{' '}
          <strong>devolvidas, negadas e canceladas antes de agendar</strong>, unidade por unidade. O
          passado (jan/2025 a ago/2026) já foi carregado; o coletor cuida só dos últimos meses.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-500">
          Anda devagar de propósito: uma requisição a cada 30 segundos, no máximo 150 por hora, só
          entre 01:20 e 18:00, e espera sempre que outro motor está usando a sessão. Se o SISREG pedir
          CAPTCHA, para por um dia inteiro. Os motivos de cancelamento do dia a dia vêm da conciliação
          de cancelamentos, que já lê essa tela; os dos meses passados, de uma amostra de 6 páginas por mês
          (cerca de 120 requisições para jan/2025 a ago/2026), sem mudar o total oficial de cada mês.
        </p>
      </header>

      {s ? (
        <>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={s.ativa}
              disabled={ocupado}
              onChange={(e) => acao(salvar.mutateAsync(e.target.checked))}
            />
            Coletar automaticamente
          </label>

          {s.pausadaAte ? (
            <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
              <p className="font-medium">
                Pausado até {dataHora(s.pausadaAte)}: o SISREG pediu CAPTCHA para o operador da integração.
              </p>
              <p className="mt-1">
                Relogar não resolve. Abra o SISREG no navegador com esse usuário, resolva o CAPTCHA e só
                então retome.
              </p>
              <Button
                variante="outline"
                className="mt-2"
                disabled={ocupado}
                onClick={() => acao(retomar.mutateAsync())}
              >
                <PlayCircle className="mr-2 h-4 w-4" />
                Já resolvi — retomar
              </Button>
            </div>
          ) : null}

          {s.ativa ? (
            <p className="mt-3 rounded-lg border border-gray-200 bg-gray-50 p-3 text-xs text-gray-700">
              {s.trabalhoAtual && !s.espera ? (
                <span className="flex items-center gap-2">
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  Lendo agora: {s.trabalhoAtual}.
                </span>
              ) : s.espera ? (
                <>
                  {TEXTO_ESPERA[s.espera]}
                  {s.trabalhoAtual ? ` Em curso: ${s.trabalhoAtual}.` : ''}
                </>
              ) : (
                'Em dia — nada para ler agora.'
              )}
              <span className="mt-1 block text-gray-500">
                {s.requisicoesNaUltimaHora} de {s.tetoPorHora} requisições na última hora · último passo{' '}
                {dataHora(s.ultimoPassoEm)}
              </span>
            </p>
          ) : null}

          <div className="mt-4 overflow-x-auto">
            <table className="min-w-full text-xs">
              <thead className="text-left text-gray-500">
                <tr>
                  <th className="py-1 pr-4 font-medium">O que é lido</th>
                  <th className="py-1 pr-3 text-right font-medium">Lidas</th>
                  <th className="py-1 pr-3 text-right font-medium">Na fila</th>
                  <th className="py-1 pr-3 text-right font-medium">Com falha</th>
                  <th className="py-1 font-medium">Última leitura</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 text-gray-700">
                {s.coletores.map((c) => (
                  <tr key={c.coletor}>
                    <td className="py-1.5 pr-4">{NOME_COLETOR[c.coletor]}</td>
                    <td className="py-1.5 pr-3 text-right">{c.concluidas}</td>
                    <td className="py-1.5 pr-3 text-right">{c.pendentes + c.emAndamento}</td>
                    <td className={`py-1.5 pr-3 text-right ${c.falhas ? 'font-medium text-red-700' : ''}`}>
                      {c.falhas}
                    </td>
                    <td className="py-1.5">{dataHora(c.ultimaLeituraEm)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {s.ultimasFalhas.length > 0 ? (
            <div className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-800">
              <p className="font-medium">Leituras que não fecharam (nada foi gravado delas):</p>
              <ul className="mt-1 space-y-0.5">
                {s.ultimasFalhas.map((f) => (
                  <li key={`${f.coletor}-${f.inicio}-${f.escopo}`}>
                    {NOME_COLETOR[f.coletor]}, {janela(f.inicio, f.fim)}
                    {/^\d{7}$/.test(f.escopo) ? ` (unidade ${f.escopo})` : ''}
                    {f.escopo === 'amostra' ? ' (amostra de motivos)' : ''} — {f.erro ?? 'sem mensagem'} (
                    {f.tentativas} tentativa{f.tentativas === 1 ? '' : 's'})
                  </li>
                ))}
              </ul>
              <p className="mt-2 text-red-700">
                Falhas são tentadas de novo sozinhas no dia seguinte, até 6 vezes.
              </p>
              <Button
                variante="outline"
                className="mt-2"
                disabled={ocupado}
                onClick={() => acao(rearmar.mutateAsync())}
              >
                <RotateCcw className="mr-2 h-4 w-4" />
                Tentar todas de novo
              </Button>
            </div>
          ) : null}
        </>
      ) : status.isLoading ? (
        <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
      ) : null}

      {erro ? (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{erro}</p>
      ) : null}
    </section>
  );
}
